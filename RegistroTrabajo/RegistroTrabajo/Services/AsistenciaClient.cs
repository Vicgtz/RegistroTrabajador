using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public class AsistenciaClient(ApiClient api)
{
    private static string Qf(AsistenciaFiltro f) => Qs.De(("page", f.Page), ("pageSize", f.PageSize), ("trabajadorId", f.TrabajadorId),
        ("desde", f.Desde?.ToString("yyyy-MM-dd")), ("hasta", f.Hasta?.ToString("yyyy-MM-dd")), ("soloIncompletas", f.SoloIncompletas));

    public Task<ApiResult<EstadoJornadaDto>> EstadoAsync() => api.GetAsync<EstadoJornadaDto>("api/asistencia/estado");
    public Task<ApiResult<RegistroAsistenciaDto>> EntradaAsync(RegistrarAsistenciaRequest r) => api.PostAsync<RegistroAsistenciaDto>("api/asistencia/entrada", r);
    public Task<ApiResult<RegistroAsistenciaDto>> SalidaAsync(RegistrarAsistenciaRequest r) => api.PostAsync<RegistroAsistenciaDto>("api/asistencia/salida", r);

    public Task<ApiResult<PagedResult<RegistroAsistenciaDto>>> MiasAsync(AsistenciaFiltro f) =>
        api.GetAsync<PagedResult<RegistroAsistenciaDto>>("api/asistencia/mias" + Qf(f));

    public Task<ApiResult<PagedResult<RegistroAsistenciaDto>>> ListarAsync(AsistenciaFiltro f) =>
        api.GetAsync<PagedResult<RegistroAsistenciaDto>>("api/asistencia" + Qf(f));

    public Task<ApiResult<RegistroAsistenciaDto>> CorregirAsync(Guid id, CorregirAsistenciaRequest r) =>
        api.PostAsync<RegistroAsistenciaDto>($"api/asistencia/{id}/corregir", r);

    public Task<ApiResult<RegistroAsistenciaDto>> AnularAsync(Guid id, AnularAsistenciaRequest r) =>
        api.PostAsync<RegistroAsistenciaDto>($"api/asistencia/{id}/anular", r);

    public Task<ApiResult<RegistroAsistenciaDto>> ManualAsync(CrearRegistroManualRequest r) =>
        api.PostAsync<RegistroAsistenciaDto>("api/asistencia/manual", r);
}
