
namespace RegistroTrabajo.Data.Entities;

public class TrabajadorSupervisor
{
    public long TrabajadorSupervisorId { get; set; }

    public Guid EmpresaId { get; set; }

    public Guid TrabajadorId { get; set; }

    public Guid SupervisorTrabajadorId { get; set; }

    public Guid? DepartamentoId { get; set; }

    public DateOnly FechaInicio { get; set; }

    public DateOnly? FechaFin { get; set; }

    public bool EsPrincipal { get; set; }

    public Departamento? Departamento { get; set; }

    public Trabajador Trabajador { get; set; } = null!;

    public Trabajador TrabajadoresNavigation { get; set; } = null!;
}
