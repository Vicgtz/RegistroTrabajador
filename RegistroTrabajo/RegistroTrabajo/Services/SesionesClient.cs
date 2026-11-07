using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public class SesionesClient(ApiClient api)
{
    public Task<ApiResult<PagedResult<SesionDto>>> ListarAsync(int page, int pageSize) =>
        api.GetAsync<PagedResult<SesionDto>>("api/sesiones" + Qs.De(("page", page), ("pageSize", pageSize)));

    public Task<ApiResult> RevocarAsync(Guid id) => api.SendAsync(HttpMethod.Delete, $"api/sesiones/{id}");

    public Task<ApiResult<PagedResult<SesionDto>>> MiasAsync(int page, int pageSize) =>
        api.GetAsync<PagedResult<SesionDto>>("api/sesiones/mias" + Qs.De(("page", page), ("pageSize", pageSize)));

    public Task<ApiResult> RevocarMiaAsync(Guid id) => api.SendAsync(HttpMethod.Delete, $"api/sesiones/mias/{id}");
}
