
namespace RegistroTrabajo.Data.Entities;

public class MovimientoSaldoPermiso
{
    public long MovimientoSaldoPermisoId { get; set; }

    public Guid EmpresaId { get; set; }

    public Guid SaldoPermisoId { get; set; }

    public Guid? SolicitudPermisoId { get; set; }

    public byte TipoMovimiento { get; set; }

    public decimal Cantidad { get; set; }

    public string? Descripcion { get; set; }

    public Guid? RegistradoPorUsuarioId { get; set; }

    public DateTime FechaUtc { get; set; }

    public SaldoPermiso SaldoPermiso { get; set; } = null!;

    public SolicitudPermiso? SolicitudPermiso { get; set; }

    public Usuario? Usuario { get; set; }
}
