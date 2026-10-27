using Microsoft.EntityFrameworkCore;
using RegistroTrabajo.Data;
using RegistroTrabajo.Data.Entities;
using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

/// <summary>Resultado del cálculo de duración de una solicitud, ya normalizado (fechas de inicio y fin listas para guardar).</summary>
public record CalculoResultado(decimal Cantidad, DateTime Inicio, DateTime Fin);

public interface ICalculoDuracionService
{
    /// <summary>
    /// Duración de una ausencia en la unidad del tipo (días u horas). Cuenta solo días laborales según el horario asignado
    /// (o lunes a viernes si no tiene), sin festivos ni descansos del trabajador.
    /// </summary>
    Task<CalculoResultado> CalcularAsync(Guid empresaId, Guid trabajadorId, TipoPermiso tipo, DateTime inicio, DateTime fin, bool porHoras, CancellationToken ct);

    /// <summary>Vista previa para el trabajador autenticado (duración y efecto sobre su saldo).</summary>
    Task<CalculoDuracionDto> PrevisualizarAsync(CalculoDuracionRequest request, CancellationToken ct);
}

public class CalculoDuracionService : ICalculoDuracionService
{
    private const int MaxDiasPorSolicitud = 366;

    private readonly RegistroTrabajoDbContext _db;
    private readonly ICurrentUser _current;
    private readonly TimeProvider _clock;

    public CalculoDuracionService(RegistroTrabajoDbContext db, ICurrentUser current, TimeProvider clock)
    {
        _db = db; _current = current; _clock = clock;
    }

    public async Task<CalculoResultado> CalcularAsync(Guid empresaId, Guid trabajadorId, TipoPermiso tipo, DateTime inicio, DateTime fin, bool porHoras, CancellationToken ct)
    {
        if (fin < inicio) throw ServiceException.Validation("La fecha de fin no puede ser anterior a la de inicio.");
        var d0 = DateOnly.FromDateTime(inicio);
        var d1 = DateOnly.FromDateTime(fin);
        if (d1.DayNumber - d0.DayNumber >= MaxDiasPorSolicitud) throw ServiceException.Validation("El periodo solicitado es demasiado largo.");

        var calendario = await CargarCalendarioAsync(empresaId, trabajadorId, d0, d1, ct);

        if (porHoras)
        {
            if (tipo.UnidadBase != UnidadPermiso.Horas && !tipo.PermiteFraccionar)
                throw ServiceException.Validation("Este tipo de permiso no admite solicitudes por horas.");
            if (d0 != d1) throw ServiceException.Validation("Una solicitud por horas debe ser de un solo día.");
            var ini = Recortar(inicio);
            var fn = Recortar(fin);
            if (fn <= ini) throw ServiceException.Validation("La hora de fin debe ser posterior a la de inicio.");
            if (!calendario.EsLaboral(d0)) throw ServiceException.Validation("El día solicitado no es laborable (descanso, festivo o día no laboral).");
            var horas = Math.Round((decimal)(fn - ini).TotalHours, 2);
            var cantidad = tipo.UnidadBase == UnidadPermiso.Horas ? horas : Math.Round(horas / calendario.HorasJornada(d0), 2);
            if (cantidad <= 0) throw ServiceException.Validation("La duración solicitada no es válida.");
            return new CalculoResultado(cantidad, ini, fn);
        }

        decimal total = 0;
        var laborales = 0;
        for (var d = d0; d <= d1; d = d.AddDays(1))
        {
            if (!calendario.EsLaboral(d)) continue;
            laborales++;
            total += tipo.UnidadBase == UnidadPermiso.Horas ? calendario.HorasJornada(d) : 1m;
        }
        if (laborales == 0) throw ServiceException.Validation("El periodo seleccionado no contiene días laborales.");
        return new CalculoResultado(Math.Round(total, 2), d0.ToDateTime(TimeOnly.MinValue), d1.ToDateTime(new TimeOnly(23, 59, 59)));
    }

    public async Task<CalculoDuracionDto> PrevisualizarAsync(CalculoDuracionRequest r, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var trabajadorId = await TrabajadorHelper.IdDeUsuarioAsync(_db, empresaId, _current.RequireUsuarioId(), ct)
            ?? throw ServiceException.NotFound("Su cuenta no está vinculada a un expediente de trabajador.");
        var tipo = await _db.TiposPermiso.AsNoTracking().FirstOrDefaultAsync(x => x.TipoPermisoId == r.TipoPermisoId && x.EmpresaId == empresaId, ct)
            ?? throw ServiceException.Validation("El tipo de permiso indicado no existe.");
        var calculo = await CalcularAsync(empresaId, trabajadorId, tipo, r.FechaInicio, r.FechaFin, r.EsPorHoras, ct);

        var dto = new CalculoDuracionDto { Cantidad = calculo.Cantidad, Unidad = tipo.UnidadBase, FechaInicio = calculo.Inicio, FechaFin = calculo.Fin };
        var fecha = DateOnly.FromDateTime(calculo.Inicio);
        var regla = await PermisosHelper.ReglaVigenteAsync(_db, empresaId, tipo.TipoPermisoId, fecha, ct);
        var saldo = await _db.SaldosPermiso.AsNoTracking().FirstOrDefaultAsync(s => s.EmpresaId == empresaId && s.TrabajadorId == trabajadorId
            && s.TipoPermisoId == tipo.TipoPermisoId && s.PeriodoAnio == (short)fecha.Year, ct);
        if (saldo is not null || regla?.CantidadAnual is not null)
        {
            dto.ControlaSaldo = true;
            var disponible = saldo is null ? regla!.CantidadAnual!.Value : saldo.CantidadAsignada - saldo.CantidadUtilizada - saldo.CantidadReservada;
            dto.SaldoDisponible = disponible;
            dto.SaldoRestante = disponible - calculo.Cantidad;
        }
        return dto;
    }

    private static DateTime Recortar(DateTime d) => new(d.Year, d.Month, d.Day, d.Hour, d.Minute, 0);

    private async Task<CalendarioLaboral> CargarCalendarioAsync(Guid empresaId, Guid trabajadorId, DateOnly d0, DateOnly d1, CancellationToken ct)
    {
        var festivos = await _db.DiasFestivos.AsNoTracking()
            .Where(f => f.EmpresaId == empresaId && (f.EsRecurrenteAnual || (f.Fecha >= d0 && f.Fecha <= d1)))
            .Select(f => new { f.Fecha, f.EsRecurrenteAnual }).ToListAsync(ct);
        var descansos = await _db.DescansosTrabajador.AsNoTracking()
            .Where(x => x.EmpresaId == empresaId && x.TrabajadorId == trabajadorId && x.FechaDesde <= d1 && x.FechaHasta >= d0)
            .Select(x => new { x.FechaDesde, x.FechaHasta }).ToListAsync(ct);
        var asignaciones = await _db.AsignacionesHorario.AsNoTracking()
            .Where(a => a.EmpresaId == empresaId && a.TrabajadorId == trabajadorId && a.FechaDesde <= d1 && (a.FechaHasta == null || a.FechaHasta >= d0))
            .OrderByDescending(a => a.MotivoExcepcion != null) // la excepción individual prevalece sobre el horario habitual
            .ThenByDescending(a => a.FechaDesde)
            .Select(a => new AsignacionInfo(a.HorarioLaboralId, a.FechaDesde, a.FechaHasta)).ToListAsync(ct);
        var horarioIds = asignaciones.Select(a => a.HorarioId).Distinct().ToList();
        var detalles = horarioIds.Count == 0
            ? new List<HorarioDetalleDia>()
            : await _db.HorarioDetalleDia.AsNoTracking().Where(h => h.EmpresaId == empresaId && horarioIds.Contains(h.HorarioLaboralId)).ToListAsync(ct);

        var fest = new HashSet<DateOnly>();
        var recurrentes = new HashSet<(int, int)>();
        foreach (var f in festivos)
        {
            if (f.EsRecurrenteAnual) recurrentes.Add((f.Fecha.Month, f.Fecha.Day));
            else fest.Add(f.Fecha);
        }
        return new CalendarioLaboral(fest, recurrentes,
            descansos.Select(x => (x.FechaDesde, x.FechaHasta)).ToList(), asignaciones, detalles);
    }

    private record AsignacionInfo(Guid HorarioId, DateOnly Desde, DateOnly? Hasta);

    /// <summary>Reglas de calendario de un trabajador en un rango. Sin horario asignado se asume lunes a viernes, 8 horas.</summary>
    private sealed class CalendarioLaboral
    {
        private readonly HashSet<DateOnly> _festivos;
        private readonly HashSet<(int, int)> _recurrentes;
        private readonly List<(DateOnly Desde, DateOnly Hasta)> _descansos;
        private readonly List<AsignacionInfo> _asignaciones;
        private readonly List<HorarioDetalleDia> _detalles;

        public CalendarioLaboral(HashSet<DateOnly> festivos, HashSet<(int, int)> recurrentes, List<(DateOnly, DateOnly)> descansos,
            List<AsignacionInfo> asignaciones, List<HorarioDetalleDia> detalles)
        {
            _festivos = festivos; _recurrentes = recurrentes; _descansos = descansos; _asignaciones = asignaciones; _detalles = detalles;
        }

        // DiaSemana en la BD: 1 = lunes ... 7 = domingo.
        private static byte DiaSemana(DateOnly d) => d.DayOfWeek == DayOfWeek.Sunday ? (byte)7 : (byte)d.DayOfWeek;

        private HorarioDetalleDia? Detalle(DateOnly d, out bool hayHorario)
        {
            var a = _asignaciones.FirstOrDefault(x => x.Desde <= d && (x.Hasta == null || x.Hasta >= d));
            hayHorario = a is not null;
            return a is null ? null : _detalles.FirstOrDefault(x => x.HorarioLaboralId == a.HorarioId && x.DiaSemana == DiaSemana(d));
        }

        public bool EsLaboral(DateOnly d)
        {
            if (_festivos.Contains(d) || _recurrentes.Contains((d.Month, d.Day))) return false;
            if (_descansos.Any(x => x.Desde <= d && x.Hasta >= d)) return false;
            var det = Detalle(d, out var hayHorario);
            if (!hayHorario) return d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
            return det is { EsDiaLaboral: true };
        }

        public decimal HorasJornada(DateOnly d)
        {
            var det = Detalle(d, out _);
            if (det?.HoraEntrada is not { } entrada || det.HoraSalida is not { } salida) return PermisosHelper.HorasJornadaDefault;
            var horas = (decimal)(salida.ToTimeSpan() - entrada.ToTimeSpan()).TotalHours;
            if (horas <= 0) horas += 24m; // jornada que cruza la medianoche
            if (det.InicioDescanso is { } di && det.FinDescanso is { } df && df > di) horas -= (decimal)(df.ToTimeSpan() - di.ToTimeSpan()).TotalHours;
            return horas > 0 ? Math.Round(horas, 2) : PermisosHelper.HorasJornadaDefault;
        }
    }
}

/// <summary>Piezas compartidas por los servicios de permisos: reglas vigentes, alcance de revisión, saldos y notificaciones.</summary>
internal static class PermisosHelper
{
    public const decimal HorasJornadaDefault = 8m;

    public static Task<ReglaPermiso?> ReglaVigenteAsync(RegistroTrabajoDbContext db, Guid empresaId, Guid tipoId, DateOnly fecha, CancellationToken ct) =>
        db.ReglasPermiso.AsNoTracking()
            .Where(r => r.EmpresaId == empresaId && r.TipoPermisoId == tipoId && r.Estado
                && (r.ActivaDesde == null || r.ActivaDesde <= fecha) && (r.ActivaHasta == null || r.ActivaHasta >= fecha))
            .OrderByDescending(r => r.ActivaDesde)
            .FirstOrDefaultAsync(ct);

    /// <summary>Quién puede ver o revisar solicitudes de otros trabajadores.</summary>
    public sealed record Alcance(bool Todos, Guid? MiTrabajadorId, IReadOnlyList<Guid> Supervisados)
    {
        public bool Incluye(Guid trabajadorId) => Todos || Supervisados.Contains(trabajadorId);
        public bool Revisa(Guid trabajadorId) => MiTrabajadorId != trabajadorId && Incluye(trabajadorId);
    }

    /// <param name="soloRevision">Verdadero para aprobar (permisos.aprobar / aprobar_todos); falso para consultar (incluye consultar_todos).</param>
    public static async Task<Alcance> AlcanceAsync(RegistroTrabajoDbContext db, ICurrentUser current, IPermisoService permisos,
        TimeProvider clock, bool soloRevision, CancellationToken ct)
    {
        var empresaId = current.RequireEmpresaId();
        var mi = await TrabajadorHelper.IdDeUsuarioAsync(db, empresaId, current.RequireUsuarioId(), ct);
        var todos = await permisos.TienePermisoAsync(Permisos.PermisosAprobarTodos, ct)
            || (!soloRevision && await permisos.TienePermisoAsync(Permisos.PermisosConsultarTodos, ct));
        var supervisados = new List<Guid>();
        if (!todos && mi is { } m && await permisos.TienePermisoAsync(Permisos.PermisosAprobar, ct))
        {
            var hoy = TrabajadorHelper.Hoy(clock);
            supervisados = await db.TrabajadorSupervisores.AsNoTracking()
                .Where(s => s.EmpresaId == empresaId && s.SupervisorTrabajadorId == m && s.FechaInicio <= hoy && (s.FechaFin == null || s.FechaFin >= hoy))
                .Select(s => s.TrabajadorId).Distinct().ToListAsync(ct);
        }
        return new Alcance(todos, mi, supervisados);
    }

    /// <summary>Devuelve el saldo del año o lo crea con la asignación anual de la regla (y el remanente del año anterior si la regla acumula).</summary>
    public static async Task<SaldoPermiso> ObtenerOCrearSaldoAsync(RegistroTrabajoDbContext db, Guid empresaId, Guid trabajadorId, Guid tipoId,
        short anio, ReglaPermiso? regla, Guid? usuarioId, DateTime now, CancellationToken ct)
    {
        var saldo = await db.SaldosPermiso.FirstOrDefaultAsync(s => s.EmpresaId == empresaId && s.TrabajadorId == trabajadorId
            && s.TipoPermisoId == tipoId && s.PeriodoAnio == anio, ct);
        if (saldo is not null) return saldo;

        saldo = new SaldoPermiso
        {
            SaldoPermisoId = Guid.NewGuid(), EmpresaId = empresaId, TrabajadorId = trabajadorId, TipoPermisoId = tipoId,
            PeriodoAnio = anio, FechaActualizacionUtc = now
        };
        db.SaldosPermiso.Add(saldo);
        var anual = regla?.CantidadAnual ?? 0m;
        if (anual > 0)
        {
            saldo.CantidadAsignada = anual;
            db.MovimientosSaldoPermiso.Add(Movimiento(saldo, null, TipoMovimientoSaldo.Asignacion, anual, $"Asignación anual {anio}", usuarioId, now));
        }
        if (regla is { ReiniciaAnualmente: false })
        {
            var previo = await db.SaldosPermiso.AsNoTracking().FirstOrDefaultAsync(s => s.EmpresaId == empresaId && s.TrabajadorId == trabajadorId
                && s.TipoPermisoId == tipoId && s.PeriodoAnio == anio - 1, ct);
            var remanente = previo is null ? 0m : previo.CantidadAsignada - previo.CantidadUtilizada - previo.CantidadReservada;
            if (remanente > 0)
            {
                saldo.CantidadAsignada += remanente;
                db.MovimientosSaldoPermiso.Add(Movimiento(saldo, null, TipoMovimientoSaldo.Asignacion, remanente, $"Saldo acumulado de {anio - 1}", usuarioId, now));
            }
        }
        return saldo;
    }

    public static decimal Disponible(SaldoPermiso s) => s.CantidadAsignada - s.CantidadUtilizada - s.CantidadReservada;

    public static void Reservar(SaldoPermiso s, decimal cantidad, DateTime now)
    {
        s.CantidadReservada += cantidad;
        s.FechaActualizacionUtc = now;
    }

    public static void LiberarReserva(SaldoPermiso s, decimal cantidad, DateTime now)
    {
        s.CantidadReservada = Math.Max(0m, s.CantidadReservada - cantidad);
        s.FechaActualizacionUtc = now;
    }

    /// <summary>Pasa lo reservado a utilizado y deja el movimiento de consumo (único por solicitud gracias al índice del script 005).</summary>
    public static void Consumir(RegistroTrabajoDbContext db, SaldoPermiso s, Guid solicitudId, decimal cantidad, bool desdeReserva, Guid? usuarioId, DateTime now, string descripcion)
    {
        if (desdeReserva) s.CantidadReservada = Math.Max(0m, s.CantidadReservada - cantidad);
        s.CantidadUtilizada += cantidad;
        s.FechaActualizacionUtc = now;
        db.MovimientosSaldoPermiso.Add(Movimiento(s, solicitudId, TipoMovimientoSaldo.Consumo, cantidad, descripcion, usuarioId, now));
    }

    public static void Devolver(RegistroTrabajoDbContext db, SaldoPermiso s, Guid solicitudId, decimal cantidad, Guid? usuarioId, DateTime now, string descripcion)
    {
        s.CantidadUtilizada = Math.Max(0m, s.CantidadUtilizada - cantidad);
        s.FechaActualizacionUtc = now;
        db.MovimientosSaldoPermiso.Add(Movimiento(s, solicitudId, TipoMovimientoSaldo.Devolucion, cantidad, descripcion, usuarioId, now));
    }

    public static MovimientoSaldoPermiso Movimiento(SaldoPermiso s, Guid? solicitudId, byte tipo, decimal cantidad, string? descripcion, Guid? usuarioId, DateTime now) => new()
    {
        EmpresaId = s.EmpresaId, SaldoPermisoId = s.SaldoPermisoId, SolicitudPermisoId = solicitudId, TipoMovimiento = tipo,
        Cantidad = cantidad, Descripcion = descripcion, RegistradoPorUsuarioId = usuarioId, FechaUtc = now
    };

    public static async Task<SaldoPermiso?> SaldoDeSolicitudAsync(RegistroTrabajoDbContext db, SolicitudPermiso s, CancellationToken ct)
    {
        var anio = (short)s.FechaInicio.Year;
        return await db.SaldosPermiso.FirstOrDefaultAsync(x => x.EmpresaId == s.EmpresaId && x.TrabajadorId == s.TrabajadorId
            && x.TipoPermisoId == s.TipoPermisoId && x.PeriodoAnio == anio, ct);
    }

    /// <summary>
    /// Agrega notificaciones sin datos de salud ni motivos (el texto es genérico a propósito) y respeta las preferencias del usuario.
    /// Se persisten con el SaveChanges de la operación.
    /// </summary>
    public static Task NotificarAsync(RegistroTrabajoDbContext db, Guid empresaId, IEnumerable<Guid> destinatarios, string tipo,
        string titulo, string mensaje, Guid solicitudId, DateTime now, CancellationToken ct, string? clave = null) =>
        NotificacionesHelper.NotificarAsync(db, empresaId, destinatarios, tipo, titulo, mensaje, nameof(SolicitudPermiso), solicitudId.ToString(), now, ct, clave);

    /// <summary>Cuentas de los supervisores vigentes del trabajador.</summary>
    public static async Task<List<Guid>> UsuariosSupervisoresAsync(RegistroTrabajoDbContext db, Guid empresaId, Guid trabajadorId, TimeProvider clock, CancellationToken ct)
    {
        var hoy = TrabajadorHelper.Hoy(clock);
        return await db.TrabajadorSupervisores.AsNoTracking()
            .Where(s => s.EmpresaId == empresaId && s.TrabajadorId == trabajadorId && s.FechaInicio <= hoy && (s.FechaFin == null || s.FechaFin >= hoy)
                && s.Supervisor.UsuarioId != null)
            .Select(s => s.Supervisor.UsuarioId!.Value).Distinct().ToListAsync(ct);
    }
}
