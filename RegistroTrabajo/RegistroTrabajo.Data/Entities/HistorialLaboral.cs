
namespace RegistroTrabajo.Data.Entities;

public class HistorialLaboral
{
    public long HistorialLaboralId { get; set; }

    public Guid EmpresaId { get; set; }

    public Guid TrabajadorId { get; set; }

    public DateOnly FechaEfectiva { get; set; }

    public string TipoCambio { get; set; } = null!;

    public string? DatosAntesJson { get; set; }

    public string? DatosDespuesJson { get; set; }

    public string? Motivo { get; set; }

    public Guid? RegistradoPorUsuarioId { get; set; }

    public DateTime RegistradoUtc { get; set; }

    public Trabajador Trabajador { get; set; } = null!;

    public Usuario? Usuario { get; set; }
}
