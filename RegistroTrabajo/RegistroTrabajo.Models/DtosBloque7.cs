using System.ComponentModel.DataAnnotations;

namespace RegistroTrabajo.Models;

// ---------- Estados de proyecto (catálogo por empresa) ----------

public class EstadoProyectoDto
{
    public Guid EstadoProyectoId { get; set; }
    public string Nombre { get; set; } = null!;
    public short Orden { get; set; }
    /// <summary>Un estado final cierra el proyecto (Finalizado, Cancelado...).</summary>
    public bool EsFinal { get; set; }
    public bool Activo { get; set; }
    public int Proyectos { get; set; }
}

public class GuardarEstadoProyectoRequest
{
    [Required, StringLength(80, MinimumLength = 2)] public string Nombre { get; set; } = null!;
    [Range(1, 1000)] public short Orden { get; set; } = 1;
    public bool EsFinal { get; set; }
    public bool Activo { get; set; } = true;
}

// ---------- Proyectos ----------

public class ParticipanteProyectoDto
{
    public Guid TrabajadorId { get; set; }
    public string Nombre { get; set; } = null!;
    public string? RolEnProyecto { get; set; }
    public bool EsResponsable { get; set; }
    public DateOnly FechaAsignacion { get; set; }
}

public class ProyectoResumenDto
{
    public Guid ProyectoId { get; set; }
    public string Nombre { get; set; } = null!;
    public Guid EstadoProyectoId { get; set; }
    public string Estado { get; set; } = null!;
    public bool EstadoFinal { get; set; }
    /// <summary><see cref="PrioridadTrabajo"/>: 1..4.</summary>
    public byte Prioridad { get; set; }
    public Guid ResponsableTrabajadorId { get; set; }
    public string Responsable { get; set; } = null!;
    public DateOnly? FechaInicio { get; set; }
    public DateOnly? FechaFinPrevista { get; set; }
    public int Participantes { get; set; }
    public int TareasTotal { get; set; }
    public int TareasCompletadas { get; set; }
    public int TareasVencidas { get; set; }
    /// <summary>Promedio simple del avance de las tareas no canceladas (indicador de seguimiento; la ponderación queda para una versión posterior).</summary>
    public int Avance { get; set; }
    /// <summary>Faltan pocos días para la fecha de fin prevista y el proyecto sigue abierto.</summary>
    public bool ProximoAVencer { get; set; }
    public bool Vencido { get; set; }
}

public class ProyectoDetalleDto : ProyectoResumenDto
{
    public string? Descripcion { get; set; }
    public DateTime? FechaCierreUtc { get; set; }
    public string CreadoPor { get; set; } = null!;
    public DateTime FechaCreacionUtc { get; set; }
    public DateTime? FechaActualizacionUtc { get; set; }
    public List<ParticipanteProyectoDto> Equipo { get; set; } = new();
    public string RowVersion { get; set; } = null!;
    // Lo que el usuario autenticado puede hacer sobre este proyecto (solo para mostrar u ocultar acciones; la API valida de nuevo).
    public bool PuedeModificar { get; set; }
    public bool PuedeAsignar { get; set; }
    public bool PuedeGestionarTareas { get; set; }
    public bool PuedeFinalizar { get; set; }
    public bool PuedeReabrir { get; set; }
    public bool PuedeCambiarResponsable { get; set; }
}

public class ProyectoFiltro : PageQuery
{
    public string? Buscar { get; set; }
    public Guid? EstadoProyectoId { get; set; }
    [Range(1, 4)] public byte? Prioridad { get; set; }
    /// <summary>Solo proyectos donde el usuario participa o es responsable.</summary>
    public bool SoloMios { get; set; }
    public bool SoloProximosAVencer { get; set; }
    public bool IncluirFinalizados { get; set; } = true;
}

public class ResumenEstadoDto
{
    public Guid EstadoProyectoId { get; set; }
    public string Nombre { get; set; } = null!;
    public bool EsFinal { get; set; }
    public int Total { get; set; }
}

public class ProyectosResumenDto
{
    public int Total { get; set; }
    public List<ResumenEstadoDto> PorEstado { get; set; } = new();
    public int ProximosAVencer { get; set; }
    public int Vencidos { get; set; }
    public int TareasVencidasMias { get; set; }
}

public class GuardarProyectoRequest : IValidatableObject
{
    [Required, StringLength(180, MinimumLength = 2)] public string Nombre { get; set; } = null!;
    [StringLength(4000)] public string? Descripcion { get; set; }
    public DateOnly? FechaInicio { get; set; }
    public DateOnly? FechaFinPrevista { get; set; }
    [Range(1, 4)] public byte Prioridad { get; set; } = PrioridadTrabajo.Media;
    /// <summary>Solo al crear: estado inicial (si es nulo, el primer estado activo no final). Para cambiar de estado use el cambio de estado.</summary>
    public Guid? EstadoProyectoId { get; set; }
    /// <summary>Solo al crear. Si es nulo, el responsable es el propio trabajador que lo crea.</summary>
    public Guid? ResponsableTrabajadorId { get; set; }
    /// <summary>Solo al editar: versión que el cliente vio.</summary>
    public string? RowVersion { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (FechaInicio is { } i && FechaFinPrevista is { } f && f < i)
            yield return new ValidationResult("La fecha de fin prevista no puede ser anterior a la de inicio.", new[] { nameof(FechaFinPrevista) });
    }
}

public class AsignarTrabajadoresRequest
{
    [Required, MinLength(1), MaxLength(100)] public List<Guid> TrabajadorIds { get; set; } = new();
    [StringLength(100)] public string? RolEnProyecto { get; set; }
}

public class CambiarResponsableRequest
{
    public Guid ResponsableTrabajadorId { get; set; }
    [Required] public string RowVersion { get; set; } = null!;
}

public class CambiarEstadoProyectoRequest
{
    public Guid EstadoProyectoId { get; set; }
    [StringLength(500)] public string? Comentario { get; set; }
    [Required] public string RowVersion { get; set; } = null!;
}

public class HistorialEstadoProyectoDto
{
    public long Id { get; set; }
    public string? EstadoAnterior { get; set; }
    public string EstadoNuevo { get; set; } = null!;
    public string CambiadoPor { get; set; } = null!;
    public DateTime FechaUtc { get; set; }
    public string? Comentario { get; set; }
}

// ---------- Tareas ----------

public class AsignadoTareaDto
{
    public Guid TrabajadorId { get; set; }
    public string Nombre { get; set; } = null!;
    public DateTime FechaAsignacionUtc { get; set; }
}

public class TareaResumenDto
{
    public Guid TareaId { get; set; }
    public Guid ProyectoId { get; set; }
    public string Proyecto { get; set; } = null!;
    public string Titulo { get; set; } = null!;
    /// <summary><see cref="EstadoTarea"/>: 1..6.</summary>
    public byte Estado { get; set; }
    public byte PorcentajeAvance { get; set; }
    public byte Prioridad { get; set; }
    public DateOnly? FechaInicio { get; set; }
    public DateOnly? FechaVencimiento { get; set; }
    /// <summary>Fecha límite anterior a hoy (zona de la empresa) y tarea aún abierta.</summary>
    public bool Vencida { get; set; }
    public List<string> Asignados { get; set; } = new();
}

public class TareaDetalleDto : TareaResumenDto
{
    public string? Descripcion { get; set; }
    public string CreadaPor { get; set; } = null!;
    public DateTime FechaCreacionUtc { get; set; }
    public DateTime? FechaActualizacionUtc { get; set; }
    public DateTime? FechaFinalizacionUtc { get; set; }
    public List<AsignadoTareaDto> Equipo { get; set; } = new();
    public string RowVersion { get; set; } = null!;
    public bool PuedeEditar { get; set; }
    public bool PuedeActualizarAvance { get; set; }
    public bool PuedeRevisar { get; set; }
    public bool PuedeComentar { get; set; }
    /// <summary>Estados a los que el usuario actual puede llevar la tarea desde el actual (validado en la API).</summary>
    public List<byte> TransicionesPermitidas { get; set; } = new();
}

public class TareaFiltro : PageQuery
{
    public string? Buscar { get; set; }
    [Range(1, 6)] public byte? Estado { get; set; }
    public Guid? TrabajadorId { get; set; }
    public bool SoloVencidas { get; set; }
    public bool IncluirCerradas { get; set; } = true;
}

public class MisTareasFiltro : PageQuery
{
    [Range(1, 6)] public byte? Estado { get; set; }
    public bool SoloVencidas { get; set; }
    public bool IncluirCerradas { get; set; }
}

public class GuardarTareaRequest : IValidatableObject
{
    [Required, StringLength(180, MinimumLength = 2)] public string Titulo { get; set; } = null!;
    [StringLength(4000)] public string? Descripcion { get; set; }
    public DateOnly? FechaInicio { get; set; }
    public DateOnly? FechaVencimiento { get; set; }
    [Range(1, 4)] public byte Prioridad { get; set; } = PrioridadTrabajo.Media;
    /// <summary>Solo al crear: trabajadores asignados desde el inicio (deben participar en el proyecto).</summary>
    public List<Guid>? TrabajadorIds { get; set; }
    /// <summary>Solo al editar: versión que el cliente vio.</summary>
    public string? RowVersion { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (FechaInicio is { } i && FechaVencimiento is { } f && f < i)
            yield return new ValidationResult("La fecha límite no puede ser anterior a la de inicio.", new[] { nameof(FechaVencimiento) });
    }
}

public class AsignarTareaRequest
{
    [Required, MinLength(1), MaxLength(50)] public List<Guid> TrabajadorIds { get; set; } = new();
}

public class ActualizarAvanceRequest
{
    [Range(0, 100)] public byte PorcentajeAvance { get; set; }
    /// <summary>Opcional. Sin valor, el estado se deduce del porcentaje (0 = pendiente, 1..99 = en proceso, 100 = en revisión).</summary>
    [Range(1, 6)] public byte? Estado { get; set; }
    [StringLength(2000)] public string? Comentario { get; set; }
    [Required] public string RowVersion { get; set; } = null!;
}

public class RevisarTareaRequest
{
    public bool Aprobar { get; set; }
    /// <summary>Obligatorio al devolver la tarea.</summary>
    [StringLength(2000)] public string? Comentario { get; set; }
    [Required] public string RowVersion { get; set; } = null!;
}

public class MotivoTareaRequest
{
    [Required, StringLength(500, MinimumLength = 3)] public string Motivo { get; set; } = null!;
    [Required] public string RowVersion { get; set; } = null!;
}

public class HistorialAvanceTareaDto
{
    public long Id { get; set; }
    public byte? PorcentajeAnterior { get; set; }
    public byte PorcentajeNuevo { get; set; }
    public byte? EstadoAnterior { get; set; }
    public byte EstadoNuevo { get; set; }
    public string ActualizadoPor { get; set; } = null!;
    public DateTime FechaUtc { get; set; }
    public string? Comentario { get; set; }
}

// ---------- Comentarios ----------

public class ComentarioTareaDto
{
    public Guid ComentarioId { get; set; }
    public Guid UsuarioId { get; set; }
    public string Autor { get; set; } = null!;
    public string Contenido { get; set; } = null!;
    public DateTime FechaCreacionUtc { get; set; }
    public Guid? ComentarioPadreId { get; set; }
    public List<string> Menciones { get; set; } = new();
}

public class PublicarComentarioRequest
{
    [Required, StringLength(4000, MinimumLength = 1)] public string Contenido { get; set; } = null!;
    public Guid? ComentarioPadreId { get; set; }
    /// <summary>Cuentas de usuario mencionadas; deben participar en el proyecto de la tarea.</summary>
    [MaxLength(20)] public List<Guid>? MencionesUsuarioIds { get; set; }
}

/// <summary>Quién puede ser mencionado o consultado en una tarea (equipo del proyecto con cuenta de usuario).</summary>
public class UsuarioMencionableDto
{
    public Guid UsuarioId { get; set; }
    public string Nombre { get; set; } = null!;
}

