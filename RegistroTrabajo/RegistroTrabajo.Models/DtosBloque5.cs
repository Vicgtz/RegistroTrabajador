using System.ComponentModel.DataAnnotations;

namespace RegistroTrabajo.Models;

// ---------- Turnos ----------
public class TurnoDto
{
    public Guid TurnoId { get; set; }
    public string Nombre { get; set; } = null!;
    public TimeOnly HoraInicio { get; set; }
    public TimeOnly HoraFin { get; set; }
    public bool CruzaMedianoche { get; set; }
    public short ToleranciaMinutos { get; set; }
    public bool Estado { get; set; }
}

public class GuardarTurnoRequest
{
    [Required, StringLength(80)] public string Nombre { get; set; } = null!;
    public TimeOnly HoraInicio { get; set; }
    public TimeOnly HoraFin { get; set; }
    [Range(0, 720)] public short ToleranciaMinutos { get; set; }
}

// ---------- Horarios ----------
public class HorarioDetalleDiaDto
{
    /// <summary>1 = lunes ... 7 = domingo.</summary>
    [Range(1, 7)] public byte DiaSemana { get; set; }
    public bool EsDiaLaboral { get; set; }
    public TimeOnly? HoraEntrada { get; set; }
    public TimeOnly? HoraSalida { get; set; }
    public TimeOnly? InicioDescanso { get; set; }
    public TimeOnly? FinDescanso { get; set; }
    /// <summary>Calculado: la salida es menor o igual que la entrada (la jornada termina al día siguiente).</summary>
    public bool CruzaMedianoche { get; set; }
}

public class HorarioLaboralDto
{
    public Guid HorarioLaboralId { get; set; }
    public string Nombre { get; set; } = null!;
    public Guid? DepartamentoId { get; set; }
    public string? Departamento { get; set; }
    public Guid? PuestoId { get; set; }
    public string? Puesto { get; set; }
    public Guid? TurnoId { get; set; }
    public string? Turno { get; set; }
    public DateOnly FechaVigenciaDesde { get; set; }
    public DateOnly? FechaVigenciaHasta { get; set; }
    public bool Estado { get; set; }
    public List<HorarioDetalleDiaDto> Detalle { get; set; } = new();
}

public class GuardarHorarioRequest
{
    [Required, StringLength(120)] public string Nombre { get; set; } = null!;
    public Guid? DepartamentoId { get; set; }
    public Guid? PuestoId { get; set; }
    public Guid? TurnoId { get; set; }
    public DateOnly FechaVigenciaDesde { get; set; }
    public DateOnly? FechaVigenciaHasta { get; set; }
    [Required] public List<HorarioDetalleDiaDto> Detalle { get; set; } = new();
}

// ---------- Asignaciones, descansos y festivos ----------
public class AsignacionHorarioDto
{
    public Guid AsignacionHorarioId { get; set; }
    public Guid TrabajadorId { get; set; }
    public string Trabajador { get; set; } = null!;
    public Guid HorarioLaboralId { get; set; }
    public string Horario { get; set; } = null!;
    public DateOnly FechaDesde { get; set; }
    public DateOnly? FechaHasta { get; set; }
    /// <summary>Con valor = excepción individual temporal; nulo = asignación habitual.</summary>
    public string? MotivoExcepcion { get; set; }
}

public class AsignarHorarioRequest
{
    public Guid TrabajadorId { get; set; }
    public Guid HorarioLaboralId { get; set; }
    public DateOnly FechaDesde { get; set; }
    public DateOnly? FechaHasta { get; set; }
    [StringLength(300)] public string? MotivoExcepcion { get; set; }
}

public class DescansoTrabajadorDto
{
    public Guid DescansoTrabajadorId { get; set; }
    public Guid TrabajadorId { get; set; }
    public string Trabajador { get; set; } = null!;
    public DateOnly FechaDesde { get; set; }
    public DateOnly FechaHasta { get; set; }
    public byte Tipo { get; set; }
    public string? Descripcion { get; set; }
}

public class GuardarDescansoRequest
{
    public Guid TrabajadorId { get; set; }
    public DateOnly FechaDesde { get; set; }
    public DateOnly FechaHasta { get; set; }
    [Range(1, 2)] public byte Tipo { get; set; }
    [StringLength(300)] public string? Descripcion { get; set; }
}

public class DiaFestivoDto
{
    public Guid DiaFestivoId { get; set; }
    public DateOnly Fecha { get; set; }
    public string Nombre { get; set; } = null!;
    public bool EsRecurrenteAnual { get; set; }
}

public class GuardarDiaFestivoRequest
{
    public DateOnly Fecha { get; set; }
    [Required, StringLength(120)] public string Nombre { get; set; } = null!;
    public bool EsRecurrenteAnual { get; set; }
}

/// <summary>Qué le toca a un trabajador en una fecha: horario vigente, festivo y descanso.</summary>
public class HorarioAplicableDto
{
    public Guid TrabajadorId { get; set; }
    public DateOnly Fecha { get; set; }
    public Guid? HorarioLaboralId { get; set; }
    public string? Horario { get; set; }
    /// <summary>"Excepcion", "Asignacion", "Puesto", "Departamento" o nulo si no hay horario.</summary>
    public string? Origen { get; set; }
    public bool EsDiaLaboral { get; set; }
    public HorarioDetalleDiaDto? Dia { get; set; }
    public string? Festivo { get; set; }
    public string? Descanso { get; set; }
}

// ---------- Asistencia ----------
public class RegistrarAsistenciaRequest
{
    // La hora la genera el servidor; el cliente solo puede enviar la ubicación (si la empresa la exige).
    [Range(-90, 90)] public decimal? Latitud { get; set; }
    [Range(-180, 180)] public decimal? Longitud { get; set; }
}

public class EstadoJornadaDto
{
    public Guid TrabajadorId { get; set; }
    public DateTime HoraServidorUtc { get; set; }
    /// <summary>"Entrada" o "Salida": la próxima acción válida.</summary>
    public string Siguiente { get; set; } = "Entrada";
    public bool ExigeUbicacion { get; set; }
    public RegistroAsistenciaDto? JornadaAbierta { get; set; }
    public RegistroAsistenciaDto? Ultimo { get; set; }
    public HorarioAplicableDto? HorarioHoy { get; set; }
}

public class CorreccionAsistenciaDto
{
    public long CorreccionAsistenciaId { get; set; }
    public DateTime FechaCorreccionUtc { get; set; }
    public string CorregidoPor { get; set; } = null!;
    public DateTime? EntradaAnteriorUtc { get; set; }
    public DateTime? SalidaAnteriorUtc { get; set; }
    public DateTime? EntradaNuevaUtc { get; set; }
    public DateTime? SalidaNuevaUtc { get; set; }
    public string Motivo { get; set; } = null!;
}

public class RegistroAsistenciaDto
{
    public Guid RegistroAsistenciaId { get; set; }
    public Guid TrabajadorId { get; set; }
    public string Trabajador { get; set; } = null!;
    public DateOnly FechaLaboral { get; set; }
    public DateTime? EntradaUtc { get; set; }
    public DateTime? SalidaUtc { get; set; }
    public decimal? EntradaLatitud { get; set; }
    public decimal? EntradaLongitud { get; set; }
    public decimal? SalidaLatitud { get; set; }
    public decimal? SalidaLongitud { get; set; }
    public byte Estado { get; set; }
    public bool Incompleta { get; set; }
    public bool Corregida { get; set; }
    public decimal? HorasTrabajadas { get; set; }
    public string? Observaciones { get; set; }
    /// <summary>Horario que tenía asignado ese día (informativo; no se calculan retardos ni horas extra).</summary>
    public string? Horario { get; set; }
    public string? HorarioEsperado { get; set; }
    public List<CorreccionAsistenciaDto> Correcciones { get; set; } = new();
    /// <summary>Se llena para dar contexto cuando existe un permiso aprobado ese día.</summary>
    public bool TienePermisoAprobado { get; set; }
}

public class AsistenciaFiltro : PageQuery
{
    public Guid? TrabajadorId { get; set; }
    public DateOnly? Desde { get; set; }
    public DateOnly? Hasta { get; set; }
    public bool SoloIncompletas { get; set; }
}

public class CorregirAsistenciaRequest
{
    public DateTime? EntradaUtc { get; set; }
    public DateTime? SalidaUtc { get; set; }
    [Required, StringLength(400, MinimumLength = 5)] public string Motivo { get; set; } = null!;
}

public class AnularAsistenciaRequest
{
    [Required, StringLength(400, MinimumLength = 5)] public string Motivo { get; set; } = null!;
}

public class CrearRegistroManualRequest
{
    public Guid TrabajadorId { get; set; }
    public DateTime EntradaUtc { get; set; }
    public DateTime? SalidaUtc { get; set; }
    [Required, StringLength(400, MinimumLength = 5)] public string Motivo { get; set; } = null!;
}
