
namespace RegistroTrabajo.Data.Entities;

public class PreferenciaNotificacion
{
    public long PreferenciaNotificacionId { get; set; }

    public Guid EmpresaId { get; set; }

    public Guid UsuarioId { get; set; }

    public string TipoNotificacion { get; set; } = null!;

    public bool Habilitada { get; set; }

    public Usuario Usuario { get; set; } = null!;
}
