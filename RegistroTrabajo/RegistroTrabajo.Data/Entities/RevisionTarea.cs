namespace RegistroTrabajo.Data.Entities;

/// <summary>Revisión del responsable sobre una tarea enviada a revisión (Bloque 8). Resultado: 1 Aprobada, 2 Devuelta.</summary>
public class RevisionTarea
{
    public long RevisionTareaId { get; set; }

    public Guid EmpresaId { get; set; }

    public Guid TareaId { get; set; }

    public Guid RevisorUsuarioId { get; set; }

    public byte Resultado { get; set; }

    public string? Observacion { get; set; }

    public byte PorcentajeAlRevisar { get; set; }

    public DateTime FechaUtc { get; set; }

    public Tarea Tarea { get; set; } = null!;

    public Usuario Revisor { get; set; } = null!;
}
