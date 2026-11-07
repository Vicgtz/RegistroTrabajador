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
