
namespace RegistroTrabajo.Data.Entities;

public class EstadoProyecto
{
    public Guid EstadoProyectoId { get; set; }

    public Guid EmpresaId { get; set; }

    public string Nombre { get; set; } = null!;

    public short Orden { get; set; }

    public bool EsFinal { get; set; }

    public bool Estado { get; set; }

    public Empresa Empresa { get; set; } = null!;

    public ICollection<HistorialEstadoProyecto> HistorialEstadoProyectoEstadosProyecto { get; set; } = new List<HistorialEstadoProyecto>();

    public ICollection<HistorialEstadoProyecto> HistorialEstadoProyectoEstadosProyectoNavigation { get; set; } = new List<HistorialEstadoProyecto>();

    public ICollection<Proyecto> Proyectos { get; set; } = new List<Proyecto>();
}
