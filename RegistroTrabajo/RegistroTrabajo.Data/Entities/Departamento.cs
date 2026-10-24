
namespace RegistroTrabajo.Data.Entities;

public class Departamento
{
    public Guid DepartamentoId { get; set; }

    public Guid EmpresaId { get; set; }

    public string Nombre { get; set; } = null!;

    public string? Descripcion { get; set; }

    public bool Estado { get; set; }

    public Empresa Empresa { get; set; } = null!;

    public ICollection<HorarioLaboral> HorariosLaborales { get; set; } = new List<HorarioLaboral>();

    public ICollection<TrabajadorSupervisor> TrabajadorSupervisores { get; set; } = new List<TrabajadorSupervisor>();

    public ICollection<Trabajador> Trabajadores { get; set; } = new List<Trabajador>();
}
