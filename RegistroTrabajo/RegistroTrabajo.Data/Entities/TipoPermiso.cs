
namespace RegistroTrabajo.Data.Entities;

public class TipoPermiso
{
    public Guid TipoPermisoId { get; set; }

    public Guid EmpresaId { get; set; }

    public string Nombre { get; set; } = null!;

    public string? Descripcion { get; set; }

    public byte UnidadBase { get; set; }

    public bool RequiereAprobacion { get; set; }

    public bool RequiereComprobante { get; set; }

    public bool PermiteFraccionar { get; set; }

    public bool Estado { get; set; }

    public Empresa Empresa { get; set; } = null!;

    public ICollection<ReglaPermiso> ReglasPermiso { get; set; } = new List<ReglaPermiso>();

    public ICollection<SaldoPermiso> SaldosPermiso { get; set; } = new List<SaldoPermiso>();

    public ICollection<SolicitudPermiso> SolicitudesPermiso { get; set; } = new List<SolicitudPermiso>();
}
