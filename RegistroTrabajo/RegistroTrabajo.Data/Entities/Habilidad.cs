namespace RegistroTrabajo.Data.Entities;

/// <summary>Bloque 14 (script 017): catálogo de habilidades por empresa. El nombre normalizado es único por empresa.</summary>
public class Habilidad
{
    public Guid HabilidadId { get; set; }
    public Guid EmpresaId { get; set; }
    public string Nombre { get; set; } = null!;
    public string NombreNormalizado { get; set; } = null!;
    public bool Activa { get; set; } = true;
    public DateTime FechaCreacionUtc { get; set; }
}
