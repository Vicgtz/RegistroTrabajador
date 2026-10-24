
namespace RegistroTrabajo.Data.Entities;

public class AsignacionProyecto
{
    public Guid AsignacionProyectoId { get; set; }

    public Guid EmpresaId { get; set; }

    public Guid ProyectoId { get; set; }

    public Guid TrabajadorId { get; set; }

    public DateOnly FechaAsignacion { get; set; }

    public DateOnly? FechaSalida { get; set; }

    public Guid AsignadoPorUsuarioId { get; set; }

    public string? RolEnProyecto { get; set; }

    public Proyecto Proyecto { get; set; } = null!;

    public Trabajador Trabajador { get; set; } = null!;

    public Usuario Usuario { get; set; } = null!;
}
