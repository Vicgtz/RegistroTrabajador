
namespace RegistroTrabajo.Data.Entities;

public class MencionComentario
{
    public long MencionComentarioId { get; set; }

    public Guid EmpresaId { get; set; }

    public Guid ComentarioId { get; set; }

    public Guid UsuarioMencionadoId { get; set; }

    public DateTime FechaCreacionUtc { get; set; }

    public Comentario Comentario { get; set; } = null!;

    public Usuario Usuario { get; set; } = null!;
}
