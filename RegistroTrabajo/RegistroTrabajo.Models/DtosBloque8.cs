using System.ComponentModel.DataAnnotations;

namespace RegistroTrabajo.Models;

// ---------- Línea de tiempo y revisiones ----------

/// <summary>Una entrada de la línea de tiempo de la tarea: evento de seguimiento o actualización de avance.</summary>
public class EventoSeguimientoDto
{
    /// <summary><see cref="TipoEventoTarea"/> (incluye "avance_actualizado" para las filas de HistorialAvanceTarea).</summary>
    public string Tipo { get; set; } = null!;
    public string Usuario { get; set; } = null!;
    public DateTime FechaUtc { get; set; }
    public string? ValorAnterior { get; set; }
    public string? ValorNuevo { get; set; }
    public string? Detalle { get; set; }
    public byte? PorcentajeAnterior { get; set; }
    public byte? PorcentajeNuevo { get; set; }
    public byte? EstadoAnterior { get; set; }
    public byte? EstadoNuevo { get; set; }
}

public class RevisionTareaDto
{
    public long RevisionTareaId { get; set; }
    /// <summary><see cref="ResultadoRevision"/>: 1 Aprobada, 2 Devuelta.</summary>
    public byte Resultado { get; set; }
    public string Revisor { get; set; } = null!;
    public string? Observacion { get; set; }
    public byte PorcentajeAlRevisar { get; set; }
    public DateTime FechaUtc { get; set; }
}

// ---------- Panel de seguimiento ----------

public class SeguimientoFiltro
{
    public Guid? TrabajadorId { get; set; }
    public Guid? ProyectoId { get; set; }
}

public class TareaSeguimientoDto : TareaResumenDto
{
    /// <summary>Días hábiles transcurridos desde la fecha límite (solo tareas vencidas).</summary>
    public int? DiasRetraso { get; set; }
    /// <summary>Días hábiles que faltan para la fecha límite (solo próximas a vencer).</summary>
    public int? DiasParaVencer { get; set; }
    public DateTime UltimaActualizacionUtc { get; set; }
    public int DiasHabilesSinActualizar { get; set; }
    /// <summary>Quien puede revisar esta tarea con sus permisos actuales.</summary>
    public bool PuedeRevisar { get; set; }
    public string RowVersion { get; set; } = null!;
}

public class PanelSeguimientoDto
{
    public int TotalVencidas { get; set; }
    public int TotalProximasAVencer { get; set; }
    public int TotalPendientesRevision { get; set; }
    public int TotalReabiertas { get; set; }
    public int TotalSinActualizacion { get; set; }
    public byte DiasAlertaVencimiento { get; set; }
    public short? DiasSinActualizacion { get; set; }
    /// <summary>true cuando el usuario ve el panel de su equipo; false cuando solo ve sus propias tareas.</summary>
    public bool VerEquipo { get; set; }
    public List<TareaSeguimientoDto> Vencidas { get; set; } = new();
    public List<TareaSeguimientoDto> ProximasAVencer { get; set; } = new();
    public List<TareaSeguimientoDto> PendientesRevision { get; set; } = new();
    public List<TareaSeguimientoDto> Reabiertas { get; set; } = new();
    public List<TareaSeguimientoDto> SinActualizacion { get; set; } = new();
}

// ---------- Configuración de criterios ----------

public class ConfiguracionSeguimientoDto
{
    [Range(0, 30)] public byte DiasAlertaVencimiento { get; set; } = 3;
    /// <summary>Días hábiles sin actualización para alertar; vacío desactiva la alerta.</summary>
    [Range(1, 365)] public short? DiasSinActualizacion { get; set; }
}
