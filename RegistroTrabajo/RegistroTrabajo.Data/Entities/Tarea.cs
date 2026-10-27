
namespace RegistroTrabajo.Data.Entities;

public class Tarea
{
    public Guid TareaId { get; set; }

    public Guid EmpresaId { get; set; }

    public Guid ProyectoId { get; set; }

    public string Titulo { get; set; } = null!;

    public string? Descripcion { get; set; }

    public DateOnly? FechaInicio { get; set; }

    public DateOnly? FechaVencimiento { get; set; }

    public byte Estado { get; set; }

    public byte PorcentajeAvance { get; set; }

    public byte Prioridad { get; set; } = 2;

    public Guid CreadaPorUsuarioId { get; set; }

    public Guid? ResponsableTrabajadorId { get; set; }

    public DateTime? FechaFinalizacionUtc { get; set; }

    public DateTime FechaCreacionUtc { get; set; }

    public DateTime? FechaActualizacionUtc { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public ICollection<AsignacionTarea> AsignacionesTarea { get; set; } = new List<AsignacionTarea>();

    public ICollection<Comentario> Comentarios { get; set; } = new List<Comentario>();

    public ICollection<HistorialAvanceTarea> HistorialAvanceTarea { get; set; } = new List<HistorialAvanceTarea>();

    public Proyecto Proyecto { get; set; } = null!;

    public Trabajador? Trabajador { get; set; }

    public Usuario Usuario { get; set; } = null!;
}
