namespace RegistroTrabajo.Data.Entities;

/// <summary>Bloque 14: currículum del trabajador (PDF, DOC o DOCX, máx. 5 MB), uno por trabajador, guardado en la BD.</summary>
public class CurriculumTrabajador
{
    public Guid TrabajadorId { get; set; }
    public Guid EmpresaId { get; set; }
    public string NombreArchivo { get; set; } = null!;
    public string TipoContenido { get; set; } = null!;
    public byte[] Contenido { get; set; } = null!;
    public int TamanoBytes { get; set; }
    public string HashSha256 { get; set; } = null!;
    public DateTime FechaActualizacionUtc { get; set; }

    public Trabajador Trabajador { get; set; } = null!;
}
