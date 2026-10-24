
namespace RegistroTrabajo.Data.Entities;

public class HorarioLaboral
{
    public Guid HorarioLaboralId { get; set; }

    public Guid EmpresaId { get; set; }

    public string Nombre { get; set; } = null!;

    public Guid? DepartamentoId { get; set; }

    public Guid? PuestoId { get; set; }

    public Guid? TurnoId { get; set; }

    public DateOnly FechaVigenciaDesde { get; set; }

    public DateOnly? FechaVigenciaHasta { get; set; }

    public bool Estado { get; set; }

    public ICollection<AsignacionHorario> AsignacionesHorario { get; set; } = new List<AsignacionHorario>();

    public Departamento? Departamento { get; set; }

    public Empresa Empresa { get; set; } = null!;

    public ICollection<HorarioDetalleDia> HorarioDetalleDia { get; set; } = new List<HorarioDetalleDia>();

    public Puesto? Puesto { get; set; }

    public Turno? Turno { get; set; }
}
