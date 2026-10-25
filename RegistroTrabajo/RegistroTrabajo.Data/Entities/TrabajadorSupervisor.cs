
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

    /// <summary>Quien supervisa (FK SupervisorTrabajadorId).</summary>
    public Trabajador Supervisor { get; set; } = null!;

    /// <summary>Quien es supervisado (FK TrabajadorId).</summary>
    public Trabajador Supervisado { get; set; } = null!;
}
