using Microsoft.EntityFrameworkCore;
using RegistroTrabajo.Data;
using RegistroTrabajo.Data.Entities;
using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public interface IAsistenciaService
{
    /// <summary>Qué debe registrar el trabajador autenticado (entrada o salida), con su jornada abierta y el horario de hoy.</summary>
    Task<EstadoJornadaDto> EstadoActualAsync(CancellationToken ct);
    Task<RegistroAsistenciaDto> RegistrarEntradaAsync(RegistrarAsistenciaRequest r, CancellationToken ct);
    Task<RegistroAsistenciaDto> RegistrarSalidaAsync(RegistrarAsistenciaRequest r, CancellationToken ct);
    Task<PagedResult<RegistroAsistenciaDto>> MisRegistrosAsync(AsistenciaFiltro filtro, CancellationToken ct);
    /// <summary>Historial por alcance: toda la empresa (consultar_todos) o solo los trabajadores supervisados vigentes (consultar).</summary>
    Task<PagedResult<RegistroAsistenciaDto>> ListarAsync(AsistenciaFiltro filtro, CancellationToken ct);
    Task<RegistroAsistenciaDto> ObtenerAsync(Guid registroId, CancellationToken ct);
    /// <summary>Uso interno de otros servicios: arma el detalle sin revalidar el alcance (quien llama ya lo validó).</summary>
    Task<RegistroAsistenciaDto> DetalleSinAlcanceAsync(Guid registroId, CancellationToken ct);
}

public interface ICorreccionesAsistenciaService
{
    Task<RegistroAsistenciaDto> CorregirAsync(Guid registroId, CorregirAsistenciaRequest r, CancellationToken ct);
    Task<RegistroAsistenciaDto> AnularAsync(Guid registroId, AnularAsistenciaRequest r, CancellationToken ct);
    Task<RegistroAsistenciaDto> CrearManualAsync(CrearRegistroManualRequest r, CancellationToken ct);
}

internal static class AsistenciaHelper
{
    public static TimeZoneInfo Zona(string? id)
    {
        if (!string.IsNullOrWhiteSpace(id))
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.Utc;
    }

    public static DateOnly FechaLocal(DateTime utc, TimeZoneInfo zona) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zona));

    public static DateTime ComoUtc(DateTime d) => d.Kind switch
    {
        DateTimeKind.Local => d.ToUniversalTime(),
        DateTimeKind.Utc => d,
        _ => DateTime.SpecifyKind(d, DateTimeKind.Utc)
    };

    public sealed record Alcance(bool Todos, Guid? MiTrabajadorId, IReadOnlyList<Guid> Supervisados)
    {
        public bool Incluye(Guid trabajadorId) => Todos || Supervisados.Contains(trabajadorId);
        public bool Revisa(Guid trabajadorId) => MiTrabajadorId != trabajadorId && Incluye(trabajadorId);
    }

    /// <summary>Alcance sobre otros trabajadores: todos si tiene el permiso global; si no, los supervisados con vigencia (si tiene el permiso por supervisión).</summary>
    public static async Task<Alcance> AlcanceAsync(RegistroTrabajoDbContext db, ICurrentUser current, IPermisoService permisos, TimeProvider clock,
        string permisoTodos, string permisoSupervision, CancellationToken ct)
    {
        var empresaId = current.RequireEmpresaId();
        var mi = await TrabajadorHelper.IdDeUsuarioAsync(db, empresaId, current.RequireUsuarioId(), ct);
        var todos = await permisos.TienePermisoAsync(permisoTodos, ct);
        var supervisados = new List<Guid>();
        if (!todos && mi is { } m && await permisos.TienePermisoAsync(permisoSupervision, ct))
        {
            var hoy = TrabajadorHelper.Hoy(clock);
            supervisados = await db.TrabajadorSupervisores.AsNoTracking()
                .Where(s => s.EmpresaId == empresaId && s.SupervisorTrabajadorId == m && s.FechaInicio <= hoy && (s.FechaFin == null || s.FechaFin >= hoy))
                .Select(s => s.TrabajadorId).Distinct().ToListAsync(ct);
        }
        return new Alcance(todos, mi, supervisados);
    }
}

public class AsistenciaService : IAsistenciaService
{
    private readonly RegistroTrabajoDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IPermisoService _permisos;
    private readonly IHorariosService _horarios;
    private readonly TimeProvider _clock;

    public AsistenciaService(RegistroTrabajoDbContext db, ICurrentUser current, IPermisoService permisos, IHorariosService horarios, TimeProvider clock)
    {
        _db = db; _current = current; _permisos = permisos; _horarios = horarios; _clock = clock;
    }

    private async Task<(Guid TrabajadorId, ConfiguracionEmpresa? Config)> ContextoAsync(Guid empresaId, bool exigirActivo, CancellationToken ct)
    {
        var trabajadorId = await TrabajadorHelper.IdDeUsuarioAsync(_db, empresaId, _current.RequireUsuarioId(), ct)
            ?? throw ServiceException.NotFound("Su cuenta no está vinculada a un expediente de trabajador.");
        if (exigirActivo)
        {
            var estado = await _db.Trabajadores.AsNoTracking().Where(t => t.TrabajadorId == trabajadorId && t.EmpresaId == empresaId)
                .Select(t => (byte?)t.EstadoLaboral).FirstOrDefaultAsync(ct);
            if (estado != EstadoLaboral.Activo) throw ServiceException.Forbidden("Solo los trabajadores activos pueden registrar asistencia.");
        }
        var config = await _db.ConfiguracionEmpresa.AsNoTracking().FirstOrDefaultAsync(c => c.EmpresaId == empresaId, ct);
        return (trabajadorId, config);
    }

    /// <summary>Con GPS exigido la ubicación es obligatoria y válida; sin GPS se descarta para no guardar datos que la empresa no necesita.</summary>
    private static (decimal? Lat, decimal? Lon) UbicacionPara(ConfiguracionEmpresa? config, RegistrarAsistenciaRequest r)
    {
        if (config?.PermitirValidacionGps != true) return (null, null);
        if (r.Latitud is not { } lat || r.Longitud is not { } lon)
            throw ServiceException.Validation("Su empresa exige la ubicación para registrar asistencia. Autorice el acceso a la ubicación e intente de nuevo.");
        if (lat is < -90 or > 90 || lon is < -180 or > 180)
            throw ServiceException.Validation("La ubicación recibida no es válida.");
        return (Math.Round(lat, 6), Math.Round(lon, 6));
    }

    public async Task<EstadoJornadaDto> EstadoActualAsync(CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var (trabajadorId, config) = await ContextoAsync(empresaId, false, ct);
        var now = _clock.GetUtcNow().UtcDateTime;
        var zona = AsistenciaHelper.Zona(config?.ZonaHorariaId);

        var abierta = await _db.RegistrosAsistencia.AsNoTracking().Where(a => a.EmpresaId == empresaId && a.TrabajadorId == trabajadorId
            && a.Estado == EstadoAsistencia.Abierta && a.SalidaUtc == null).OrderByDescending(a => a.EntradaUtc).FirstOrDefaultAsync(ct);
        var ultimo = await _db.RegistrosAsistencia.AsNoTracking().Where(a => a.EmpresaId == empresaId && a.TrabajadorId == trabajadorId
            && a.Estado != EstadoAsistencia.Anulada).OrderByDescending(a => a.EntradaUtc).FirstOrDefaultAsync(ct);

        return new EstadoJornadaDto
        {
            TrabajadorId = trabajadorId, HoraServidorUtc = now, Siguiente = abierta is null ? "Entrada" : "Salida",
            ExigeUbicacion = config?.PermitirValidacionGps == true,
            JornadaAbierta = abierta is null ? null : (await MapearAsync(empresaId, new[] { abierta }, ct))[0],
            Ultimo = ultimo is null ? null : (await MapearAsync(empresaId, new[] { ultimo }, ct))[0],
            HorarioHoy = await _horarios.AplicableAsync(empresaId, trabajadorId, AsistenciaHelper.FechaLocal(now, zona), ct)
        };
    }

    public async Task<RegistroAsistenciaDto> RegistrarEntradaAsync(RegistrarAsistenciaRequest r, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var (trabajadorId, config) = await ContextoAsync(empresaId, true, ct);
        var (lat, lon) = UbicacionPara(config, r);

        if (await _db.RegistrosAsistencia.AnyAsync(a => a.EmpresaId == empresaId && a.TrabajadorId == trabajadorId
                && a.Estado == EstadoAsistencia.Abierta && a.SalidaUtc == null, ct))
            throw ServiceException.Conflict("Ya tiene una jornada abierta. Registre su salida; si olvidó hacerlo, pida a su supervisor que la corrija.");

        var now = _clock.GetUtcNow().UtcDateTime;
        var zona = AsistenciaHelper.Zona(config?.ZonaHorariaId);
        var registro = new RegistroAsistencia
        {
            RegistroAsistenciaId = Guid.NewGuid(), EmpresaId = empresaId, TrabajadorId = trabajadorId,
            FechaLaboral = AsistenciaHelper.FechaLocal(now, zona), EntradaUtc = now, EntradaLatitud = lat, EntradaLongitud = lon,
            EntradaRegistradaPorUsuarioId = _current.RequireUsuarioId(), Estado = EstadoAsistencia.Abierta, FechaCreacionUtc = now
        };
        _db.RegistrosAsistencia.Add(registro);
        await _db.SaveSafeAsync(ct);
        return (await MapearAsync(empresaId, new[] { registro }, ct))[0];
    }

    public async Task<RegistroAsistenciaDto> RegistrarSalidaAsync(RegistrarAsistenciaRequest r, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var (trabajadorId, config) = await ContextoAsync(empresaId, true, ct);
        var (lat, lon) = UbicacionPara(config, r);

        // La salida cierra la jornada abierta aunque haya cruzado la medianoche: conserva la FechaLaboral de su entrada.
        var registro = await _db.RegistrosAsistencia.Where(a => a.EmpresaId == empresaId && a.TrabajadorId == trabajadorId
                && a.Estado == EstadoAsistencia.Abierta && a.SalidaUtc == null)
            .OrderByDescending(a => a.EntradaUtc).FirstOrDefaultAsync(ct)
            ?? throw ServiceException.Conflict("No tiene una entrada abierta. Registre primero su entrada.");

        var now = _clock.GetUtcNow().UtcDateTime;
        if (registro.EntradaUtc is { } entrada && now < entrada) now = entrada;
        registro.SalidaUtc = now; registro.SalidaLatitud = lat; registro.SalidaLongitud = lon;
        registro.SalidaRegistradaPorUsuarioId = _current.RequireUsuarioId();
        registro.Estado = EstadoAsistencia.Completa;
        await _db.SaveSafeAsync(ct);
        return (await MapearAsync(empresaId, new[] { registro }, ct))[0];
    }

    private IQueryable<RegistroAsistencia> Consulta(Guid empresaId) => _db.RegistrosAsistencia.AsNoTracking()
        .Include(a => a.Trabajador).Include(a => a.CorreccionesAsistencia).ThenInclude(c => c.Usuario)
        .Where(a => a.EmpresaId == empresaId);

    private static IQueryable<RegistroAsistencia> Filtrar(IQueryable<RegistroAsistencia> q, AsistenciaFiltro f)
    {
        if (f.Desde is { } d0) q = q.Where(a => a.FechaLaboral >= d0);
        if (f.Hasta is { } d1) q = q.Where(a => a.FechaLaboral <= d1);
        if (f.SoloIncompletas) q = q.Where(a => a.Estado == EstadoAsistencia.Abierta);
        return q.OrderByDescending(a => a.FechaLaboral).ThenByDescending(a => a.EntradaUtc);
    }

    private async Task<PagedResult<RegistroAsistenciaDto>> PaginarAsync(Guid empresaId, IQueryable<RegistroAsistencia> q, AsistenciaFiltro f, CancellationToken ct)
    {
        var page = Math.Max(1, f.Page);
        var size = Math.Clamp(f.PageSize, 1, 200);
        var total = await q.CountAsync(ct);
        var items = await q.Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new PagedResult<RegistroAsistenciaDto> { Items = await MapearAsync(empresaId, items, ct), Page = page, PageSize = size, Total = total };
    }

    public async Task<PagedResult<RegistroAsistenciaDto>> MisRegistrosAsync(AsistenciaFiltro filtro, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var (trabajadorId, _) = await ContextoAsync(empresaId, false, ct);
        var q = Filtrar(Consulta(empresaId).Where(a => a.TrabajadorId == trabajadorId), filtro);
        return await PaginarAsync(empresaId, q, filtro, ct);
    }

    public async Task<PagedResult<RegistroAsistenciaDto>> ListarAsync(AsistenciaFiltro filtro, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var alcance = await AsistenciaHelper.AlcanceAsync(_db, _current, _permisos, _clock, Permisos.AsistenciaConsultarTodos, Permisos.AsistenciaConsultar, ct);
        if (!alcance.Todos && alcance.Supervisados.Count == 0 && alcance.MiTrabajadorId is null)
            throw ServiceException.Forbidden("No tiene permiso para consultar la asistencia.");

        var q = Consulta(empresaId);
        if (filtro.TrabajadorId is { } t)
        {
            if (alcance.MiTrabajadorId != t && !alcance.Incluye(t)) throw ServiceException.NotFound("Trabajador no encontrado.");
            q = q.Where(a => a.TrabajadorId == t);
        }
        else if (!alcance.Todos)
        {
            var ids = alcance.Supervisados.ToList();
            if (alcance.MiTrabajadorId is { } mi) ids.Add(mi);
            q = q.Where(a => ids.Contains(a.TrabajadorId));
        }
        return await PaginarAsync(empresaId, Filtrar(q, filtro), filtro, ct);
    }

    public async Task<RegistroAsistenciaDto> ObtenerAsync(Guid registroId, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var registro = await Consulta(empresaId).FirstOrDefaultAsync(a => a.RegistroAsistenciaId == registroId, ct)
            ?? throw ServiceException.NotFound("Registro no encontrado.");
        var alcance = await AsistenciaHelper.AlcanceAsync(_db, _current, _permisos, _clock, Permisos.AsistenciaConsultarTodos, Permisos.AsistenciaConsultar, ct);
        var propio = alcance.MiTrabajadorId == registro.TrabajadorId && await _permisos.TienePermisoAsync(Permisos.AsistenciaConsultarPropia, ct);
        if (!propio && !alcance.Incluye(registro.TrabajadorId)) throw ServiceException.NotFound("Registro no encontrado.");
        return (await MapearAsync(empresaId, new[] { registro }, ct))[0];
    }

    public async Task<RegistroAsistenciaDto> DetalleSinAlcanceAsync(Guid registroId, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var registro = await Consulta(empresaId).FirstOrDefaultAsync(a => a.RegistroAsistenciaId == registroId, ct)
            ?? throw ServiceException.NotFound("Registro no encontrado.");
        return (await MapearAsync(empresaId, new[] { registro }, ct))[0];
    }

    /// <summary>Convierte registros a DTO con horario aplicable de ese día, correcciones y contexto de permisos aprobados (sin calcular retardos).</summary>
    private async Task<List<RegistroAsistenciaDto>> MapearAsync(Guid empresaId, IReadOnlyCollection<RegistroAsistencia> registros, CancellationToken ct)
    {
        if (registros.Count == 0) return new();
        var ids = registros.Select(r => r.RegistroAsistenciaId).ToList();
        Dictionary<Guid, Trabajador> trabajadores = new();
        var faltantes = registros.Where(r => r.Trabajador is null).Select(r => r.TrabajadorId).Distinct().ToList();
        if (faltantes.Count > 0)
            trabajadores = await _db.Trabajadores.AsNoTracking().Where(t => t.EmpresaId == empresaId && faltantes.Contains(t.TrabajadorId)).ToDictionaryAsync(t => t.TrabajadorId, ct);

        var correcciones = await _db.CorreccionesAsistencia.AsNoTracking().Include(c => c.Usuario)
            .Where(c => c.EmpresaId == empresaId && ids.Contains(c.RegistroAsistenciaId)).OrderBy(c => c.FechaCorreccionUtc).ToListAsync(ct);

        var trabajadorIds = registros.Select(r => r.TrabajadorId).Distinct().ToList();
        var minFecha = registros.Min(r => r.FechaLaboral).ToDateTime(TimeOnly.MinValue);
        var maxFecha = registros.Max(r => r.FechaLaboral).ToDateTime(new TimeOnly(23, 59, 59));
        var permisos = await _db.SolicitudesPermiso.AsNoTracking()
            .Where(s => s.EmpresaId == empresaId && s.Estado == EstadoSolicitudPermiso.Aprobada && trabajadorIds.Contains(s.TrabajadorId)
                && s.FechaInicio <= maxFecha && s.FechaFin >= minFecha)
            .Select(s => new { s.TrabajadorId, s.FechaInicio, s.FechaFin }).ToListAsync(ct);

        var cacheHorario = new Dictionary<(Guid, DateOnly), HorarioAplicableDto>();
        var lista = new List<RegistroAsistenciaDto>();
        foreach (var r in registros)
        {
            var key = (r.TrabajadorId, r.FechaLaboral);
            if (!cacheHorario.TryGetValue(key, out var horario))
                cacheHorario[key] = horario = await _horarios.AplicableAsync(empresaId, r.TrabajadorId, r.FechaLaboral, ct);

            var nombre = r.Trabajador is not null ? HorariosHelper.NombreCompleto(r.Trabajador)
                : trabajadores.TryGetValue(r.TrabajadorId, out var tr) ? HorariosHelper.NombreCompleto(tr) : "";
            var dia0 = r.FechaLaboral.ToDateTime(TimeOnly.MinValue);
            var dia1 = r.FechaLaboral.ToDateTime(new TimeOnly(23, 59, 59));
            var propias = correcciones.Where(c => c.RegistroAsistenciaId == r.RegistroAsistenciaId).ToList();
            lista.Add(new RegistroAsistenciaDto
            {
                RegistroAsistenciaId = r.RegistroAsistenciaId, TrabajadorId = r.TrabajadorId, Trabajador = nombre, FechaLaboral = r.FechaLaboral,
                EntradaUtc = r.EntradaUtc, SalidaUtc = r.SalidaUtc, EntradaLatitud = r.EntradaLatitud, EntradaLongitud = r.EntradaLongitud,
                SalidaLatitud = r.SalidaLatitud, SalidaLongitud = r.SalidaLongitud, Estado = r.Estado,
                Incompleta = r.Estado == EstadoAsistencia.Abierta, Corregida = propias.Count > 0,
                HorasTrabajadas = r.EntradaUtc is { } e && r.SalidaUtc is { } s ? Math.Round((decimal)(s - e).TotalHours, 2) : null,
                Observaciones = r.Observaciones, Horario = horario.Horario,
                HorarioEsperado = horario.Horario is null ? null : HorariosHelper.Etiqueta(horario.Dia),
                TienePermisoAprobado = permisos.Any(p => p.TrabajadorId == r.TrabajadorId && p.FechaInicio <= dia1 && p.FechaFin >= dia0),
                Correcciones = propias.Select(c => new CorreccionAsistenciaDto
                {
                    CorreccionAsistenciaId = c.CorreccionAsistenciaId, FechaCorreccionUtc = c.FechaCorreccionUtc,
                    CorregidoPor = $"{c.Usuario.Nombre} {c.Usuario.Apellido}".Trim(), EntradaAnteriorUtc = c.EntradaAnteriorUtc,
                    SalidaAnteriorUtc = c.SalidaAnteriorUtc, EntradaNuevaUtc = c.EntradaNuevaUtc, SalidaNuevaUtc = c.SalidaNuevaUtc, Motivo = c.Motivo
                }).ToList()
            });
        }
        return lista;
    }
}

public class CorreccionesAsistenciaService : ICorreccionesAsistenciaService
{
    /// <summary>Un supervisor solo corrige registros recientes; fuera de esta ventana hace falta corregir_todos (RH/Administración).</summary>
    public const int VentanaDiasSupervisor = 31;

    private readonly RegistroTrabajoDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IPermisoService _permisos;
    private readonly IAuditoriaService _auditoria;
    private readonly IAsistenciaService _asistencia;
    private readonly TimeProvider _clock;

    public CorreccionesAsistenciaService(RegistroTrabajoDbContext db, ICurrentUser current, IPermisoService permisos,
        IAuditoriaService auditoria, IAsistenciaService asistencia, TimeProvider clock)
    {
        _db = db; _current = current; _permisos = permisos; _auditoria = auditoria; _asistencia = asistencia; _clock = clock;
    }

    private async Task<AsistenciaHelper.Alcance> AlcanceAsync(CancellationToken ct) =>
        await AsistenciaHelper.AlcanceAsync(_db, _current, _permisos, _clock, Permisos.AsistenciaCorregirTodos, Permisos.AsistenciaCorregir, ct);

    private static AsistenciaHelper.Alcance Exigir(AsistenciaHelper.Alcance a, Guid trabajadorId)
    {
        if (a.MiTrabajadorId == trabajadorId) throw ServiceException.Forbidden("No puede corregir sus propios registros.");
        if (!a.Incluye(trabajadorId)) throw ServiceException.NotFound("Registro no encontrado.");
        return a;
    }

    private void ValidarVentana(AsistenciaHelper.Alcance a, DateOnly fecha)
    {
        if (a.Todos) return;
        if (fecha < TrabajadorHelper.Hoy(_clock).AddDays(-VentanaDiasSupervisor))
            throw ServiceException.Forbidden($"Solo se pueden corregir registros de los últimos {VentanaDiasSupervisor} días; solicite apoyo a Recursos Humanos.");
    }

    private async Task<TimeZoneInfo> ZonaAsync(Guid empresaId, CancellationToken ct) =>
        AsistenciaHelper.Zona(await _db.ConfiguracionEmpresa.AsNoTracking().Where(c => c.EmpresaId == empresaId).Select(c => c.ZonaHorariaId).FirstOrDefaultAsync(ct));

    private async Task ValidarTiemposAsync(Guid empresaId, Guid trabajadorId, Guid? excluirId, DateTime entrada, DateTime? salida, CancellationToken ct)
    {
        var limite = _clock.GetUtcNow().UtcDateTime.AddMinutes(1);
        if (entrada > limite || (salida is { } s0 && s0 > limite)) throw ServiceException.Validation("Las horas no pueden estar en el futuro.");
        if (salida is { } s && s < entrada) throw ServiceException.Validation("La salida no puede ser anterior a la entrada.");
        if (salida is { } s2 && (s2 - entrada).TotalHours > 24) throw ServiceException.Validation("Una jornada no puede durar más de 24 horas.");

        var fin = salida ?? DateTime.MaxValue;
        var traslape = await _db.RegistrosAsistencia.AnyAsync(a => a.EmpresaId == empresaId && a.TrabajadorId == trabajadorId
            && a.Estado != EstadoAsistencia.Anulada && (excluirId == null || a.RegistroAsistenciaId != excluirId)
            && a.EntradaUtc != null && a.EntradaUtc < fin && (a.SalidaUtc == null ? DateTime.MaxValue : a.SalidaUtc) > entrada, ct);
        if (traslape) throw ServiceException.Conflict("Las horas se traslapan con otra jornada del trabajador.");
    }

    public async Task<RegistroAsistenciaDto> CorregirAsync(Guid registroId, CorregirAsistenciaRequest r, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var actor = _current.RequireUsuarioId();
        var registro = await _db.RegistrosAsistencia.FirstOrDefaultAsync(a => a.RegistroAsistenciaId == registroId && a.EmpresaId == empresaId, ct)
            ?? throw ServiceException.NotFound("Registro no encontrado.");
        var alcance = await AlcanceAsync(ct);
        Exigir(alcance, registro.TrabajadorId);
        ValidarVentana(alcance, registro.FechaLaboral);

        var motivo = Helpers.Limpiar(r.Motivo) ?? throw ServiceException.Validation("El motivo de la corrección es obligatorio.");
        if (registro.Estado == EstadoAsistencia.Anulada) throw ServiceException.Conflict("El registro está anulado y no se puede corregir.");
        if (r.EntradaUtc is null && r.SalidaUtc is null) throw ServiceException.Validation("Indique la nueva hora de entrada o de salida.");

        var entradaAnterior = registro.EntradaUtc; var salidaAnterior = registro.SalidaUtc;
        var entrada = r.EntradaUtc is { } e ? AsistenciaHelper.ComoUtc(e) : registro.EntradaUtc;
        var salida = r.SalidaUtc is { } s ? AsistenciaHelper.ComoUtc(s) : registro.SalidaUtc;
        if (entrada is null) throw ServiceException.Validation("El registro no tiene entrada; indique la hora de entrada.");
        if (entrada == entradaAnterior && salida == salidaAnterior) throw ServiceException.Validation("La corrección no cambia ningún valor.");
        await ValidarTiemposAsync(empresaId, registro.TrabajadorId, registro.RegistroAsistenciaId, entrada.Value, salida, ct);

        registro.EntradaUtc = entrada; registro.SalidaUtc = salida;
        if (salida is not null) registro.Estado = EstadoAsistencia.Completa;
        if (entrada != entradaAnterior) registro.FechaLaboral = AsistenciaHelper.FechaLocal(entrada.Value, await ZonaAsync(empresaId, ct));
        var now = _clock.GetUtcNow().UtcDateTime;
        _db.CorreccionesAsistencia.Add(new CorreccionAsistencia
        {
            EmpresaId = empresaId, RegistroAsistenciaId = registro.RegistroAsistenciaId, CorregidoPorUsuarioId = actor, FechaCorreccionUtc = now,
            EntradaAnteriorUtc = entradaAnterior, SalidaAnteriorUtc = salidaAnterior, EntradaNuevaUtc = entrada, SalidaNuevaUtc = salida, Motivo = motivo
        });
        _auditoria.Registrar("Asistencia", nameof(RegistroAsistencia), registro.RegistroAsistenciaId.ToString(), "Corregir",
            new { EntradaUtc = entradaAnterior, SalidaUtc = salidaAnterior }, new { EntradaUtc = entrada, SalidaUtc = salida, Motivo = motivo });
        await _db.SaveSafeAsync(ct);
        return await _asistencia.DetalleSinAlcanceAsync(registroId, ct);
    }

    public async Task<RegistroAsistenciaDto> AnularAsync(Guid registroId, AnularAsistenciaRequest r, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var registro = await _db.RegistrosAsistencia.FirstOrDefaultAsync(a => a.RegistroAsistenciaId == registroId && a.EmpresaId == empresaId, ct)
            ?? throw ServiceException.NotFound("Registro no encontrado.");
        var alcance = await AlcanceAsync(ct);
        Exigir(alcance, registro.TrabajadorId);
        ValidarVentana(alcance, registro.FechaLaboral);
        var motivo = Helpers.Limpiar(r.Motivo) ?? throw ServiceException.Validation("El motivo es obligatorio.");
        if (registro.Estado == EstadoAsistencia.Anulada) throw ServiceException.Conflict("El registro ya está anulado.");

        registro.Estado = EstadoAsistencia.Anulada;
        _db.CorreccionesAsistencia.Add(new CorreccionAsistencia
        {
            EmpresaId = empresaId, RegistroAsistenciaId = registro.RegistroAsistenciaId, CorregidoPorUsuarioId = _current.RequireUsuarioId(),
            FechaCorreccionUtc = _clock.GetUtcNow().UtcDateTime, EntradaAnteriorUtc = registro.EntradaUtc, SalidaAnteriorUtc = registro.SalidaUtc,
            EntradaNuevaUtc = null, SalidaNuevaUtc = null, Motivo = "Anulación: " + motivo
        });
        _auditoria.Registrar("Asistencia", nameof(RegistroAsistencia), registro.RegistroAsistenciaId.ToString(), "Anular", new { registro.EntradaUtc, registro.SalidaUtc }, new { Motivo = motivo });
        await _db.SaveSafeAsync(ct);
        return await _asistencia.DetalleSinAlcanceAsync(registroId, ct);
    }

    public async Task<RegistroAsistenciaDto> CrearManualAsync(CrearRegistroManualRequest r, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var trabajador = await _db.Trabajadores.AsNoTracking().FirstOrDefaultAsync(t => t.TrabajadorId == r.TrabajadorId && t.EmpresaId == empresaId, ct);
        var alcance = await AlcanceAsync(ct);
        if (trabajador is null) throw ServiceException.NotFound("Trabajador no encontrado.");
        Exigir(alcance, trabajador.TrabajadorId);
        var motivo = Helpers.Limpiar(r.Motivo) ?? throw ServiceException.Validation("El motivo es obligatorio.");

        var entrada = AsistenciaHelper.ComoUtc(r.EntradaUtc);
        var salida = r.SalidaUtc is { } s ? AsistenciaHelper.ComoUtc(s) : (DateTime?)null;
        var fecha = AsistenciaHelper.FechaLocal(entrada, await ZonaAsync(empresaId, ct));
        ValidarVentana(alcance, fecha);
        await ValidarTiemposAsync(empresaId, trabajador.TrabajadorId, null, entrada, salida, ct);
        if (salida is null && await _db.RegistrosAsistencia.AnyAsync(a => a.EmpresaId == empresaId && a.TrabajadorId == trabajador.TrabajadorId
                && a.Estado == EstadoAsistencia.Abierta && a.SalidaUtc == null, ct))
            throw ServiceException.Conflict("El trabajador ya tiene una jornada abierta.");

        var now = _clock.GetUtcNow().UtcDateTime;
        var actor = _current.RequireUsuarioId();
        var registro = new RegistroAsistencia
        {
            RegistroAsistenciaId = Guid.NewGuid(), EmpresaId = empresaId, TrabajadorId = trabajador.TrabajadorId, FechaLaboral = fecha,
            EntradaUtc = entrada, SalidaUtc = salida, EntradaRegistradaPorUsuarioId = actor, SalidaRegistradaPorUsuarioId = salida is null ? null : actor,
            Estado = salida is null ? EstadoAsistencia.Abierta : EstadoAsistencia.Completa, Observaciones = "Registro manual", FechaCreacionUtc = now
        };
        _db.RegistrosAsistencia.Add(registro);
        _db.CorreccionesAsistencia.Add(new CorreccionAsistencia
        {
            EmpresaId = empresaId, RegistroAsistenciaId = registro.RegistroAsistenciaId, CorregidoPorUsuarioId = actor, FechaCorreccionUtc = now,
            EntradaNuevaUtc = entrada, SalidaNuevaUtc = salida, Motivo = "Registro manual: " + motivo
        });
        _auditoria.Registrar("Asistencia", nameof(RegistroAsistencia), registro.RegistroAsistenciaId.ToString(), "CrearManual", null,
            new { registro.TrabajadorId, EntradaUtc = entrada, SalidaUtc = salida, Motivo = motivo });
        await _db.SaveSafeAsync(ct);
        return await _asistencia.DetalleSinAlcanceAsync(registro.RegistroAsistenciaId, ct);
    }
}
