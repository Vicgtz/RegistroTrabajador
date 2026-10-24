
namespace RegistroTrabajo.Data.Entities;

public class Turno
{
    public Guid TurnoId { get; set; }

    public Guid EmpresaId { get; set; }

    public string Nombre { get; set; } = null!;

    public TimeOnly HoraInicio { get; set; }

    public TimeOnly HoraFin { get; set; }

    public bool CruzaMedianoche { get; set; }

    public short ToleranciaMinutos { get; set; }

    public bool Estado { get; set; }

    public Empresa Empresa { get; set; } = null!;

    public ICollection<HorarioLaboral> HorariosLaborales { get; set; } = new List<HorarioLaboral>();
}
