
namespace RegistroTrabajo.Data.Entities;

public class SaldoPermiso
{
    public Guid SaldoPermisoId { get; set; }

    public Guid EmpresaId { get; set; }

    public Guid TrabajadorId { get; set; }

    public Guid TipoPermisoId { get; set; }

    public short PeriodoAnio { get; set; }

    public decimal CantidadAsignada { get; set; }

    public decimal CantidadUtilizada { get; set; }

    public decimal CantidadReservada { get; set; }

    public DateTime FechaActualizacionUtc { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public ICollection<MovimientoSaldoPermiso> MovimientosSaldoPermiso { get; set; } = new List<MovimientoSaldoPermiso>();

    public TipoPermiso TipoPermiso { get; set; } = null!;

    public Trabajador Trabajador { get; set; } = null!;
}
