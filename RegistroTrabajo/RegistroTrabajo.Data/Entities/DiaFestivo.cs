
namespace RegistroTrabajo.Data.Entities;

public class DiaFestivo
{
    public Guid DiaFestivoId { get; set; }

    public Guid EmpresaId { get; set; }

    public DateOnly Fecha { get; set; }

    public string Nombre { get; set; } = null!;

    public bool EsRecurrenteAnual { get; set; }

    public Empresa Empresa { get; set; } = null!;
}
