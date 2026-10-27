using Microsoft.EntityFrameworkCore;
using RegistroTrabajo.Data;
using RegistroTrabajo.Data.Entities;
using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public interface IEstadosProyectoService
{
    /// <summary>Estados de proyecto de la empresa; si aún no tiene, se crean los habituales. Los inactivos solo los ve quien configura estados.</summary>
    Task<IReadOnlyList<EstadoProyectoDto>> ListarAsync(bool incluirInactivos, CancellationToken ct);
    Task<EstadoProyectoDto> CrearAsync(GuardarEstadoProyectoRequest request, CancellationToken ct);
    Task<EstadoProyectoDto> ActualizarAsync(Guid estadoId, GuardarEstadoProyectoRequest request, CancellationToken ct);
}

public interface IProyectosService
{
    Task<ProyectosResumenDto> ResumenAsync(CancellationToken ct);
    Task<PagedResult<ProyectoResumenDto>> ListarAsync(ProyectoFiltro filtro, CancellationToken ct);
    /// <summary>Un proyecto que el usuario no puede ver responde 404, aunque conozca el identificador.</summary>
    Task<ProyectoDetalleDto> ObtenerAsync(Guid proyectoId, CancellationToken ct);
    Task<ProyectoDetalleDto> CrearAsync(GuardarProyectoRequest request, CancellationToken ct);
    Task<ProyectoDetalleDto> ActualizarAsync(Guid proyectoId, GuardarProyectoRequest request, CancellationToken ct);
    Task<ProyectoDetalleDto> AsignarTrabajadoresAsync(Guid proyectoId, AsignarTrabajadoresRequest request, CancellationToken ct);
    Task<ProyectoDetalleDto> RetirarTrabajadorAsync(Guid proyectoId, Guid trabajadorId, CancellationToken ct);
    Task<ProyectoDetalleDto> CambiarResponsableAsync(Guid proyectoId, CambiarResponsableRequest request, CancellationToken ct);
    /// <summary>Mover a un estado final finaliza o cancela el proyecto; salir de un estado final lo reabre (solo administración).</summary>
    Task<ProyectoDetalleDto> CambiarEstadoAsync(Guid proyectoId, CambiarEstadoProyectoRequest request, CancellationToken ct);
    Task<IReadOnlyList<HistorialEstadoProyectoDto>> HistorialAsync(Guid proyectoId, CancellationToken ct);
}

/// <summary>Quién es el usuario frente a los proyectos: su expediente, si ve todo y a quién supervisa.</summary>
internal sealed record AccesoProyectos(Guid EmpresaId, Guid UsuarioId, Guid? MiTrabajadorId, bool VerTodos, IReadOnlyList<Guid> Supervisados)
{
    /// <summary>Administra el proyecto quien ve todo, es su responsable o supervisa al responsable.</summary>
    public bool Administra(Guid responsableId) => VerTodos || MiTrabajadorId == responsableId || Supervisados.Contains(responsableId);
    /// <summary>Solo el responsable (o quien ve todo) finaliza y revisa en nombre del proyecto.</summary>
    public bool EsResponsableOTodos(Guid responsableId) => VerTodos || MiTrabajadorId == responsableId;
}

internal static class ProyectoHelper
{
    public const int DiasAvisoVencimiento = 7;

    public static async Task<AccesoProyectos> AccesoAsync(RegistroTrabajoDbContext db, ICurrentUser current, IPermisoService permisos, TimeProvider clock, CancellationToken ct)
    {
        var empresaId = current.RequireEmpresaId();
        var usuarioId = current.RequireUsuarioId();
        var mi = await TrabajadorHelper.IdDeUsuarioAsync(db, empresaId, usuarioId, ct);
        var todos = await permisos.TienePermisoAsync(Permisos.ProyectosConsultarTodos, ct);
        var supervisados = new List<Guid>();
        // Un supervisor con permiso para modificar ve y administra los proyectos que lidera su gente.
        if (!todos && mi is { } m && await permisos.TienePermisoAsync(Permisos.ProyectosModificar, ct))
        {
            var hoy = TrabajadorHelper.Hoy(clock);
            supervisados = await db.TrabajadorSupervisores.AsNoTracking()
                .Where(s => s.EmpresaId == empresaId && s.SupervisorTrabajadorId == m && s.FechaInicio <= hoy && (s.FechaFin == null || s.FechaFin >= hoy))
                .Select(s => s.TrabajadorId).Distinct().ToListAsync(ct);
        }
        return new AccesoProyectos(empresaId, usuarioId, mi, todos, supervisados);
    }

    /// <summary>Proyectos que el usuario puede ver: todos, o aquellos donde participa (él o quienes supervisa).</summary>
    public static IQueryable<Proyecto> Visibles(RegistroTrabajoDbContext db, AccesoProyectos a, bool tracking = false)
    {
        var q = tracking ? db.Proyectos : db.Proyectos.AsNoTracking();
        q = q.Where(p => p.EmpresaId == a.EmpresaId);
        if (a.VerTodos) return q;
        var grupo = a.Supervisados.ToList();
        if (a.MiTrabajadorId is { } mi) grupo.Add(mi);
        var empresaId = a.EmpresaId;
        return q.Where(p => grupo.Contains(p.ResponsableTrabajadorId)
            || db.AsignacionesProyecto.Any(x => x.EmpresaId == empresaId && x.ProyectoId == p.ProyectoId && x.FechaSalida == null && grupo.Contains(x.TrabajadorId)));
    }

    public static async Task<Proyecto> CargarVisibleAsync(RegistroTrabajoDbContext db, AccesoProyectos a, Guid proyectoId, bool tracking, CancellationToken ct) =>
        await Visibles(db, a, tracking).FirstOrDefaultAsync(p => p.ProyectoId == proyectoId, ct)
            ?? throw ServiceException.NotFound("Proyecto no encontrado.");

    public static async Task<DateOnly> HoyAsync(RegistroTrabajoDbContext db, Guid empresaId, TimeProvider clock, CancellationToken ct)
    {
        var zona = AsistenciaHelper.Zona(await db.ConfiguracionEmpresa.AsNoTracking().Where(c => c.EmpresaId == empresaId).Select(c => c.ZonaHorariaId).FirstOrDefaultAsync(ct));
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(clock.GetUtcNow().UtcDateTime, zona));
    }

    public static Task<bool> EstaCerradoAsync(RegistroTrabajoDbContext db, Proyecto p, CancellationToken ct) =>
        db.EstadosProyecto.AsNoTracking().AnyAsync(e => e.EmpresaId == p.EmpresaId && e.EstadoProyectoId == p.EstadoProyectoId && e.EsFinal, ct);

    public static async Task<List<Guid>> ParticipantesAsync(RegistroTrabajoDbContext db, Proyecto p, CancellationToken ct)
    {
        var ids = await db.AsignacionesProyecto.AsNoTracking()
            .Where(a => a.EmpresaId == p.EmpresaId && a.ProyectoId == p.ProyectoId && a.FechaSalida == null)
            .Select(a => a.TrabajadorId).ToListAsync(ct);
        if (!ids.Contains(p.ResponsableTrabajadorId)) ids.Add(p.ResponsableTrabajadorId);
        return ids;
    }

    public static async Task<Dictionary<Guid, string>> NombresTrabajadoresAsync(RegistroTrabajoDbContext db, Guid empresaId, IEnumerable<Guid> ids, CancellationToken ct)
    {
        var lista = ids.Distinct().ToList();
        if (lista.Count == 0) return new();
        return await db.Trabajadores.AsNoTracking().Where(t => t.EmpresaId == empresaId && lista.Contains(t.TrabajadorId))
            .Select(t => new { t.TrabajadorId, t.Nombres, t.Apellidos })
            .ToDictionaryAsync(t => t.TrabajadorId, t => (t.Nombres + " " + t.Apellidos).Trim(), ct);
    }

    public static async Task<Dictionary<Guid, string>> NombresUsuariosAsync(RegistroTrabajoDbContext db, Guid empresaId, IEnumerable<Guid> ids, CancellationToken ct)
    {
        var lista = ids.Distinct().ToList();
        if (lista.Count == 0) return new();
        return await db.Usuarios.AsNoTracking().Where(u => u.EmpresaId == empresaId && lista.Contains(u.UsuarioId))
            .Select(u => new { u.UsuarioId, u.Nombre, u.Apellido })
            .ToDictionaryAsync(u => u.UsuarioId, u => (u.Nombre + " " + u.Apellido).Trim(), ct);
    }

    /// <summary>Cuenta de usuario de cada trabajador que tiene una (por Trabajadores.UsuarioId o Usuarios.TrabajadorId).</summary>
    public static async Task<Dictionary<Guid, Guid>> UsuariosDeTrabajadoresAsync(RegistroTrabajoDbContext db, Guid empresaId, IEnumerable<Guid> trabajadorIds, CancellationToken ct)
    {
        var lista = trabajadorIds.Distinct().ToList();
        var res = new Dictionary<Guid, Guid>();
        if (lista.Count == 0) return res;
        var a = await db.Trabajadores.AsNoTracking().Where(t => t.EmpresaId == empresaId && lista.Contains(t.TrabajadorId) && t.UsuarioId != null)
            .Select(t => new { t.TrabajadorId, UsuarioId = t.UsuarioId!.Value }).ToListAsync(ct);
        foreach (var x in a) res[x.TrabajadorId] = x.UsuarioId;
        var b = await db.Usuarios.AsNoTracking().Where(u => u.EmpresaId == empresaId && u.TrabajadorId != null && lista.Contains(u.TrabajadorId!.Value))
            .Select(u => new { TrabajadorId = u.TrabajadorId!.Value, u.UsuarioId }).ToListAsync(ct);
        foreach (var x in b) res.TryAdd(x.TrabajadorId, x.UsuarioId);
        return res;
    }

    /// <summary>Trabajadores activos de la empresa; lanza validación si alguno no existe o no está activo.</summary>
    public static async Task ValidarTrabajadoresActivosAsync(RegistroTrabajoDbContext db, Guid empresaId, IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        var activos = await db.Trabajadores.AsNoTracking()
            .CountAsync(t => t.EmpresaId == empresaId && ids.Contains(t.TrabajadorId) && t.EstadoLaboral == EstadoLaboral.Activo, ct);
        if (activos != ids.Count) throw ServiceException.Validation("Algún trabajador indicado no existe o no está activo.");
    }

    /// <summary>Compara la versión que vio el cliente con la actual y la fija como original para detectar ediciones concurrentes.</summary>
    public static void AplicarRowVersion<T>(DbContext db, T entidad, byte[]? actual, string? rowVersion, string que) where T : class
    {
        if (string.IsNullOrWhiteSpace(rowVersion)) throw ServiceException.Validation($"Falta la versión de {que}. Recargue e intente de nuevo.");
        byte[] enviada;
        try { enviada = Convert.FromBase64String(rowVersion); }
        catch (FormatException) { throw ServiceException.Validation($"La versión de {que} no es válida."); }
        if (actual is { Length: > 0 } && !actual.AsSpan().SequenceEqual(enviada))
            throw ServiceException.Conflict($"{char.ToUpperInvariant(que[0])}{que[1..]} fue modificado por otra persona. Recargue e intente de nuevo.");
        db.Entry(entidad).Property("RowVersion").OriginalValue = enviada;
    }

    public static string Version(byte[]? rv) => Convert.ToBase64String(rv ?? Array.Empty<byte>());

    public static string Recortar(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";
}

internal static class NotificacionesHelper
{
    /// <summary>
    /// Agrega notificaciones internas (se guardan con el SaveChanges de la operación) y respeta las preferencias del usuario.
    /// Los textos son genéricos a propósito: no copian descripciones ni comentarios.
    /// </summary>
    /// <remarks>
    /// Los tipos obligatorios (<see cref="CatalogoNotificaciones"/>) ignoran las preferencias; los opcionales se silencian por tipo o por categoría.
    /// Con <paramref name="clave"/> el mismo evento no avisa dos veces al mismo usuario (índice único filtrado en BD y comprobación previa).
    /// </remarks>
    public static async Task NotificarAsync(RegistroTrabajoDbContext db, Guid empresaId, IEnumerable<Guid> destinatarios, string tipo,
        string titulo, string mensaje, string entidadTipo, string entidadId, DateTime now, CancellationToken ct, string? clave = null)
    {
        var ids = destinatarios.Distinct().ToList();
        if (ids.Count == 0) return;

        if (!CatalogoNotificaciones.EsObligatoria(tipo))
        {
            var categoria = CatalogoNotificaciones.CategoriaDe(tipo);
            var silenciados = await db.PreferenciasNotificacion.AsNoTracking()
                .Where(p => p.EmpresaId == empresaId && (p.TipoNotificacion == tipo || p.TipoNotificacion == categoria) && !p.Habilitada && ids.Contains(p.UsuarioId))
                .Select(p => p.UsuarioId).ToListAsync(ct);
            ids = ids.Except(silenciados).ToList();
        }

        if (clave is not null)
        {
            clave = ProyectoHelper.Recortar(clave, 200);
            var yaAvisados = await db.Notificaciones.AsNoTracking()
                .Where(n => n.EmpresaId == empresaId && n.ClaveDeduplicacion == clave && ids.Contains(n.UsuarioDestinoId))
                .Select(n => n.UsuarioDestinoId).ToListAsync(ct);
            var pendientes = db.Notificaciones.Local.Where(n => n.EmpresaId == empresaId && n.ClaveDeduplicacion == clave).Select(n => n.UsuarioDestinoId);
            ids = ids.Except(yaAvisados).Except(pendientes).ToList();
        }

        foreach (var u in ids)
            db.Notificaciones.Add(new Notificacion
            {
                NotificacionId = Guid.NewGuid(), EmpresaId = empresaId, UsuarioDestinoId = u, Tipo = tipo,
                Titulo = ProyectoHelper.Recortar(titulo, 180), Mensaje = ProyectoHelper.Recortar(mensaje, 1000),
                EntidadTipo = entidadTipo, EntidadId = entidadId, CreadaUtc = now, ClaveDeduplicacion = clave
            });
    }
}

public class EstadosProyectoService : IEstadosProyectoService
{
    private static readonly (string Nombre, bool EsFinal)[] Habituales =
    {
        ("Planeación", false), ("En proceso", false), ("En pausa", false), ("Finalizado", true), ("Cancelado", true)
    };

    private readonly RegistroTrabajoDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IPermisoService _permisos;
    private readonly IAuditoriaService _auditoria;

    public EstadosProyectoService(RegistroTrabajoDbContext db, ICurrentUser current, IPermisoService permisos, IAuditoriaService auditoria)
    {
        _db = db; _current = current; _permisos = permisos; _auditoria = auditoria;
    }

    /// <summary>Siembra los estados habituales la primera vez (las empresas nuevas no los traen del bootstrap).</summary>
    internal static async Task AsegurarAsync(RegistroTrabajoDbContext db, Guid empresaId, CancellationToken ct)
    {
        if (await db.EstadosProyecto.AnyAsync(e => e.EmpresaId == empresaId, ct)) return;
        short orden = 1;
        foreach (var (nombre, final) in Habituales)
            db.EstadosProyecto.Add(new EstadoProyecto { EstadoProyectoId = Guid.NewGuid(), EmpresaId = empresaId, Nombre = nombre, Orden = orden++, EsFinal = final, Estado = true });
        await db.SaveSafeAsync(ct);
    }

    public async Task<IReadOnlyList<EstadoProyectoDto>> ListarAsync(bool incluirInactivos, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.ProyectosConsultar, ct);
        var empresaId = _current.RequireEmpresaId();
        await AsegurarAsync(_db, empresaId, ct);
        var verInactivos = incluirInactivos && await _permisos.TienePermisoAsync(Permisos.ProyectosConfigurarEstados, ct);
        var q = _db.EstadosProyecto.AsNoTracking().Where(e => e.EmpresaId == empresaId);
        if (!verInactivos) q = q.Where(e => e.Estado);
        return await q.OrderBy(e => e.Orden).ThenBy(e => e.Nombre)
            .Select(e => new EstadoProyectoDto
            {
                EstadoProyectoId = e.EstadoProyectoId, Nombre = e.Nombre, Orden = e.Orden, EsFinal = e.EsFinal, Activo = e.Estado,
                Proyectos = _db.Proyectos.Count(p => p.EmpresaId == empresaId && p.EstadoProyectoId == e.EstadoProyectoId)
            }).ToListAsync(ct);
    }

    public async Task<EstadoProyectoDto> CrearAsync(GuardarEstadoProyectoRequest r, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.ProyectosConfigurarEstados, ct);
        var empresaId = _current.RequireEmpresaId();
        await AsegurarAsync(_db, empresaId, ct);
        var nombre = Helpers.Limpiar(r.Nombre) ?? throw ServiceException.Validation("El nombre es obligatorio.");
        await ValidarNombreAsync(empresaId, nombre, null, ct);
        var e = new EstadoProyecto { EstadoProyectoId = Guid.NewGuid(), EmpresaId = empresaId, Nombre = nombre, Orden = r.Orden, EsFinal = r.EsFinal, Estado = r.Activo };
        _db.EstadosProyecto.Add(e);
        _auditoria.Registrar("Proyectos", nameof(EstadoProyecto), e.EstadoProyectoId.ToString(), "CrearEstado", null, new { e.Nombre, e.Orden, e.EsFinal, e.Estado });
        await _db.SaveSafeAsync(ct);
        return new EstadoProyectoDto { EstadoProyectoId = e.EstadoProyectoId, Nombre = e.Nombre, Orden = e.Orden, EsFinal = e.EsFinal, Activo = e.Estado };
    }

    public async Task<EstadoProyectoDto> ActualizarAsync(Guid estadoId, GuardarEstadoProyectoRequest r, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.ProyectosConfigurarEstados, ct);
        var empresaId = _current.RequireEmpresaId();
        var e = await _db.EstadosProyecto.FirstOrDefaultAsync(x => x.EstadoProyectoId == estadoId && x.EmpresaId == empresaId, ct)
            ?? throw ServiceException.NotFound("Estado no encontrado.");
        var nombre = Helpers.Limpiar(r.Nombre) ?? throw ServiceException.Validation("El nombre es obligatorio.");
        await ValidarNombreAsync(empresaId, nombre, estadoId, ct);

        var enUso = await _db.Proyectos.CountAsync(p => p.EmpresaId == empresaId && p.EstadoProyectoId == estadoId, ct);
        if (enUso > 0 && r.EsFinal != e.EsFinal)
            throw ServiceException.Conflict("No se puede cambiar si un estado es final mientras haya proyectos en ese estado.");
        if (enUso > 0 && e.Estado && !r.Activo)
            throw ServiceException.Conflict("No se puede desactivar un estado que tienen proyectos.");
        if (e.Estado && (!r.Activo || r.EsFinal != e.EsFinal))
        {
            // Siempre debe quedar al menos un estado activo abierto y uno final para iniciar y cerrar proyectos.
            var otros = _db.EstadosProyecto.Where(x => x.EmpresaId == empresaId && x.EstadoProyectoId != estadoId && x.Estado);
            if (!await otros.AnyAsync(x => !x.EsFinal, ct) && (r.EsFinal || !r.Activo) || !await otros.AnyAsync(x => x.EsFinal, ct) && (!r.EsFinal || !r.Activo))
                throw ServiceException.Conflict("Debe existir al menos un estado activo abierto y uno final.");
        }

        var antes = new { e.Nombre, e.Orden, e.EsFinal, e.Estado };
        e.Nombre = nombre; e.Orden = r.Orden; e.EsFinal = r.EsFinal; e.Estado = r.Activo;
        _auditoria.Registrar("Proyectos", nameof(EstadoProyecto), e.EstadoProyectoId.ToString(), "ActualizarEstado", antes, new { e.Nombre, e.Orden, e.EsFinal, e.Estado });
        await _db.SaveSafeAsync(ct);
        return new EstadoProyectoDto { EstadoProyectoId = e.EstadoProyectoId, Nombre = e.Nombre, Orden = e.Orden, EsFinal = e.EsFinal, Activo = e.Estado, Proyectos = enUso };
    }

    private async Task ValidarNombreAsync(Guid empresaId, string nombre, Guid? excepto, CancellationToken ct)
    {
        var n = nombre.ToLower();
        if (await _db.EstadosProyecto.AnyAsync(x => x.EmpresaId == empresaId && x.Nombre.ToLower() == n && x.EstadoProyectoId != excepto, ct))
            throw ServiceException.Conflict("Ya existe un estado con ese nombre.");
    }
}

public class ProyectosService : IProyectosService
{
    private readonly RegistroTrabajoDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IPermisoService _permisos;
    private readonly IAuditoriaService _auditoria;
    private readonly TimeProvider _clock;

    public ProyectosService(RegistroTrabajoDbContext db, ICurrentUser current, IPermisoService permisos, IAuditoriaService auditoria, TimeProvider clock)
    {
        _db = db; _current = current; _permisos = permisos; _auditoria = auditoria; _clock = clock;
    }

    private DateTime Ahora => _clock.GetUtcNow().UtcDateTime;

    // ---------------- Consulta ----------------
    public async Task<ProyectosResumenDto> ResumenAsync(CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.ProyectosConsultar, ct);
        var a = await ProyectoHelper.AccesoAsync(_db, _current, _permisos, _clock, ct);
        await EstadosProyectoService.AsegurarAsync(_db, a.EmpresaId, ct);
        var hoy = await ProyectoHelper.HoyAsync(_db, a.EmpresaId, _clock, ct);
        var limite = hoy.AddDays(ProyectoHelper.DiasAvisoVencimiento);
        var cal = await CalendarioHabil.CargarAsync(_db, a.EmpresaId, ct);
        var ultimoHabil = cal.UltimoHabilAnterior(hoy);

        var estados = await _db.EstadosProyecto.AsNoTracking().Where(e => e.EmpresaId == a.EmpresaId && e.Estado).OrderBy(e => e.Orden).ToListAsync(ct);
        var porEstado = await ProyectoHelper.Visibles(_db, a).GroupBy(p => p.EstadoProyectoId).Select(g => new { Id = g.Key, N = g.Count() }).ToListAsync(ct);
        var finales = estados.Where(e => e.EsFinal).Select(e => e.EstadoProyectoId).ToList();

        var res = new ProyectosResumenDto { Total = porEstado.Sum(x => x.N) };
        foreach (var e in estados)
            res.PorEstado.Add(new ResumenEstadoDto { EstadoProyectoId = e.EstadoProyectoId, Nombre = e.Nombre, EsFinal = e.EsFinal, Total = porEstado.FirstOrDefault(x => x.Id == e.EstadoProyectoId)?.N ?? 0 });
        var abiertos = ProyectoHelper.Visibles(_db, a).Where(p => !finales.Contains(p.EstadoProyectoId) && p.FechaFinPrevista != null);
        res.ProximosAVencer = await abiertos.CountAsync(p => p.FechaFinPrevista >= hoy && p.FechaFinPrevista <= limite, ct);
        res.Vencidos = await abiertos.CountAsync(p => p.FechaFinPrevista < hoy, ct);
        if (a.MiTrabajadorId is { } mi)
            res.TareasVencidasMias = await _db.Tareas.AsNoTracking().CountAsync(t => t.EmpresaId == a.EmpresaId && t.Estado != EstadoTarea.Completada && t.Estado != EstadoTarea.Cancelada
                && t.FechaVencimiento != null && t.FechaVencimiento <= ultimoHabil
                && _db.AsignacionesTarea.Any(x => x.EmpresaId == a.EmpresaId && x.TareaId == t.TareaId && x.TrabajadorId == mi && x.FechaRetiroUtc == null), ct);
        return res;
    }

    public async Task<PagedResult<ProyectoResumenDto>> ListarAsync(ProyectoFiltro f, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.ProyectosConsultar, ct);
        var a = await ProyectoHelper.AccesoAsync(_db, _current, _permisos, _clock, ct);
        var hoy = await ProyectoHelper.HoyAsync(_db, a.EmpresaId, _clock, ct);
        var q = ProyectoHelper.Visibles(_db, a);

        if (!string.IsNullOrWhiteSpace(f.Buscar))
        {
            var b = f.Buscar.Trim().ToLower();
            q = q.Where(p => p.Nombre.ToLower().Contains(b));
        }
        if (f.EstadoProyectoId is { } est) q = q.Where(p => p.EstadoProyectoId == est);
        if (f.Prioridad is { } pr) q = q.Where(p => p.Prioridad == pr);
        if (!f.IncluirFinalizados) q = q.Where(p => !_db.EstadosProyecto.Any(e => e.EstadoProyectoId == p.EstadoProyectoId && e.EsFinal));
        if (f.SoloMios && a.MiTrabajadorId is { } mi)
            q = q.Where(p => p.ResponsableTrabajadorId == mi || _db.AsignacionesProyecto.Any(x => x.EmpresaId == p.EmpresaId && x.ProyectoId == p.ProyectoId && x.FechaSalida == null && x.TrabajadorId == mi));
        else if (f.SoloMios) q = q.Where(_ => false);
        if (f.SoloProximosAVencer)
        {
            var limite = hoy.AddDays(ProyectoHelper.DiasAvisoVencimiento);
            q = q.Where(p => p.FechaFinPrevista != null && p.FechaFinPrevista >= hoy && p.FechaFinPrevista <= limite
                && !_db.EstadosProyecto.Any(e => e.EstadoProyectoId == p.EstadoProyectoId && e.EsFinal));
        }

        var pagina = await q.OrderBy(p => _db.EstadosProyecto.Any(e => e.EstadoProyectoId == p.EstadoProyectoId && e.EsFinal) ? 1 : 0)
            .ThenByDescending(p => p.Prioridad).ThenBy(p => p.FechaFinPrevista == null).ThenBy(p => p.FechaFinPrevista).ThenBy(p => p.Nombre)
            .ToPagedAsync(f, ct);
        var items = await ResumenesAsync(a.EmpresaId, pagina.Items, hoy, ct);
        return new PagedResult<ProyectoResumenDto> { Items = items, Page = pagina.Page, PageSize = pagina.PageSize, Total = pagina.Total };
    }

    public async Task<ProyectoDetalleDto> ObtenerAsync(Guid proyectoId, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.ProyectosConsultar, ct);
        var a = await ProyectoHelper.AccesoAsync(_db, _current, _permisos, _clock, ct);
        var p = await ProyectoHelper.CargarVisibleAsync(_db, a, proyectoId, false, ct);
        return await DetalleAsync(p, a, ct);
    }

    public async Task<IReadOnlyList<HistorialEstadoProyectoDto>> HistorialAsync(Guid proyectoId, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.ProyectosConsultar, ct);
        var a = await ProyectoHelper.AccesoAsync(_db, _current, _permisos, _clock, ct);
        await ProyectoHelper.CargarVisibleAsync(_db, a, proyectoId, false, ct);
        var filas = await _db.HistorialEstadoProyecto.AsNoTracking().Where(h => h.EmpresaId == a.EmpresaId && h.ProyectoId == proyectoId)
            .OrderByDescending(h => h.FechaUtc).ThenByDescending(h => h.HistorialEstadoProyectoId).ToListAsync(ct);
        var estados = await _db.EstadosProyecto.AsNoTracking().Where(e => e.EmpresaId == a.EmpresaId).ToDictionaryAsync(e => e.EstadoProyectoId, e => e.Nombre, ct);
        var nombres = await ProyectoHelper.NombresUsuariosAsync(_db, a.EmpresaId, filas.Select(h => h.CambiadoPorUsuarioId), ct);
        return filas.Select(h => new HistorialEstadoProyectoDto
        {
            Id = h.HistorialEstadoProyectoId,
            EstadoAnterior = h.EstadoAnteriorId is { } ant && estados.TryGetValue(ant, out var na) ? na : null,
            EstadoNuevo = estados.GetValueOrDefault(h.EstadoNuevoId, "—"),
            CambiadoPor = nombres.GetValueOrDefault(h.CambiadoPorUsuarioId, "—"), FechaUtc = h.FechaUtc, Comentario = h.Comentario
        }).ToList();
    }

    // ---------------- Alta y edición ----------------
    public async Task<ProyectoDetalleDto> CrearAsync(GuardarProyectoRequest r, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.ProyectosCrear, ct);
        var a = await ProyectoHelper.AccesoAsync(_db, _current, _permisos, _clock, ct);
        await EstadosProyectoService.AsegurarAsync(_db, a.EmpresaId, ct);

        var estado = r.EstadoProyectoId is { } eid
            ? await _db.EstadosProyecto.FirstOrDefaultAsync(e => e.EmpresaId == a.EmpresaId && e.EstadoProyectoId == eid && e.Estado, ct)
            : await _db.EstadosProyecto.Where(e => e.EmpresaId == a.EmpresaId && e.Estado && !e.EsFinal).OrderBy(e => e.Orden).FirstOrDefaultAsync(ct);
        if (estado is null) throw ServiceException.Validation("El estado indicado no existe o no está activo.");
        if (estado.EsFinal) throw ServiceException.Validation("Un proyecto nuevo no puede iniciar en un estado final.");

        var responsable = r.ResponsableTrabajadorId ?? a.MiTrabajadorId
            ?? throw ServiceException.Validation("Indique el responsable del proyecto (su cuenta no está vinculada a un expediente).");
        if (!a.Administra(responsable)) throw ServiceException.Forbidden("Solo puede designar como responsable a usted o a quienes supervisa.");
        await ProyectoHelper.ValidarTrabajadoresActivosAsync(_db, a.EmpresaId, new[] { responsable }, ct);

        var now = Ahora;
        var hoy = await ProyectoHelper.HoyAsync(_db, a.EmpresaId, _clock, ct);
        var nombre = Helpers.Limpiar(r.Nombre) ?? throw ServiceException.Validation("El nombre es obligatorio.");
        var p = new Proyecto
        {
            ProyectoId = Guid.NewGuid(), EmpresaId = a.EmpresaId, Nombre = nombre, Descripcion = Helpers.Limpiar(r.Descripcion),
            FechaInicio = r.FechaInicio, FechaFinPrevista = r.FechaFinPrevista, EstadoProyectoId = estado.EstadoProyectoId, Prioridad = r.Prioridad,
            ResponsableTrabajadorId = responsable, CreadoPorUsuarioId = a.UsuarioId, FechaCreacionUtc = now
        };
        _db.Proyectos.Add(p);
        _db.AsignacionesProyecto.Add(new AsignacionProyecto
        {
            AsignacionProyectoId = Guid.NewGuid(), EmpresaId = a.EmpresaId, ProyectoId = p.ProyectoId, TrabajadorId = responsable,
            FechaAsignacion = hoy, AsignadoPorUsuarioId = a.UsuarioId, RolEnProyecto = "Responsable"
        });
        _db.HistorialEstadoProyecto.Add(new HistorialEstadoProyecto
        {
            EmpresaId = a.EmpresaId, ProyectoId = p.ProyectoId, EstadoAnteriorId = null, EstadoNuevoId = estado.EstadoProyectoId,
            CambiadoPorUsuarioId = a.UsuarioId, FechaUtc = now, Comentario = "Creación del proyecto"
        });
        _auditoria.Registrar("Proyectos", nameof(Proyecto), p.ProyectoId.ToString(), "Crear", null, Resumen(p));
        await NotificarNuevosAsync(a, p, new[] { responsable }, now, ct);
        await _db.SaveSafeAsync(ct);
        return await DetalleAsync(p, a, ct);
    }

    public async Task<ProyectoDetalleDto> ActualizarAsync(Guid proyectoId, GuardarProyectoRequest r, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.ProyectosModificar, ct);
        var a = await ProyectoHelper.AccesoAsync(_db, _current, _permisos, _clock, ct);
        var p = await ProyectoHelper.CargarVisibleAsync(_db, a, proyectoId, true, ct);
        if (!a.Administra(p.ResponsableTrabajadorId)) throw ServiceException.Forbidden("No administra este proyecto.");
        await AsegurarAbiertoAsync(p, ct);
        ProyectoHelper.AplicarRowVersion(_db, p, p.RowVersion, r.RowVersion, "el proyecto");

        var antes = Resumen(p);
        var fechaAnterior = p.FechaFinPrevista;
        p.Nombre = Helpers.Limpiar(r.Nombre) ?? throw ServiceException.Validation("El nombre es obligatorio.");
        p.Descripcion = Helpers.Limpiar(r.Descripcion);
        p.FechaInicio = r.FechaInicio; p.FechaFinPrevista = r.FechaFinPrevista; p.Prioridad = r.Prioridad;
        var now = Ahora;
        p.FechaActualizacionUtc = now;
        _auditoria.Registrar("Proyectos", nameof(Proyecto), p.ProyectoId.ToString(), "Actualizar", antes, Resumen(p));
        if (fechaAnterior != r.FechaFinPrevista) await NotificarFechaAsync(a, p, now, ct);
        await _db.SaveSafeAsync(ct);
        return await DetalleAsync(p, a, ct);
    }

    // ---------------- Equipo ----------------
    public async Task<ProyectoDetalleDto> AsignarTrabajadoresAsync(Guid proyectoId, AsignarTrabajadoresRequest r, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.ProyectosAsignar, ct);
        var a = await ProyectoHelper.AccesoAsync(_db, _current, _permisos, _clock, ct);
        var p = await ProyectoHelper.CargarVisibleAsync(_db, a, proyectoId, true, ct);
        if (!a.Administra(p.ResponsableTrabajadorId)) throw ServiceException.Forbidden("No administra este proyecto.");
        await AsegurarAbiertoAsync(p, ct);

        var ids = r.TrabajadorIds.Distinct().ToList();
        await ProyectoHelper.ValidarTrabajadoresActivosAsync(_db, a.EmpresaId, ids, ct);
        var actuales = await ProyectoHelper.ParticipantesAsync(_db, p, ct);
        var nuevos = ids.Except(actuales).ToList();
        if (nuevos.Count == 0) throw ServiceException.Conflict("Los trabajadores indicados ya participan en el proyecto.");

        var now = Ahora;
        var hoy = await ProyectoHelper.HoyAsync(_db, a.EmpresaId, _clock, ct);
        foreach (var t in nuevos)
            _db.AsignacionesProyecto.Add(new AsignacionProyecto
            {
                AsignacionProyectoId = Guid.NewGuid(), EmpresaId = a.EmpresaId, ProyectoId = p.ProyectoId, TrabajadorId = t,
                FechaAsignacion = hoy, AsignadoPorUsuarioId = a.UsuarioId, RolEnProyecto = Helpers.Limpiar(r.RolEnProyecto)
            });
        p.FechaActualizacionUtc = now;
        _auditoria.Registrar("Proyectos", nameof(Proyecto), p.ProyectoId.ToString(), "AsignarTrabajadores", null, new { Trabajadores = nuevos });
        await NotificarNuevosAsync(a, p, nuevos, now, ct);
        await _db.SaveSafeAsync(ct);
        return await DetalleAsync(p, a, ct);
    }

    public async Task<ProyectoDetalleDto> RetirarTrabajadorAsync(Guid proyectoId, Guid trabajadorId, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.ProyectosAsignar, ct);
        var a = await ProyectoHelper.AccesoAsync(_db, _current, _permisos, _clock, ct);
        var p = await ProyectoHelper.CargarVisibleAsync(_db, a, proyectoId, true, ct);
        if (!a.Administra(p.ResponsableTrabajadorId)) throw ServiceException.Forbidden("No administra este proyecto.");
        await AsegurarAbiertoAsync(p, ct);
        if (trabajadorId == p.ResponsableTrabajadorId) throw ServiceException.Conflict("No se puede retirar al responsable; cambie primero al responsable del proyecto.");

        var asignacion = await _db.AsignacionesProyecto.FirstOrDefaultAsync(x => x.EmpresaId == a.EmpresaId && x.ProyectoId == proyectoId && x.TrabajadorId == trabajadorId && x.FechaSalida == null, ct)
            ?? throw ServiceException.NotFound("El trabajador no participa en el proyecto.");
        var now = Ahora;
        var hoy = await ProyectoHelper.HoyAsync(_db, a.EmpresaId, _clock, ct);
        asignacion.FechaSalida = hoy < asignacion.FechaAsignacion ? asignacion.FechaAsignacion : hoy;

        // Sus tareas abiertas del proyecto quedan sin él; las completadas conservan su historial.
        var abiertas = await _db.AsignacionesTarea.Where(x => x.EmpresaId == a.EmpresaId && x.TrabajadorId == trabajadorId && x.FechaRetiroUtc == null
            && _db.Tareas.Any(t => t.EmpresaId == a.EmpresaId && t.TareaId == x.TareaId && t.ProyectoId == proyectoId
                && t.Estado != EstadoTarea.Completada && t.Estado != EstadoTarea.Cancelada)).ToListAsync(ct);
        foreach (var x in abiertas)
        {
            x.FechaRetiroUtc = now < x.FechaAsignacionUtc ? x.FechaAsignacionUtc : now;
            SeguimientoHelper.Evento(_db, a.EmpresaId, x.TareaId, TipoEventoTarea.AsignacionRetirada, a.UsuarioId, now,
                anterior: (await ProyectoHelper.NombresTrabajadoresAsync(_db, a.EmpresaId, new[] { trabajadorId }, ct)).GetValueOrDefault(trabajadorId, "—"),
                detalle: "Retirado del proyecto");
        }
        p.FechaActualizacionUtc = now;
        _auditoria.Registrar("Proyectos", nameof(Proyecto), p.ProyectoId.ToString(), "RetirarTrabajador", new { TrabajadorId = trabajadorId }, new { TareasLiberadas = abiertas.Count });
        await _db.SaveSafeAsync(ct);
        return await DetalleAsync(p, a, ct);
    }

    public async Task<ProyectoDetalleDto> CambiarResponsableAsync(Guid proyectoId, CambiarResponsableRequest r, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.ProyectosCambiarResponsable, ct);
        var a = await ProyectoHelper.AccesoAsync(_db, _current, _permisos, _clock, ct);
        var p = await ProyectoHelper.CargarVisibleAsync(_db, a, proyectoId, true, ct);
        await AsegurarAbiertoAsync(p, ct);
        ProyectoHelper.AplicarRowVersion(_db, p, p.RowVersion, r.RowVersion, "el proyecto");
        if (r.ResponsableTrabajadorId == p.ResponsableTrabajadorId) throw ServiceException.Validation("Ese trabajador ya es el responsable.");
        await ProyectoHelper.ValidarTrabajadoresActivosAsync(_db, a.EmpresaId, new[] { r.ResponsableTrabajadorId }, ct);

        var now = Ahora;
        var anterior = p.ResponsableTrabajadorId;
        p.ResponsableTrabajadorId = r.ResponsableTrabajadorId;
        p.FechaActualizacionUtc = now;
        var participa = await _db.AsignacionesProyecto.AnyAsync(x => x.EmpresaId == a.EmpresaId && x.ProyectoId == proyectoId && x.TrabajadorId == r.ResponsableTrabajadorId && x.FechaSalida == null, ct);
        if (!participa)
            _db.AsignacionesProyecto.Add(new AsignacionProyecto
            {
                AsignacionProyectoId = Guid.NewGuid(), EmpresaId = a.EmpresaId, ProyectoId = proyectoId, TrabajadorId = r.ResponsableTrabajadorId,
                FechaAsignacion = await ProyectoHelper.HoyAsync(_db, a.EmpresaId, _clock, ct), AsignadoPorUsuarioId = a.UsuarioId, RolEnProyecto = "Responsable"
            });
        _auditoria.Registrar("Proyectos", nameof(Proyecto), p.ProyectoId.ToString(), "CambiarResponsable",
            new { ResponsableTrabajadorId = anterior }, new { p.ResponsableTrabajadorId });
        await NotificarResponsableAsync(a, p, anterior, r.ResponsableTrabajadorId, now, ct);
        await _db.SaveSafeAsync(ct);
        return await DetalleAsync(p, a, ct);
    }

    // ---------------- Estado ----------------
    public async Task<ProyectoDetalleDto> CambiarEstadoAsync(Guid proyectoId, CambiarEstadoProyectoRequest r, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.ProyectosConsultar, ct);
        var a = await ProyectoHelper.AccesoAsync(_db, _current, _permisos, _clock, ct);
        var p = await ProyectoHelper.CargarVisibleAsync(_db, a, proyectoId, true, ct);
        ProyectoHelper.AplicarRowVersion(_db, p, p.RowVersion, r.RowVersion, "el proyecto");

        var destino = await _db.EstadosProyecto.AsNoTracking().FirstOrDefaultAsync(e => e.EmpresaId == a.EmpresaId && e.EstadoProyectoId == r.EstadoProyectoId && e.Estado, ct)
            ?? throw ServiceException.Validation("El estado indicado no existe o no está activo.");
        var actual = await _db.EstadosProyecto.AsNoTracking().FirstAsync(e => e.EmpresaId == a.EmpresaId && e.EstadoProyectoId == p.EstadoProyectoId, ct);
        if (destino.EstadoProyectoId == actual.EstadoProyectoId) throw ServiceException.Validation("El proyecto ya está en ese estado.");

        if (destino.EsFinal && actual.EsFinal)
            throw ServiceException.Conflict("El proyecto ya está cerrado; reábralo antes de elegir otro estado final.");
        if (destino.EsFinal)
        {
            await _permisos.AsegurarPermisoAsync(Permisos.ProyectosFinalizar, ct);
            if (!a.EsResponsableOTodos(p.ResponsableTrabajadorId)) throw ServiceException.Forbidden("Solo el responsable del proyecto o la administración pueden finalizarlo.");
        }
        else if (actual.EsFinal)
        {
            await _permisos.AsegurarPermisoAsync(Permisos.ProyectosReabrir, ct);
        }
        else
        {
            await _permisos.AsegurarPermisoAsync(Permisos.ProyectosModificar, ct);
            if (!a.Administra(p.ResponsableTrabajadorId)) throw ServiceException.Forbidden("No administra este proyecto.");
        }

        var now = Ahora;
        p.EstadoProyectoId = destino.EstadoProyectoId;
        p.FechaCierreUtc = destino.EsFinal ? now : null;
        p.FechaActualizacionUtc = now;
        _db.HistorialEstadoProyecto.Add(new HistorialEstadoProyecto
        {
            EmpresaId = a.EmpresaId, ProyectoId = p.ProyectoId, EstadoAnteriorId = actual.EstadoProyectoId, EstadoNuevoId = destino.EstadoProyectoId,
            CambiadoPorUsuarioId = a.UsuarioId, FechaUtc = now, Comentario = Helpers.Limpiar(r.Comentario)
        });
        var accion = destino.EsFinal ? "Finalizar" : actual.EsFinal ? "Reabrir" : "CambiarEstado";
        _auditoria.Registrar("Proyectos", nameof(Proyecto), p.ProyectoId.ToString(), accion, new { Estado = actual.Nombre }, new { Estado = destino.Nombre });

        var participantes = await ProyectoHelper.ParticipantesAsync(_db, p, ct);
        var usuarios = await ProyectoHelper.UsuariosDeTrabajadoresAsync(_db, a.EmpresaId, participantes, ct);
        await NotificacionesHelper.NotificarAsync(_db, a.EmpresaId, usuarios.Values.Where(u => u != a.UsuarioId), TipoNotificacionTrabajo.ProyectoEstado,
            "Cambio de estado de proyecto", $"El proyecto «{p.Nombre}» pasó a «{destino.Nombre}».", nameof(Proyecto), p.ProyectoId.ToString(), now, ct);
        await _db.SaveSafeAsync(ct);
        return await DetalleAsync(p, a, ct);
    }

    // ---------------- Apoyo ----------------
    private async Task AsegurarAbiertoAsync(Proyecto p, CancellationToken ct)
    {
        if (await ProyectoHelper.EstaCerradoAsync(_db, p, ct))
            throw ServiceException.Conflict("El proyecto está cerrado. Solo la administración puede reabrirlo.");
    }

    /// <summary>Avisa al nuevo responsable y al resto del equipo (incluido el anterior responsable). Los textos no incluyen descripciones.</summary>
    private async Task NotificarResponsableAsync(AccesoProyectos a, Proyecto p, Guid anterior, Guid nuevo, DateTime now, CancellationToken ct)
    {
        var participantes = await ProyectoHelper.ParticipantesAsync(_db, p, ct);
        participantes.Add(anterior);
        var usuarios = await ProyectoHelper.UsuariosDeTrabajadoresAsync(_db, a.EmpresaId, participantes.Distinct().ToList(), ct);
        if (usuarios.TryGetValue(nuevo, out var uNuevo) && uNuevo != a.UsuarioId)
            await NotificacionesHelper.NotificarAsync(_db, a.EmpresaId, new[] { uNuevo }, TipoNotificacionTrabajo.ProyectoResponsable,
                "Eres responsable de un proyecto", $"Ahora eres el responsable del proyecto «{p.Nombre}».", nameof(Proyecto), p.ProyectoId.ToString(), now, ct);
        await NotificacionesHelper.NotificarAsync(_db, a.EmpresaId, usuarios.Where(x => x.Key != nuevo).Select(x => x.Value).Where(u => u != a.UsuarioId),
            TipoNotificacionTrabajo.ProyectoResponsable, "Cambió el responsable de un proyecto", $"Cambió el responsable del proyecto «{p.Nombre}».",
            nameof(Proyecto), p.ProyectoId.ToString(), now, ct);
    }

    private async Task NotificarFechaAsync(AccesoProyectos a, Proyecto p, DateTime now, CancellationToken ct)
    {
        var participantes = await ProyectoHelper.ParticipantesAsync(_db, p, ct);
        var usuarios = await ProyectoHelper.UsuariosDeTrabajadoresAsync(_db, a.EmpresaId, participantes, ct);
        await NotificacionesHelper.NotificarAsync(_db, a.EmpresaId, usuarios.Values.Where(u => u != a.UsuarioId), TipoNotificacionTrabajo.ProyectoFecha,
            "Cambió la fecha de término", $"Se modificó la fecha de término del proyecto «{p.Nombre}».", nameof(Proyecto), p.ProyectoId.ToString(), now, ct);
    }

    private async Task NotificarNuevosAsync(AccesoProyectos a, Proyecto p, IEnumerable<Guid> trabajadores, DateTime now, CancellationToken ct)
    {
        var usuarios = await ProyectoHelper.UsuariosDeTrabajadoresAsync(_db, a.EmpresaId, trabajadores, ct);
        await NotificacionesHelper.NotificarAsync(_db, a.EmpresaId, usuarios.Values.Where(u => u != a.UsuarioId), TipoNotificacionTrabajo.ProyectoAsignado,
            "Nuevo proyecto asignado", $"Fue asignado al proyecto «{p.Nombre}».", nameof(Proyecto), p.ProyectoId.ToString(), now, ct);
    }

    private static object Resumen(Proyecto p) => new
    {
        p.Nombre, p.FechaInicio, p.FechaFinPrevista, p.Prioridad, p.EstadoProyectoId, p.ResponsableTrabajadorId
    };

    private async Task<List<ProyectoResumenDto>> ResumenesAsync(Guid empresaId, IReadOnlyList<Proyecto> proyectos, DateOnly hoy, CancellationToken ct)
    {
        var ids = proyectos.Select(p => p.ProyectoId).ToList();
        var estados = await _db.EstadosProyecto.AsNoTracking().Where(e => e.EmpresaId == empresaId).ToDictionaryAsync(e => e.EstadoProyectoId, ct);
        var nombres = await ProyectoHelper.NombresTrabajadoresAsync(_db, empresaId, proyectos.Select(p => p.ResponsableTrabajadorId), ct);
        var ultimoHabil = (await CalendarioHabil.CargarAsync(_db, empresaId, ct)).UltimoHabilAnterior(hoy);
        var stats = await _db.Tareas.AsNoTracking().Where(t => t.EmpresaId == empresaId && ids.Contains(t.ProyectoId))
            .GroupBy(t => t.ProyectoId)
            .Select(g => new
            {
                Id = g.Key,
                Total = g.Count(x => x.Estado != EstadoTarea.Cancelada),
                Completadas = g.Count(x => x.Estado == EstadoTarea.Completada),
                Vencidas = g.Count(x => x.Estado != EstadoTarea.Completada && x.Estado != EstadoTarea.Cancelada && x.FechaVencimiento != null && x.FechaVencimiento <= ultimoHabil),
                Suma = g.Sum(x => x.Estado != EstadoTarea.Cancelada ? (int)x.PorcentajeAvance : 0)
            }).ToListAsync(ct);
        var equipo = await _db.AsignacionesProyecto.AsNoTracking().Where(x => x.EmpresaId == empresaId && ids.Contains(x.ProyectoId) && x.FechaSalida == null)
            .GroupBy(x => x.ProyectoId).Select(g => new { Id = g.Key, N = g.Count() }).ToListAsync(ct);

        return proyectos.Select(p =>
        {
            var e = estados[p.EstadoProyectoId];
            var s = stats.FirstOrDefault(x => x.Id == p.ProyectoId);
            return new ProyectoResumenDto
            {
                ProyectoId = p.ProyectoId, Nombre = p.Nombre, EstadoProyectoId = p.EstadoProyectoId, Estado = e.Nombre, EstadoFinal = e.EsFinal,
                Prioridad = p.Prioridad, ResponsableTrabajadorId = p.ResponsableTrabajadorId, Responsable = nombres.GetValueOrDefault(p.ResponsableTrabajadorId, "—"),
                FechaInicio = p.FechaInicio, FechaFinPrevista = p.FechaFinPrevista,
                Participantes = Math.Max(1, equipo.FirstOrDefault(x => x.Id == p.ProyectoId)?.N ?? 0),
                TareasTotal = s?.Total ?? 0, TareasCompletadas = s?.Completadas ?? 0, TareasVencidas = s?.Vencidas ?? 0,
                Avance = s is { Total: > 0 } ? (int)Math.Round(s.Suma / (double)s.Total) : 0,
                ProximoAVencer = !e.EsFinal && p.FechaFinPrevista is { } f && f >= hoy && f <= hoy.AddDays(ProyectoHelper.DiasAvisoVencimiento),
                Vencido = !e.EsFinal && p.FechaFinPrevista is { } f2 && f2 < hoy
            };
        }).ToList();
    }

    private async Task<ProyectoDetalleDto> DetalleAsync(Proyecto p, AccesoProyectos a, CancellationToken ct)
    {
        var hoy = await ProyectoHelper.HoyAsync(_db, a.EmpresaId, _clock, ct);
        var resumen = (await ResumenesAsync(a.EmpresaId, new[] { p }, hoy, ct))[0];
        var asignaciones = await _db.AsignacionesProyecto.AsNoTracking()
            .Where(x => x.EmpresaId == a.EmpresaId && x.ProyectoId == p.ProyectoId && x.FechaSalida == null).OrderBy(x => x.FechaAsignacion).ToListAsync(ct);
        var nombres = await ProyectoHelper.NombresTrabajadoresAsync(_db, a.EmpresaId, asignaciones.Select(x => x.TrabajadorId), ct);
        var creador = await ProyectoHelper.NombresUsuariosAsync(_db, a.EmpresaId, new[] { p.CreadoPorUsuarioId }, ct);

        var dto = new ProyectoDetalleDto
        {
            ProyectoId = resumen.ProyectoId, Nombre = resumen.Nombre, EstadoProyectoId = resumen.EstadoProyectoId, Estado = resumen.Estado,
            EstadoFinal = resumen.EstadoFinal, Prioridad = resumen.Prioridad, ResponsableTrabajadorId = resumen.ResponsableTrabajadorId,
            Responsable = resumen.Responsable, FechaInicio = resumen.FechaInicio, FechaFinPrevista = resumen.FechaFinPrevista,
            Participantes = resumen.Participantes, TareasTotal = resumen.TareasTotal, TareasCompletadas = resumen.TareasCompletadas,
            TareasVencidas = resumen.TareasVencidas, Avance = resumen.Avance, ProximoAVencer = resumen.ProximoAVencer, Vencido = resumen.Vencido,
            Descripcion = p.Descripcion, FechaCierreUtc = p.FechaCierreUtc, CreadoPor = creador.GetValueOrDefault(p.CreadoPorUsuarioId, "—"),
            FechaCreacionUtc = p.FechaCreacionUtc, FechaActualizacionUtc = p.FechaActualizacionUtc, RowVersion = ProyectoHelper.Version(p.RowVersion),
            Equipo = asignaciones.Select(x => new ParticipanteProyectoDto
            {
                TrabajadorId = x.TrabajadorId, Nombre = nombres.GetValueOrDefault(x.TrabajadorId, "—"), RolEnProyecto = x.RolEnProyecto,
                EsResponsable = x.TrabajadorId == p.ResponsableTrabajadorId, FechaAsignacion = x.FechaAsignacion
            }).ToList()
        };

        var administra = a.Administra(p.ResponsableTrabajadorId);
        var abierto = !resumen.EstadoFinal;
        dto.PuedeModificar = abierto && administra && await _permisos.TienePermisoAsync(Permisos.ProyectosModificar, ct);
        dto.PuedeAsignar = abierto && administra && await _permisos.TienePermisoAsync(Permisos.ProyectosAsignar, ct);
        dto.PuedeGestionarTareas = abierto && administra && await _permisos.TienePermisoAsync(Permisos.TareasGestionar, ct);
        dto.PuedeFinalizar = abierto && a.EsResponsableOTodos(p.ResponsableTrabajadorId) && await _permisos.TienePermisoAsync(Permisos.ProyectosFinalizar, ct);
        dto.PuedeReabrir = !abierto && await _permisos.TienePermisoAsync(Permisos.ProyectosReabrir, ct);
        dto.PuedeCambiarResponsable = abierto && await _permisos.TienePermisoAsync(Permisos.ProyectosCambiarResponsable, ct);
        return dto;
    }
}
