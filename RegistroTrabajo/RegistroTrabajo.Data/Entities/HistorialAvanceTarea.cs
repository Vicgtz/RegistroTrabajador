
namespace RegistroTrabajo.Data.Entities;

public class HistorialAvanceTarea
{
    public long HistorialAvanceTareaId { get; set; }

    public Guid EmpresaId { get; set; }

    public Guid TareaId { get; set; }

    public byte? PorcentajeAnterior { get; set; }

    public byte PorcentajeNuevo { get; set; }

    public byte? EstadoAnterior { get; set; }

    public byte EstadoNuevo { get; set; }

    public Guid ActualizadoPorUsuarioId { get; set; }

    public DateTime FechaUtc { get; set; }

    public string? Comentario { get; set; }

    public Tarea Tarea { get; set; } = null!;

    public Usuario Usuario { get; set; } = null!;
}
