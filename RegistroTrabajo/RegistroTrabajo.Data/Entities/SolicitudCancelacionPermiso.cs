
namespace RegistroTrabajo.Data.Entities;

public class SolicitudCancelacionPermiso
{
    public Guid SolicitudCancelacionId { get; set; }

    public Guid EmpresaId { get; set; }

    public Guid SolicitudPermisoId { get; set; }

    public Guid SolicitadaPorUsuarioId { get; set; }

    public string? Motivo { get; set; }

    public byte Estado { get; set; }

    public DateTime SolicitadaUtc { get; set; }

    public Guid? RevisadaPorUsuarioId { get; set; }

    public DateTime? RevisadaUtc { get; set; }

    public string? ComentarioRevision { get; set; }

    public SolicitudPermiso SolicitudPermiso { get; set; } = null!;

    public Usuario? Usuario { get; set; }

    public Usuario UsuariosNavigation { get; set; } = null!;
}
