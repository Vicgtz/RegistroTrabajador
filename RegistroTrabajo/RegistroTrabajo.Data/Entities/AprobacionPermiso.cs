
namespace RegistroTrabajo.Data.Entities;

public class AprobacionPermiso
{
    public long AprobacionPermisoId { get; set; }

    public Guid EmpresaId { get; set; }

    public Guid SolicitudPermisoId { get; set; }

    public Guid RevisorUsuarioId { get; set; }

    public short Nivel { get; set; }

    public byte Decision { get; set; }

    public string? Comentario { get; set; }

    public DateTime FechaDecisionUtc { get; set; }

    public SolicitudPermiso SolicitudPermiso { get; set; } = null!;

    public Usuario Usuario { get; set; } = null!;
}
