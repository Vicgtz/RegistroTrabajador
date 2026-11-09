using Microsoft.EntityFrameworkCore;
using RegistroTrabajo.Data;
using RegistroTrabajo.Data.Entities;
using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public interface IExpedienteProfesionalService
{
    Task<ExpedienteProfesionalDto> ObtenerAsync(Guid trabajadorId, CancellationToken ct);
    Task<ExpedienteProfesionalDto> MiExpedienteAsync(CancellationToken ct);

    Task<TrabajadorHabilidadDto> GuardarHabilidadAsync(Guid trabajadorId, GuardarHabilidadRequest request, CancellationToken ct);
    Task EliminarHabilidadAsync(Guid trabajadorId, Guid trabajadorHabilidadId, CancellationToken ct);

    Task<EstudioDto> CrearEstudioAsync(Guid trabajadorId, GuardarEstudioRequest request, CancellationToken ct);
    Task<EstudioDto> ActualizarEstudioAsync(Guid trabajadorId, Guid estudioId, GuardarEstudioRequest request, CancellationToken ct);
    Task EliminarEstudioAsync(Guid trabajadorId, Guid estudioId, CancellationToken ct);

    Task<IReadOnlyList<PendienteExpedienteDto>> PendientesAsync(CancellationToken ct);
    Task ResolverHabilidadAsync(Guid trabajadorHabilidadId, ResolverExpedienteRequest request, CancellationToken ct);
    Task ResolverEstudioAsync(Guid estudioId, ResolverExpedienteRequest request, CancellationToken ct);

    Task<IReadOnlyList<HabilidadDto>> ListarHabilidadesAsync(string? texto, bool incluirInactivas, CancellationToken ct);
    Task<HabilidadDto> ActualizarHabilidadCatalogoAsync(Guid habilidadId, ActualizarHabilidadRequest request, CancellationToken ct);

    Task<PagedResult<PerfilResultadoDto>> BuscarAsync(PerfilFiltro filtro, CancellationToken ct);
}

/// <summary>
/// Bloque 14: estudios y habilidades del trabajador. Quien tiene expediente.editar registra directamente (estado Aprobado);
/// el propio trabajador solo propone (estado Pendiente) y un revisor con expediente.aprobar lo resuelve. La búsqueda solo
/// considera registros aprobados. La empresa sale siempre del token.
/// </summary>
public class ExpedienteProfesionalService : IExpedienteProfesionalService
{
    private readonly RegistroTrabajoDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IPermisoService _permisos;
    private readonly IAuditoriaService _auditoria;
    private readonly TimeProvider _clock;

    public ExpedienteProfesionalService(RegistroTrabajoDbContext db, ICurrentUser current, IPermisoService permisos,
        IAuditoriaService auditoria, TimeProvider clock)
    {
        _db = db; _current = current; _permisos = permisos; _auditoria = auditoria; _clock = clock;
    }

    // ---------------- Acceso ----------------
    private sealed record Acceso(Guid EmpresaId, Trabajador Trabajador, bool Propio, bool PuedeEditar, bool PuedeAprobar, bool PuedeConsultar);

    private async Task<Acceso> AccesoAsync(Guid trabajadorId, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var t = await TrabajadorHelper.CargarAsync(_db, empresaId, trabajadorId, false, ct);
        var propio = await TrabajadorHelper.IdDeUsuarioAsync(_db, empresaId, _current.RequireUsuarioId(), ct) == trabajadorId;
        var editar = await _permisos.TienePermisoAsync(Permisos.ExpedienteEditar, ct);
        var aprobar = await _permisos.TienePermisoAsync(Permisos.ExpedienteAprobar, ct);
        var consultar = propio || editar || aprobar || await _permisos.TienePermisoAsync(Permisos.ExpedienteConsultar, ct);
        return new Acceso(empresaId, t, propio, editar, aprobar, consultar);
    }

    /// <summary>Acceso de escritura: edita directo quien tiene expediente.editar; el propio trabajador propone.</summary>
    private async Task<(Acceso a, byte estado)> AccesoEscrituraAsync(Guid trabajadorId, CancellationToken ct)
    {
        var a = await AccesoAsync(trabajadorId, ct);
        if (a.PuedeEditar) return (a, EstadoExpediente.Aprobado);
        if (!a.Propio) throw ServiceException.Forbidden("No tiene permiso para modificar el expediente profesional de este trabajador.");
        if (a.Trabajador.EstadoLaboral == EstadoLaboral.Baja) throw ServiceException.Validation("El trabajador está dado de baja.");
        return (a, EstadoExpediente.Pendiente);
    }

    // ---------------- Consulta ----------------
    public async Task<ExpedienteProfesionalDto> ObtenerAsync(Guid trabajadorId, CancellationToken ct)
    {
        var a = await AccesoAsync(trabajadorId, ct);
        if (!a.PuedeConsultar) throw ServiceException.Forbidden("No tiene permiso para consultar el expediente profesional.");
        // Quien solo consulta ve únicamente lo aprobado; el dueño y los revisores ven también lo pendiente/rechazado.
        var verTodo = a.Propio || a.PuedeEditar || a.PuedeAprobar;

        var estudios = _db.TrabajadoresEstudio.AsNoTracking().Where(e => e.EmpresaId == a.EmpresaId && e.TrabajadorId == trabajadorId);
        var habilidades = _db.TrabajadoresHabilidad.AsNoTracking().Where(h => h.EmpresaId == a.EmpresaId && h.TrabajadorId == trabajadorId);
        if (!verTodo)
        {
            estudios = estudios.Where(e => e.Estado == EstadoExpediente.Aprobado);
            habilidades = habilidades.Where(h => h.Estado == EstadoExpediente.Aprobado);
        }

        var listaEstudios = await estudios
            .OrderByDescending(e => e.EnCurso).ThenByDescending(e => e.FechaFin).ThenByDescending(e => e.FechaInicio)
            .ToListAsync(ct);
        var listaHabilidades = await habilidades
            .OrderByDescending(h => h.Nivel).ThenBy(h => h.Habilidad.Nombre)
            .Select(h => new TrabajadorHabilidadDto
            {
                TrabajadorHabilidadId = h.TrabajadorHabilidadId, HabilidadId = h.HabilidadId, Nombre = h.Habilidad.Nombre,
                Nivel = h.Nivel, AniosExperiencia = h.AniosExperiencia, Notas = h.Notas, Estado = h.Estado, MotivoRechazo = h.MotivoRechazo
            }).ToListAsync(ct);
        var cv = await _db.CurriculosTrabajador.AsNoTracking()
            .Where(c => c.EmpresaId == a.EmpresaId && c.TrabajadorId == trabajadorId)
            .Select(c => new CurriculumInfoDto
            {
                NombreArchivo = c.NombreArchivo, TipoContenido = c.TipoContenido, TamanoBytes = c.TamanoBytes,
                FechaActualizacionUtc = c.FechaActualizacionUtc
            }).FirstOrDefaultAsync(ct);

        return new ExpedienteProfesionalDto
        {
            TrabajadorId = trabajadorId, EsPropio = a.Propio, PuedeEditar = a.PuedeEditar, PuedeAprobar = a.PuedeAprobar,
            Estudios = listaEstudios.Select(ToDto).ToList(), Habilidades = listaHabilidades, Curriculum = cv
        };
    }

    public async Task<ExpedienteProfesionalDto> MiExpedienteAsync(CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var id = await TrabajadorHelper.IdDeUsuarioAsync(_db, empresaId, _current.RequireUsuarioId(), ct)
            ?? throw ServiceException.NotFound("Su cuenta no está vinculada a un expediente de trabajador.");
        return await ObtenerAsync(id, ct);
    }

    // ---------------- Habilidades del trabajador ----------------
    public async Task<TrabajadorHabilidadDto> GuardarHabilidadAsync(Guid trabajadorId, GuardarHabilidadRequest r, CancellationToken ct)
    {
        var (a, estado) = await AccesoEscrituraAsync(trabajadorId, ct);
        var nombre = NormalizarEspacios(r.Nombre);
        if (nombre.Length is < 2 or > 80) throw ServiceException.Validation("El nombre de la habilidad debe tener entre 2 y 80 caracteres.");
        if (r.Nivel is < 1 or > 3) throw ServiceException.Validation("Nivel no válido.");
        var ahora = _clock.GetUtcNow().UtcDateTime;

        var normalizado = nombre.ToUpperInvariant();
        var habilidad = await _db.Habilidades.FirstOrDefaultAsync(h => h.EmpresaId == a.EmpresaId && h.NombreNormalizado == normalizado, ct);
        if (habilidad is null)
        {
            habilidad = new Habilidad
            {
                HabilidadId = Guid.NewGuid(), EmpresaId = a.EmpresaId, Nombre = nombre, NombreNormalizado = normalizado,
                Activa = true, FechaCreacionUtc = ahora
            };
            _db.Habilidades.Add(habilidad);
            _auditoria.Registrar("Expediente", nameof(Habilidad), habilidad.HabilidadId.ToString(), "CrearHabilidad", null, new { habilidad.Nombre });
        }
        else if (!habilidad.Activa && !a.PuedeEditar)
            throw ServiceException.Validation("Esa habilidad está desactivada en el catálogo.");

        var th = await _db.TrabajadoresHabilidad.FirstOrDefaultAsync(h => h.EmpresaId == a.EmpresaId
            && h.TrabajadorId == trabajadorId && h.HabilidadId == habilidad.HabilidadId, ct);
        var antes = th is null ? null : new { th.Nivel, th.AniosExperiencia, th.Estado };
        if (th is null)
        {
            th = new TrabajadorHabilidad
            {
                TrabajadorHabilidadId = Guid.NewGuid(), EmpresaId = a.EmpresaId, TrabajadorId = trabajadorId,
                HabilidadId = habilidad.HabilidadId, FechaCreacionUtc = ahora
            };
            _db.TrabajadoresHabilidad.Add(th);
        }
        th.Nivel = r.Nivel;
        th.AniosExperiencia = r.AniosExperiencia;
        th.Notas = Helpers.Limpiar(r.Notas);
        AplicarEstado(th, estado, ahora);
        th.FechaActualizacionUtc = ahora;

        _auditoria.Registrar("Expediente", nameof(TrabajadorHabilidad), th.TrabajadorHabilidadId.ToString(),
            antes is null ? "AgregarHabilidad" : "ActualizarHabilidad", antes, new { trabajadorId, habilidad = habilidad.Nombre, th.Nivel, th.Estado });
        await _db.SaveSafeAsync(ct);
        return new TrabajadorHabilidadDto
        {
            TrabajadorHabilidadId = th.TrabajadorHabilidadId, HabilidadId = habilidad.HabilidadId, Nombre = habilidad.Nombre,
            Nivel = th.Nivel, AniosExperiencia = th.AniosExperiencia, Notas = th.Notas, Estado = th.Estado, MotivoRechazo = th.MotivoRechazo
        };
    }

    public async Task EliminarHabilidadAsync(Guid trabajadorId, Guid id, CancellationToken ct)
    {
        var (a, _) = await AccesoEscrituraAsync(trabajadorId, ct);
        var th = await _db.TrabajadoresHabilidad.Include(h => h.Habilidad).FirstOrDefaultAsync(h => h.TrabajadorHabilidadId == id
            && h.EmpresaId == a.EmpresaId && h.TrabajadorId == trabajadorId, ct)
            ?? throw ServiceException.NotFound("Habilidad no encontrada.");
        _db.TrabajadoresHabilidad.Remove(th);
        _auditoria.Registrar("Expediente", nameof(TrabajadorHabilidad), id.ToString(), "QuitarHabilidad", new { trabajadorId, th.Habilidad.Nombre }, null);
        await _db.SaveSafeAsync(ct);
    }

    // ---------------- Estudios ----------------
    public async Task<EstudioDto> CrearEstudioAsync(Guid trabajadorId, GuardarEstudioRequest r, CancellationToken ct)
    {
        var (a, estado) = await AccesoEscrituraAsync(trabajadorId, ct);
        var ahora = _clock.GetUtcNow().UtcDateTime;
        var e = new TrabajadorEstudio { EstudioId = Guid.NewGuid(), EmpresaId = a.EmpresaId, TrabajadorId = trabajadorId, FechaCreacionUtc = ahora };
        Aplicar(e, r);
        AplicarEstado(e, estado, ahora);
        e.FechaActualizacionUtc = ahora;
        _db.TrabajadoresEstudio.Add(e);
        _auditoria.Registrar("Expediente", nameof(TrabajadorEstudio), e.EstudioId.ToString(), "AgregarEstudio", null,
            new { trabajadorId, e.Tipo, e.Titulo, e.Institucion, e.Estado });
        await _db.SaveSafeAsync(ct);
        return ToDto(e);
    }

    public async Task<EstudioDto> ActualizarEstudioAsync(Guid trabajadorId, Guid estudioId, GuardarEstudioRequest r, CancellationToken ct)
    {
        var (a, estado) = await AccesoEscrituraAsync(trabajadorId, ct);
        var e = await CargarEstudioAsync(a.EmpresaId, trabajadorId, estudioId, ct);
        var antes = new { e.Tipo, e.Titulo, e.Institucion, e.Estado };
        var ahora = _clock.GetUtcNow().UtcDateTime;
        Aplicar(e, r);
        AplicarEstado(e, estado, ahora);
        e.FechaActualizacionUtc = ahora;
        _auditoria.Registrar("Expediente", nameof(TrabajadorEstudio), estudioId.ToString(), "ActualizarEstudio", antes,
            new { e.Tipo, e.Titulo, e.Institucion, e.Estado });
        await _db.SaveSafeAsync(ct);
        return ToDto(e);
    }

    public async Task EliminarEstudioAsync(Guid trabajadorId, Guid estudioId, CancellationToken ct)
    {
        var (a, _) = await AccesoEscrituraAsync(trabajadorId, ct);
        var e = await CargarEstudioAsync(a.EmpresaId, trabajadorId, estudioId, ct);
        _db.TrabajadoresEstudio.Remove(e);
        _auditoria.Registrar("Expediente", nameof(TrabajadorEstudio), estudioId.ToString(), "QuitarEstudio", new { trabajadorId, e.Tipo, e.Titulo }, null);
        await _db.SaveSafeAsync(ct);
    }

    private async Task<TrabajadorEstudio> CargarEstudioAsync(Guid empresaId, Guid trabajadorId, Guid estudioId, CancellationToken ct) =>
        await _db.TrabajadoresEstudio.FirstOrDefaultAsync(x => x.EstudioId == estudioId && x.EmpresaId == empresaId && x.TrabajadorId == trabajadorId, ct)
        ?? throw ServiceException.NotFound("Estudio no encontrado.");

    // ---------------- Aprobación ----------------
    public async Task<IReadOnlyList<PendienteExpedienteDto>> PendientesAsync(CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.ExpedienteAprobar, ct);
        var empresaId = _current.RequireEmpresaId();

        var habilidades = await _db.TrabajadoresHabilidad.AsNoTracking()
            .Where(h => h.EmpresaId == empresaId && h.Estado == EstadoExpediente.Pendiente)
            .Select(h => new PendienteExpedienteDto
            {
                Clase = "Habilidad", Id = h.TrabajadorHabilidadId, TrabajadorId = h.TrabajadorId,
                TrabajadorNombre = h.Trabajador.Nombres + " " + h.Trabajador.Apellidos,
                Descripcion = h.Habilidad.Nombre + " (nivel " + h.Nivel + ")", FechaCreacionUtc = h.FechaActualizacionUtc
            }).ToListAsync(ct);
        var estudios = await _db.TrabajadoresEstudio.AsNoTracking()
            .Where(e => e.EmpresaId == empresaId && e.Estado == EstadoExpediente.Pendiente)
            .Select(e => new PendienteExpedienteDto
            {
                Clase = "Estudio", Id = e.EstudioId, TrabajadorId = e.TrabajadorId,
                TrabajadorNombre = e.Trabajador.Nombres + " " + e.Trabajador.Apellidos,
                Descripcion = e.Titulo + (e.Institucion != null ? " — " + e.Institucion : ""), FechaCreacionUtc = e.FechaActualizacionUtc
            }).ToListAsync(ct);
        return habilidades.Concat(estudios).OrderBy(p => p.FechaCreacionUtc).ToList();
    }

    public async Task ResolverHabilidadAsync(Guid id, ResolverExpedienteRequest r, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.ExpedienteAprobar, ct);
        var empresaId = _current.RequireEmpresaId();
        var th = await _db.TrabajadoresHabilidad.Include(h => h.Habilidad).FirstOrDefaultAsync(h => h.TrabajadorHabilidadId == id && h.EmpresaId == empresaId, ct)
            ?? throw ServiceException.NotFound("Habilidad no encontrada.");
        Resolver(th, r, "Habilidad", th.Habilidad.Nombre, th.TrabajadorId, id);
        th.FechaActualizacionUtc = _clock.GetUtcNow().UtcDateTime;
        await _db.SaveSafeAsync(ct);
    }

    public async Task ResolverEstudioAsync(Guid id, ResolverExpedienteRequest r, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.ExpedienteAprobar, ct);
        var empresaId = _current.RequireEmpresaId();
        var e = await _db.TrabajadoresEstudio.FirstOrDefaultAsync(x => x.EstudioId == id && x.EmpresaId == empresaId, ct)
            ?? throw ServiceException.NotFound("Estudio no encontrado.");
        Resolver(e, r, "Estudio", e.Titulo, e.TrabajadorId, id);
        e.FechaActualizacionUtc = _clock.GetUtcNow().UtcDateTime;
        await _db.SaveSafeAsync(ct);
    }

    private void Resolver(object registro, ResolverExpedienteRequest r, string clase, string descripcion, Guid trabajadorId, Guid id)
    {
        var estadoActual = registro is TrabajadorHabilidad h ? h.Estado : ((TrabajadorEstudio)registro).Estado;
        if (estadoActual != EstadoExpediente.Pendiente) throw ServiceException.Conflict("El registro ya fue resuelto.");
        var motivo = Helpers.Limpiar(r.MotivoRechazo);
        if (!r.Aprobar && motivo is null) throw ServiceException.Validation("Indique el motivo del rechazo.");
        var nuevo = r.Aprobar ? EstadoExpediente.Aprobado : EstadoExpediente.Rechazado;
        var usuario = _current.RequireUsuarioId();
        var ahora = _clock.GetUtcNow().UtcDateTime;
        switch (registro)
        {
            case TrabajadorHabilidad th:
                th.Estado = nuevo; th.MotivoRechazo = r.Aprobar ? null : motivo; th.ResueltoPorUsuarioId = usuario; th.ResueltoUtc = ahora; break;
            case TrabajadorEstudio te:
                te.Estado = nuevo; te.MotivoRechazo = r.Aprobar ? null : motivo; te.ResueltoPorUsuarioId = usuario; te.ResueltoUtc = ahora; break;
        }
        _auditoria.Registrar("Expediente", clase == "Habilidad" ? nameof(TrabajadorHabilidad) : nameof(TrabajadorEstudio), id.ToString(),
            r.Aprobar ? "Aprobar" + clase : "Rechazar" + clase, null, new { trabajadorId, descripcion });
    }

    // ---------------- Catálogo ----------------
    public async Task<IReadOnlyList<HabilidadDto>> ListarHabilidadesAsync(string? texto, bool incluirInactivas, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        // Autocompletado y búsqueda: cualquier usuario con acceso al módulo (consultar/buscar/editar) o trabajador vinculado.
        var autorizado = await _permisos.TienePermisoAsync(Permisos.ExpedienteConsultar, ct)
            || await _permisos.TienePermisoAsync(Permisos.ExpedienteBuscar, ct)
            || await _permisos.TienePermisoAsync(Permisos.ExpedienteEditar, ct)
            || await TrabajadorHelper.IdDeUsuarioAsync(_db, empresaId, _current.RequireUsuarioId(), ct) is not null;
        if (!autorizado) throw ServiceException.Forbidden("No tiene permiso para consultar el catálogo de habilidades.");
        if (incluirInactivas) await _permisos.AsegurarPermisoAsync(Permisos.ExpedienteEditar, ct);

        var q = _db.Habilidades.AsNoTracking().Where(h => h.EmpresaId == empresaId);
        if (!incluirInactivas) q = q.Where(h => h.Activa);
        var t = Helpers.Limpiar(texto)?.ToLower();
        if (t is not null) q = q.Where(h => h.Nombre.ToLower().Contains(t));
        return await q.OrderBy(h => h.Nombre).Take(200)
            .Select(h => new HabilidadDto
            {
                HabilidadId = h.HabilidadId, Nombre = h.Nombre, Activa = h.Activa,
                Trabajadores = _db.TrabajadoresHabilidad.Count(x => x.EmpresaId == empresaId && x.HabilidadId == h.HabilidadId
                    && x.Estado == EstadoExpediente.Aprobado)
            }).ToListAsync(ct);
    }

    public async Task<HabilidadDto> ActualizarHabilidadCatalogoAsync(Guid habilidadId, ActualizarHabilidadRequest r, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.ExpedienteEditar, ct);
        var empresaId = _current.RequireEmpresaId();
        var h = await _db.Habilidades.FirstOrDefaultAsync(x => x.HabilidadId == habilidadId && x.EmpresaId == empresaId, ct)
            ?? throw ServiceException.NotFound("Habilidad no encontrada.");
        var nombre = NormalizarEspacios(r.Nombre);
        if (nombre.Length is < 2 or > 80) throw ServiceException.Validation("El nombre de la habilidad debe tener entre 2 y 80 caracteres.");
        var normalizado = nombre.ToUpperInvariant();
        if (await _db.Habilidades.AnyAsync(x => x.EmpresaId == empresaId && x.NombreNormalizado == normalizado && x.HabilidadId != habilidadId, ct))
            throw ServiceException.Conflict("Ya existe una habilidad con ese nombre.");
        var antes = new { h.Nombre, h.Activa };
        h.Nombre = nombre; h.NombreNormalizado = normalizado; h.Activa = r.Activa;
        _auditoria.Registrar("Expediente", nameof(Habilidad), habilidadId.ToString(), "ActualizarHabilidadCatalogo", antes, new { h.Nombre, h.Activa });
        await _db.SaveSafeAsync(ct);
        var total = await _db.TrabajadoresHabilidad.CountAsync(x => x.EmpresaId == empresaId && x.HabilidadId == habilidadId && x.Estado == EstadoExpediente.Aprobado, ct);
        return new HabilidadDto { HabilidadId = h.HabilidadId, Nombre = h.Nombre, Activa = h.Activa, Trabajadores = total };
    }

    // ---------------- Búsqueda por perfil ----------------
    public async Task<PagedResult<PerfilResultadoDto>> BuscarAsync(PerfilFiltro f, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.ExpedienteBuscar, ct);
        var empresaId = _current.RequireEmpresaId();
        var ids = (f.HabilidadIds ?? new()).Distinct().ToList();
        if (ids.Count > LimitesCurriculum.MaxHabilidadesPorBusqueda)
            throw ServiceException.Validation($"Puede buscar hasta {LimitesCurriculum.MaxHabilidadesPorBusqueda} habilidades a la vez.");
        var nivel = f.NivelMinimo;

        var q = _db.Trabajadores.AsNoTracking().Where(t => t.EmpresaId == empresaId);
        if (f.SoloActivos) q = q.Where(t => t.EstadoLaboral == EstadoLaboral.Activo);

        if (ids.Count > 0 && f.TodasLasHabilidades)
        {
            foreach (var hid in ids)
            {
                var id = hid;
                q = q.Where(t => _db.TrabajadoresHabilidad.Any(h => h.EmpresaId == empresaId && h.TrabajadorId == t.TrabajadorId
                    && h.HabilidadId == id && h.Estado == EstadoExpediente.Aprobado && (nivel == null || h.Nivel >= nivel)));
            }
        }
        else if (ids.Count > 0)
        {
            q = q.Where(t => _db.TrabajadoresHabilidad.Any(h => h.EmpresaId == empresaId && h.TrabajadorId == t.TrabajadorId
                && ids.Contains(h.HabilidadId) && h.Estado == EstadoExpediente.Aprobado && (nivel == null || h.Nivel >= nivel)));
        }
        else if (nivel is not null)
        {
            q = q.Where(t => _db.TrabajadoresHabilidad.Any(h => h.EmpresaId == empresaId && h.TrabajadorId == t.TrabajadorId
                && h.Estado == EstadoExpediente.Aprobado && h.Nivel >= nivel));
        }

        var tipo = f.TipoEstudio;
        var texto = Helpers.Limpiar(f.Estudio)?.ToLower();
        if (tipo is not null || texto is not null)
        {
            q = q.Where(t => _db.TrabajadoresEstudio.Any(e => e.EmpresaId == empresaId && e.TrabajadorId == t.TrabajadorId
                && e.Estado == EstadoExpediente.Aprobado && (tipo == null || e.Tipo == tipo)
                && (texto == null || e.Titulo.ToLower().Contains(texto) || (e.Institucion != null && e.Institucion.ToLower().Contains(texto)))));
        }

        var page = await q.OrderBy(t => t.Apellidos).ThenBy(t => t.Nombres)
            .Select(t => new PerfilResultadoDto
            {
                TrabajadorId = t.TrabajadorId, NumeroEmpleado = t.NumeroEmpleado, Nombres = t.Nombres, Apellidos = t.Apellidos,
                DepartamentoId = t.DepartamentoId, Departamento = t.Departamento != null ? t.Departamento.Nombre : null,
                PuestoId = t.PuestoId, Puesto = t.Puesto != null ? t.Puesto.Nombre : null, EstadoLaboral = t.EstadoLaboral
            }).ToPagedAsync(f, ct);

        var pagina = page.Items.Select(i => i.TrabajadorId).ToList();
        var habs = await _db.TrabajadoresHabilidad.AsNoTracking()
            .Where(h => h.EmpresaId == empresaId && pagina.Contains(h.TrabajadorId) && h.Estado == EstadoExpediente.Aprobado)
            .OrderByDescending(h => h.Nivel).ThenBy(h => h.Habilidad.Nombre)
            .Select(h => new { h.TrabajadorId, h.Habilidad.Nombre, h.Nivel }).ToListAsync(ct);
        var ests = await _db.TrabajadoresEstudio.AsNoTracking()
            .Where(e => e.EmpresaId == empresaId && pagina.Contains(e.TrabajadorId) && e.Estado == EstadoExpediente.Aprobado)
            .OrderByDescending(e => e.Tipo).ThenByDescending(e => e.FechaFin)
            .Select(e => new { e.TrabajadorId, e.Tipo, e.Titulo }).ToListAsync(ct);
        foreach (var item in page.Items)
        {
            item.Habilidades = habs.Where(h => h.TrabajadorId == item.TrabajadorId)
                .Select(h => $"{h.Nombre} ({NivelHabilidad.Nombre(h.Nivel)})").ToList();
            item.Estudios = ests.Where(e => e.TrabajadorId == item.TrabajadorId)
                .Select(e => $"{TipoEstudio.Nombre(e.Tipo)}: {e.Titulo}").ToList();
        }
        return page;
    }

    // ---------------- Utilidades ----------------
    private static string NormalizarEspacios(string? s) => string.Join(' ', (s ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Estado tras guardar: aprobado si lo registra RH; pendiente si lo propone el trabajador (se limpian datos de resolución previos).</summary>
    private void AplicarEstado(object registro, byte estado, DateTime ahora)
    {
        var usuario = estado == EstadoExpediente.Aprobado ? _current.RequireUsuarioId() : (Guid?)null;
        var resuelto = estado == EstadoExpediente.Aprobado ? ahora : (DateTime?)null;
        switch (registro)
        {
            case TrabajadorHabilidad h: h.Estado = estado; h.MotivoRechazo = null; h.ResueltoPorUsuarioId = usuario; h.ResueltoUtc = resuelto; break;
            case TrabajadorEstudio e: e.Estado = estado; e.MotivoRechazo = null; e.ResueltoPorUsuarioId = usuario; e.ResueltoUtc = resuelto; break;
        }
    }

    private static void Aplicar(TrabajadorEstudio e, GuardarEstudioRequest r)
    {
        if (!TipoEstudio.Todos.Contains(r.Tipo)) throw ServiceException.Validation("Tipo de estudio no válido.");
        var titulo = NormalizarEspacios(r.Titulo);
        if (titulo.Length is < 2 or > 200) throw ServiceException.Validation("El título debe tener entre 2 y 200 caracteres.");
        if (r.FechaInicio is { } i && r.FechaFin is { } f && f < i) throw ServiceException.Validation("La fecha de fin no puede ser anterior a la de inicio.");
        if (r.EnCurso && r.FechaFin is not null) throw ServiceException.Validation("Un estudio en curso no puede tener fecha de fin.");
        e.Tipo = r.Tipo;
        e.Titulo = titulo;
        e.Institucion = Helpers.Limpiar(r.Institucion);
        e.FechaInicio = r.FechaInicio;
        e.FechaFin = r.FechaFin;
        e.EnCurso = r.EnCurso;
        e.Notas = Helpers.Limpiar(r.Notas);
    }

    private static EstudioDto ToDto(TrabajadorEstudio e) => new()
    {
        EstudioId = e.EstudioId, Tipo = e.Tipo, Titulo = e.Titulo, Institucion = e.Institucion, FechaInicio = e.FechaInicio,
        FechaFin = e.FechaFin, EnCurso = e.EnCurso, Notas = e.Notas, Estado = e.Estado, MotivoRechazo = e.MotivoRechazo
    };
}
