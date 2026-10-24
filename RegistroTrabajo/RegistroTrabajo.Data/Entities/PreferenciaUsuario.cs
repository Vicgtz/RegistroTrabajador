
namespace RegistroTrabajo.Data.Entities;

public class PreferenciaUsuario
{
    public Guid EmpresaId { get; set; }

    public Guid UsuarioId { get; set; }

    public string Tema { get; set; } = null!;

    public string? Cultura { get; set; }

    public string? ZonaHorariaId { get; set; }

    public string? PreferenciasJson { get; set; }

    public DateTime FechaActualizacionUtc { get; set; }

    public Usuario Usuario { get; set; } = null!;
}
