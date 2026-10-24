
namespace RegistroTrabajo.Data.Entities;

public class OperacionRespaldo
{
    public Guid OperacionRespaldoId { get; set; }

    public Guid? EmpresaId { get; set; }

    public Guid EjecutadaPorUsuarioId { get; set; }

    public byte TipoOperacion { get; set; }

    public byte Estado { get; set; }

    public string? UbicacionArchivo { get; set; }

    public long? TamanoBytes { get; set; }

    public DateTime FechaInicioUtc { get; set; }

    public DateTime? FechaFinUtc { get; set; }

    public string? Mensaje { get; set; }

    public Usuario EjecutadaPorUsuario { get; set; } = null!;

    public Empresa? Empresa { get; set; }
}
