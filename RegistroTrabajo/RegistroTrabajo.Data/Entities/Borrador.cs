
namespace RegistroTrabajo.Data.Entities;

public class Borrador
{
    public Guid BorradorId { get; set; }

    public Guid EmpresaId { get; set; }

    public Guid UsuarioCreadorId { get; set; }

    public string Modulo { get; set; } = null!;

    public string TipoFormulario { get; set; } = null!;

    public string DatosJson { get; set; } = null!;

    public DateTime FechaCreacionUtc { get; set; }

    public DateTime FechaActualizacionUtc { get; set; }

    public DateTime? ExpiraUtc { get; set; }

    public Usuario Usuario { get; set; } = null!;
}
