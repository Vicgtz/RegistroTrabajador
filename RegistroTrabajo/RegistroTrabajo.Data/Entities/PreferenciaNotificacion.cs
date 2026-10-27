
namespace RegistroTrabajo.Data.Entities;

public class PreferenciaNotificacion
{
    public long PreferenciaNotificacionId { get; set; }

    public Guid EmpresaId { get; set; }

    public Guid UsuarioId { get; set; }

    public string TipoNotificacion { get; set; } = null!;

    public bool Habilitada { get; set; }

    /// <summary>Bloque 9 (script 010): el usuario quiere ver estos avisos como prioritarios.</summary>
    public bool Prioritaria { get; set; }

    public Usuario Usuario { get; set; } = null!;
}
