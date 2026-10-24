
namespace RegistroTrabajo.Data.Entities;

public class Comentario
{
    public Guid ComentarioId { get; set; }

    public Guid EmpresaId { get; set; }

    public Guid? ProyectoId { get; set; }

    public Guid? TareaId { get; set; }

    public Guid UsuarioId { get; set; }

    public Guid? ComentarioPadreId { get; set; }

    public string Contenido { get; set; } = null!;

    public DateTime FechaCreacionUtc { get; set; }

    public Comentario? ComentariosNavigation { get; set; }

    public ICollection<Comentario> InverseComentariosNavigation { get; set; } = new List<Comentario>();

    public ICollection<MencionComentario> MencionesComentario { get; set; } = new List<MencionComentario>();

    public Proyecto? Proyecto { get; set; }

    public Tarea? Tarea { get; set; }

    public Usuario Usuario { get; set; } = null!;
}
