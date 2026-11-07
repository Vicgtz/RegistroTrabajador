using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

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
