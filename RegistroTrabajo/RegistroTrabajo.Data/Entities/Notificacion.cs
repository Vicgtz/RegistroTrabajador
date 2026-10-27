
namespace RegistroTrabajo.Data.Entities;

public class Notificacion
{
    public Guid NotificacionId { get; set; }

    public Guid EmpresaId { get; set; }

    public Guid UsuarioDestinoId { get; set; }

    public string Tipo { get; set; } = null!;

    public string Titulo { get; set; } = null!;

    public string Mensaje { get; set; } = null!;

    public string? EntidadTipo { get; set; }

    public string? EntidadId { get; set; }

    public DateTime CreadaUtc { get; set; }

    public DateTime? LeidaUtc { get; set; }

    public DateTime? ArchivadaUtc { get; set; }

    /// <summary>Bloque 9 (script 010): identifica el evento de origen para no avisar dos veces al mismo usuario.</summary>
    public string? ClaveDeduplicacion { get; set; }

    public Usuario Usuario { get; set; } = null!;
}
