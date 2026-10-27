using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RegistroTrabajo.Data;
using RegistroTrabajo.Data.Entities;
using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public interface ISeguimientoService
{
    /// <summary>Evolución de la tarea: eventos de seguimiento y actualizaciones de avance, del más reciente al más antiguo. Se conserva aunque cambien los responsables o se reabra.</summary>
    Task<IReadOnlyList<EventoSeguimientoDto>> LineaDeTiempoAsync(Guid tareaId, CancellationToken ct);
    Task<IReadOnlyList<RevisionTareaDto>> RevisionesAsync(Guid tareaId, CancellationToken ct);
    /// <summary>Retrasos y pendientes. Con <c>tareas.ver_seguimiento</c> abarca los proyectos visibles; sin él, solo las tareas asignadas al usuario.</summary>
    Task<PanelSeguimientoDto> PanelAsync(SeguimientoFiltro filtro, CancellationToken ct);
    Task<IReadOnlyList<TareaSeguimientoDto>> PendientesRevisionAsync(SeguimientoFiltro filtro, CancellationToken ct);
    Task<ConfiguracionSeguimientoDto> ObtenerConfiguracionAsync(CancellationToken ct);
    Task<ConfiguracionSeguimientoDto> GuardarConfiguracionAsync(ConfiguracionSeguimientoDto request, CancellationToken ct);
}

/// <summary>
/// Matriz explícita de transiciones de estado de una tarea. La API es quien valida: el cliente solo la consulta para mostrar acciones.
/// </summary>
public static class TransicionesTarea
{
    /// <summary>Estados a los que puede llevar la tarea una actualización de avance. Quien no revisa nunca la cierra (Completada).</summary>
    public static IReadOnlyList<byte> DestinosAvance(byte actual, bool revisa)
    {
        if (EstadoTarea.EsCerrada(actual)) return Array.Empty<byte>();
        var l = new List<byte> { EstadoTarea.Pendiente, EstadoTarea.EnProceso, EstadoTarea.EnRevision };
        if (revisa) l.Add(EstadoTarea.Completada);
        return l;
    }

    public static bool AvancePermitido(byte actual, byte destino, bool revisa) => DestinosAvance(actual, revisa).Contains(destino);

    /// <summary>Unión de destinos que el usuario puede provocar con las operaciones disponibles (avance, revisión, reapertura, cancelación).</summary>
    public static List<byte> Destinos(byte actual, bool puedeAvance, bool revisa, bool gestiona)
    {
        var d = new List<byte>();
        if (puedeAvance) d.AddRange(DestinosAvance(actual, revisa));
        if (revisa && actual == EstadoTarea.EnRevision) { d.Add(EstadoTarea.Completada); d.Add(EstadoTarea.Reabierta); }
        if (revisa && EstadoTarea.EsCerrada(actual)) d.Add(EstadoTarea.Reabierta);
        if (gestiona && actual is not (EstadoTarea.Completada or EstadoTarea.Cancelada)) d.Add(EstadoTarea.Cancelada);
        return d.Where(e => e != actual).Distinct().OrderBy(e => e).ToList();
    }
}

/// <summary>Días hábiles de la empresa: sábado y domingo no laboran, ni los festivos (Bloque 5; los recurrentes cada año).</summary>
internal sealed class CalendarioHabil
{
    private readonly HashSet<DateOnly> _fijos;
    private readonly HashSet<(int Mes, int Dia)> _anuales;

    private CalendarioHabil(HashSet<DateOnly> fijos, HashSet<(int, int)> anuales) { _fijos = fijos; _anuales = anuales; }

    public static async Task<CalendarioHabil> CargarAsync(RegistroTrabajoDbContext db, Guid empresaId, CancellationToken ct)
    {
        var festivos = await db.DiasFestivos.AsNoTracking().Where(f => f.EmpresaId == empresaId).Select(f => new { f.Fecha, f.EsRecurrenteAnual }).ToListAsync(ct);
        return new CalendarioHabil(
            festivos.Where(f => !f.EsRecurrenteAnual).Select(f => f.Fecha).ToHashSet(),
            festivos.Where(f => f.EsRecurrenteAnual).Select(f => (f.Fecha.Month, f.Fecha.Day)).ToHashSet());
    }

    public bool EsHabil(DateOnly d) =>
        d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !_fijos.Contains(d) && !_anuales.Contains((d.Month, d.Day));

    /// <summary>La propia fecha si es hábil; si no, el siguiente día hábil. Es el vencimiento efectivo de una fecha límite.</summary>
    public DateOnly SiguienteHabil(DateOnly d)
    {
        for (var i = 0; i < 366 && !EsHabil(d); i++) d = d.AddDays(1);
        return d;
    }

    /// <summary>Último día hábil estrictamente anterior a <paramref name="hoy"/>: una tarea está vencida si su fecha límite es de ese día o anterior.</summary>
    public DateOnly UltimoHabilAnterior(DateOnly hoy)
    {
        var d = hoy.AddDays(-1);
        for (var i = 0; i < 366 && !EsHabil(d); i++) d = d.AddDays(-1);
        return d;
    }

    public bool EstaVencida(DateOnly fechaLimite, DateOnly hoy) => fechaLimite <= UltimoHabilAnterior(hoy);

    /// <summary>Días hábiles en (desde, hasta]; negativo si <paramref name="hasta"/> es anterior.</summary>
    public int DiasHabilesEntre(DateOnly desde, DateOnly hasta)
    {
        if (hasta < desde) return -DiasHabilesEntre(hasta, desde);
        var n = 0;
        for (var d = desde.AddDays(1); d <= hasta && n < 3660; d = d.AddDays(1)) if (EsHabil(d)) n++;
        return n;
    }
}

internal static class SeguimientoHelper
{
    public static string? Json(string? valor) => valor is null ? null : JsonSerializer.Serialize(valor);

    public static string? Desjson(string? json)
    {
        if (json is null) return null;
        try { return JsonSerializer.Deserialize<string>(json); }
        catch (JsonException) { return json; }
    }

    /// <summary>Agrega el evento al contexto: se guarda con el mismo SaveChanges que la operación, así estado e historial son consistentes.</summary>
    public static void Evento(RegistroTrabajoDbContext db, Guid empresaId, Guid tareaId, string tipo, Guid usuarioId, DateTime now,
        string? anterior = null, string? nuevo = null, string? detalle = null) =>
        db.EventosSeguimientoTarea.Add(new EventoSeguimientoTarea
        {
            EmpresaId = empresaId, TareaId = tareaId, Tipo = tipo, UsuarioId = usuarioId, FechaUtc = now,
            ValorAnteriorJson = Json(anterior is null ? null : ProyectoHelper.Recortar(anterior, 500)),
            ValorNuevoJson = Json(nuevo is null ? null : ProyectoHelper.Recortar(nuevo, 500)),
            Detalle = detalle is null ? null : ProyectoHelper.Recortar(detalle, 1000)
        });

    public static string Fecha(DateOnly? f) => f?.ToString("yyyy-MM-dd") ?? "sin fecha";
}

internal static class TareaHelper
{
    /// <summary>Resumen de tareas con proyecto y asignados; "vencida" según los días hábiles de la empresa.</summary>
    public static async Task<List<TareaResumenDto>> ResumenesAsync(RegistroTrabajoDbContext db, Guid empresaId, IReadOnlyList<Tarea> tareas, DateOnly hoy, CalendarioHabil cal, CancellationToken ct)
    {
        var ids = tareas.Select(t => t.TareaId).ToList();
        var proyectoIds = tareas.Select(t => t.ProyectoId).Distinct().ToList();
        var proyectos = await db.Proyectos.AsNoTracking().Where(p => p.EmpresaId == empresaId && proyectoIds.Contains(p.ProyectoId)).ToDictionaryAsync(p => p.ProyectoId, p => p.Nombre, ct);
        var asig = await db.AsignacionesTarea.AsNoTracking().Where(x => x.EmpresaId == empresaId && ids.Contains(x.TareaId) && x.FechaRetiroUtc == null)
            .Select(x => new { x.TareaId, x.TrabajadorId }).ToListAsync(ct);
        var nombres = await ProyectoHelper.NombresTrabajadoresAsync(db, empresaId, asig.Select(x => x.TrabajadorId), ct);
        return tareas.Select(t => new TareaResumenDto
        {
            TareaId = t.TareaId, ProyectoId = t.ProyectoId, Proyecto = proyectos.GetValueOrDefault(t.ProyectoId, "—"), Titulo = t.Titulo, Estado = t.Estado,
            PorcentajeAvance = t.PorcentajeAvance, Prioridad = t.Prioridad, FechaInicio = t.FechaInicio, FechaVencimiento = t.FechaVencimiento,
            Vencida = !EstadoTarea.EsCerrada(t.Estado) && t.FechaVencimiento is { } f && cal.EstaVencida(f, hoy),
            Asignados = asig.Where(x => x.TareaId == t.TareaId).Select(x => nombres.GetValueOrDefault(x.TrabajadorId, "—")).OrderBy(n => n).ToList()
        }).ToList();
    }
}

public class SeguimientoService : ISeguimientoService
{
    private const int MaxPorLista = 50;
    private const int MaxTareas = 2000;

    private readonly RegistroTrabajoDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IPermisoService _permisos;
    private readonly IAuditoriaService _auditoria;
    private readonly TimeProvider _clock;

    public SeguimientoService(RegistroTrabajoDbContext db, ICurrentUser current, IPermisoService permisos, IAuditoriaService auditoria, TimeProvider clock)
    {
        _db = db; _current = current; _permisos = permisos; _auditoria = auditoria; _clock = clock;
    }

    private async Task<(AccesoProyectos A, Tarea T)> CargarTareaAsync(Guid tareaId, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.ProyectosConsultar, ct);
        var a = await ProyectoHelper.AccesoAsync(_db, _current, _permisos, _clock, ct);
        var t = await _db.Tareas.AsNoTracking().FirstOrDefaultAsync(x => x.TareaId == tareaId && x.EmpresaId == a.EmpresaId, ct) ?? throw ServiceException.NotFound("Tarea no encontrada.");
        await ProyectoHelper.CargarVisibleAsync(_db, a, t.ProyectoId, false, ct); // 404 si el proyecto no es visible
        return (a, t);
    }

    // Los eventos que ya tienen su fila de avance en la misma operación se fusionan en una sola entrada.
    private static readonly HashSet<string> EventosConAvance = new()
    {
        TipoEventoTarea.EnviadaARevision, TipoEventoTarea.RevisionAprobada, TipoEventoTarea.RevisionDevuelta, TipoEventoTarea.Reabierta, TipoEventoTarea.Cancelada
    };

    /// <summary>Estado en que queda la tarea tras cada evento que comparte operación con una fila de avance.</summary>
    private static byte EstadoDelEvento(string tipo) => tipo switch
    {
        TipoEventoTarea.EnviadaARevision => EstadoTarea.EnRevision,
        TipoEventoTarea.RevisionAprobada => EstadoTarea.Completada,
        TipoEventoTarea.Cancelada => EstadoTarea.Cancelada,
        _ => EstadoTarea.Reabierta // devuelta y reabierta
    };

    public async Task<IReadOnlyList<EventoSeguimientoDto>> LineaDeTiempoAsync(Guid tareaId, CancellationToken ct)
    {
        var (a, _) = await CargarTareaAsync(tareaId, ct);
        var eventos = await _db.EventosSeguimientoTarea.AsNoTracking().Where(e => e.EmpresaId == a.EmpresaId && e.TareaId == tareaId).ToListAsync(ct);
        var avances = await _db.HistorialAvanceTarea.AsNoTracking().Where(h => h.EmpresaId == a.EmpresaId && h.TareaId == tareaId).ToListAsync(ct);
        var nombres = await ProyectoHelper.NombresUsuariosAsync(_db, a.EmpresaId, eventos.Select(e => e.UsuarioId).Concat(avances.Select(h => h.ActualizadoPorUsuarioId)), ct);

        var lista = new List<(DateTime Fecha, long Orden, EventoSeguimientoDto Dto)>();
        var fusionados = new HashSet<long>();
        foreach (var e in eventos)
        {
            var dto = new EventoSeguimientoDto
            {
                Tipo = e.Tipo, Usuario = nombres.GetValueOrDefault(e.UsuarioId, "—"), FechaUtc = e.FechaUtc,
                ValorAnterior = SeguimientoHelper.Desjson(e.ValorAnteriorJson), ValorNuevo = SeguimientoHelper.Desjson(e.ValorNuevoJson), Detalle = e.Detalle
            };
            if (EventosConAvance.Contains(e.Tipo))
            {
                var destino = EstadoDelEvento(e.Tipo);
                var h = avances.FirstOrDefault(x => !fusionados.Contains(x.HistorialAvanceTareaId) && x.FechaUtc == e.FechaUtc && x.ActualizadoPorUsuarioId == e.UsuarioId && x.EstadoNuevo == destino);
                if (h is not null)
                {
                    fusionados.Add(h.HistorialAvanceTareaId);
                    dto.PorcentajeAnterior = h.PorcentajeAnterior; dto.PorcentajeNuevo = h.PorcentajeNuevo;
                    dto.EstadoAnterior = h.EstadoAnterior; dto.EstadoNuevo = h.EstadoNuevo;
                    dto.Detalle ??= h.Comentario;
                }
            }
            lista.Add((e.FechaUtc, e.EventoSeguimientoTareaId, dto));
        }
        foreach (var h in avances.Where(h => !fusionados.Contains(h.HistorialAvanceTareaId)))
            lista.Add((h.FechaUtc, h.HistorialAvanceTareaId, new EventoSeguimientoDto
            {
                Tipo = TipoEventoTarea.AvanceActualizado, Usuario = nombres.GetValueOrDefault(h.ActualizadoPorUsuarioId, "—"), FechaUtc = h.FechaUtc,
                PorcentajeAnterior = h.PorcentajeAnterior, PorcentajeNuevo = h.PorcentajeNuevo, EstadoAnterior = h.EstadoAnterior, EstadoNuevo = h.EstadoNuevo, Detalle = h.Comentario
            }));
        return lista.OrderByDescending(x => x.Fecha).ThenByDescending(x => x.Orden).Select(x => x.Dto).ToList();
    }

    public async Task<IReadOnlyList<RevisionTareaDto>> RevisionesAsync(Guid tareaId, CancellationToken ct)
    {
        var (a, _) = await CargarTareaAsync(tareaId, ct);
        var filas = await _db.RevisionesTarea.AsNoTracking().Where(r => r.EmpresaId == a.EmpresaId && r.TareaId == tareaId)
            .OrderByDescending(r => r.FechaUtc).ThenByDescending(r => r.RevisionTareaId).ToListAsync(ct);
        var nombres = await ProyectoHelper.NombresUsuariosAsync(_db, a.EmpresaId, filas.Select(r => r.RevisorUsuarioId), ct);
        return filas.Select(r => new RevisionTareaDto
        {
            RevisionTareaId = r.RevisionTareaId, Resultado = r.Resultado, Revisor = nombres.GetValueOrDefault(r.RevisorUsuarioId, "—"),
            Observacion = r.Observacion, PorcentajeAlRevisar = r.PorcentajeAlRevisar, FechaUtc = r.FechaUtc
        }).ToList();
    }

    // ---------------- Panel ----------------
    private sealed record Ambito(AccesoProyectos A, bool VerEquipo, DateOnly Hoy, TimeZoneInfo Zona, CalendarioHabil Cal, ConfiguracionEmpresa Cfg, bool Revisa);

    private async Task<Ambito> AmbitoAsync(CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.ProyectosConsultar, ct);
        var a = await ProyectoHelper.AccesoAsync(_db, _current, _permisos, _clock, ct);
        var cfg = await _db.ConfiguracionEmpresa.AsNoTracking().FirstAsync(c => c.EmpresaId == a.EmpresaId, ct);
        var zona = AsistenciaHelper.Zona(cfg.ZonaHorariaId);
        var hoy = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(_clock.GetUtcNow().UtcDateTime, zona));
        var cal = await CalendarioHabil.CargarAsync(_db, a.EmpresaId, ct);
        var verEquipo = await _permisos.TienePermisoAsync(Permisos.TareasVerSeguimiento, ct);
        var revisa = await _permisos.TienePermisoAsync(Permisos.TareasRevisar, ct);
        return new Ambito(a, verEquipo, hoy, zona, cal, cfg, revisa);
    }

    /// <summary>Tareas abiertas del alcance del usuario (equipo o propias), con el filtro aplicado.</summary>
    private async Task<(List<Tarea> Tareas, Dictionary<Guid, Guid> Responsables)> TareasAsync(Ambito m, SeguimientoFiltro f, bool soloEnRevision, CancellationToken ct)
    {
        var visibles = ProyectoHelper.Visibles(_db, m.A);
        var q = _db.Tareas.AsNoTracking().Where(t => t.EmpresaId == m.A.EmpresaId && visibles.Any(p => p.ProyectoId == t.ProyectoId));
        q = soloEnRevision ? q.Where(t => t.Estado == EstadoTarea.EnRevision) : q.Where(t => t.Estado != EstadoTarea.Completada && t.Estado != EstadoTarea.Cancelada);
        if (f.ProyectoId is { } pid) q = q.Where(t => t.ProyectoId == pid);

        Guid? trabajador = m.VerEquipo ? f.TrabajadorId : m.A.MiTrabajadorId;
        if (!m.VerEquipo && trabajador is null) return (new(), new());
        if (trabajador is { } w)
            q = q.Where(t => _db.AsignacionesTarea.Any(x => x.EmpresaId == t.EmpresaId && x.TareaId == t.TareaId && x.TrabajadorId == w && x.FechaRetiroUtc == null));

        var tareas = await q.OrderBy(t => t.FechaVencimiento == null).ThenBy(t => t.FechaVencimiento).Take(MaxTareas).ToListAsync(ct);
        var proyectoIds = tareas.Select(t => t.ProyectoId).Distinct().ToList();
        var resp = await _db.Proyectos.AsNoTracking().Where(p => p.EmpresaId == m.A.EmpresaId && proyectoIds.Contains(p.ProyectoId))
            .ToDictionaryAsync(p => p.ProyectoId, p => p.ResponsableTrabajadorId, ct);
        return (tareas, resp);
    }

    private TareaSeguimientoDto Mapear(Ambito m, TareaResumenDto r, Tarea t, Dictionary<Guid, Guid> resp)
    {
        var ultima = t.FechaActualizacionUtc ?? t.FechaCreacionUtc;
        var ultimaLocal = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(ultima, DateTimeKind.Utc), m.Zona));
        var dto = new TareaSeguimientoDto
        {
            TareaId = r.TareaId, ProyectoId = r.ProyectoId, Proyecto = r.Proyecto, Titulo = r.Titulo, Estado = r.Estado, PorcentajeAvance = r.PorcentajeAvance,
            Prioridad = r.Prioridad, FechaInicio = r.FechaInicio, FechaVencimiento = r.FechaVencimiento, Vencida = r.Vencida, Asignados = r.Asignados,
            UltimaActualizacionUtc = ultima, DiasHabilesSinActualizar = Math.Max(0, m.Cal.DiasHabilesEntre(ultimaLocal, m.Hoy)),
            RowVersion = ProyectoHelper.Version(t.RowVersion),
            PuedeRevisar = m.Revisa && resp.TryGetValue(t.ProyectoId, out var rid) && m.A.Administra(rid)
        };
        if (t.FechaVencimiento is { } f)
        {
            var efectiva = m.Cal.SiguienteHabil(f);
            if (r.Vencida) dto.DiasRetraso = m.Cal.DiasHabilesEntre(efectiva, m.Hoy);
            else dto.DiasParaVencer = m.Cal.DiasHabilesEntre(m.Hoy, efectiva);
        }
        return dto;
    }

    public async Task<PanelSeguimientoDto> PanelAsync(SeguimientoFiltro f, CancellationToken ct)
    {
        var m = await AmbitoAsync(ct);
        var (tareas, resp) = await TareasAsync(m, f, false, ct);
        var resumenes = await TareaHelper.ResumenesAsync(_db, m.A.EmpresaId, tareas, m.Hoy, m.Cal, ct);
        var items = tareas.Select((t, i) => Mapear(m, resumenes[i], t, resp)).ToList();

        var alerta = m.Cfg.DiasAlertaVencimiento;
        var sinAct = m.Cfg.DiasSinActualizacion;
        var vencidas = items.Where(i => i.Vencida).OrderByDescending(i => i.DiasRetraso).ToList();
        var proximas = items.Where(i => !i.Vencida && i.DiasParaVencer is { } d && d <= alerta).OrderBy(i => i.DiasParaVencer).ToList();
        var revision = items.Where(i => i.Estado == EstadoTarea.EnRevision).OrderBy(i => i.UltimaActualizacionUtc).ToList();
        var reabiertas = items.Where(i => i.Estado == EstadoTarea.Reabierta).OrderBy(i => i.UltimaActualizacionUtc).ToList();
        var inactivas = sinAct is { } n
            ? items.Where(i => i.Estado is not EstadoTarea.EnRevision && i.DiasHabilesSinActualizar >= n).OrderByDescending(i => i.DiasHabilesSinActualizar).ToList()
            : new List<TareaSeguimientoDto>();

        return new PanelSeguimientoDto
        {
            VerEquipo = m.VerEquipo, DiasAlertaVencimiento = alerta, DiasSinActualizacion = sinAct,
            TotalVencidas = vencidas.Count, TotalProximasAVencer = proximas.Count, TotalPendientesRevision = revision.Count,
            TotalReabiertas = reabiertas.Count, TotalSinActualizacion = inactivas.Count,
            Vencidas = vencidas.Take(MaxPorLista).ToList(), ProximasAVencer = proximas.Take(MaxPorLista).ToList(),
            PendientesRevision = revision.Take(MaxPorLista).ToList(), Reabiertas = reabiertas.Take(MaxPorLista).ToList(),
            SinActualizacion = inactivas.Take(MaxPorLista).ToList()
        };
    }

    public async Task<IReadOnlyList<TareaSeguimientoDto>> PendientesRevisionAsync(SeguimientoFiltro f, CancellationToken ct)
    {
        var m = await AmbitoAsync(ct);
        // Quien no revisa ni ve el seguimiento del equipo solo ve sus propias tareas en revisión.
        var (tareas, resp) = await TareasAsync(m, f, true, ct);
        var resumenes = await TareaHelper.ResumenesAsync(_db, m.A.EmpresaId, tareas, m.Hoy, m.Cal, ct);
        return tareas.Select((t, i) => Mapear(m, resumenes[i], t, resp)).OrderBy(i => i.UltimaActualizacionUtc).Take(200).ToList();
    }

    // ---------------- Configuración ----------------
    public async Task<ConfiguracionSeguimientoDto> ObtenerConfiguracionAsync(CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.EmpresaConsultar, ct);
        var cfg = await _db.ConfiguracionEmpresa.AsNoTracking().FirstAsync(c => c.EmpresaId == _current.RequireEmpresaId(), ct);
        return new ConfiguracionSeguimientoDto { DiasAlertaVencimiento = cfg.DiasAlertaVencimiento, DiasSinActualizacion = cfg.DiasSinActualizacion };
    }

    public async Task<ConfiguracionSeguimientoDto> GuardarConfiguracionAsync(ConfiguracionSeguimientoDto r, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.EmpresaConfigurar, ct);
        var empresaId = _current.RequireEmpresaId();
        if (r.DiasAlertaVencimiento > 30) throw ServiceException.Validation("Los días de alerta de vencimiento deben estar entre 0 y 30.");
        if (r.DiasSinActualizacion is { } d && d is < 1 or > 365) throw ServiceException.Validation("Los días sin actualización deben estar entre 1 y 365 (o vacío para desactivar).");
        var cfg = await _db.ConfiguracionEmpresa.FirstAsync(c => c.EmpresaId == empresaId, ct);
        var antes = new { cfg.DiasAlertaVencimiento, cfg.DiasSinActualizacion };
        cfg.DiasAlertaVencimiento = r.DiasAlertaVencimiento;
        cfg.DiasSinActualizacion = r.DiasSinActualizacion;
        cfg.FechaActualizacionUtc = _clock.GetUtcNow().UtcDateTime;
        _auditoria.Registrar("Tareas", nameof(ConfiguracionEmpresa), empresaId.ToString(), "ConfigurarSeguimiento", antes, new { r.DiasAlertaVencimiento, r.DiasSinActualizacion });
        await _db.SaveSafeAsync(ct);
        return r;
    }
}
