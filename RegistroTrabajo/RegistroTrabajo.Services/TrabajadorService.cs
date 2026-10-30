using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RegistroTrabajo.Data;
using RegistroTrabajo.Data.Entities;
using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public interface ITrabajadorService
{
    Task<PagedResult<TrabajadorResumenDto>> ListarAsync(TrabajadorFiltro filtro, CancellationToken ct);
    Task<TrabajadorDetalleDto> ObtenerAsync(Guid trabajadorId, CancellationToken ct);
    Task<TrabajadorDetalleDto> MiExpedienteAsync(CancellationToken ct);
    Task<TrabajadorDetalleDto> CrearAsync(CrearTrabajadorRequest request, CancellationToken ct);
    Task<TrabajadorDetalleDto> ActualizarAsync(Guid trabajadorId, ActualizarTrabajadorRequest request, CancellationToken ct);
    Task<TrabajadorDetalleDto> CambiarEstadoAsync(Guid trabajadorId, CambiarEstadoLaboralRequest request, CancellationToken ct);
    Task<TrabajadorDetalleDto> DarDeBajaAsync(Guid trabajadorId, BajaTrabajadorRequest request, CancellationToken ct);
    Task<TrabajadorDetalleDto> ReingresarAsync(Guid trabajadorId, string? motivo, CancellationToken ct);
    Task<IReadOnlyList<HistorialLaboralDto>> HistorialAsync(Guid trabajadorId, CancellationToken ct);
}

/// <summary>Utilidades compartidas entre los servicios del Bloque 3 (todas acotadas a la empresa).</summary>
internal static class TrabajadorHelper
{
    /// <summary>Trabajador vinculado a la cuenta (por Trabajadores.UsuarioId o Usuarios.TrabajadorId).</summary>
    public static async Task<Guid?> IdDeUsuarioAsync(RegistroTrabajoDbContext db, Guid empresaId, Guid usuarioId, CancellationToken ct)
    {
        var id = await db.Trabajadores.AsNoTracking()
            .Where(t => t.EmpresaId == empresaId && t.UsuarioId == usuarioId)
            .Select(t => (Guid?)t.TrabajadorId).FirstOrDefaultAsync(ct);
        if (id is not null) return id;
        return await db.Usuarios.AsNoTracking()
            .Where(u => u.EmpresaId == empresaId && u.UsuarioId == usuarioId)
            .Select(u => u.TrabajadorId).FirstOrDefaultAsync(ct);
    }

    public static async Task<Trabajador> CargarAsync(RegistroTrabajoDbContext db, Guid empresaId, Guid trabajadorId, bool tracking, CancellationToken ct)
    {
        var q = tracking ? db.Trabajadores : db.Trabajadores.AsNoTracking();
        return await q.FirstOrDefaultAsync(t => t.TrabajadorId == trabajadorId && t.EmpresaId == empresaId, ct)
            ?? throw ServiceException.NotFound("Trabajador no encontrado.");
    }

    public static DateOnly Hoy(TimeProvider clock) => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    public static HistorialLaboral Historial(Trabajador t, string tipo, object? antes, object? despues, DateOnly fechaEfectiva,
        string? motivo, Guid? usuarioId, DateTime now) => new()
    {
        EmpresaId = t.EmpresaId, TrabajadorId = t.TrabajadorId, FechaEfectiva = fechaEfectiva, TipoCambio = tipo,
        DatosAntesJson = antes is null ? null : JsonSerializer.Serialize(antes),
        DatosDespuesJson = despues is null ? null : JsonSerializer.Serialize(despues),
        Motivo = motivo, RegistradoPorUsuarioId = usuarioId, RegistradoUtc = now
    };
}

public class TrabajadorService : ITrabajadorService
{
    private readonly RegistroTrabajoDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IPermisoService _permisos;
    private readonly IAuditoriaService _auditoria;
    private readonly TimeProvider _clock;
    private readonly IUsuarioService _usuarios;

    public TrabajadorService(RegistroTrabajoDbContext db, ICurrentUser current, IPermisoService permisos,
        IAuditoriaService auditoria, TimeProvider clock, IUsuarioService usuarios)
    {
        _db = db; _current = current; _permisos = permisos; _auditoria = auditoria; _clock = clock; _usuarios = usuarios;
    }

    // ---------------- Consulta ----------------
    public async Task<PagedResult<TrabajadorResumenDto>> ListarAsync(TrabajadorFiltro f, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var q = _db.Trabajadores.AsNoTracking().Where(t => t.EmpresaId == empresaId);
        if (!string.IsNullOrWhiteSpace(f.Buscar))
        {
            var b = f.Buscar.Trim().ToLower();
            q = q.Where(t => (t.Nombres + " " + t.Apellidos).ToLower().Contains(b) || (t.NumeroEmpleado != null && t.NumeroEmpleado.ToLower().Contains(b)));
        }
        if (f.DepartamentoId is { } d) q = q.Where(t => t.DepartamentoId == d);
        if (f.PuestoId is { } p) q = q.Where(t => t.PuestoId == p);
        if (f.EstadoLaboral is { } e) q = q.Where(t => t.EstadoLaboral == e);

        var page = await q.OrderBy(t => t.Apellidos).ThenBy(t => t.Nombres)
            .Select(t => new TrabajadorResumenDto
            {
                TrabajadorId = t.TrabajadorId, NumeroEmpleado = t.NumeroEmpleado, Nombres = t.Nombres, Apellidos = t.Apellidos,
                DepartamentoId = t.DepartamentoId, Departamento = t.Departamento != null ? t.Departamento.Nombre : null,
                PuestoId = t.PuestoId, Puesto = t.Puesto != null ? t.Puesto.Nombre : null, EstadoLaboral = t.EstadoLaboral
            }).ToPagedAsync(f, ct);

        var supervisores = await SupervisoresVigentesAsync(empresaId, page.Items.Select(i => i.TrabajadorId).ToList(), ct);
        foreach (var item in page.Items)
            item.Supervisores = supervisores.TryGetValue(item.TrabajadorId, out var s) ? s : Array.Empty<string>();
        return page;
    }

    public async Task<TrabajadorDetalleDto> ObtenerAsync(Guid trabajadorId, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var t = await TrabajadorHelper.CargarAsync(_db, empresaId, trabajadorId, false, ct);
        return await ConstruirDetalleAsync(t, ct);
    }

    public async Task<TrabajadorDetalleDto> MiExpedienteAsync(CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var id = await TrabajadorHelper.IdDeUsuarioAsync(_db, empresaId, _current.RequireUsuarioId(), ct)
            ?? throw ServiceException.NotFound("Su cuenta no está vinculada a un expediente de trabajador.");
        return await ObtenerAsync(id, ct);
    }

    public async Task<IReadOnlyList<HistorialLaboralDto>> HistorialAsync(Guid trabajadorId, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        await TrabajadorHelper.CargarAsync(_db, empresaId, trabajadorId, false, ct);
        // El historial incluye datos de contacto/estado: solo para quien puede ver datos sensibles o el propio trabajador.
        if (!await PuedeVerSensiblesAsync(empresaId, trabajadorId, ct))
            throw ServiceException.Forbidden("No tiene permiso para consultar el historial laboral.");
        return await _db.HistorialLaboral.AsNoTracking()
            .Where(h => h.EmpresaId == empresaId && h.TrabajadorId == trabajadorId)
            .OrderByDescending(h => h.FechaEfectiva).ThenByDescending(h => h.HistorialLaboralId)
            .Select(h => new HistorialLaboralDto
            {
                HistorialLaboralId = h.HistorialLaboralId, FechaEfectiva = h.FechaEfectiva, TipoCambio = h.TipoCambio,
                DatosAntesJson = h.DatosAntesJson, DatosDespuesJson = h.DatosDespuesJson, Motivo = h.Motivo, RegistradoUtc = h.RegistradoUtc
            }).ToListAsync(ct);
    }

    // ---------------- Alta y edición ----------------
    public async Task<TrabajadorDetalleDto> CrearAsync(CrearTrabajadorRequest r, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var actorId = _current.RequireUsuarioId();
        var now = _clock.GetUtcNow().UtcDateTime;
        var hoy = TrabajadorHelper.Hoy(_clock);

        var nombres = ValidarTexto(r.Nombres, "Los nombres");
        var apellidos = ValidarTexto(r.Apellidos, "Los apellidos");
        ValidarFechas(r.FechaNacimiento, r.FechaIngreso, hoy);
        var numero = Helpers.Limpiar(r.NumeroEmpleado);
        var correo = Helpers.Limpiar(r.CorreoContacto);

        await ValidarDuplicadosAsync(empresaId, null, numero, correo, nombres, apellidos, r.FechaNacimiento, ct);
        await ValidarCatalogoAsync(empresaId, r.DepartamentoId, r.PuestoId, null, null, ct);
        var usuario = await ValidarUsuarioAsync(empresaId, r.UsuarioId, null, ct);
        if (r.Cuenta is not null && r.UsuarioId is not null)
            throw ServiceException.Validation("Indique una cuenta nueva o un usuario existente, no ambos.");
        if (r.Cuenta is not null)
        {
            await _permisos.AsegurarPermisoAsync(Permisos.UsuariosCrear, ct);
            if (nombres.Length > 100 || apellidos.Length > 150)
                throw ServiceException.Validation("Para crear la cuenta, los nombres no pueden superar 100 caracteres ni los apellidos 150.");
        }

        // Alta conjunta: el usuario se guarda primero (la cuenta y el expediente se apuntan entre sí) y todo va en una sola transacción.
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        Usuario? cuentaNueva = null;
        if (r.Cuenta is { } cuenta)
        {
            var rolId = cuenta.RolId ?? await RolTrabajadorAsync(empresaId, ct);
            cuentaNueva = await _usuarios.PrepararAltaAsync(new CrearUsuarioRequest
            {
                Correo = cuenta.Correo, Nombre = nombres, Apellido = apellidos, Contrasena = cuenta.Contrasena,
                DebeCambiarContrasena = cuenta.DebeCambiarContrasena, RolIds = new[] { rolId }
            }, ct);
            await _db.SaveSafeAsync(ct);
        }
        var usuarioIdVinculado = cuentaNueva?.UsuarioId ?? r.UsuarioId;

        var t = new Trabajador
        {
            TrabajadorId = Guid.NewGuid(), EmpresaId = empresaId, UsuarioId = usuarioIdVinculado, NumeroEmpleado = numero,
            Nombres = nombres, Apellidos = apellidos, FechaNacimiento = r.FechaNacimiento, CorreoContacto = correo,
            Telefono = Helpers.Limpiar(r.Telefono), Direccion = Helpers.Limpiar(r.Direccion),
            DepartamentoId = r.DepartamentoId, PuestoId = r.PuestoId, FechaIngreso = r.FechaIngreso,
            EstadoLaboral = EstadoLaboral.Activo, FechaCreacionUtc = now
        };
        _db.Trabajadores.Add(t);
        var cuentaVinculada = cuentaNueva ?? usuario;
        if (cuentaVinculada is not null) cuentaVinculada.TrabajadorId = t.TrabajadorId;

        var asignacion = await NombresAsignacionAsync(empresaId, t.DepartamentoId, t.PuestoId, ct);
        _db.HistorialLaboral.Add(TrabajadorHelper.Historial(t, TipoCambioLaboral.Ingreso, null, asignacion, r.FechaIngreso, null, actorId, now));
        _auditoria.Registrar("Trabajadores", nameof(Trabajador), t.TrabajadorId.ToString(), "Crear", null,
            new { t.NumeroEmpleado, t.Nombres, t.Apellidos, t.DepartamentoId, t.PuestoId, t.FechaIngreso, t.UsuarioId, cuentaCreada = cuentaNueva is not null });
        await _db.SaveSafeAsync(ct);
        await tx.CommitAsync(ct);
        if (cuentaNueva is not null) await _usuarios.NotificarAltaAsync(cuentaNueva, ct);
        return await ConstruirDetalleAsync(t, ct);
    }

    private async Task<Guid> RolTrabajadorAsync(Guid empresaId, CancellationToken ct) =>
        await _db.Roles.AsNoTracking().Where(x => x.EmpresaId == empresaId && x.EsSistema && x.Nombre == NombresRol.Trabajador)
            .Select(x => (Guid?)x.RolId).FirstOrDefaultAsync(ct)
        ?? throw ServiceException.Validation("No existe el rol «Trabajador» en la empresa; elija un rol para la cuenta.");

    public async Task<TrabajadorDetalleDto> ActualizarAsync(Guid trabajadorId, ActualizarTrabajadorRequest r, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var actorId = _current.RequireUsuarioId();
        var now = _clock.GetUtcNow().UtcDateTime;
        var hoy = TrabajadorHelper.Hoy(_clock);
        var t = await TrabajadorHelper.CargarAsync(_db, empresaId, trabajadorId, true, ct);
        // La edición reemplaza el expediente completo: quien no ve los datos personales no puede sobrescribirlos a ciegas.
        if (!await _permisos.TienePermisoAsync(Permisos.TrabajadoresVerSensibles, ct))
            throw ServiceException.Forbidden("Para editar el expediente se requiere permiso sobre los datos personales.");
        if (t.EstadoLaboral == EstadoLaboral.Baja)
            throw ServiceException.Validation("El trabajador está dado de baja; reingréselo para modificar su expediente.");

        AplicarRowVersion(t, r.RowVersion);
        var nombres = ValidarTexto(r.Nombres, "Los nombres");
        var apellidos = ValidarTexto(r.Apellidos, "Los apellidos");
        ValidarFechas(r.FechaNacimiento, r.FechaIngreso, hoy);
        var numero = Helpers.Limpiar(r.NumeroEmpleado);
        var correo = Helpers.Limpiar(r.CorreoContacto);
        var efectiva = r.FechaEfectiva ?? hoy;
        if (efectiva > hoy) throw ServiceException.Validation("La fecha efectiva del cambio no puede ser futura.");

        await ValidarDuplicadosAsync(empresaId, t.TrabajadorId, numero, correo, nombres, apellidos, r.FechaNacimiento, ct);
        await ValidarCatalogoAsync(empresaId, r.DepartamentoId, r.PuestoId, t.DepartamentoId, t.PuestoId, ct);
        Usuario? usuarioNuevo = null;
        if (r.UsuarioId != t.UsuarioId) usuarioNuevo = await ValidarUsuarioAsync(empresaId, r.UsuarioId, t.TrabajadorId, ct);

        var antesPerfil = new { t.NumeroEmpleado, t.Nombres, t.Apellidos, t.FechaNacimiento, t.CorreoContacto, t.Telefono, t.Direccion, t.FechaIngreso, t.UsuarioId };
        var antesAsignacion = await NombresAsignacionAsync(empresaId, t.DepartamentoId, t.PuestoId, ct);
        var deptoCambio = t.DepartamentoId != r.DepartamentoId;
        var puestoCambio = t.PuestoId != r.PuestoId;

        if (r.UsuarioId != t.UsuarioId)
        {
            if (t.UsuarioId is { } anterior)
            {
                var u = await _db.Usuarios.FirstOrDefaultAsync(x => x.EmpresaId == empresaId && x.UsuarioId == anterior, ct);
                if (u is not null && u.TrabajadorId == t.TrabajadorId) u.TrabajadorId = null;
            }
            if (usuarioNuevo is not null) usuarioNuevo.TrabajadorId = t.TrabajadorId;
            t.UsuarioId = r.UsuarioId;
        }

        t.NumeroEmpleado = numero; t.Nombres = nombres; t.Apellidos = apellidos; t.FechaNacimiento = r.FechaNacimiento;
        t.CorreoContacto = correo; t.Telefono = Helpers.Limpiar(r.Telefono); t.Direccion = Helpers.Limpiar(r.Direccion);
        t.FechaIngreso = r.FechaIngreso; t.DepartamentoId = r.DepartamentoId; t.PuestoId = r.PuestoId;
        t.FechaActualizacionUtc = now;

        var despuesAsignacion = await NombresAsignacionAsync(empresaId, t.DepartamentoId, t.PuestoId, ct);
        var motivo = Helpers.Limpiar(r.MotivoCambio);
        if (deptoCambio)
            _db.HistorialLaboral.Add(TrabajadorHelper.Historial(t, TipoCambioLaboral.Departamento,
                new { antesAsignacion.departamentoId, antesAsignacion.departamento },
                new { despuesAsignacion.departamentoId, despuesAsignacion.departamento }, efectiva, motivo, actorId, now));
        if (puestoCambio)
            _db.HistorialLaboral.Add(TrabajadorHelper.Historial(t, TipoCambioLaboral.Puesto,
                new { antesAsignacion.puestoId, antesAsignacion.puesto },
                new { despuesAsignacion.puestoId, despuesAsignacion.puesto }, efectiva, motivo, actorId, now));

        _auditoria.Registrar("Trabajadores", nameof(Trabajador), t.TrabajadorId.ToString(), "Actualizar", antesPerfil,
            new { t.NumeroEmpleado, t.Nombres, t.Apellidos, t.FechaNacimiento, t.CorreoContacto, t.Telefono, t.Direccion, t.FechaIngreso, t.UsuarioId });
        await _db.SaveSafeAsync(ct);
        return await ConstruirDetalleAsync(t, ct);
    }

    public async Task<TrabajadorDetalleDto> CambiarEstadoAsync(Guid trabajadorId, CambiarEstadoLaboralRequest r, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var actorId = _current.RequireUsuarioId();
        if (r.EstadoLaboral != EstadoLaboral.Activo && r.EstadoLaboral != EstadoLaboral.Inactivo)
            throw ServiceException.Validation("Estado no válido; para la baja use la operación de baja.");
        var t = await TrabajadorHelper.CargarAsync(_db, empresaId, trabajadorId, true, ct);
        if (t.EstadoLaboral == EstadoLaboral.Baja)
            throw ServiceException.Validation("El trabajador está dado de baja; use el reingreso.");
        if (t.EstadoLaboral == r.EstadoLaboral) return await ConstruirDetalleAsync(t, ct);

        var now = _clock.GetUtcNow().UtcDateTime;
        var antes = new { t.EstadoLaboral };
        t.EstadoLaboral = r.EstadoLaboral;
        t.FechaActualizacionUtc = now;
        _db.HistorialLaboral.Add(TrabajadorHelper.Historial(t, TipoCambioLaboral.Estado, antes, new { t.EstadoLaboral },
            TrabajadorHelper.Hoy(_clock), Helpers.Limpiar(r.Motivo), actorId, now));
        _auditoria.Registrar("Trabajadores", nameof(Trabajador), t.TrabajadorId.ToString(), "CambiarEstado", antes, new { t.EstadoLaboral });
        await _db.SaveSafeAsync(ct);
        return await ConstruirDetalleAsync(t, ct);
    }

    // ---------------- Baja y reingreso ----------------
    public async Task<TrabajadorDetalleDto> DarDeBajaAsync(Guid trabajadorId, BajaTrabajadorRequest r, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var actorId = _current.RequireUsuarioId();
        var now = _clock.GetUtcNow().UtcDateTime;
        var hoy = TrabajadorHelper.Hoy(_clock);
        var t = await TrabajadorHelper.CargarAsync(_db, empresaId, trabajadorId, true, ct);

        if (t.EstadoLaboral == EstadoLaboral.Baja) throw ServiceException.Conflict("El trabajador ya está dado de baja.");
        if (await TrabajadorHelper.IdDeUsuarioAsync(_db, empresaId, actorId, ct) == t.TrabajadorId)
            throw ServiceException.Forbidden("No puede darse de baja a sí mismo.");
        if (r.FechaBaja < t.FechaIngreso) throw ServiceException.Validation("La fecha de baja no puede ser anterior a la fecha de ingreso.");
        if (r.FechaBaja > hoy) throw ServiceException.Validation("La fecha de baja no puede ser futura.");
        var motivo = r.Motivo.Trim();
        if (motivo.Length < 3) throw ServiceException.Validation("Indique el motivo de la baja.");

        // Cuentas vinculadas (por cualquiera de los dos lados del vínculo): se desactivan y se revocan sus sesiones.
        var usuarios = await _db.Usuarios.Where(u => u.EmpresaId == empresaId
            && (u.TrabajadorId == t.TrabajadorId || (t.UsuarioId != null && u.UsuarioId == t.UsuarioId))).ToListAsync(ct);
        foreach (var u in usuarios.Where(u => u.Estado == EstadoUsuario.Activo))
            await AsegurarNoEsUltimoAdminAsync(empresaId, u.UsuarioId, ct);

        var antes = new { t.EstadoLaboral, t.FechaBaja, t.MotivoBaja };
        t.EstadoLaboral = EstadoLaboral.Baja;
        t.FechaBaja = r.FechaBaja;
        t.MotivoBaja = motivo;
        t.FechaActualizacionUtc = now;

        foreach (var u in usuarios)
        {
            if (u.Estado != EstadoUsuario.Inactivo)
            {
                u.Estado = EstadoUsuario.Inactivo;
                u.FechaBajaUtc = now;
                u.FechaActualizacionUtc = now;
            }
            await SesionService.RevocarActivasAsync(_db, empresaId, u.UsuarioId, "Baja del trabajador", now, ct);
        }

        // Se cierran las supervisiones vigentes (como supervisado o como supervisor); el historial se conserva.
        var vigentes = await _db.TrabajadorSupervisores.Where(s => s.EmpresaId == empresaId
            && (s.TrabajadorId == t.TrabajadorId || s.SupervisorTrabajadorId == t.TrabajadorId)
            && (s.FechaFin == null || s.FechaFin > r.FechaBaja)).ToListAsync(ct);
        foreach (var s in vigentes)
            s.FechaFin = r.FechaBaja < s.FechaInicio ? s.FechaInicio : r.FechaBaja;

        // Las solicitudes de cambio de perfil pendientes ya no tienen sentido.
        var pendientes = await _db.SolicitudesCambioPerfil.Where(s => s.EmpresaId == empresaId
            && s.TrabajadorId == t.TrabajadorId && s.Estado == EstadoSolicitudPerfil.Pendiente).ToListAsync(ct);
        foreach (var s in pendientes)
        {
            s.Estado = EstadoSolicitudPerfil.Cancelada;
            s.RevisadaUtc = now;
            s.RevisadaPorUsuarioId = actorId;
            s.ComentarioRevision = "Cancelada por la baja del trabajador.";
        }

        _db.HistorialLaboral.Add(TrabajadorHelper.Historial(t, TipoCambioLaboral.Baja, antes,
            new { t.EstadoLaboral, t.FechaBaja, t.MotivoBaja }, r.FechaBaja, motivo, actorId, now));
        _auditoria.Registrar("Trabajadores", nameof(Trabajador), t.TrabajadorId.ToString(), "Baja", antes,
            new { t.EstadoLaboral, t.FechaBaja, t.MotivoBaja, cuentasDesactivadas = usuarios.Count, supervisionesCerradas = vigentes.Count });
        await _db.SaveSafeAsync(ct);
        return await ConstruirDetalleAsync(t, ct);
    }

    public async Task<TrabajadorDetalleDto> ReingresarAsync(Guid trabajadorId, string? motivo, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var actorId = _current.RequireUsuarioId();
        var now = _clock.GetUtcNow().UtcDateTime;
        var t = await TrabajadorHelper.CargarAsync(_db, empresaId, trabajadorId, true, ct);
        if (t.EstadoLaboral != EstadoLaboral.Baja) throw ServiceException.Conflict("El trabajador no está dado de baja.");

        var antes = new { t.EstadoLaboral, t.FechaBaja, t.MotivoBaja };
        t.EstadoLaboral = EstadoLaboral.Activo;
        t.FechaBaja = null;
        t.MotivoBaja = null;
        t.FechaActualizacionUtc = now;
        _db.HistorialLaboral.Add(TrabajadorHelper.Historial(t, TipoCambioLaboral.Reingreso, antes, new { t.EstadoLaboral },
            TrabajadorHelper.Hoy(_clock), Helpers.Limpiar(motivo), actorId, now));
        // El acceso de la cuenta vinculada NO se reactiva solo: lo decide un administrador de usuarios.
        _auditoria.Registrar("Trabajadores", nameof(Trabajador), t.TrabajadorId.ToString(), "Reingreso", antes, new { t.EstadoLaboral });
        await _db.SaveSafeAsync(ct);
        return await ConstruirDetalleAsync(t, ct);
    }

    // ---------------- Privados ----------------
    private async Task<bool> PuedeVerSensiblesAsync(Guid empresaId, Guid trabajadorId, CancellationToken ct)
    {
        if (await _permisos.TienePermisoAsync(Permisos.TrabajadoresVerSensibles, ct)) return true;
        return await TrabajadorHelper.IdDeUsuarioAsync(_db, empresaId, _current.RequireUsuarioId(), ct) == trabajadorId;
    }

    private async Task<TrabajadorDetalleDto> ConstruirDetalleAsync(Trabajador t, CancellationToken ct)
    {
        var sensibles = await PuedeVerSensiblesAsync(t.EmpresaId, t.TrabajadorId, ct);
        var nombres = await NombresAsignacionAsync(t.EmpresaId, t.DepartamentoId, t.PuestoId, ct);
        var sup = await SupervisoresVigentesAsync(t.EmpresaId, new List<Guid> { t.TrabajadorId }, ct);
        var dto = new TrabajadorDetalleDto
        {
            TrabajadorId = t.TrabajadorId, NumeroEmpleado = t.NumeroEmpleado, Nombres = t.Nombres, Apellidos = t.Apellidos,
            DepartamentoId = t.DepartamentoId, Departamento = nombres.departamento, PuestoId = t.PuestoId, Puesto = nombres.puesto,
            EstadoLaboral = t.EstadoLaboral,
            Supervisores = sup.TryGetValue(t.TrabajadorId, out var s) ? s : Array.Empty<string>(),
            VerSensibles = sensibles, RowVersion = Convert.ToBase64String(t.RowVersion ?? Array.Empty<byte>())
        };
        if (sensibles)
        {
            dto.FechaIngreso = t.FechaIngreso; dto.FechaBaja = t.FechaBaja; dto.MotivoBaja = t.MotivoBaja;
            dto.UsuarioId = t.UsuarioId; dto.FechaNacimiento = t.FechaNacimiento; dto.CorreoContacto = t.CorreoContacto;
            dto.Telefono = t.Telefono; dto.Direccion = t.Direccion;
        }
        return dto;
    }

    private async Task<Dictionary<Guid, IReadOnlyList<string>>> SupervisoresVigentesAsync(Guid empresaId, List<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return new();
        var hoy = TrabajadorHelper.Hoy(_clock);
        var filas = await _db.TrabajadorSupervisores.AsNoTracking()
            .Where(s => s.EmpresaId == empresaId && ids.Contains(s.TrabajadorId) && s.FechaInicio <= hoy && (s.FechaFin == null || s.FechaFin >= hoy))
            .OrderByDescending(s => s.EsPrincipal)
            .Select(s => new { s.TrabajadorId, Nombre = s.Supervisor.Nombres + " " + s.Supervisor.Apellidos })
            .ToListAsync(ct);
        return filas.GroupBy(f => f.TrabajadorId).ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.Nombre).ToList());
    }

    private async Task<(Guid? departamentoId, string? departamento, Guid? puestoId, string? puesto)> NombresAsignacionAsync(
        Guid empresaId, Guid? departamentoId, Guid? puestoId, CancellationToken ct)
    {
        string? depto = null, puesto = null;
        if (departamentoId is { } d)
            depto = await _db.Departamentos.AsNoTracking().Where(x => x.EmpresaId == empresaId && x.DepartamentoId == d).Select(x => x.Nombre).FirstOrDefaultAsync(ct);
        if (puestoId is { } p)
            puesto = await _db.Puestos.AsNoTracking().Where(x => x.EmpresaId == empresaId && x.PuestoId == p).Select(x => x.Nombre).FirstOrDefaultAsync(ct);
        return (departamentoId, depto, puestoId, puesto);
    }

    private static string ValidarTexto(string? valor, string que)
    {
        var v = (valor ?? "").Trim();
        if (v.Length == 0) throw ServiceException.Validation($"{que} son obligatorios.");
        return v;
    }

    private static void ValidarFechas(DateOnly? nacimiento, DateOnly ingreso, DateOnly hoy)
    {
        if (ingreso == default) throw ServiceException.Validation("La fecha de ingreso es obligatoria.");
        if (ingreso > hoy.AddYears(1)) throw ServiceException.Validation("La fecha de ingreso no es válida.");
        if (nacimiento is { } n && (n >= hoy || n.Year < 1900)) throw ServiceException.Validation("La fecha de nacimiento no es válida.");
    }

    /// <summary>Evita duplicados dentro de la empresa: mismo número de empleado, mismo correo, o misma persona (nombre + nacimiento).</summary>
    private async Task ValidarDuplicadosAsync(Guid empresaId, Guid? actualId, string? numero, string? correo,
        string nombres, string apellidos, DateOnly? nacimiento, CancellationToken ct)
    {
        var otros = _db.Trabajadores.AsNoTracking().Where(t => t.EmpresaId == empresaId && t.TrabajadorId != actualId);
        if (numero is not null && await otros.AnyAsync(t => t.NumeroEmpleado == numero, ct))
            throw ServiceException.Conflict("Ya existe un trabajador con ese número de empleado.");
        if (correo is not null)
        {
            var c = correo.ToLower();
            if (await otros.AnyAsync(t => t.CorreoContacto != null && t.CorreoContacto.ToLower() == c, ct))
                throw ServiceException.Conflict("Ya existe un trabajador con ese correo de contacto.");
        }
        if (nacimiento is { } n)
        {
            var nom = nombres.ToLower();
            var ape = apellidos.ToLower();
            if (await otros.AnyAsync(t => t.FechaNacimiento == n && t.Nombres.ToLower() == nom && t.Apellidos.ToLower() == ape, ct))
                throw ServiceException.Conflict("Ya existe un trabajador con el mismo nombre y fecha de nacimiento.");
        }
    }

    /// <summary>Departamento y puesto deben ser de la empresa; si cambian, el nuevo debe estar activo.</summary>
    private async Task ValidarCatalogoAsync(Guid empresaId, Guid? departamentoId, Guid? puestoId,
        Guid? departamentoActual, Guid? puestoActual, CancellationToken ct)
    {
        if (departamentoId is { } d)
        {
            var estado = await _db.Departamentos.AsNoTracking().Where(x => x.EmpresaId == empresaId && x.DepartamentoId == d)
                .Select(x => (bool?)x.Estado).FirstOrDefaultAsync(ct);
            if (estado is null) throw ServiceException.Validation("El departamento indicado no existe.");
            if (estado == false && d != departamentoActual) throw ServiceException.Validation("El departamento está inactivo.");
        }
        if (puestoId is { } p)
        {
            var estado = await _db.Puestos.AsNoTracking().Where(x => x.EmpresaId == empresaId && x.PuestoId == p)
                .Select(x => (bool?)x.Estado).FirstOrDefaultAsync(ct);
            if (estado is null) throw ServiceException.Validation("El puesto indicado no existe.");
            if (estado == false && p != puestoActual) throw ServiceException.Validation("El puesto está inactivo.");
        }
    }

    /// <summary>La cuenta debe ser de la empresa y no estar vinculada a otro expediente.</summary>
    private async Task<Usuario?> ValidarUsuarioAsync(Guid empresaId, Guid? usuarioId, Guid? trabajadorActualId, CancellationToken ct)
    {
        if (usuarioId is not { } id) return null;
        var u = await _db.Usuarios.FirstOrDefaultAsync(x => x.UsuarioId == id && x.EmpresaId == empresaId, ct)
            ?? throw ServiceException.Validation("El usuario indicado no existe en la empresa.");
        if (u.TrabajadorId is { } otro && otro != trabajadorActualId)
            throw ServiceException.Conflict("El usuario ya está vinculado a otro trabajador.");
        if (await _db.Trabajadores.AnyAsync(x => x.UsuarioId == id && x.TrabajadorId != trabajadorActualId, ct))
            throw ServiceException.Conflict("El usuario ya está vinculado a otro trabajador.");
        return u;
    }

    private async Task AsegurarNoEsUltimoAdminAsync(Guid empresaId, Guid usuarioId, CancellationToken ct)
    {
        var esAdmin = await _db.UsuarioRoles.AnyAsync(ur => ur.EmpresaId == empresaId && ur.UsuarioId == usuarioId
            && ur.Rol.Nombre == NombresRol.Administrador && ur.Rol.EsSistema, ct);
        if (!esAdmin) return;
        var otros = await _db.UsuarioRoles.CountAsync(ur => ur.EmpresaId == empresaId && ur.Rol.Nombre == NombresRol.Administrador
            && ur.Rol.EsSistema && ur.UsuarioId != usuarioId && ur.Usuario.Estado == EstadoUsuario.Activo, ct);
        if (otros == 0) throw ServiceException.Conflict("No se puede dar de baja al último administrador activo.");
    }

    /// <summary>Control optimista: el expediente debe seguir como lo recibió el cliente.</summary>
    private void AplicarRowVersion(Trabajador t, string rowVersion)
    {
        byte[] enviada;
        try { enviada = Convert.FromBase64String(rowVersion); }
        catch (FormatException) { throw ServiceException.Validation("La versión del expediente no es válida."); }
        if (t.RowVersion is { } actual && !actual.AsSpan().SequenceEqual(enviada))
            throw ServiceException.Conflict("El expediente fue modificado por otra persona. Recargue e intente de nuevo.");
        _db.Entry(t).Property(x => x.RowVersion).OriginalValue = enviada;
    }
}
