using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

/// <summary>Presentación del centro de notificaciones.</summary>
public static class FormatoNotificaciones
{
    public static string ClaseCategoria(string categoria) => categoria switch
    {
        CategoriaNotificacion.Permisos => "info",
        CategoriaNotificacion.Proyectos => "primary",
        CategoriaNotificacion.Tareas => "warning",
        CategoriaNotificacion.Menciones => "success",
        _ => "secondary"
    };
}
