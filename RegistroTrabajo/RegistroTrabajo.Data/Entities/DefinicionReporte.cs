
namespace RegistroTrabajo.Data.Entities;

public class DefinicionReporte
{
    public Guid DefinicionReporteId { get; set; }

    public Guid EmpresaId { get; set; }

    public string Nombre { get; set; } = null!;

    public string? Descripcion { get; set; }

    public string TipoReporte { get; set; } = null!;

    public string ConfiguracionJson { get; set; } = null!;

    public Guid CreadoPorUsuarioId { get; set; }

    public DateTime FechaCreacionUtc { get; set; }

    public bool Estado { get; set; }

    public Empresa Empresa { get; set; } = null!;

    public Usuario Usuario { get; set; } = null!;
}
