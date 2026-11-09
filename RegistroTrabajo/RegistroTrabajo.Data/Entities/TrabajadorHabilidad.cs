namespace RegistroTrabajo.Data.Entities;

/// <summary>Bloque 14: habilidad de un trabajador. Estado 1 Aprobado, 2 Pendiente (propuesto por el trabajador), 3 Rechazado.</summary>
public class TrabajadorHabilidad
{
    public Guid TrabajadorHabilidadId { get; set; }
    public Guid EmpresaId { get; set; }
    public Guid TrabajadorId { get; set; }
    public Guid HabilidadId { get; set; }
    /// <summary>1 Básico, 2 Intermedio, 3 Avanzado.</summary>
    public byte Nivel { get; set; }
    public byte? AniosExperiencia { get; set; }
    public string? Notas { get; set; }
    public byte Estado { get; set; } = 1;
    public string? MotivoRechazo { get; set; }
    public Guid? ResueltoPorUsuarioId { get; set; }
    public DateTime? ResueltoUtc { get; set; }
    public DateTime FechaCreacionUtc { get; set; }
    public DateTime FechaActualizacionUtc { get; set; }

    public Trabajador Trabajador { get; set; } = null!;
    public Habilidad Habilidad { get; set; } = null!;
}
