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
