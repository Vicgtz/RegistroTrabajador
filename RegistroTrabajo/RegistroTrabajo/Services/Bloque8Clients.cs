using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public class SeguimientoClient(ApiClient api)
{
    public Task<ApiResult<List<EventoSeguimientoDto>>> LineaDeTiempoAsync(Guid tareaId) => api.GetAsync<List<EventoSeguimientoDto>>($"api/tareas/{tareaId}/linea-de-tiempo");
    public Task<ApiResult<List<RevisionTareaDto>>> RevisionesAsync(Guid tareaId) => api.GetAsync<List<RevisionTareaDto>>($"api/tareas/{tareaId}/revisiones");

    public Task<ApiResult<PanelSeguimientoDto>> PanelAsync(SeguimientoFiltro f) =>
        api.GetAsync<PanelSeguimientoDto>("api/seguimiento/panel" + Qs.De(("trabajadorId", f.TrabajadorId), ("proyectoId", f.ProyectoId)));

    public Task<ApiResult<List<TareaSeguimientoDto>>> PendientesRevisionAsync(SeguimientoFiltro f) =>
        api.GetAsync<List<TareaSeguimientoDto>>("api/seguimiento/revisiones" + Qs.De(("trabajadorId", f.TrabajadorId), ("proyectoId", f.ProyectoId)));

    public Task<ApiResult<ConfiguracionSeguimientoDto>> ConfiguracionAsync() => api.GetAsync<ConfiguracionSeguimientoDto>("api/seguimiento/configuracion");

    public Task<ApiResult<ConfiguracionSeguimientoDto>> GuardarConfiguracionAsync(ConfiguracionSeguimientoDto r) =>
        api.SendAsync<ConfiguracionSeguimientoDto>(HttpMethod.Put, "api/seguimiento/configuracion", r);
}

/// <summary>Textos de la línea de tiempo del seguimiento de tareas.</summary>
public static class FormatoSeguimiento
{
    public static string Titulo(EventoSeguimientoDto e) => e.Tipo switch
    {
        TipoEventoTarea.AvanceActualizado => "Avance actualizado",
        TipoEventoTarea.FechaLimiteCambiada => "Fecha límite modificada",
        TipoEventoTarea.PrioridadCambiada => "Prioridad modificada",
        TipoEventoTarea.AsignacionAgregada => "Trabajador asignado",
        TipoEventoTarea.AsignacionRetirada => "Trabajador retirado",
        TipoEventoTarea.EnviadaARevision => "Enviada a revisión",
        TipoEventoTarea.RevisionAprobada => "Revisión aprobada",
        TipoEventoTarea.RevisionDevuelta => "Devuelta con correcciones",
        TipoEventoTarea.Reabierta => "Tarea reabierta",
        TipoEventoTarea.Cancelada => "Tarea cancelada",
        TipoEventoTarea.Comentario => "Comentó",
        _ => e.Tipo
    };

    /// <summary>Grupo para el filtro de la línea de tiempo.</summary>
    public static string Grupo(string tipo) => tipo switch
    {
        TipoEventoTarea.AvanceActualizado or TipoEventoTarea.EnviadaARevision => "avance",
        TipoEventoTarea.RevisionAprobada or TipoEventoTarea.RevisionDevuelta or TipoEventoTarea.Reabierta or TipoEventoTarea.Cancelada => "revision",
        TipoEventoTarea.AsignacionAgregada or TipoEventoTarea.AsignacionRetirada => "asignacion",
        TipoEventoTarea.FechaLimiteCambiada or TipoEventoTarea.PrioridadCambiada => "datos",
        TipoEventoTarea.Comentario => "comentario",
        _ => "otro"
    };

    public static string ClaseTipo(string tipo) => tipo switch
    {
        TipoEventoTarea.RevisionAprobada => "success",
        TipoEventoTarea.RevisionDevuelta or TipoEventoTarea.Reabierta => "danger",
        TipoEventoTarea.EnviadaARevision => "warning",
        TipoEventoTarea.Cancelada => "dark",
        TipoEventoTarea.Comentario => "info",
        _ => "secondary"
    };

    /// <summary>"antes → después" cuando el evento trae valores (la prioridad se guarda como número).</summary>
    public static string? Cambio(EventoSeguimientoDto e)
    {
        string? F(string? v) => e.Tipo == TipoEventoTarea.PrioridadCambiada && byte.TryParse(v, out var p) ? FormatoProyectos.Prioridad(p) : v;
        var a = F(e.ValorAnterior); var n = F(e.ValorNuevo);
        if (a is not null && n is not null) return $"{a} → {n}";
        return n ?? a;
    }
}
