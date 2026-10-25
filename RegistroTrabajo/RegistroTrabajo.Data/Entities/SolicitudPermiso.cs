
namespace RegistroTrabajo.Data.Entities;

public class SolicitudPermiso
{
    public Guid SolicitudPermisoId { get; set; }

    public Guid EmpresaId { get; set; }

    public Guid TrabajadorId { get; set; }

    public Guid TipoPermisoId { get; set; }

    public DateTime FechaInicio { get; set; }

    public DateTime FechaFin { get; set; }

    public bool EsPorHoras { get; set; }

    public decimal CantidadSolicitada { get; set; }

    public string? MotivoPrivado { get; set; }

    public byte Estado { get; set; }

    public DateTime SolicitadaUtc { get; set; }

    public DateTime? ModificadaUtc { get; set; }

    /// <summary>Clave enviada por el cliente para que un reintento no cree la solicitud dos veces (script 005).</summary>
    public string? ClaveIdempotencia { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public ICollection<AprobacionPermiso> AprobacionesPermiso { get; set; } = new List<AprobacionPermiso>();

    public ICollection<MovimientoSaldoPermiso> MovimientosSaldoPermiso { get; set; } = new List<MovimientoSaldoPermiso>();

    public ICollection<SolicitudCancelacionPermiso> SolicitudesCancelacionPermiso { get; set; } = new List<SolicitudCancelacionPermiso>();

    public TipoPermiso TipoPermiso { get; set; } = null!;

    public Trabajador Trabajador { get; set; } = null!;
}
