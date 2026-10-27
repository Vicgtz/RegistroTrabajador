namespace RegistroTrabajo.Data.Entities;

/// <summary>Operación importante sobre una tarea (Bloque 8). Solo se inserta; nunca se modifica desde la aplicación.</summary>
public class EventoSeguimientoTarea
{
    public long EventoSeguimientoTareaId { get; set; }

    public Guid EmpresaId { get; set; }

    public Guid TareaId { get; set; }

    public string Tipo { get; set; } = null!;

    public Guid UsuarioId { get; set; }

    public DateTime FechaUtc { get; set; }

    public string? ValorAnteriorJson { get; set; }

    public string? ValorNuevoJson { get; set; }

    public string? Detalle { get; set; }

    public Tarea Tarea { get; set; } = null!;

    public Usuario Usuario { get; set; } = null!;
}
