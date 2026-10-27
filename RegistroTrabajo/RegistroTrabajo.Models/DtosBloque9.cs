using System.ComponentModel.DataAnnotations;

namespace RegistroTrabajo.Models;

// ---------- Centro de notificaciones ----------

public class NotificacionDto
{
    public Guid NotificacionId { get; set; }
    public string Tipo { get; set; } = null!;
    /// <summary><see cref="CategoriaNotificacion"/>: permisos, proyectos, tareas o menciones.</summary>
    public string Categoria { get; set; } = null!;
    public string Titulo { get; set; } = null!;
    public string Mensaje { get; set; } = null!;
    public string? EntidadTipo { get; set; }
    public string? EntidadId { get; set; }
    public DateTime CreadaUtc { get; set; }
    public bool Leida { get; set; }
    public bool Archivada { get; set; }
    /// <summary>El usuario pidió ver esta categoría o tipo como prioritario.</summary>
    public bool Prioritaria { get; set; }
    /// <summary>Operación importante: no se puede desactivar.</summary>
    public bool Obligatoria { get; set; }
}

/// <summary>Estado de la notificación en la bandeja.</summary>
public enum EstadoNotificacionFiltro
{
    /// <summary>Bandeja principal: no archivadas (leídas y no leídas). Es el comportamiento por defecto.</summary>
    Bandeja = 0,
    NoLeidas = 1,
    Leidas = 2,
    /// <summary>Historial: solo las archivadas.</summary>
    Archivadas = 3,
    /// <summary>Todas, incluidas las archivadas.</summary>
    Todas = 4
}

public class NotificacionFiltro : PageQuery
{
    public EstadoNotificacionFiltro Estado { get; set; } = EstadoNotificacionFiltro.Bandeja;
    /// <summary>Código de <see cref="CategoriaNotificacion"/>.</summary>
    [StringLength(20)] public string? Categoria { get; set; }
    /// <summary>Fecha local (de la empresa) inicial, inclusiva.</summary>
    public DateOnly? Desde { get; set; }
    public DateOnly? Hasta { get; set; }
    [StringLength(100)] public string? Buscar { get; set; }
    /// <summary>Compatibilidad con el Bloque 7: equivale a <see cref="EstadoNotificacionFiltro.NoLeidas"/>.</summary>
    public bool SoloNoLeidas { get; set; }
    /// <summary>Compatibilidad con el Bloque 7: equivale a <see cref="EstadoNotificacionFiltro.Todas"/>.</summary>
    public bool IncluirArchivadas { get; set; }
}

public class ResumenNotificacionesDto
{
    /// <summary>No leídas y no archivadas del usuario conectado.</summary>
    public int NoLeidas { get; set; }
    public int Prioritarias { get; set; }
    public Dictionary<string, int> NoLeidasPorCategoria { get; set; } = new();
}

/// <summary>Ruta de la aplicación a la que lleva la notificación, ya validada contra los permisos actuales del usuario.</summary>
public class DestinoNotificacionDto
{
    public string Ruta { get; set; } = null!;
}

// ---------- Preferencias ----------

public class PreferenciaCategoriaDto
{
    public string Categoria { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string Descripcion { get; set; } = null!;
    /// <summary>Recibir los avisos opcionales de la categoría.</summary>
    public bool Activa { get; set; } = true;
    public bool Prioritaria { get; set; }
    /// <summary>La categoría incluye avisos obligatorios que siempre se reciben, aunque esté desactivada.</summary>
    public bool TieneObligatorias { get; set; }
    /// <summary>Todos los avisos de la categoría son obligatorios: no se puede desactivar.</summary>
    public bool TodasObligatorias { get; set; }
    public List<string> Opcionales { get; set; } = new();
    public List<string> Obligatorias { get; set; } = new();
}

public class GuardarPreferenciaCategoria
{
    [Required, StringLength(20)] public string Categoria { get; set; } = null!;
    public bool Activa { get; set; } = true;
    public bool Prioritaria { get; set; }
}

public class GuardarPreferenciasRequest
{
    [Required, MinLength(1), MaxLength(10)] public List<GuardarPreferenciaCategoria> Categorias { get; set; } = new();
}
