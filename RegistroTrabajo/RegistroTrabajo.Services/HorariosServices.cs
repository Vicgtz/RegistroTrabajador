using Microsoft.EntityFrameworkCore;
using RegistroTrabajo.Data;
using RegistroTrabajo.Data.Entities;
using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public interface ITurnosService
{
    Task<IReadOnlyList<TurnoDto>> ListarAsync(bool soloActivos, CancellationToken ct);
    Task<TurnoDto> CrearAsync(GuardarTurnoRequest r, CancellationToken ct);
    Task<TurnoDto> ActualizarAsync(Guid turnoId, GuardarTurnoRequest r, CancellationToken ct);
    Task<TurnoDto> CambiarEstadoAsync(Guid turnoId, bool estado, CancellationToken ct);
    /// <summary>Crea los turnos matutino, vespertino y nocturno si la empresa aún no los tiene.</summary>
    Task<IReadOnlyList<TurnoDto>> SembrarInicialesAsync(CancellationToken ct);
}

public interface IHorariosService
{
    Task<IReadOnlyList<HorarioLaboralDto>> ListarAsync(bool soloActivos, CancellationToken ct);
    Task<HorarioLaboralDto> ObtenerAsync(Guid horarioId, CancellationToken ct);
    Task<HorarioLaboralDto> CrearAsync(GuardarHorarioRequest r, CancellationToken ct);
    /// <summary>Modifica el horario solo si nadie lo usa todavía; si ya rige para alguien hay que crear una nueva versión.</summary>
    Task<HorarioLaboralDto> ActualizarAsync(Guid horarioId, GuardarHorarioRequest r, CancellationToken ct);
    /// <summary>Cierra la vigencia del horario el día anterior y crea uno nuevo desde la fecha indicada, trasladando las asignaciones vigentes.</summary>
    Task<HorarioLaboralDto> NuevaVersionAsync(Guid horarioId, GuardarHorarioRequest r, CancellationToken ct);
    Task<HorarioLaboralDto> CambiarEstadoAsync(Guid horarioId, bool estado, CancellationToken ct);

    Task<IReadOnlyList<AsignacionHorarioDto>> ListarAsignacionesAsync(Guid? trabajadorId, CancellationToken ct);
    Task<AsignacionHorarioDto> AsignarAsync(AsignarHorarioRequest r, CancellationToken ct);
    Task CerrarAsignacionAsync(Guid asignacionId, DateOnly fechaHasta, CancellationToken ct);

    /// <summary>Horario, festivo y descanso de un trabajador en una fecha. Prioridad: excepción individual, asignación habitual, horario del puesto, horario del departamento.</summary>
    Task<HorarioAplicableDto> AplicableAsync(Guid trabajadorId, DateOnly fecha, CancellationToken ct);
    /// <summary>Misma consulta para el trabajador autenticado o con empresa ya resuelta (uso interno).</summary>
    Task<HorarioAplicableDto> AplicableAsync(Guid empresaId, Guid trabajadorId, DateOnly fecha, CancellationToken ct);
}

public interface ICalendarioLaboralService
{
    Task<IReadOnlyList<DescansoTrabajadorDto>> ListarDescansosAsync(Guid? trabajadorId, DateOnly? desde, DateOnly? hasta, CancellationToken ct);
    Task<DescansoTrabajadorDto> CrearDescansoAsync(GuardarDescansoRequest r, CancellationToken ct);
    Task<DescansoTrabajadorDto> ActualizarDescansoAsync(Guid id, GuardarDescansoRequest r, CancellationToken ct);
    /// <summary>Nada se borra del historial, pero un descanso aún no iniciado puede retirarse; uno iniciado se acorta.</summary>
    Task<DescansoTrabajadorDto> TerminarDescansoAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<DiaFestivoDto>> ListarFestivosAsync(int? anio, CancellationToken ct);
    Task<DiaFestivoDto> CrearFestivoAsync(GuardarDiaFestivoRequest r, CancellationToken ct);
    Task<DiaFestivoDto> ActualizarFestivoAsync(Guid id, GuardarDiaFestivoRequest r, CancellationToken ct);
    Task EliminarFestivoAsync(Guid id, CancellationToken ct);
}

internal static class HorariosHelper
{
    public static string NombreCompleto(Trabajador t) => $"{t.Nombres} {t.Apellidos}".Trim();

    public static bool Cruza(TimeOnly entrada, TimeOnly salida) => salida <= entrada;

    public static HorarioDetalleDiaDto ToDto(HorarioDetalleDia d) => new()
    {
        DiaSemana = d.DiaSemana, EsDiaLaboral = d.EsDiaLaboral, HoraEntrada = d.HoraEntrada, HoraSalida = d.HoraSalida,
        InicioDescanso = d.InicioDescanso, FinDescanso = d.FinDescanso,
        CruzaMedianoche = d.HoraEntrada is { } e && d.HoraSalida is { } s && Cruza(e, s)
    };

    public static byte DiaSemana(DateOnly d) => d.DayOfWeek == DayOfWeek.Sunday ? (byte)7 : (byte)d.DayOfWeek;

    public static string Etiqueta(HorarioDetalleDiaDto? d) => d is { EsDiaLaboral: true, HoraEntrada: { } e, HoraSalida: { } s }
        ? $"{e:HH\\:mm} - {s:HH\\:mm}{(d.CruzaMedianoche ? " (+1)" : "")}"
        : "Descanso";
}

public class TurnosService : ITurnosService
{
    private readonly RegistroTrabajoDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IAuditoriaService _auditoria;

    public TurnosService(RegistroTrabajoDbContext db, ICurrentUser current, IAuditoriaService auditoria)
    {
        _db = db; _current = current; _auditoria = auditoria;
    }

    private static TurnoDto ToDto(Turno t) => new()
    {
        TurnoId = t.TurnoId, Nombre = t.Nombre, HoraInicio = t.HoraInicio, HoraFin = t.HoraFin,
        CruzaMedianoche = t.CruzaMedianoche, ToleranciaMinutos = t.ToleranciaMinutos, Estado = t.Estado
    };

    public async Task<IReadOnlyList<TurnoDto>> ListarAsync(bool soloActivos, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var q = _db.Turnos.AsNoTracking().Where(t => t.EmpresaId == empresaId);
        if (soloActivos) q = q.Where(t => t.Estado);
        return (await q.OrderBy(t => t.HoraInicio).ThenBy(t => t.Nombre).ToListAsync(ct)).Select(ToDto).ToList();
    }

    private static void Validar(GuardarTurnoRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Nombre)) throw ServiceException.Validation("El nombre del turno es obligatorio.");
        if (r.HoraInicio == r.HoraFin) throw ServiceException.Validation("La hora de inicio y la de fin del turno no pueden ser iguales.");
    }

    public async Task<TurnoDto> CrearAsync(GuardarTurnoRequest r, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        Validar(r);
        var nombre = r.Nombre.Trim();
        if (await _db.Turnos.AnyAsync(t => t.EmpresaId == empresaId && t.Nombre.ToLower() == nombre.ToLower(), ct))
            throw ServiceException.Conflict("Ya existe un turno con ese nombre.");
        var t = new Turno
        {
            TurnoId = Guid.NewGuid(), EmpresaId = empresaId, Nombre = nombre, HoraInicio = r.HoraInicio, HoraFin = r.HoraFin,
            CruzaMedianoche = HorariosHelper.Cruza(r.HoraInicio, r.HoraFin), ToleranciaMinutos = r.ToleranciaMinutos, Estado = true
        };
        _db.Turnos.Add(t);
        _auditoria.Registrar("Horarios", nameof(Turno), t.TurnoId.ToString(), "Crear", null, ToDto(t));
        await _db.SaveSafeAsync(ct);
        return ToDto(t);
    }

    public async Task<TurnoDto> ActualizarAsync(Guid turnoId, GuardarTurnoRequest r, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        Validar(r);
        var t = await _db.Turnos.FirstOrDefaultAsync(x => x.TurnoId == turnoId && x.EmpresaId == empresaId, ct)
            ?? throw ServiceException.NotFound("Turno no encontrado.");
        var nombre = r.Nombre.Trim();
        if (await _db.Turnos.AnyAsync(x => x.EmpresaId == empresaId && x.Nombre.ToLower() == nombre.ToLower() && x.TurnoId != turnoId, ct))
            throw ServiceException.Conflict("Ya existe un turno con ese nombre.");
        var antes = ToDto(t);
        t.Nombre = nombre; t.HoraInicio = r.HoraInicio; t.HoraFin = r.HoraFin;
        t.CruzaMedianoche = HorariosHelper.Cruza(r.HoraInicio, r.HoraFin); t.ToleranciaMinutos = r.ToleranciaMinutos;
        _auditoria.Registrar("Horarios", nameof(Turno), t.TurnoId.ToString(), "Actualizar", antes, ToDto(t));
        await _db.SaveSafeAsync(ct);
        return ToDto(t);
    }

    public async Task<TurnoDto> CambiarEstadoAsync(Guid turnoId, bool estado, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var t = await _db.Turnos.FirstOrDefaultAsync(x => x.TurnoId == turnoId && x.EmpresaId == empresaId, ct)
            ?? throw ServiceException.NotFound("Turno no encontrado.");
        var antes = ToDto(t);
        t.Estado = estado;
        _auditoria.Registrar("Horarios", nameof(Turno), t.TurnoId.ToString(), estado ? "Activar" : "Desactivar", antes, ToDto(t));
        await _db.SaveSafeAsync(ct);
        return ToDto(t);
    }

    public async Task<IReadOnlyList<TurnoDto>> SembrarInicialesAsync(CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var existentes = await _db.Turnos.Where(t => t.EmpresaId == empresaId).Select(t => t.Nombre).ToListAsync(ct);
        var nuevos = new (string Nombre, TimeOnly Ini, TimeOnly Fin)[]
        {
            ("Matutino", new TimeOnly(6, 0), new TimeOnly(14, 0)),
            ("Vespertino", new TimeOnly(14, 0), new TimeOnly(22, 0)),
            ("Nocturno", new TimeOnly(22, 0), new TimeOnly(6, 0))
        };
        foreach (var n in nuevos.Where(n => !existentes.Contains(n.Nombre)))
        {
            var t = new Turno
            {
                TurnoId = Guid.NewGuid(), EmpresaId = empresaId, Nombre = n.Nombre, HoraInicio = n.Ini, HoraFin = n.Fin,
                CruzaMedianoche = HorariosHelper.Cruza(n.Ini, n.Fin), ToleranciaMinutos = 10, Estado = true
            };
            _db.Turnos.Add(t);
            _auditoria.Registrar("Horarios", nameof(Turno), t.TurnoId.ToString(), "Crear", null, ToDto(t));
        }
        await _db.SaveSafeAsync(ct);
        return await ListarAsync(false, ct);
    }
}

public class HorariosService : IHorariosService
{
    private readonly RegistroTrabajoDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IAuditoriaService _auditoria;
    private readonly TimeProvider _clock;

    public HorariosService(RegistroTrabajoDbContext db, ICurrentUser current, IAuditoriaService auditoria, TimeProvider clock)
    {
        _db = db; _current = current; _auditoria = auditoria; _clock = clock;
    }

    private static HorarioLaboralDto ToDto(HorarioLaboral h) => new()
    {
        HorarioLaboralId = h.HorarioLaboralId, Nombre = h.Nombre, DepartamentoId = h.DepartamentoId, Departamento = h.Departamento?.Nombre,
        PuestoId = h.PuestoId, Puesto = h.Puesto?.Nombre, TurnoId = h.TurnoId, Turno = h.Turno?.Nombre,
        FechaVigenciaDesde = h.FechaVigenciaDesde, FechaVigenciaHasta = h.FechaVigenciaHasta, Estado = h.Estado,
        Detalle = h.HorarioDetalleDia.OrderBy(d => d.DiaSemana).Select(HorariosHelper.ToDto).ToList()
    };

    private IQueryable<HorarioLaboral> Consulta(Guid empresaId) => _db.HorariosLaborales
        .Include(h => h.Departamento).Include(h => h.Puesto).Include(h => h.Turno).Include(h => h.HorarioDetalleDia)
        .Where(h => h.EmpresaId == empresaId);

    public async Task<IReadOnlyList<HorarioLaboralDto>> ListarAsync(bool soloActivos, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var q = Consulta(empresaId).AsNoTracking();
        if (soloActivos) q = q.Where(h => h.Estado);
        return (await q.OrderBy(h => h.Nombre).ThenByDescending(h => h.FechaVigenciaDesde).ToListAsync(ct)).Select(ToDto).ToList();
    }

    public async Task<HorarioLaboralDto> ObtenerAsync(Guid horarioId, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var h = await Consulta(empresaId).AsNoTracking().FirstOrDefaultAsync(x => x.HorarioLaboralId == horarioId, ct)
            ?? throw ServiceException.NotFound("Horario no encontrado.");
        return ToDto(h);
    }

    private async Task ValidarAsync(Guid empresaId, GuardarHorarioRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Nombre)) throw ServiceException.Validation("El nombre del horario es obligatorio.");
        if (r.FechaVigenciaHasta is { } h && h < r.FechaVigenciaDesde)
            throw ServiceException.Validation("La vigencia final no puede ser anterior a la inicial.");
        if (r.Detalle.Count == 0) throw ServiceException.Validation("Indique la jornada de al menos un día.");
        if (r.Detalle.Select(d => d.DiaSemana).Distinct().Count() != r.Detalle.Count)
            throw ServiceException.Validation("Hay días de la semana repetidos.");
        foreach (var d in r.Detalle)
        {
            if (d.DiaSemana is < 1 or > 7) throw ServiceException.Validation("El día de la semana debe estar entre 1 (lunes) y 7 (domingo).");
            if (!d.EsDiaLaboral) continue;
            if (d.HoraEntrada is not { } e || d.HoraSalida is not { } s)
                throw ServiceException.Validation("Un día laborable requiere hora de entrada y de salida.");
            if (e == s) throw ServiceException.Validation("La hora de entrada y la de salida no pueden ser iguales.");
            if ((d.InicioDescanso is null) != (d.FinDescanso is null))
                throw ServiceException.Validation("El descanso requiere hora de inicio y de fin.");
        }
        if (r.DepartamentoId is { } dep && !await _db.Departamentos.AnyAsync(x => x.EmpresaId == empresaId && x.DepartamentoId == dep, ct))
            throw ServiceException.Validation("El departamento indicado no existe.");
        if (r.PuestoId is { } pu && !await _db.Puestos.AnyAsync(x => x.EmpresaId == empresaId && x.PuestoId == pu, ct))
            throw ServiceException.Validation("El puesto indicado no existe.");
        if (r.TurnoId is { } tu && !await _db.Turnos.AnyAsync(x => x.EmpresaId == empresaId && x.TurnoId == tu, ct))
            throw ServiceException.Validation("El turno indicado no existe.");
    }

    private static void Aplicar(HorarioLaboral h, GuardarHorarioRequest r)
    {
        h.Nombre = r.Nombre.Trim(); h.DepartamentoId = r.DepartamentoId; h.PuestoId = r.PuestoId; h.TurnoId = r.TurnoId;
        h.FechaVigenciaDesde = r.FechaVigenciaDesde; h.FechaVigenciaHasta = r.FechaVigenciaHasta;
    }

    private static HorarioDetalleDia NuevoDetalle(Guid empresaId, Guid horarioId, HorarioDetalleDiaDto d) => new()
    {
        EmpresaId = empresaId, HorarioLaboralId = horarioId, DiaSemana = d.DiaSemana, EsDiaLaboral = d.EsDiaLaboral,
        HoraEntrada = d.EsDiaLaboral ? d.HoraEntrada : null, HoraSalida = d.EsDiaLaboral ? d.HoraSalida : null,
        InicioDescanso = d.EsDiaLaboral ? d.InicioDescanso : null, FinDescanso = d.EsDiaLaboral ? d.FinDescanso : null
    };

    private async Task<HorarioLaboralDto> CrearInternoAsync(Guid empresaId, GuardarHorarioRequest r, CancellationToken ct)
    {
        var h = new HorarioLaboral { HorarioLaboralId = Guid.NewGuid(), EmpresaId = empresaId, Estado = true };
        Aplicar(h, r);
        _db.HorariosLaborales.Add(h);
        foreach (var d in r.Detalle) _db.HorarioDetalleDia.Add(NuevoDetalle(empresaId, h.HorarioLaboralId, d));
        _auditoria.Registrar("Horarios", nameof(HorarioLaboral), h.HorarioLaboralId.ToString(), "Crear", null, new { h.Nombre, h.FechaVigenciaDesde, h.FechaVigenciaHasta, Dias = r.Detalle.Count });
        await _db.SaveSafeAsync(ct);
        return await ObtenerAsync(h.HorarioLaboralId, ct);
    }

    public async Task<HorarioLaboralDto> CrearAsync(GuardarHorarioRequest r, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        await ValidarAsync(empresaId, r, ct);
        return await CrearInternoAsync(empresaId, r, ct);
    }

    public async Task<HorarioLaboralDto> ActualizarAsync(Guid horarioId, GuardarHorarioRequest r, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        await ValidarAsync(empresaId, r, ct);
        var h = await _db.HorariosLaborales.Include(x => x.HorarioDetalleDia)
            .FirstOrDefaultAsync(x => x.HorarioLaboralId == horarioId && x.EmpresaId == empresaId, ct)
            ?? throw ServiceException.NotFound("Horario no encontrado.");
        var hoy = TrabajadorHelper.Hoy(_clock);
        if (await _db.AsignacionesHorario.AnyAsync(a => a.EmpresaId == empresaId && a.HorarioLaboralId == horarioId && a.FechaDesde <= hoy, ct))
            throw ServiceException.Conflict("El horario ya rige para trabajadores; cree una nueva versión para no alterar el historial.");

        var antes = ToDto(h);
        Aplicar(h, r);
        // Se actualiza en sitio (día por día) para no chocar con el índice único (horario, día).
        foreach (var existente in h.HorarioDetalleDia.ToList())
        {
            var d = r.Detalle.FirstOrDefault(x => x.DiaSemana == existente.DiaSemana);
            if (d is null) { _db.HorarioDetalleDia.Remove(existente); continue; }
            var nuevo = NuevoDetalle(empresaId, h.HorarioLaboralId, d);
            existente.EsDiaLaboral = nuevo.EsDiaLaboral; existente.HoraEntrada = nuevo.HoraEntrada; existente.HoraSalida = nuevo.HoraSalida;
            existente.InicioDescanso = nuevo.InicioDescanso; existente.FinDescanso = nuevo.FinDescanso;
        }
        foreach (var d in r.Detalle.Where(x => h.HorarioDetalleDia.All(e => e.DiaSemana != x.DiaSemana)))
            _db.HorarioDetalleDia.Add(NuevoDetalle(empresaId, h.HorarioLaboralId, d));
        _auditoria.Registrar("Horarios", nameof(HorarioLaboral), h.HorarioLaboralId.ToString(), "Actualizar", antes, new { h.Nombre, h.FechaVigenciaDesde, h.FechaVigenciaHasta, Dias = r.Detalle.Count });
        await _db.SaveSafeAsync(ct);
        return await ObtenerAsync(horarioId, ct);
    }

    public async Task<HorarioLaboralDto> NuevaVersionAsync(Guid horarioId, GuardarHorarioRequest r, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        await ValidarAsync(empresaId, r, ct);
        var anterior = await _db.HorariosLaborales.FirstOrDefaultAsync(x => x.HorarioLaboralId == horarioId && x.EmpresaId == empresaId, ct)
            ?? throw ServiceException.NotFound("Horario no encontrado.");
        var desde = r.FechaVigenciaDesde;
        if (desde <= anterior.FechaVigenciaDesde)
            throw ServiceException.Validation("La nueva versión debe comenzar después del inicio de la vigencia actual.");
        if (anterior.FechaVigenciaHasta is { } fin && fin < desde)
            throw ServiceException.Validation("El horario actual ya terminó antes de la fecha indicada.");

        var nuevo = new HorarioLaboral { HorarioLaboralId = Guid.NewGuid(), EmpresaId = empresaId, Estado = true };
        Aplicar(nuevo, r);
        _db.HorariosLaborales.Add(nuevo);
        foreach (var d in r.Detalle) _db.HorarioDetalleDia.Add(NuevoDetalle(empresaId, nuevo.HorarioLaboralId, d));

        var vigenciaAnterior = anterior.FechaVigenciaHasta;
        anterior.FechaVigenciaHasta = desde.AddDays(-1);

        // Las asignaciones habituales que siguen vigentes pasan a la nueva versión; el historial anterior queda intacto.
        var asignaciones = await _db.AsignacionesHorario
            .Where(a => a.EmpresaId == empresaId && a.HorarioLaboralId == horarioId && a.FechaDesde < desde && (a.FechaHasta == null || a.FechaHasta >= desde))
            .ToListAsync(ct);
        var usuarioId = _current.RequireUsuarioId();
        foreach (var a in asignaciones)
        {
            var hastaOriginal = a.FechaHasta;
            a.FechaHasta = desde.AddDays(-1);
            _db.AsignacionesHorario.Add(new AsignacionHorario
            {
                AsignacionHorarioId = Guid.NewGuid(), EmpresaId = empresaId, TrabajadorId = a.TrabajadorId, HorarioLaboralId = nuevo.HorarioLaboralId,
                FechaDesde = desde, FechaHasta = hastaOriginal, MotivoExcepcion = a.MotivoExcepcion, AsignadoPorUsuarioId = usuarioId
            });
        }
        _auditoria.Registrar("Horarios", nameof(HorarioLaboral), nuevo.HorarioLaboralId.ToString(), "NuevaVersion",
            new { Anterior = anterior.HorarioLaboralId, VigenciaHastaAnterior = vigenciaAnterior },
            new { nuevo.Nombre, nuevo.FechaVigenciaDesde, nuevo.FechaVigenciaHasta, Asignaciones = asignaciones.Count });
        await _db.SaveSafeAsync(ct);
        return await ObtenerAsync(nuevo.HorarioLaboralId, ct);
    }

    public async Task<HorarioLaboralDto> CambiarEstadoAsync(Guid horarioId, bool estado, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var h = await _db.HorariosLaborales.FirstOrDefaultAsync(x => x.HorarioLaboralId == horarioId && x.EmpresaId == empresaId, ct)
            ?? throw ServiceException.NotFound("Horario no encontrado.");
        h.Estado = estado;
        _auditoria.Registrar("Horarios", nameof(HorarioLaboral), h.HorarioLaboralId.ToString(), estado ? "Activar" : "Desactivar", null, new { h.Nombre, Estado = estado });
        await _db.SaveSafeAsync(ct);
        return await ObtenerAsync(horarioId, ct);
    }

    // ---------------- Asignaciones ----------------
    private static AsignacionHorarioDto ToDto(AsignacionHorario a) => new()
    {
        AsignacionHorarioId = a.AsignacionHorarioId, TrabajadorId = a.TrabajadorId, Trabajador = HorariosHelper.NombreCompleto(a.Trabajador),
        HorarioLaboralId = a.HorarioLaboralId, Horario = a.HorarioLaboral.Nombre, FechaDesde = a.FechaDesde, FechaHasta = a.FechaHasta,
        MotivoExcepcion = a.MotivoExcepcion
    };

    public async Task<IReadOnlyList<AsignacionHorarioDto>> ListarAsignacionesAsync(Guid? trabajadorId, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var q = _db.AsignacionesHorario.AsNoTracking().Include(a => a.Trabajador).Include(a => a.HorarioLaboral).Where(a => a.EmpresaId == empresaId);
        if (trabajadorId is { } t) q = q.Where(a => a.TrabajadorId == t);
        return (await q.OrderByDescending(a => a.FechaDesde).Take(500).ToListAsync(ct)).Select(ToDto).ToList();
    }

    public async Task<AsignacionHorarioDto> AsignarAsync(AsignarHorarioRequest r, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        if (r.FechaHasta is { } h && h < r.FechaDesde) throw ServiceException.Validation("La fecha final no puede ser anterior a la inicial.");
        var trabajador = await TrabajadorHelper.CargarAsync(_db, empresaId, r.TrabajadorId, false, ct);
        var horario = await _db.HorariosLaborales.AsNoTracking().FirstOrDefaultAsync(x => x.HorarioLaboralId == r.HorarioLaboralId && x.EmpresaId == empresaId, ct)
            ?? throw ServiceException.Validation("El horario indicado no existe.");
        if (!horario.Estado) throw ServiceException.Validation("El horario está desactivado.");
        if (r.FechaDesde < horario.FechaVigenciaDesde || (horario.FechaVigenciaHasta is { } vh && (r.FechaHasta ?? DateOnly.MaxValue) > vh))
            throw ServiceException.Validation("El periodo de la asignación debe quedar dentro de la vigencia del horario.");

        var motivo = Helpers.Limpiar(r.MotivoExcepcion);
        var esExcepcion = motivo is not null;
        var hasta = r.FechaHasta ?? DateOnly.MaxValue;
        // No se traslapan asignaciones del mismo tipo: la excepción se apoya en la habitual sin reemplazarla.
        var traslape = await _db.AsignacionesHorario.AnyAsync(a => a.EmpresaId == empresaId && a.TrabajadorId == r.TrabajadorId
            && (a.MotivoExcepcion != null) == esExcepcion && a.FechaDesde <= hasta && (a.FechaHasta == null || a.FechaHasta >= r.FechaDesde), ct);
        if (traslape) throw ServiceException.Conflict(esExcepcion
            ? "Ya existe otra excepción en ese periodo."
            : "El trabajador ya tiene un horario habitual en ese periodo; ciérrelo o use una excepción.");
        if (esExcepcion && r.FechaHasta is null) throw ServiceException.Validation("Una excepción individual debe tener fecha final.");

        var a = new AsignacionHorario
        {
            AsignacionHorarioId = Guid.NewGuid(), EmpresaId = empresaId, TrabajadorId = r.TrabajadorId, HorarioLaboralId = r.HorarioLaboralId,
            FechaDesde = r.FechaDesde, FechaHasta = r.FechaHasta, MotivoExcepcion = motivo, AsignadoPorUsuarioId = _current.RequireUsuarioId()
        };
        _db.AsignacionesHorario.Add(a);
        _auditoria.Registrar("Horarios", nameof(AsignacionHorario), a.AsignacionHorarioId.ToString(), esExcepcion ? "AsignarExcepcion" : "Asignar", null,
            new { a.TrabajadorId, a.HorarioLaboralId, a.FechaDesde, a.FechaHasta, a.MotivoExcepcion });
        await _db.SaveSafeAsync(ct);
        // El DTO se arma a mano: asignar navegaciones con instancias sin seguimiento rompería el siguiente SaveChanges del contexto.
        return new AsignacionHorarioDto
        {
            AsignacionHorarioId = a.AsignacionHorarioId, TrabajadorId = a.TrabajadorId, Trabajador = HorariosHelper.NombreCompleto(trabajador),
            HorarioLaboralId = a.HorarioLaboralId, Horario = horario.Nombre, FechaDesde = a.FechaDesde, FechaHasta = a.FechaHasta, MotivoExcepcion = a.MotivoExcepcion
        };
    }

    public async Task CerrarAsignacionAsync(Guid asignacionId, DateOnly fechaHasta, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var a = await _db.AsignacionesHorario.FirstOrDefaultAsync(x => x.AsignacionHorarioId == asignacionId && x.EmpresaId == empresaId, ct)
            ?? throw ServiceException.NotFound("Asignación no encontrada.");
        if (fechaHasta < a.FechaDesde) throw ServiceException.Validation("La fecha final no puede ser anterior al inicio de la asignación.");
        if (a.FechaHasta is { } actual && fechaHasta > actual) throw ServiceException.Validation("Solo se puede acortar la asignación.");
        var antes = new { a.FechaHasta };
        a.FechaHasta = fechaHasta;
        _auditoria.Registrar("Horarios", nameof(AsignacionHorario), a.AsignacionHorarioId.ToString(), "CerrarAsignacion", antes, new { a.FechaHasta });
        await _db.SaveSafeAsync(ct);
    }

    // ---------------- Horario aplicable ----------------
    public async Task<HorarioAplicableDto> AplicableAsync(Guid trabajadorId, DateOnly fecha, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        await TrabajadorHelper.CargarAsync(_db, empresaId, trabajadorId, false, ct);
        return await AplicableAsync(empresaId, trabajadorId, fecha, ct);
    }

    public async Task<HorarioAplicableDto> AplicableAsync(Guid empresaId, Guid trabajadorId, DateOnly fecha, CancellationToken ct)
    {
        var dto = new HorarioAplicableDto { TrabajadorId = trabajadorId, Fecha = fecha };
        var t = await _db.Trabajadores.AsNoTracking().Where(x => x.TrabajadorId == trabajadorId && x.EmpresaId == empresaId)
            .Select(x => new { x.PuestoId, x.DepartamentoId }).FirstOrDefaultAsync(ct);
        if (t is null) return dto;

        var asignaciones = await _db.AsignacionesHorario.AsNoTracking()
            .Where(a => a.EmpresaId == empresaId && a.TrabajadorId == trabajadorId && a.FechaDesde <= fecha && (a.FechaHasta == null || a.FechaHasta >= fecha))
            .Join(_db.HorariosLaborales.Where(h => h.EmpresaId == empresaId && h.Estado), a => a.HorarioLaboralId, h => h.HorarioLaboralId, (a, h) => new { a, h.Nombre })
            .ToListAsync(ct);
        var elegido = asignaciones.Where(x => x.a.MotivoExcepcion != null).OrderByDescending(x => x.a.FechaDesde).FirstOrDefault();
        string? origen = elegido is null ? null : "Excepcion";
        if (elegido is null)
        {
            elegido = asignaciones.OrderByDescending(x => x.a.FechaDesde).FirstOrDefault();
            if (elegido is not null) origen = "Asignacion";
        }

        Guid? horarioId = elegido?.a.HorarioLaboralId;
        string? nombre = elegido?.Nombre;
        if (horarioId is null)
        {
            var vigentes = _db.HorariosLaborales.AsNoTracking().Where(h => h.EmpresaId == empresaId && h.Estado
                && h.FechaVigenciaDesde <= fecha && (h.FechaVigenciaHasta == null || h.FechaVigenciaHasta >= fecha));
            var porPuesto = t.PuestoId is { } pu
                ? await vigentes.Where(h => h.PuestoId == pu).OrderByDescending(h => h.FechaVigenciaDesde).Select(h => new { h.HorarioLaboralId, h.Nombre }).FirstOrDefaultAsync(ct)
                : null;
            if (porPuesto is not null) { horarioId = porPuesto.HorarioLaboralId; nombre = porPuesto.Nombre; origen = "Puesto"; }
            else if (t.DepartamentoId is { } de)
            {
                var porDep = await vigentes.Where(h => h.DepartamentoId == de && h.PuestoId == null).OrderByDescending(h => h.FechaVigenciaDesde)
                    .Select(h => new { h.HorarioLaboralId, h.Nombre }).FirstOrDefaultAsync(ct);
                if (porDep is not null) { horarioId = porDep.HorarioLaboralId; nombre = porDep.Nombre; origen = "Departamento"; }
            }
        }

        dto.HorarioLaboralId = horarioId; dto.Horario = nombre; dto.Origen = origen;
        if (horarioId is { } hid)
        {
            var dia = HorariosHelper.DiaSemana(fecha);
            var det = await _db.HorarioDetalleDia.AsNoTracking().FirstOrDefaultAsync(d => d.EmpresaId == empresaId && d.HorarioLaboralId == hid && d.DiaSemana == dia, ct);
            dto.Dia = det is null ? null : HorariosHelper.ToDto(det);
        }

        dto.Festivo = await _db.DiasFestivos.AsNoTracking()
            .Where(f => f.EmpresaId == empresaId && (f.Fecha == fecha || (f.EsRecurrenteAnual && f.Fecha.Month == fecha.Month && f.Fecha.Day == fecha.Day)))
            .Select(f => f.Nombre).FirstOrDefaultAsync(ct);
        dto.Descanso = await _db.DescansosTrabajador.AsNoTracking()
            .Where(d => d.EmpresaId == empresaId && d.TrabajadorId == trabajadorId && d.FechaDesde <= fecha && d.FechaHasta >= fecha)
            .OrderByDescending(d => d.Tipo).Select(d => d.Descripcion ?? (d.Tipo == TipoDescanso.Habitual ? "Descanso" : "Descanso (excepción)")).FirstOrDefaultAsync(ct);

        dto.EsDiaLaboral = dto.Festivo is null && dto.Descanso is null
            && (horarioId is null ? fecha.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) : dto.Dia is { EsDiaLaboral: true });
        return dto;
    }
}

public class CalendarioLaboralService : ICalendarioLaboralService
{
    private readonly RegistroTrabajoDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IAuditoriaService _auditoria;
    private readonly TimeProvider _clock;

    public CalendarioLaboralService(RegistroTrabajoDbContext db, ICurrentUser current, IAuditoriaService auditoria, TimeProvider clock)
    {
        _db = db; _current = current; _auditoria = auditoria; _clock = clock;
    }

    private static DescansoTrabajadorDto ToDto(DescansoTrabajador d, string trabajador) => new()
    {
        DescansoTrabajadorId = d.DescansoTrabajadorId, TrabajadorId = d.TrabajadorId, Trabajador = trabajador,
        FechaDesde = d.FechaDesde, FechaHasta = d.FechaHasta, Tipo = d.Tipo, Descripcion = d.Descripcion
    };

    private static DiaFestivoDto ToDto(DiaFestivo f) => new() { DiaFestivoId = f.DiaFestivoId, Fecha = f.Fecha, Nombre = f.Nombre, EsRecurrenteAnual = f.EsRecurrenteAnual };

    public async Task<IReadOnlyList<DescansoTrabajadorDto>> ListarDescansosAsync(Guid? trabajadorId, DateOnly? desde, DateOnly? hasta, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var q = _db.DescansosTrabajador.AsNoTracking().Include(d => d.Trabajador).Where(d => d.EmpresaId == empresaId);
        if (trabajadorId is { } t) q = q.Where(d => d.TrabajadorId == t);
        if (desde is { } d0) q = q.Where(d => d.FechaHasta >= d0);
        if (hasta is { } d1) q = q.Where(d => d.FechaDesde <= d1);
        return (await q.OrderByDescending(d => d.FechaDesde).Take(500).ToListAsync(ct)).Select(d => ToDto(d, HorariosHelper.NombreCompleto(d.Trabajador))).ToList();
    }

    private async Task<Trabajador> ValidarDescansoAsync(Guid empresaId, GuardarDescansoRequest r, CancellationToken ct)
    {
        if (r.Tipo is not (TipoDescanso.Habitual or TipoDescanso.Excepcion)) throw ServiceException.Validation("Tipo de descanso no válido.");
        if (r.FechaHasta < r.FechaDesde) throw ServiceException.Validation("La fecha final no puede ser anterior a la inicial.");
        if (r.FechaHasta.DayNumber - r.FechaDesde.DayNumber > 366) throw ServiceException.Validation("El periodo es demasiado largo.");
        return await TrabajadorHelper.CargarAsync(_db, empresaId, r.TrabajadorId, false, ct);
    }

    public async Task<DescansoTrabajadorDto> CrearDescansoAsync(GuardarDescansoRequest r, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var t = await ValidarDescansoAsync(empresaId, r, ct);
        var d = new DescansoTrabajador
        {
            DescansoTrabajadorId = Guid.NewGuid(), EmpresaId = empresaId, TrabajadorId = r.TrabajadorId, FechaDesde = r.FechaDesde,
            FechaHasta = r.FechaHasta, Tipo = r.Tipo, Descripcion = Helpers.Limpiar(r.Descripcion), RegistradoPorUsuarioId = _current.RequireUsuarioId()
        };
        _db.DescansosTrabajador.Add(d);
        _auditoria.Registrar("Horarios", nameof(DescansoTrabajador), d.DescansoTrabajadorId.ToString(), "Crear", null, new { d.TrabajadorId, d.FechaDesde, d.FechaHasta, d.Tipo });
        await _db.SaveSafeAsync(ct);
        return ToDto(d, HorariosHelper.NombreCompleto(t));
    }

    public async Task<DescansoTrabajadorDto> ActualizarDescansoAsync(Guid id, GuardarDescansoRequest r, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var t = await ValidarDescansoAsync(empresaId, r, ct);
        var d = await _db.DescansosTrabajador.FirstOrDefaultAsync(x => x.DescansoTrabajadorId == id && x.EmpresaId == empresaId, ct)
            ?? throw ServiceException.NotFound("Descanso no encontrado.");
        if (d.TrabajadorId != r.TrabajadorId) throw ServiceException.Validation("No se puede cambiar el trabajador de un descanso.");
        var antes = new { d.FechaDesde, d.FechaHasta, d.Tipo, d.Descripcion };
        d.FechaDesde = r.FechaDesde; d.FechaHasta = r.FechaHasta; d.Tipo = r.Tipo; d.Descripcion = Helpers.Limpiar(r.Descripcion);
        d.RegistradoPorUsuarioId = _current.RequireUsuarioId();
        _auditoria.Registrar("Horarios", nameof(DescansoTrabajador), d.DescansoTrabajadorId.ToString(), "Actualizar", antes, new { d.FechaDesde, d.FechaHasta, d.Tipo, d.Descripcion });
        await _db.SaveSafeAsync(ct);
        return ToDto(d, HorariosHelper.NombreCompleto(t));
    }

    public async Task<DescansoTrabajadorDto> TerminarDescansoAsync(Guid id, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var d = await _db.DescansosTrabajador.Include(x => x.Trabajador).FirstOrDefaultAsync(x => x.DescansoTrabajadorId == id && x.EmpresaId == empresaId, ct)
            ?? throw ServiceException.NotFound("Descanso no encontrado.");
        var ayer = TrabajadorHelper.Hoy(_clock).AddDays(-1);
        if (d.FechaHasta <= ayer) throw ServiceException.Conflict("El descanso ya terminó.");
        var antes = new { d.FechaDesde, d.FechaHasta };
        // Un descanso futuro no puede quedar con fin anterior a su inicio (CHECK de la BD): se acota al día de inicio.
        d.FechaHasta = d.FechaDesde > ayer ? d.FechaDesde : ayer;
        _auditoria.Registrar("Horarios", nameof(DescansoTrabajador), d.DescansoTrabajadorId.ToString(), "Terminar", antes, new { d.FechaDesde, d.FechaHasta });
        await _db.SaveSafeAsync(ct);
        return ToDto(d, HorariosHelper.NombreCompleto(d.Trabajador));
    }

    public async Task<IReadOnlyList<DiaFestivoDto>> ListarFestivosAsync(int? anio, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var q = _db.DiasFestivos.AsNoTracking().Where(f => f.EmpresaId == empresaId);
        if (anio is { } a) q = q.Where(f => f.Fecha.Year == a || f.EsRecurrenteAnual);
        return (await q.OrderBy(f => f.Fecha.Month).ThenBy(f => f.Fecha.Day).ToListAsync(ct)).Select(ToDto).ToList();
    }

    public async Task<DiaFestivoDto> CrearFestivoAsync(GuardarDiaFestivoRequest r, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        if (string.IsNullOrWhiteSpace(r.Nombre)) throw ServiceException.Validation("El nombre del festivo es obligatorio.");
        var nombre = r.Nombre.Trim();
        if (await _db.DiasFestivos.AnyAsync(f => f.EmpresaId == empresaId && f.Fecha == r.Fecha && f.Nombre == nombre, ct))
            throw ServiceException.Conflict("Ya existe ese festivo en esa fecha.");
        var f = new DiaFestivo { DiaFestivoId = Guid.NewGuid(), EmpresaId = empresaId, Fecha = r.Fecha, Nombre = nombre, EsRecurrenteAnual = r.EsRecurrenteAnual };
        _db.DiasFestivos.Add(f);
        _auditoria.Registrar("Horarios", nameof(DiaFestivo), f.DiaFestivoId.ToString(), "Crear", null, ToDto(f));
        await _db.SaveSafeAsync(ct);
        return ToDto(f);
    }

    public async Task<DiaFestivoDto> ActualizarFestivoAsync(Guid id, GuardarDiaFestivoRequest r, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        if (string.IsNullOrWhiteSpace(r.Nombre)) throw ServiceException.Validation("El nombre del festivo es obligatorio.");
        var f = await _db.DiasFestivos.FirstOrDefaultAsync(x => x.DiaFestivoId == id && x.EmpresaId == empresaId, ct)
            ?? throw ServiceException.NotFound("Festivo no encontrado.");
        var nombre = r.Nombre.Trim();
        if (await _db.DiasFestivos.AnyAsync(x => x.EmpresaId == empresaId && x.Fecha == r.Fecha && x.Nombre == nombre && x.DiaFestivoId != id, ct))
            throw ServiceException.Conflict("Ya existe ese festivo en esa fecha.");
        var antes = ToDto(f);
        f.Fecha = r.Fecha; f.Nombre = nombre; f.EsRecurrenteAnual = r.EsRecurrenteAnual;
        _auditoria.Registrar("Horarios", nameof(DiaFestivo), f.DiaFestivoId.ToString(), "Actualizar", antes, ToDto(f));
        await _db.SaveSafeAsync(ct);
        return ToDto(f);
    }

    public async Task EliminarFestivoAsync(Guid id, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var f = await _db.DiasFestivos.FirstOrDefaultAsync(x => x.DiaFestivoId == id && x.EmpresaId == empresaId, ct)
            ?? throw ServiceException.NotFound("Festivo no encontrado.");
        _db.DiasFestivos.Remove(f);
        _auditoria.Registrar("Horarios", nameof(DiaFestivo), f.DiaFestivoId.ToString(), "Eliminar", ToDto(f), null);
        await _db.SaveSafeAsync(ct);
    }
}
