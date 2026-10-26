using Microsoft.EntityFrameworkCore;
using RegistroTrabajo.Data;
using RegistroTrabajo.Data.Entities;
using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public interface IEventosEmpresaService
{
    /// <summary>Administración de eventos (activos y cancelados). Exige calendario.gestionar_eventos.</summary>
    Task<PagedResult<EventoEmpresaDto>> ListarAsync(EventoEmpresaFiltro filtro, CancellationToken ct);
    /// <summary>Detalle de un evento. Un evento que el usuario no puede ver responde 404, aunque conozca el identificador.</summary>
    Task<EventoEmpresaDto> ObtenerAsync(Guid eventoId, CancellationToken ct);
    Task<EventoEmpresaDto> CrearAsync(GuardarEventoEmpresaRequest request, CancellationToken ct);
    Task<EventoEmpresaDto> ActualizarAsync(Guid eventoId, GuardarEventoEmpresaRequest request, CancellationToken ct);
    /// <summary>Cancela el evento conservándolo para el historial (quién, cuándo y por qué).</summary>
    Task<EventoEmpresaDto> CancelarAsync(Guid eventoId, CancelarEventoEmpresaRequest request, CancellationToken ct);
}

public interface ICalendarioService
{
    /// <summary>Reúne eventos, festivos, ausencias aprobadas y fechas relevantes que el usuario puede ver en el rango.</summary>
    Task<CalendarioDto> ConsultarAsync(DateOnly desde, DateOnly hasta, bool incluirCancelados, CancellationToken ct);
    /// <summary>Agenda de un día: lo mismo que el calendario, acotado a esa fecha.</summary>
    Task<CalendarioDto> DiaAsync(DateOnly fecha, CancellationToken ct);
    Task<ConfiguracionCalendarioDto> ObtenerConfiguracionAsync(CancellationToken ct);
    Task<ConfiguracionCalendarioDto> GuardarConfiguracionAsync(ConfiguracionCalendarioDto request, CancellationToken ct);
}

internal static class CalendarioHelper
{
    public const int MaxDiasConsulta = 93;
    public const int MaxDiasEvento = 366;

    public static DateTime Recortar(DateTime d) => new(d.Year, d.Month, d.Day, d.Hour, d.Minute, 0, DateTimeKind.Unspecified);

    /// <summary>Hora local de la empresa → instante UTC. Una hora inexistente (salto de horario de verano) se rechaza; una ambigua toma la hora estándar.</summary>
    public static DateTime LocalAUtc(DateTime local, TimeZoneInfo zona)
    {
        var l = Recortar(local);
        if (zona.IsInvalidTime(l)) throw ServiceException.Validation("La hora indicada no existe en la zona horaria de la empresa (cambio de horario).");
        return TimeZoneInfo.ConvertTimeToUtc(l, zona);
    }

    public static DateTime UtcALocal(DateTime utc, TimeZoneInfo zona) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zona);

    /// <summary>Instante UTC en que empieza el día local (si la medianoche no existe en la zona se usa la hora siguiente).</summary>
    public static DateTime InicioDiaUtc(DateOnly dia, TimeZoneInfo zona)
    {
        var l = dia.ToDateTime(TimeOnly.MinValue);
        while (zona.IsInvalidTime(l)) l = l.AddHours(1);
        return TimeZoneInfo.ConvertTimeToUtc(l, zona);
    }

    /// <summary>
    /// Eventos que tocan el rango de días locales. Los de día completo guardan fecha local; los demás, instantes UTC,
    /// así que el rango se convierte a UTC con la zona de la empresa.
    /// </summary>
    public static IQueryable<EventoEmpresa> Solapan(IQueryable<EventoEmpresa> q, DateOnly desde, DateOnly hasta, TimeZoneInfo zona)
    {
        var diaIni = desde.ToDateTime(TimeOnly.MinValue);
        var diaFinExcl = hasta.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var utcIni = InicioDiaUtc(desde, zona);
        var utcFinExcl = InicioDiaUtc(hasta.AddDays(1), zona);
        return q.Where(e => (e.TodoElDia && e.FechaInicio < diaFinExcl && (e.FechaFin ?? e.FechaInicio) >= diaIni)
            || (!e.TodoElDia && e.FechaInicio < utcFinExcl && (e.FechaFin ?? e.FechaInicio) >= utcIni));
    }

    /// <summary>Primer y último día local que ocupa el evento. Uno con hora que termina justo a medianoche no invade el día siguiente.</summary>
    public static (DateOnly Inicio, DateOnly Fin, DateTime? HoraIni, DateTime? HoraFin) Dias(EventoEmpresa e, TimeZoneInfo zona)
    {
        if (e.TodoElDia)
            return (DateOnly.FromDateTime(e.FechaInicio), DateOnly.FromDateTime(e.FechaFin ?? e.FechaInicio), null, null);
        var ini = UtcALocal(e.FechaInicio, zona);
        DateTime? fin = e.FechaFin is null ? null : UtcALocal(e.FechaFin.Value, zona);
        var ultimo = fin ?? ini;
        if (fin is { } f && f > ini && f.TimeOfDay == TimeSpan.Zero) ultimo = f.AddDays(-1);
        return (DateOnly.FromDateTime(ini), DateOnly.FromDateTime(ultimo), ini, fin);
    }

    /// <summary>Fecha del aniversario en un año; el 29 de febrero se celebra el 28 en los años no bisiestos.</summary>
    public static DateOnly Aniversario(DateOnly origen, int anio)
    {
        var dia = origen.Month == 2 && origen.Day == 29 && !DateTime.IsLeapYear(anio) ? 28 : origen.Day;
        return new DateOnly(anio, origen.Month, dia);
    }
}

public class EventosEmpresaService : IEventosEmpresaService
{
    private readonly RegistroTrabajoDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IPermisoService _permisos;
    private readonly IAuditoriaService _auditoria;
    private readonly TimeProvider _clock;

    public EventosEmpresaService(RegistroTrabajoDbContext db, ICurrentUser current, IPermisoService permisos, IAuditoriaService auditoria, TimeProvider clock)
    {
        _db = db; _current = current; _permisos = permisos; _auditoria = auditoria; _clock = clock;
    }

    private async Task<TimeZoneInfo> ZonaAsync(Guid empresaId, CancellationToken ct) =>
        AsistenciaHelper.Zona(await _db.ConfiguracionEmpresa.AsNoTracking().Where(c => c.EmpresaId == empresaId).Select(c => c.ZonaHorariaId).FirstOrDefaultAsync(ct));

    private async Task<Dictionary<Guid, string>> NombresAsync(Guid empresaId, IEnumerable<Guid?> ids, CancellationToken ct)
    {
        var lista = ids.Where(i => i is not null).Select(i => i!.Value).Distinct().ToList();
        if (lista.Count == 0) return new();
        return await _db.Usuarios.AsNoTracking().Where(u => u.EmpresaId == empresaId && lista.Contains(u.UsuarioId))
            .Select(u => new { u.UsuarioId, u.Nombre, u.Apellido }).ToDictionaryAsync(u => u.UsuarioId, u => (u.Nombre + " " + u.Apellido).Trim(), ct);
    }

    private static EventoEmpresaDto ToDto(EventoEmpresa e, TimeZoneInfo zona, IReadOnlyDictionary<Guid, string> nombres)
    {
        string? Nombre(Guid? id) => id is { } i && nombres.TryGetValue(i, out var n) ? n : null;
        return new EventoEmpresaDto
        {
            EventoEmpresaId = e.EventoEmpresaId, Titulo = e.Titulo, Descripcion = e.Descripcion, Tipo = e.Tipo, TodoElDia = e.TodoElDia,
            FechaInicio = e.TodoElDia ? e.FechaInicio : CalendarioHelper.UtcALocal(e.FechaInicio, zona),
            FechaFin = e.FechaFin is null ? null : e.TodoElDia ? e.FechaFin : CalendarioHelper.UtcALocal(e.FechaFin.Value, zona),
            Estado = e.Estado, Visibilidad = e.Visibilidad, DepartamentoId = e.DepartamentoId, Departamento = e.Departamento?.Nombre,
            CreadoPor = Nombre(e.CreadoPorUsuarioId) ?? "—", FechaCreacionUtc = e.FechaCreacionUtc,
            FechaModificacionUtc = e.FechaModificacionUtc, ModificadoPor = Nombre(e.ModificadoPorUsuarioId),
            FechaCancelacionUtc = e.FechaCancelacionUtc, CanceladoPor = Nombre(e.CanceladoPorUsuarioId), MotivoCancelacion = e.MotivoCancelacion,
            RowVersion = Convert.ToBase64String(e.RowVersion ?? Array.Empty<byte>())
        };
    }

    private async Task<EventoEmpresaDto> DtoAsync(EventoEmpresa e, Guid empresaId, TimeZoneInfo zona, CancellationToken ct)
    {
        var nombres = await NombresAsync(empresaId, new Guid?[] { e.CreadoPorUsuarioId, e.ModificadoPorUsuarioId, e.CanceladoPorUsuarioId }, ct);
        return ToDto(e, zona, nombres);
    }

    public async Task<PagedResult<EventoEmpresaDto>> ListarAsync(EventoEmpresaFiltro f, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.CalendarioGestionarEventos, ct);
        var empresaId = _current.RequireEmpresaId();
        var zona = await ZonaAsync(empresaId, ct);

        var q = _db.EventosEmpresa.AsNoTracking().Include(e => e.Departamento).Where(e => e.EmpresaId == empresaId);
        if (f.Desde is not null || f.Hasta is not null)
        {
            var desde = f.Desde ?? DateOnly.MinValue.AddYears(1);
            var hasta = f.Hasta ?? DateOnly.MaxValue.AddYears(-1);
            if (hasta < desde) throw ServiceException.Validation("El rango de fechas no es válido.");
            q = CalendarioHelper.Solapan(q, desde, hasta, zona);
        }
        if (f.Tipo is { } t) q = q.Where(e => e.Tipo == t);
        if (f.Estado is { } est) q = q.Where(e => e.Estado == est);

        var pagina = await q.OrderByDescending(e => e.FechaInicio).ThenBy(e => e.Titulo).ToPagedAsync(f, ct);
        var nombres = await NombresAsync(empresaId, pagina.Items.SelectMany(e => new Guid?[] { e.CreadoPorUsuarioId, e.ModificadoPorUsuarioId, e.CanceladoPorUsuarioId }), ct);
        return new PagedResult<EventoEmpresaDto>
        {
            Items = pagina.Items.Select(e => ToDto(e, zona, nombres)).ToList(), Page = pagina.Page, PageSize = pagina.PageSize, Total = pagina.Total
        };
    }

    public async Task<EventoEmpresaDto> ObtenerAsync(Guid eventoId, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var e = await _db.EventosEmpresa.AsNoTracking().Include(x => x.Departamento)
            .FirstOrDefaultAsync(x => x.EventoEmpresaId == eventoId && x.EmpresaId == empresaId, ct);
        if (e is null || !await PuedeVerAsync(e, empresaId, ct)) throw ServiceException.NotFound("Evento no encontrado.");
        return await DtoAsync(e, empresaId, await ZonaAsync(empresaId, ct), ct);
    }

    /// <summary>Quien administra eventos ve todos; los demás solo los activos de su empresa o de su departamento.</summary>
    private async Task<bool> PuedeVerAsync(EventoEmpresa e, Guid empresaId, CancellationToken ct)
    {
        if (await _permisos.TienePermisoAsync(Permisos.CalendarioGestionarEventos, ct)) return true;
        if (e.Estado != EstadoEventoEmpresa.Activo) return false;
        if (e.Visibilidad == VisibilidadEvento.Empresa) return true;
        var mi = await TrabajadorHelper.IdDeUsuarioAsync(_db, empresaId, _current.RequireUsuarioId(), ct);
        if (mi is null || e.DepartamentoId is null) return false;
        var depto = await _db.Trabajadores.AsNoTracking().Where(t => t.EmpresaId == empresaId && t.TrabajadorId == mi).Select(t => t.DepartamentoId).FirstOrDefaultAsync(ct);
        return depto == e.DepartamentoId;
    }

    public async Task<EventoEmpresaDto> CrearAsync(GuardarEventoEmpresaRequest r, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.CalendarioGestionarEventos, ct);
        var empresaId = _current.RequireEmpresaId();
        var zona = await ZonaAsync(empresaId, ct);
        var e = new EventoEmpresa
        {
            EventoEmpresaId = Guid.NewGuid(), EmpresaId = empresaId, Estado = EstadoEventoEmpresa.Activo,
            CreadoPorUsuarioId = _current.RequireUsuarioId(), FechaCreacionUtc = _clock.GetUtcNow().UtcDateTime
        };
        await AplicarAsync(e, r, empresaId, zona, ct);
        _db.EventosEmpresa.Add(e);
        _auditoria.Registrar("Calendario", nameof(EventoEmpresa), e.EventoEmpresaId.ToString(), "Crear", null, Resumen(e));
        await _db.SaveSafeAsync(ct);
        await _db.Entry(e).Reference(x => x.Departamento).LoadAsync(ct);
        return await DtoAsync(e, empresaId, zona, ct);
    }

    public async Task<EventoEmpresaDto> ActualizarAsync(Guid eventoId, GuardarEventoEmpresaRequest r, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.CalendarioGestionarEventos, ct);
        var empresaId = _current.RequireEmpresaId();
        var zona = await ZonaAsync(empresaId, ct);
        var e = await CargarAsync(eventoId, empresaId, ct);
        if (e.Estado == EstadoEventoEmpresa.Cancelado) throw ServiceException.Conflict("Un evento cancelado no se puede editar.");
        AplicarRowVersion(e, r.RowVersion);

        var antes = Resumen(e);
        await AplicarAsync(e, r, empresaId, zona, ct);
        e.ModificadoPorUsuarioId = _current.RequireUsuarioId();
        e.FechaModificacionUtc = _clock.GetUtcNow().UtcDateTime;
        _auditoria.Registrar("Calendario", nameof(EventoEmpresa), e.EventoEmpresaId.ToString(), "Actualizar", antes, Resumen(e));
        await _db.SaveSafeAsync(ct);
        await _db.Entry(e).Reference(x => x.Departamento).LoadAsync(ct);
        return await DtoAsync(e, empresaId, zona, ct);
    }

    public async Task<EventoEmpresaDto> CancelarAsync(Guid eventoId, CancelarEventoEmpresaRequest r, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.CalendarioGestionarEventos, ct);
        var empresaId = _current.RequireEmpresaId();
        var motivo = Helpers.Limpiar(r.Motivo);
        if (motivo is null || motivo.Length < 3) throw ServiceException.Validation("Indique el motivo de la cancelación.");
        var e = await CargarAsync(eventoId, empresaId, ct);
        if (e.Estado == EstadoEventoEmpresa.Cancelado) throw ServiceException.Conflict("El evento ya está cancelado.");
        AplicarRowVersion(e, r.RowVersion);

        e.Estado = EstadoEventoEmpresa.Cancelado;
        e.CanceladoPorUsuarioId = _current.RequireUsuarioId();
        e.FechaCancelacionUtc = _clock.GetUtcNow().UtcDateTime;
        e.MotivoCancelacion = motivo;
        _auditoria.Registrar("Calendario", nameof(EventoEmpresa), e.EventoEmpresaId.ToString(), "Cancelar",
            new { Estado = EstadoEventoEmpresa.Activo }, new { e.Estado, e.MotivoCancelacion });
        await _db.SaveSafeAsync(ct);
        await _db.Entry(e).Reference(x => x.Departamento).LoadAsync(ct);
        return await DtoAsync(e, empresaId, await ZonaAsync(empresaId, ct), ct);
    }

    private async Task<EventoEmpresa> CargarAsync(Guid eventoId, Guid empresaId, CancellationToken ct) =>
        await _db.EventosEmpresa.FirstOrDefaultAsync(x => x.EventoEmpresaId == eventoId && x.EmpresaId == empresaId, ct)
        ?? throw ServiceException.NotFound("Evento no encontrado.");

    private static object Resumen(EventoEmpresa e) =>
        new { e.Titulo, e.Tipo, e.TodoElDia, e.FechaInicio, e.FechaFin, e.Visibilidad, e.DepartamentoId, e.Estado };

    private void AplicarRowVersion(EventoEmpresa e, string? rowVersion)
    {
        if (string.IsNullOrWhiteSpace(rowVersion)) throw ServiceException.Validation("Falta la versión del evento. Recargue e intente de nuevo.");
        byte[] enviada;
        try { enviada = Convert.FromBase64String(rowVersion); }
        catch (FormatException) { throw ServiceException.Validation("La versión del evento no es válida."); }
        if (e.RowVersion is { } actual && !actual.AsSpan().SequenceEqual(enviada))
            throw ServiceException.Conflict("El evento fue modificado por otra persona. Recargue e intente de nuevo.");
        _db.Entry(e).Property(x => x.RowVersion).OriginalValue = enviada;
    }

    /// <summary>Valida y copia los datos del formulario. Día completo: fechas locales; con hora: instantes UTC según la zona de la empresa.</summary>
    private async Task AplicarAsync(EventoEmpresa e, GuardarEventoEmpresaRequest r, Guid empresaId, TimeZoneInfo zona, CancellationToken ct)
    {
        var titulo = Helpers.Limpiar(r.Titulo);
        if (titulo is null || titulo.Length < 2) throw ServiceException.Validation("El título del evento es obligatorio.");
        if (r.Tipo is < TipoEventoEmpresa.ReunionGeneral or > TipoEventoEmpresa.Otro) throw ServiceException.Validation("Tipo de evento no válido.");

        DateTime inicio, fin;
        if (r.TodoElDia)
        {
            inicio = r.FechaInicio.Date;
            fin = (r.FechaFin ?? r.FechaInicio).Date;
            if (fin < inicio) throw ServiceException.Validation("La fecha de fin no puede ser anterior a la de inicio.");
            if ((fin - inicio).TotalDays >= CalendarioHelper.MaxDiasEvento) throw ServiceException.Validation("El evento es demasiado largo.");
            e.FechaFin = fin;
        }
        else
        {
            inicio = CalendarioHelper.LocalAUtc(r.FechaInicio, zona);
            if (r.FechaFin is { } ff)
            {
                fin = CalendarioHelper.LocalAUtc(ff, zona);
                if (fin < inicio) throw ServiceException.Validation("La fecha de fin no puede ser anterior a la de inicio.");
                if ((fin - inicio).TotalDays >= CalendarioHelper.MaxDiasEvento) throw ServiceException.Validation("El evento es demasiado largo.");
                e.FechaFin = fin;
            }
            else e.FechaFin = null;
        }

        Guid? departamentoId = null;
        if (r.Visibilidad == VisibilidadEvento.Departamento)
        {
            if (r.DepartamentoId is not { } did) throw ServiceException.Validation("Seleccione el departamento que puede ver el evento.");
            var existe = await _db.Departamentos.AsNoTracking().AnyAsync(d => d.EmpresaId == empresaId && d.DepartamentoId == did && d.Estado, ct);
            if (!existe) throw ServiceException.Validation("El departamento indicado no existe o está inactivo.");
            departamentoId = did;
        }
        else if (r.Visibilidad != VisibilidadEvento.Empresa) throw ServiceException.Validation("Visibilidad no válida.");

        e.Titulo = titulo;
        e.Descripcion = Helpers.Limpiar(r.Descripcion);
        e.Tipo = r.Tipo;
        e.TodoElDia = r.TodoElDia;
        e.FechaInicio = inicio;
        e.Visibilidad = r.Visibilidad;
        e.DepartamentoId = departamentoId;
    }
}

/// <summary>
/// Calendario unificado. No duplica datos: lee los eventos propios (EventosEmpresa), los festivos del Bloque 5, los permisos
/// aprobados del Bloque 4 y las fechas del expediente del Bloque 3, y aplica a cada fuente su regla de visibilidad.
/// </summary>
public class CalendarioService : ICalendarioService
{
    private readonly RegistroTrabajoDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IPermisoService _permisos;
    private readonly IAuditoriaService _auditoria;
    private readonly TimeProvider _clock;

    public CalendarioService(RegistroTrabajoDbContext db, ICurrentUser current, IPermisoService permisos, IAuditoriaService auditoria, TimeProvider clock)
    {
        _db = db; _current = current; _permisos = permisos; _auditoria = auditoria; _clock = clock;
    }

    public Task<CalendarioDto> DiaAsync(DateOnly fecha, CancellationToken ct) => ConsultarAsync(fecha, fecha, false, ct);

    public async Task<CalendarioDto> ConsultarAsync(DateOnly desde, DateOnly hasta, bool incluirCancelados, CancellationToken ct)
    {
        if (hasta < desde) throw ServiceException.Validation("El rango de fechas no es válido.");
        if (hasta.DayNumber - desde.DayNumber >= CalendarioHelper.MaxDiasConsulta)
            throw ServiceException.Validation($"El rango no puede superar {CalendarioHelper.MaxDiasConsulta} días.");

        var empresaId = _current.RequireEmpresaId();
        var usuarioId = _current.RequireUsuarioId();
        var config = await _db.ConfiguracionEmpresa.AsNoTracking().FirstOrDefaultAsync(c => c.EmpresaId == empresaId, ct);
        var zona = AsistenciaHelper.Zona(config?.ZonaHorariaId);
        var gestiona = await _permisos.TienePermisoAsync(Permisos.CalendarioGestionarEventos, ct);
        var verRelevantes = await _permisos.TienePermisoAsync(Permisos.CalendarioVerFechasRelevantes, ct);

        var dto = new CalendarioDto
        {
            Desde = desde, Hasta = hasta, ZonaHoraria = zona.Id, PuedeGestionarEventos = gestiona,
            MuestraCumpleanos = config?.MostrarCumpleanos == true && verRelevantes,
            MuestraAniversarios = config?.MostrarAniversarios == true && verRelevantes
        };

        var mi = await TrabajadorHelper.IdDeUsuarioAsync(_db, empresaId, usuarioId, ct);
        await AgregarEventosAsync(dto, empresaId, mi, gestiona, incluirCancelados && gestiona, zona, ct);
        await AgregarFestivosAsync(dto, empresaId, ct);
        await AgregarAusenciasAsync(dto, empresaId, ct);
        if (dto.MuestraCumpleanos || dto.MuestraAniversarios) await AgregarFechasRelevantesAsync(dto, empresaId, ct);

        dto.Items = dto.Items
            .OrderBy(i => i.FechaInicio).ThenByDescending(i => i.TodoElDia).ThenBy(i => i.HoraInicio).ThenBy(i => i.Categoria).ThenBy(i => i.Titulo)
            .ToList();
        return dto;
    }

    private async Task AgregarEventosAsync(CalendarioDto dto, Guid empresaId, Guid? mi, bool gestiona, bool incluirCancelados, TimeZoneInfo zona, CancellationToken ct)
    {
        var q = CalendarioHelper.Solapan(_db.EventosEmpresa.AsNoTracking().Where(e => e.EmpresaId == empresaId), dto.Desde, dto.Hasta, zona);
        if (!incluirCancelados) q = q.Where(e => e.Estado == EstadoEventoEmpresa.Activo);
        if (!gestiona)
        {
            Guid? depto = mi is null ? null
                : await _db.Trabajadores.AsNoTracking().Where(t => t.EmpresaId == empresaId && t.TrabajadorId == mi).Select(t => t.DepartamentoId).FirstOrDefaultAsync(ct);
            q = depto is { } d
                ? q.Where(e => e.Visibilidad == VisibilidadEvento.Empresa || e.DepartamentoId == d)
                : q.Where(e => e.Visibilidad == VisibilidadEvento.Empresa);
        }

        foreach (var e in await q.Take(2000).ToListAsync(ct))
        {
            var (ini, fin, horaIni, horaFin) = CalendarioHelper.Dias(e, zona);
            if (fin < dto.Desde || ini > dto.Hasta) continue;
            dto.Items.Add(new CalendarioItemDto
            {
                Clave = $"evento:{e.EventoEmpresaId}", Categoria = CategoriaCalendario.Evento, Titulo = e.Titulo, Descripcion = e.Descripcion,
                TipoEvento = e.Tipo, FechaInicio = ini, FechaFin = fin, TodoElDia = e.TodoElDia,
                HoraInicio = horaIni is { } hi ? TimeOnly.FromDateTime(hi) : null,
                HoraFin = horaFin is { } hf ? TimeOnly.FromDateTime(hf) : null,
                Cancelado = e.Estado == EstadoEventoEmpresa.Cancelado, RecursoId = e.EventoEmpresaId
            });
        }
    }

    /// <summary>Festivos del Bloque 5 (incluidos los recurrentes): no hay un segundo catálogo.</summary>
    private async Task AgregarFestivosAsync(CalendarioDto dto, Guid empresaId, CancellationToken ct)
    {
        var festivos = await _db.DiasFestivos.AsNoTracking()
            .Where(f => f.EmpresaId == empresaId && (f.EsRecurrenteAnual || (f.Fecha >= dto.Desde && f.Fecha <= dto.Hasta))).ToListAsync(ct);
        foreach (var f in festivos)
        {
            if (!f.EsRecurrenteAnual) { AgregarFestivo(dto, f, f.Fecha); continue; }
            for (var anio = dto.Desde.Year; anio <= dto.Hasta.Year; anio++)
            {
                // El 29 de febrero recurrente solo existe en años bisiestos (igual que en el cálculo de duración de permisos).
                if (f.Fecha.Month == 2 && f.Fecha.Day == 29 && !DateTime.IsLeapYear(anio)) continue;
                var fecha = new DateOnly(anio, f.Fecha.Month, f.Fecha.Day);
                if (fecha >= dto.Desde && fecha <= dto.Hasta) AgregarFestivo(dto, f, fecha);
            }
        }
    }

    private static void AgregarFestivo(CalendarioDto dto, DiaFestivo f, DateOnly fecha) =>
        dto.Items.Add(new CalendarioItemDto
        {
            Clave = $"festivo:{f.DiaFestivoId}:{fecha:yyyyMMdd}", Categoria = CategoriaCalendario.Festivo, Titulo = f.Nombre,
            FechaInicio = fecha, FechaFin = fecha, TodoElDia = true
        });

    /// <summary>
    /// Solo permisos aprobados (una solicitud pendiente, rechazada o cancelada no es una ausencia). Con calendario.ver_ausencias_todos
    /// se ve a toda la empresa; sin él, cada quien ve las propias y los supervisores las de sus supervisados. Nunca tipo ni motivo.
    /// </summary>
    private async Task AgregarAusenciasAsync(CalendarioDto dto, Guid empresaId, CancellationToken ct)
    {
        var alcance = await PermisosHelper.AlcanceAsync(_db, _current, _permisos, _clock, false, ct);
        var ini = dto.Desde.ToDateTime(TimeOnly.MinValue);
        var fin = dto.Hasta.ToDateTime(new TimeOnly(23, 59, 59));
        var q = _db.SolicitudesPermiso.AsNoTracking()
            .Where(s => s.EmpresaId == empresaId && s.Estado == EstadoSolicitudPermiso.Aprobada && s.FechaInicio <= fin && s.FechaFin >= ini);
        // Con calendario.ver_ausencias_todos (por defecto, todos los roles) se ve a toda la empresa; sin él, solo el alcance de permisos.
        var veTodos = alcance.Todos || await _permisos.TienePermisoAsync(Permisos.CalendarioVerAusenciasTodos, ct);
        if (!veTodos)
        {
            var ids = alcance.Supervisados.ToList();
            if (alcance.MiTrabajadorId is { } m) ids.Add(m);
            q = q.Where(s => ids.Contains(s.TrabajadorId));
        }

        var filas = await q.OrderBy(s => s.FechaInicio).Take(2000).Select(s => new
        {
            s.SolicitudPermisoId, s.TrabajadorId, s.FechaInicio, s.FechaFin, s.EsPorHoras,
            Nombre = s.Trabajador.Nombres + " " + s.Trabajador.Apellidos
        }).ToListAsync(ct);

        foreach (var s in filas)
        {
            var d0 = DateOnly.FromDateTime(s.FechaInicio);
            var d1 = s.EsPorHoras ? d0 : DateOnly.FromDateTime(s.FechaFin);
            dto.Items.Add(new CalendarioItemDto
            {
                Clave = $"ausencia:{s.SolicitudPermisoId}", Categoria = CategoriaCalendario.Ausencia, Titulo = s.Nombre,
                FechaInicio = d0, FechaFin = d1, TodoElDia = !s.EsPorHoras,
                HoraInicio = s.EsPorHoras ? TimeOnly.FromDateTime(s.FechaInicio) : null,
                HoraFin = s.EsPorHoras ? TimeOnly.FromDateTime(s.FechaFin) : null,
                // Solo se puede abrir la solicitud propia o las del alcance de permisos; ver la ausencia no da acceso a su detalle.
                RecursoId = s.TrabajadorId == alcance.MiTrabajadorId || alcance.Incluye(s.TrabajadorId) ? s.SolicitudPermisoId : null,
                TrabajadorId = s.TrabajadorId
            });
        }
    }

    /// <summary>Cumpleaños y aniversarios de trabajadores activos. Solo nombre y fecha (sin edad ni otros datos personales).</summary>
    private async Task AgregarFechasRelevantesAsync(CalendarioDto dto, Guid empresaId, CancellationToken ct)
    {
        var trabajadores = await _db.Trabajadores.AsNoTracking()
            .Where(t => t.EmpresaId == empresaId && t.EstadoLaboral == EstadoLaboral.Activo)
            .Select(t => new { t.TrabajadorId, t.Nombres, t.Apellidos, t.FechaNacimiento, t.FechaIngreso }).ToListAsync(ct);

        foreach (var t in trabajadores)
        {
            var nombre = (t.Nombres + " " + t.Apellidos).Trim();
            for (var anio = dto.Desde.Year; anio <= dto.Hasta.Year; anio++)
            {
                if (dto.MuestraCumpleanos && t.FechaNacimiento is { } nac)
                {
                    var fecha = CalendarioHelper.Aniversario(nac, anio);
                    if (fecha >= dto.Desde && fecha <= dto.Hasta)
                        dto.Items.Add(new CalendarioItemDto
                        {
                            Clave = $"cumpleanos:{t.TrabajadorId}:{fecha:yyyyMMdd}", Categoria = CategoriaCalendario.Cumpleanos, Titulo = nombre,
                            FechaInicio = fecha, FechaFin = fecha, TodoElDia = true, TrabajadorId = t.TrabajadorId
                        });
                }
                if (dto.MuestraAniversarios)
                {
                    var anios = anio - t.FechaIngreso.Year;
                    if (anios < 1) continue;
                    var fecha = CalendarioHelper.Aniversario(t.FechaIngreso, anio);
                    if (fecha >= dto.Desde && fecha <= dto.Hasta)
                        dto.Items.Add(new CalendarioItemDto
                        {
                            Clave = $"aniversario:{t.TrabajadorId}:{fecha:yyyyMMdd}", Categoria = CategoriaCalendario.Aniversario, Titulo = nombre,
                            Descripcion = anios == 1 ? "1 año en la empresa" : $"{anios} años en la empresa",
                            FechaInicio = fecha, FechaFin = fecha, TodoElDia = true, TrabajadorId = t.TrabajadorId, Anios = anios
                        });
                }
            }
        }
    }

    public async Task<ConfiguracionCalendarioDto> ObtenerConfiguracionAsync(CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var c = await _db.ConfiguracionEmpresa.AsNoTracking().FirstOrDefaultAsync(x => x.EmpresaId == empresaId, ct)
            ?? throw ServiceException.NotFound("La empresa no tiene configuración.");
        return new ConfiguracionCalendarioDto { MostrarCumpleanos = c.MostrarCumpleanos, MostrarAniversarios = c.MostrarAniversarios };
    }

    public async Task<ConfiguracionCalendarioDto> GuardarConfiguracionAsync(ConfiguracionCalendarioDto r, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.CalendarioConfigurar, ct);
        var empresaId = _current.RequireEmpresaId();
        var c = await _db.ConfiguracionEmpresa.FirstOrDefaultAsync(x => x.EmpresaId == empresaId, ct)
            ?? throw ServiceException.NotFound("La empresa no tiene configuración.");
        var antes = new { c.MostrarCumpleanos, c.MostrarAniversarios };
        c.MostrarCumpleanos = r.MostrarCumpleanos;
        c.MostrarAniversarios = r.MostrarAniversarios;
        c.FechaActualizacionUtc = _clock.GetUtcNow().UtcDateTime;
        _auditoria.Registrar("Calendario", nameof(ConfiguracionEmpresa), empresaId.ToString(), "ConfigurarCalendario", antes,
            new { c.MostrarCumpleanos, c.MostrarAniversarios });
        await _db.SaveSafeAsync(ct);
        return new ConfiguracionCalendarioDto { MostrarCumpleanos = c.MostrarCumpleanos, MostrarAniversarios = c.MostrarAniversarios };
    }
}
