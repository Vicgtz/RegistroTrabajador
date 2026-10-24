
namespace RegistroTrabajo.Data.Entities;

public class CorreccionAsistencia
{
    public long CorreccionAsistenciaId { get; set; }

    public Guid EmpresaId { get; set; }

    public Guid RegistroAsistenciaId { get; set; }

    public Guid CorregidoPorUsuarioId { get; set; }

    public DateTime FechaCorreccionUtc { get; set; }

    public DateTime? EntradaAnteriorUtc { get; set; }

    public DateTime? SalidaAnteriorUtc { get; set; }

    public DateTime? EntradaNuevaUtc { get; set; }

    public DateTime? SalidaNuevaUtc { get; set; }

    public string Motivo { get; set; } = null!;

    public RegistroAsistencia RegistroAsistencia { get; set; } = null!;

    public Usuario Usuario { get; set; } = null!;
}
