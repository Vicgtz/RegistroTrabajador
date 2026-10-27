using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public class ProyectosClient(ApiClient api)
{
    public Task<ApiResult<List<EstadoProyectoDto>>> EstadosAsync(bool incluirInactivos = false) =>
        api.GetAsync<List<EstadoProyectoDto>>("api/estados-proyecto" + Qs.De(("incluirInactivos", incluirInactivos ? true : null)));

    public Task<ApiResult<EstadoProyectoDto>> CrearEstadoAsync(GuardarEstadoProyectoRequest r) => api.PostAsync<EstadoProyectoDto>("api/estados-proyecto", r);

    public Task<ApiResult<EstadoProyectoDto>> ActualizarEstadoAsync(Guid id, GuardarEstadoProyectoRequest r) =>
        api.SendAsync<EstadoProyectoDto>(HttpMethod.Put, $"api/estados-proyecto/{id}", r);

    public Task<ApiResult<ProyectosResumenDto>> ResumenAsync() => api.GetAsync<ProyectosResumenDto>("api/proyectos/resumen");

    public Task<ApiResult<PagedResult<ProyectoResumenDto>>> ListarAsync(ProyectoFiltro f) =>
        api.GetAsync<PagedResult<ProyectoResumenDto>>("api/proyectos" + Qs.De(("page", f.Page), ("pageSize", f.PageSize), ("buscar", f.Buscar),
            ("estadoProyectoId", f.EstadoProyectoId), ("prioridad", f.Prioridad), ("soloMios", f.SoloMios ? true : null),
            ("soloProximosAVencer", f.SoloProximosAVencer ? true : null), ("incluirFinalizados", f.IncluirFinalizados ? null : false)));

    public Task<ApiResult<ProyectoDetalleDto>> ObtenerAsync(Guid id) => api.GetAsync<ProyectoDetalleDto>($"api/proyectos/{id}");
    public Task<ApiResult<ProyectoDetalleDto>> CrearAsync(GuardarProyectoRequest r) => api.PostAsync<ProyectoDetalleDto>("api/proyectos", r);

    public Task<ApiResult<ProyectoDetalleDto>> ActualizarAsync(Guid id, GuardarProyectoRequest r) =>
        api.SendAsync<ProyectoDetalleDto>(HttpMethod.Put, $"api/proyectos/{id}", r);

    public Task<ApiResult<ProyectoDetalleDto>> AsignarAsync(Guid id, AsignarTrabajadoresRequest r) => api.PostAsync<ProyectoDetalleDto>($"api/proyectos/{id}/trabajadores", r);

    public Task<ApiResult<ProyectoDetalleDto>> RetirarAsync(Guid id, Guid trabajadorId) =>
        api.SendAsync<ProyectoDetalleDto>(HttpMethod.Delete, $"api/proyectos/{id}/trabajadores/{trabajadorId}", null);

    public Task<ApiResult<ProyectoDetalleDto>> CambiarResponsableAsync(Guid id, CambiarResponsableRequest r) => api.PostAsync<ProyectoDetalleDto>($"api/proyectos/{id}/responsable", r);
    public Task<ApiResult<ProyectoDetalleDto>> CambiarEstadoAsync(Guid id, CambiarEstadoProyectoRequest r) => api.PostAsync<ProyectoDetalleDto>($"api/proyectos/{id}/estado", r);
    public Task<ApiResult<List<HistorialEstadoProyectoDto>>> HistorialAsync(Guid id) => api.GetAsync<List<HistorialEstadoProyectoDto>>($"api/proyectos/{id}/historial");

    public Task<ApiResult<PagedResult<TareaResumenDto>>> TareasAsync(Guid id, TareaFiltro f) =>
        api.GetAsync<PagedResult<TareaResumenDto>>($"api/proyectos/{id}/tareas" + Qs.De(("page", f.Page), ("pageSize", f.PageSize), ("buscar", f.Buscar),
            ("estado", f.Estado), ("trabajadorId", f.TrabajadorId), ("soloVencidas", f.SoloVencidas ? true : null), ("incluirCerradas", f.IncluirCerradas ? null : false)));

    public Task<ApiResult<TareaDetalleDto>> CrearTareaAsync(Guid proyectoId, GuardarTareaRequest r) => api.PostAsync<TareaDetalleDto>($"api/proyectos/{proyectoId}/tareas", r);
}

public class TareasClient(ApiClient api)
{
    public Task<ApiResult<PagedResult<TareaResumenDto>>> MisTareasAsync(MisTareasFiltro f) =>
        api.GetAsync<PagedResult<TareaResumenDto>>("api/tareas/mis-tareas" + Qs.De(("page", f.Page), ("pageSize", f.PageSize), ("estado", f.Estado),
            ("soloVencidas", f.SoloVencidas ? true : null), ("incluirCerradas", f.IncluirCerradas ? true : null)));

    public Task<ApiResult<TareaDetalleDto>> ObtenerAsync(Guid id) => api.GetAsync<TareaDetalleDto>($"api/tareas/{id}");

    public Task<ApiResult<TareaDetalleDto>> ActualizarAsync(Guid id, GuardarTareaRequest r) => api.SendAsync<TareaDetalleDto>(HttpMethod.Put, $"api/tareas/{id}", r);
    public Task<ApiResult<TareaDetalleDto>> AsignarAsync(Guid id, AsignarTareaRequest r) => api.PostAsync<TareaDetalleDto>($"api/tareas/{id}/asignados", r);

    public Task<ApiResult<TareaDetalleDto>> RetirarAsync(Guid id, Guid trabajadorId) =>
        api.SendAsync<TareaDetalleDto>(HttpMethod.Delete, $"api/tareas/{id}/asignados/{trabajadorId}", null);

    public Task<ApiResult<TareaDetalleDto>> AvanceAsync(Guid id, ActualizarAvanceRequest r) => api.PostAsync<TareaDetalleDto>($"api/tareas/{id}/avance", r);
    public Task<ApiResult<TareaDetalleDto>> RevisarAsync(Guid id, RevisarTareaRequest r) => api.PostAsync<TareaDetalleDto>($"api/tareas/{id}/revision", r);
    public Task<ApiResult<TareaDetalleDto>> ReabrirAsync(Guid id, MotivoTareaRequest r) => api.PostAsync<TareaDetalleDto>($"api/tareas/{id}/reabrir", r);
    public Task<ApiResult<TareaDetalleDto>> CancelarAsync(Guid id, MotivoTareaRequest r) => api.PostAsync<TareaDetalleDto>($"api/tareas/{id}/cancelar", r);
    public Task<ApiResult<List<HistorialAvanceTareaDto>>> HistorialAsync(Guid id) => api.GetAsync<List<HistorialAvanceTareaDto>>($"api/tareas/{id}/historial");
    public Task<ApiResult<List<ComentarioTareaDto>>> ComentariosAsync(Guid id) => api.GetAsync<List<ComentarioTareaDto>>($"api/tareas/{id}/comentarios");
    public Task<ApiResult<ComentarioTareaDto>> ComentarAsync(Guid id, PublicarComentarioRequest r) => api.PostAsync<ComentarioTareaDto>($"api/tareas/{id}/comentarios", r);
    public Task<ApiResult<List<UsuarioMencionableDto>>> MencionablesAsync(Guid id) => api.GetAsync<List<UsuarioMencionableDto>>($"api/tareas/{id}/mencionables");
}

/// <summary>Textos y clases de presentación de proyectos y tareas.</summary>
public static class FormatoProyectos
{
    public static readonly byte[] EstadosTarea =
    {
        EstadoTarea.Pendiente, EstadoTarea.EnProceso, EstadoTarea.EnRevision, EstadoTarea.Completada, EstadoTarea.Reabierta, EstadoTarea.Cancelada
    };

    public static readonly byte[] Prioridades = { PrioridadTrabajo.Baja, PrioridadTrabajo.Media, PrioridadTrabajo.Alta, PrioridadTrabajo.Urgente };

    public static string EstadoDeTarea(byte e) => e switch
    {
        EstadoTarea.Pendiente => "Pendiente",
        EstadoTarea.EnProceso => "En proceso",
        EstadoTarea.EnRevision => "En revisión",
        EstadoTarea.Completada => "Completada",
        EstadoTarea.Reabierta => "Reabierta",
        EstadoTarea.Cancelada => "Cancelada",
        _ => "—"
    };

    /// <summary>Sufijo de la clase text-bg-* de Bootstrap.</summary>
    public static string ClaseEstadoTarea(byte e) => e switch
    {
        EstadoTarea.Pendiente => "secondary",
        EstadoTarea.EnProceso => "primary",
        EstadoTarea.EnRevision => "warning",
        EstadoTarea.Completada => "success",
        EstadoTarea.Reabierta => "danger",
        _ => "dark"
    };

    public static string Prioridad(byte p) => p switch
    {
        PrioridadTrabajo.Baja => "Baja",
        PrioridadTrabajo.Media => "Media",
        PrioridadTrabajo.Alta => "Alta",
        PrioridadTrabajo.Urgente => "Urgente",
        _ => "—"
    };

    public static string ClasePrioridad(byte p) => p switch
    {
        PrioridadTrabajo.Baja => "secondary",
        PrioridadTrabajo.Media => "info",
        PrioridadTrabajo.Alta => "warning",
        _ => "danger"
    };

    public static string Fecha(DateOnly? d) => d is { } f ? f.ToString("dd/MM/yyyy") : "—";

    public static string FechaHora(DateTime utc) => FormatoEmpresa.FechaHora(utc);

    public static string Pct(int p) => $"{Math.Clamp(p, 0, 100)}%";

    /// <summary>Clase de la barra de progreso según el avance.</summary>
    public static string ClaseBarra(int p) => p >= 100 ? "bg-success" : p >= 50 ? "" : "bg-warning";
}
