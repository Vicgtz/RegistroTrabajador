
namespace RegistroTrabajo.Data.Entities;

public class SolicitudCambioPerfil
{
    public Guid SolicitudCambioPerfilId { get; set; }

    public Guid EmpresaId { get; set; }

    public Guid TrabajadorId { get; set; }

    public string DatosSolicitadosJson { get; set; } = null!;

    public byte Estado { get; set; }

    public DateTime SolicitadaUtc { get; set; }

    public Guid? RevisadaPorUsuarioId { get; set; }

    public DateTime? RevisadaUtc { get; set; }

    public string? ComentarioRevision { get; set; }

    public Trabajador Trabajador { get; set; } = null!;

    public Usuario? Usuario { get; set; }
}
