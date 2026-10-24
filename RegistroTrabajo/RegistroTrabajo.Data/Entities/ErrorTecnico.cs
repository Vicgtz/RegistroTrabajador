
namespace RegistroTrabajo.Data.Entities;

public class ErrorTecnico
{
    public long ErrorTecnicoId { get; set; }

    public Guid? EmpresaId { get; set; }

    public Guid? UsuarioId { get; set; }

    public DateTime FechaUtc { get; set; }

    public string Nivel { get; set; } = null!;

    public string? Codigo { get; set; }

    public string Mensaje { get; set; } = null!;

    public Guid? CorrelationId { get; set; }

    public string? Ruta { get; set; }

    public string? DetalleTecnico { get; set; }

    public Empresa? Empresa { get; set; }

    public Usuario? Usuario { get; set; }
}
