
namespace RegistroTrabajo.Data.Entities;

public class AsignacionTarea
{
    public Guid AsignacionTareaId { get; set; }

    public Guid EmpresaId { get; set; }

    public Guid TareaId { get; set; }

    public Guid TrabajadorId { get; set; }

    public DateTime FechaAsignacionUtc { get; set; }

    public DateTime? FechaRetiroUtc { get; set; }

    public Guid AsignadoPorUsuarioId { get; set; }

    public Tarea Tarea { get; set; } = null!;

    public Trabajador Trabajador { get; set; } = null!;

    public Usuario Usuario { get; set; } = null!;
}
