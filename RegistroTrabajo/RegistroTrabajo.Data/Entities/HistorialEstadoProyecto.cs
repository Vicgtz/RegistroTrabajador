
namespace RegistroTrabajo.Data.Entities;

public class HistorialEstadoProyecto
{
    public long HistorialEstadoProyectoId { get; set; }

    public Guid EmpresaId { get; set; }

    public Guid ProyectoId { get; set; }

    public Guid? EstadoAnteriorId { get; set; }

    public Guid EstadoNuevoId { get; set; }

    public Guid CambiadoPorUsuarioId { get; set; }

    public DateTime FechaUtc { get; set; }

    public string? Comentario { get; set; }

    public EstadoProyecto? EstadoProyecto { get; set; }

    public EstadoProyecto EstadosProyectoNavigation { get; set; } = null!;

    public Proyecto Proyecto { get; set; } = null!;

    public Usuario Usuario { get; set; } = null!;
}
