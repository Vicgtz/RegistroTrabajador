
namespace RegistroTrabajo.Data.Entities;

public class ContactoEmergencia
{
    public Guid ContactoEmergenciaId { get; set; }

    public Guid EmpresaId { get; set; }

    public Guid TrabajadorId { get; set; }

    public string NombreCompleto { get; set; } = null!;

    public string? Parentesco { get; set; }

    public string Telefono { get; set; } = null!;

    public string? TelefonoAlternativo { get; set; }

    public bool EsPrincipal { get; set; }

    public Trabajador Trabajador { get; set; } = null!;
}
