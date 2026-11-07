using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public class BorradoresClient(ApiClient api)
{
    public Task<ApiResult<PagedResult<BorradorDto>>> ListarAsync(BorradorFiltro f) =>
        api.GetAsync<PagedResult<BorradorDto>>("api/borradores" + Qs.De(("page", f.Page), ("pageSize", f.PageSize), ("modulo", f.Modulo)));

    public Task<ApiResult<BorradorDto>> ObtenerAsync(Guid id) => api.GetAsync<BorradorDto>($"api/borradores/{id}");
    public Task<ApiResult<BorradorDto>> CrearAsync(GuardarBorradorRequest r) => api.PostAsync<BorradorDto>("api/borradores", r);
    public Task<ApiResult<BorradorDto>> ActualizarAsync(Guid id, GuardarBorradorRequest r) => api.SendAsync<BorradorDto>(HttpMethod.Put, $"api/borradores/{id}", r);
    public Task<ApiResult> EliminarAsync(Guid id) => api.SendAsync(HttpMethod.Delete, $"api/borradores/{id}");
}
