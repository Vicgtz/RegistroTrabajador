
namespace RegistroTrabajo.Data.Entities;

public class Proyecto
{
    public Guid ProyectoId { get; set; }

    public Guid EmpresaId { get; set; }

    public string Nombre { get; set; } = null!;

    public string? Descripcion { get; set; }

    public DateOnly? FechaInicio { get; set; }

    public DateOnly? FechaFinPrevista { get; set; }

    public DateTime? FechaCierreUtc { get; set; }

    public Guid EstadoProyectoId { get; set; }

    public byte Prioridad { get; set; }

    public Guid ResponsableTrabajadorId { get; set; }

    public Guid CreadoPorUsuarioId { get; set; }

    public DateTime FechaCreacionUtc { get; set; }

    public DateTime? FechaActualizacionUtc { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public ICollection<AsignacionProyecto> AsignacionesProyecto { get; set; } = new List<AsignacionProyecto>();

    public ICollection<Comentario> Comentarios { get; set; } = new List<Comentario>();

    public Empresa Empresa { get; set; } = null!;

    public EstadoProyecto EstadoProyecto { get; set; } = null!;

    public ICollection<HistorialEstadoProyecto> HistorialEstadoProyecto { get; set; } = new List<HistorialEstadoProyecto>();

    public ICollection<Tarea> Tareas { get; set; } = new List<Tarea>();

    public Trabajador Trabajador { get; set; } = null!;

    public Usuario Usuario { get; set; } = null!;
}
