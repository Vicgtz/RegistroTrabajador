
namespace RegistroTrabajo.Data.Entities;

public class HorarioDetalleDia
{
    public long HorarioDetalleDiaId { get; set; }

    public Guid EmpresaId { get; set; }

    public Guid HorarioLaboralId { get; set; }

    public byte DiaSemana { get; set; }

    public TimeOnly? HoraEntrada { get; set; }

    public TimeOnly? HoraSalida { get; set; }

    public TimeOnly? InicioDescanso { get; set; }

    public TimeOnly? FinDescanso { get; set; }

    public bool EsDiaLaboral { get; set; }

    public HorarioLaboral HorarioLaboral { get; set; } = null!;
}
