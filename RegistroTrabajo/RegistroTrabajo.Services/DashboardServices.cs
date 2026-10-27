using Microsoft.EntityFrameworkCore;
using RegistroTrabajo.Data;
using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public interface IDashboardService
{
    /// <summary>
    /// Indicadores adaptados al perfil y a los permisos del usuario conectado. Los números no dan acceso a los registros:
    /// al abrir cualquier elemento, el módulo correspondiente vuelve a validar la autorización.
    /// </summary>
    Task<DashboardDto> ObtenerAsync(CancellationToken ct);
}

public class DashboardService : IDashboardService
{
    private const int DiasProximos = 14;
    private const int MaxActividad = 8;

    private readonly RegistroTrabajoDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IPermisoService _permisos;
    private readonly INotificacionesService _notificaciones;
    private readonly TimeProvider _clock;

    public DashboardService(RegistroTrabajoDbContext db, ICurrentUser current, IPermisoService permisos, INotificacionesService notificaciones, TimeProvider clock)
    {
        _db = db; _current = current; _permisos = permisos; _notificaciones = notificaciones; _clock = clock;
    }

    private async Task<bool> PuedeAsync(string permiso, CancellationToken ct) => await _permisos.TienePermisoAsync(permiso, ct);

    public async Task<DashboardDto> ObtenerAsync(CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var usuarioId = _current.RequireUsuarioId();
        var ahora = _clock.GetUtcNow().UtcDateTime;
        var cfgZona = await _db.ConfiguracionEmpresa.AsNoTracking().Where(x => x.EmpresaId == empresaId).Select(x => x.ZonaHorariaId).FirstOrDefaultAsync(ct);
        var zona = AsistenciaHelper.Zona(cfgZona);
        var hoy = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(ahora, zona));
        var cal = await CalendarioHabil.CargarAsync(_db, empresaId, ct);
        var mi = await TrabajadorHelper.IdDeUsuarioAsync(_db, empresaId, usuarioId, ct);

        var admin = await PuedeAsync(Permisos.EmpresaConfigurar, ct);
        var rrhh = !admin && await PuedeAsync(Permisos.TrabajadoresEditar, ct);
        var supervisor = !admin && !rrhh && (await PuedeAsync(Permisos.PermisosAprobar, ct) || await PuedeAsync(Permisos.AsistenciaConsultar, ct));
        var amplio = admin || rrhh;

        var d = new DashboardDto
        {
            Perfil = admin ? NombresRol.Administrador : rrhh ? NombresRol.RecursosHumanos : supervisor ? NombresRol.Supervisor : NombresRol.Trabajador,
            Ambito = amplio ? "empresa" : supervisor ? "equipo" : "propio",
            GeneradoUtc = ahora
        };

        // Trabajadores cuyo seguimiento corresponde al usuario (equipo vigente); null = toda la empresa.
        List<Guid>? equipo = null;
        if (!amplio)
        {
            equipo = mi is { } m
                ? await _db.TrabajadorSupervisores.AsNoTracking()
                    .Where(s => s.EmpresaId == empresaId && s.SupervisorTrabajadorId == m && s.FechaInicio <= hoy && (s.FechaFin == null || s.FechaFin >= hoy))
                    .Select(s => s.TrabajadorId).Distinct().ToListAsync(ct)
                : new List<Guid>();
        }
        var propios = new List<Guid>(equipo ?? new());
        if (mi is { } yo) propios.Add(yo);

        if (amplio || (supervisor && equipo!.Count > 0)) d.Trabajadores = await TrabajadoresAsync(empresaId, equipo, hoy, ct);
        d.Permisos = await PermisosAsync(empresaId, mi, equipo, amplio, ahora, zona, ct);
        d.Proyectos = await ProyectosAsync(ct);
        d.Tareas = await TareasAsync(empresaId, mi, amplio || supervisor, hoy, cal, ct);
        d.Asistencia = await AsistenciaAsync(empresaId, equipo, mi, amplio, hoy, ct);
        if (admin && await PuedeAsync(Permisos.UsuariosConsultar, ct))
            d.UsuariosActivos = await _db.Usuarios.AsNoTracking().CountAsync(u => u.EmpresaId == empresaId && u.Estado == EstadoUsuario.Activo, ct);

        if (await PuedeAsync(Permisos.NotificacionesConsultar, ct))
        {
            var n = await _notificaciones.ResumenAsync(ct);
            d.NotificacionesNoLeidas = n.NoLeidas;
            d.NotificacionesPrioritarias = n.Prioritarias;
        }
        d.Actividad = await ActividadAsync(empresaId, zona, ct);
        d.Accesos = await AccesosAsync(ct);
        return d;
    }

    // ---------------- Indicadores ----------------
    private async Task<IndicadorTrabajadoresDto> TrabajadoresAsync(Guid empresaId, List<Guid>? ambito, DateOnly hoy, CancellationToken ct)
    {
        var q = _db.Trabajadores.AsNoTracking().Where(t => t.EmpresaId == empresaId);
        if (ambito is not null) q = q.Where(t => ambito.Contains(t.TrabajadorId));
        var por = await q.GroupBy(t => t.EstadoLaboral).Select(g => new { g.Key, N = g.Count() }).ToListAsync(ct);
        var desde = hoy.AddDays(-30);
        return new IndicadorTrabajadoresDto
        {
            Activos = por.FirstOrDefault(x => x.Key == EstadoLaboral.Activo)?.N ?? 0,
            Inactivos = por.FirstOrDefault(x => x.Key == EstadoLaboral.Inactivo)?.N ?? 0,
            Baja = por.FirstOrDefault(x => x.Key == EstadoLaboral.Baja)?.N ?? 0,
            AltasUltimos30Dias = await q.CountAsync(t => t.FechaIngreso >= desde && t.FechaIngreso <= hoy, ct)
        };
    }

    private async Task<IndicadorPermisosDto?> PermisosAsync(Guid empresaId, Guid? mi, List<Guid>? equipo, bool amplio, DateTime ahoraUtc, TimeZoneInfo zona, CancellationToken ct)
    {
        var puedeSolicitar = await PuedeAsync(Permisos.PermisosSolicitar, ct);
        var puedeRevisar = amplio ? await PuedeAsync(Permisos.PermisosAprobarTodos, ct) || await PuedeAsync(Permisos.PermisosAprobar, ct) : await PuedeAsync(Permisos.PermisosAprobar, ct);
        if (!puedeSolicitar && !puedeRevisar) return null;

        var q = _db.SolicitudesPermiso.AsNoTracking().Where(s => s.EmpresaId == empresaId);
        var ind = new IndicadorPermisosDto();
        if (mi is { } yo) ind.MisPendientes = await q.CountAsync(s => s.TrabajadorId == yo && s.Estado == EstadoSolicitudPermiso.Pendiente, ct);

        var todos = amplio && (await PuedeAsync(Permisos.PermisosAprobarTodos, ct) || await PuedeAsync(Permisos.PermisosConsultarTodos, ct));
        var alcance = new List<Guid>(equipo ?? new());
        if (mi is { } y) alcance.Add(y);

        if (puedeRevisar)
        {
            var rev = q.Where(s => s.Estado == EstadoSolicitudPermiso.Pendiente && (mi == null || s.TrabajadorId != mi));
            if (!await PuedeAsync(Permisos.PermisosAprobarTodos, ct)) { var e = equipo ?? new(); rev = rev.Where(s => e.Contains(s.TrabajadorId)); }
            ind.PendientesPorRevisar = await rev.CountAsync(ct);
        }

        var local = TimeZoneInfo.ConvertTimeFromUtc(ahoraUtc, zona);
        var inicio = local.Date; var fin = inicio.AddDays(DiasProximos + 1);
        var prox = q.Where(s => s.Estado == EstadoSolicitudPermiso.Aprobada && s.FechaInicio >= inicio && s.FechaInicio < fin);
        if (!todos) prox = prox.Where(s => alcance.Contains(s.TrabajadorId));
        ind.ProximosAprobados = await prox.CountAsync(ct);
        return ind;
    }

    private async Task<IndicadorProyectosDto?> ProyectosAsync(CancellationToken ct)
    {
        if (!await PuedeAsync(Permisos.ProyectosConsultar, ct)) return null;
        var a = await ProyectoHelper.AccesoAsync(_db, _current, _permisos, _clock, ct);
        var por = await ProyectoHelper.Visibles(_db, a).GroupBy(p => new { p.EstadoProyecto.Nombre, p.EstadoProyecto.EsFinal })
            .Select(g => new { g.Key.Nombre, g.Key.EsFinal, N = g.Count() }).ToListAsync(ct);
        return new IndicadorProyectosDto
        {
            Activos = por.Where(x => !x.EsFinal && !ReporteService.EsPausa(x.Nombre)).Sum(x => x.N),
            Pausados = por.Where(x => !x.EsFinal && ReporteService.EsPausa(x.Nombre)).Sum(x => x.N),
            Finalizados = por.Where(x => x.EsFinal).Sum(x => x.N)
        };
    }

    private async Task<IndicadorTareasDto?> TareasAsync(Guid empresaId, Guid? mi, bool delEquipo, DateOnly hoy, CalendarioHabil cal, CancellationToken ct)
    {
        if (!await PuedeAsync(Permisos.ProyectosConsultar, ct)) return null;
        var a = await ProyectoHelper.AccesoAsync(_db, _current, _permisos, _clock, ct);
        var visibles = ProyectoHelper.Visibles(_db, a);
        var q = _db.Tareas.AsNoTracking().Where(t => t.EmpresaId == empresaId && visibles.Any(p => p.ProyectoId == t.ProyectoId));
        // El trabajador ve sus propias tareas; supervisores y administración, las de los proyectos que ven.
        if (!delEquipo)
        {
            if (mi is not { } yo) return new IndicadorTareasDto();
            q = q.Where(t => _db.AsignacionesTarea.Any(x => x.EmpresaId == t.EmpresaId && x.TareaId == t.TareaId && x.TrabajadorId == yo && x.FechaRetiroUtc == null));
        }
        var por = await q.GroupBy(t => t.Estado).Select(g => new { g.Key, N = g.Count() }).ToListAsync(ct);
        int N(byte e) => por.FirstOrDefault(x => x.Key == e)?.N ?? 0;
        var fechas = await q.Where(t => t.Estado != EstadoTarea.Completada && t.Estado != EstadoTarea.Cancelada && t.FechaVencimiento != null)
            .Select(t => t.FechaVencimiento!.Value).Take(50_000).ToListAsync(ct);
        var alerta = await _db.ConfiguracionEmpresa.AsNoTracking().Where(c => c.EmpresaId == empresaId).Select(c => (int)c.DiasAlertaVencimiento).FirstOrDefaultAsync(ct);
        return new IndicadorTareasDto
        {
            Pendientes = N(EstadoTarea.Pendiente), EnProceso = N(EstadoTarea.EnProceso), EnRevision = N(EstadoTarea.EnRevision),
            Reabiertas = N(EstadoTarea.Reabierta), Completadas = N(EstadoTarea.Completada),
            Vencidas = fechas.Count(f => cal.EstaVencida(f, hoy)),
            ProximasAVencer = fechas.Count(f => !cal.EstaVencida(f, hoy) && cal.DiasHabilesEntre(hoy, cal.SiguienteHabil(f)) <= alerta)
        };
    }

    private async Task<IndicadorAsistenciaDto?> AsistenciaAsync(Guid empresaId, List<Guid>? equipo, Guid? mi, bool amplio, DateOnly hoy, CancellationToken ct)
    {
        var todos = await PuedeAsync(Permisos.AsistenciaConsultarTodos, ct);
        var supervisa = await PuedeAsync(Permisos.AsistenciaConsultar, ct);
        var propia = await PuedeAsync(Permisos.AsistenciaConsultarPropia, ct) || await PuedeAsync(Permisos.AsistenciaRegistrar, ct);
        var ids = new List<Guid>();
        if (!todos)
        {
            if (supervisa && equipo is not null) ids.AddRange(equipo);
            if (propia && mi is { } y) ids.Add(y);
            if (ids.Count == 0) return null;
        }
        var q = _db.RegistrosAsistencia.AsNoTracking().Where(a => a.EmpresaId == empresaId && a.Estado != EstadoAsistencia.Anulada);
        if (!todos) q = q.Where(a => ids.Contains(a.TrabajadorId));
        return new IndicadorAsistenciaDto
        {
            ConRegistroHoy = await q.Where(a => a.FechaLaboral == hoy).Select(a => a.TrabajadorId).Distinct().CountAsync(ct),
            IncompletasAnteriores = await q.CountAsync(a => a.FechaLaboral < hoy && a.EntradaUtc != null && a.SalidaUtc == null, ct)
        };
    }

    // ---------------- Actividad reciente ----------------
    private async Task<List<ActividadRecienteDto>> ActividadAsync(Guid empresaId, TimeZoneInfo zona, CancellationToken ct)
    {
        var lista = new List<ActividadRecienteDto>();
        if (!await PuedeAsync(Permisos.ProyectosConsultar, ct)) return lista;
        var a = await ProyectoHelper.AccesoAsync(_db, _current, _permisos, _clock, ct);
        var visibles = ProyectoHelper.Visibles(_db, a);
        var eventos = await (from e in _db.EventosSeguimientoTarea.AsNoTracking()
                             join t in _db.Tareas.AsNoTracking() on new { e.EmpresaId, e.TareaId } equals new { t.EmpresaId, t.TareaId }
                             where e.EmpresaId == empresaId && e.Tipo != TipoEventoTarea.Comentario && visibles.Any(p => p.ProyectoId == t.ProyectoId)
                             orderby e.FechaUtc descending, e.EventoSeguimientoTareaId descending
                             select new { e.Tipo, e.UsuarioId, e.FechaUtc, e.TareaId, t.Titulo }).Take(MaxActividad).ToListAsync(ct);
        var nombres = await ProyectoHelper.NombresUsuariosAsync(_db, empresaId, eventos.Select(e => e.UsuarioId), ct);
        foreach (var e in eventos)
            lista.Add(new ActividadRecienteDto
            {
                FechaUtc = e.FechaUtc, Ruta = $"tareas/{e.TareaId}",
                Texto = $"{nombres.GetValueOrDefault(e.UsuarioId, "—")}: {TextoEvento(e.Tipo)} en «{ProyectoHelper.Recortar(e.Titulo, 60)}»"
            });
        return lista.OrderByDescending(x => x.FechaUtc).Take(MaxActividad).ToList();
    }

    private static string TextoEvento(string tipo) => tipo switch
    {
        TipoEventoTarea.FechaLimiteCambiada => "cambió la fecha límite",
        TipoEventoTarea.PrioridadCambiada => "cambió la prioridad",
        TipoEventoTarea.AsignacionAgregada => "asignó a alguien",
        TipoEventoTarea.AsignacionRetirada => "retiró a alguien",
        TipoEventoTarea.EnviadaARevision => "envió a revisión",
        TipoEventoTarea.RevisionAprobada => "aprobó la revisión",
        TipoEventoTarea.RevisionDevuelta => "devolvió con correcciones",
        TipoEventoTarea.Reabierta => "reabrió la tarea",
        TipoEventoTarea.Cancelada => "canceló la tarea",
        _ => "actualizó la tarea"
    };

    // ---------------- Accesos rápidos ----------------
    private async Task<List<AccesoRapidoDto>> AccesosAsync(CancellationToken ct)
    {
        var candidatos = new (string Titulo, string Ruta, string[] Permisos)[]
        {
            ("Registrar asistencia", "asistencia", new[] { Models.Permisos.AsistenciaRegistrar }),
            ("Solicitar un permiso", "permisos", new[] { Models.Permisos.PermisosSolicitar }),
            ("Aprobar permisos", "permisos/aprobaciones", new[] { Models.Permisos.PermisosAprobar, Models.Permisos.PermisosAprobarTodos }),
            ("Mis tareas", "mis-tareas", new[] { Models.Permisos.ProyectosConsultar }),
            ("Proyectos", "proyectos", new[] { Models.Permisos.ProyectosConsultar }),
            ("Seguimiento de tareas", "seguimiento", new[] { Models.Permisos.ProyectosConsultar }),
            ("Revisión de tareas", "revision-tareas", new[] { Models.Permisos.TareasRevisar }),
            ("Calendario", "calendario", new[] { Models.Permisos.CalendarioConsultar }),
            ("Trabajadores", "trabajadores", new[] { Models.Permisos.TrabajadoresConsultar }),
            ("Reportes", "reportes", new[] { Models.Permisos.ReportesConsultar }),
            ("Notificaciones", "notificaciones", new[] { Models.Permisos.NotificacionesConsultar }),
            ("Mis borradores", "borradores", Array.Empty<string>())
        };
        var lista = new List<AccesoRapidoDto>();
        foreach (var c in candidatos)
        {
            var ok = c.Permisos.Length == 0;
            foreach (var p in c.Permisos) if (await PuedeAsync(p, ct)) { ok = true; break; }
            if (ok) lista.Add(new AccesoRapidoDto { Titulo = c.Titulo, Ruta = c.Ruta });
        }
        return lista;
    }
}
