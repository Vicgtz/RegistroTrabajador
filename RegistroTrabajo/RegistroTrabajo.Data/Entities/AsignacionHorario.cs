
namespace RegistroTrabajo.Data.Entities;

public class AsignacionHorario
{
    public Guid AsignacionHorarioId { get; set; }

    public Guid EmpresaId { get; set; }

    public Guid TrabajadorId { get; set; }

    public Guid HorarioLaboralId { get; set; }

    public DateOnly FechaDesde { get; set; }

    public DateOnly? FechaHasta { get; set; }

    public string? MotivoExcepcion { get; set; }

    public Guid? AsignadoPorUsuarioId { get; set; }

    public HorarioLaboral HorarioLaboral { get; set; } = null!;

    public Trabajador Trabajador { get; set; } = null!;

    public Usuario? Usuario { get; set; }
}
