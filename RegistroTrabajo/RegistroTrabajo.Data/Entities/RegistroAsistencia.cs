
namespace RegistroTrabajo.Data.Entities;

public class RegistroAsistencia
{
    public Guid RegistroAsistenciaId { get; set; }

    public Guid EmpresaId { get; set; }

    public Guid TrabajadorId { get; set; }

    public DateOnly FechaLaboral { get; set; }

    public DateTime? EntradaUtc { get; set; }

    public DateTime? SalidaUtc { get; set; }

    public decimal? EntradaLatitud { get; set; }

    public decimal? EntradaLongitud { get; set; }

    public decimal? SalidaLatitud { get; set; }

    public decimal? SalidaLongitud { get; set; }

    public Guid? EntradaRegistradaPorUsuarioId { get; set; }

    public Guid? SalidaRegistradaPorUsuarioId { get; set; }

    public byte Estado { get; set; }

    public string? Observaciones { get; set; }

    public DateTime FechaCreacionUtc { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public ICollection<CorreccionAsistencia> CorreccionesAsistencia { get; set; } = new List<CorreccionAsistencia>();

    public Trabajador Trabajador { get; set; } = null!;

    public Usuario? Usuario { get; set; }

    public Usuario? UsuariosNavigation { get; set; }
}
