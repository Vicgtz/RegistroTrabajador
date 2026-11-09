namespace RegistroTrabajo.Data.Entities;

/// <summary>
/// Bloque 14: estudio de un trabajador. Tipo 1 Técnico, 2 Licenciatura, 3 Maestría, 4 Doctorado, 5 Certificado, 6 Curso.
/// Estado 1 Aprobado, 2 Pendiente (propuesto por el trabajador), 3 Rechazado.
/// </summary>
public class TrabajadorEstudio
{
    public Guid EstudioId { get; set; }
    public Guid EmpresaId { get; set; }
    public Guid TrabajadorId { get; set; }
    public byte Tipo { get; set; }
    public string Titulo { get; set; } = null!;
    public string? Institucion { get; set; }
    public DateOnly? FechaInicio { get; set; }
    public DateOnly? FechaFin { get; set; }
    public bool EnCurso { get; set; }
    public string? Notas { get; set; }
    public byte Estado { get; set; } = 1;
    public string? MotivoRechazo { get; set; }
    public Guid? ResueltoPorUsuarioId { get; set; }
    public DateTime? ResueltoUtc { get; set; }
    public DateTime FechaCreacionUtc { get; set; }
    public DateTime FechaActualizacionUtc { get; set; }

    public Trabajador Trabajador { get; set; } = null!;
}
