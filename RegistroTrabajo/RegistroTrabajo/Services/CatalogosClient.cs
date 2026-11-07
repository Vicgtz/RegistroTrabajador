using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public class CatalogosClient(ApiClient api)
{
    public Task<ApiResult<List<CatalogoItemDto>>> DepartamentosAsync(bool soloActivos = false) =>
        api.GetAsync<List<CatalogoItemDto>>("api/departamentos" + Qs.De(("soloActivos", soloActivos)));

    public Task<ApiResult<CatalogoItemDto>> CrearDepartamentoAsync(GuardarCatalogoRequest r) => api.PostAsync<CatalogoItemDto>("api/departamentos", r);

    public Task<ApiResult<CatalogoItemDto>> ActualizarDepartamentoAsync(Guid id, GuardarCatalogoRequest r) =>
        api.SendAsync<CatalogoItemDto>(HttpMethod.Put, $"api/departamentos/{id}", r);

    public Task<ApiResult<CatalogoItemDto>> EstadoDepartamentoAsync(Guid id, bool estado) =>
        api.PostAsync<CatalogoItemDto>($"api/departamentos/{id}/estado", new CambiarEstadoRolRequest { Estado = estado });

    public Task<ApiResult<List<CatalogoItemDto>>> PuestosAsync(bool soloActivos = false) =>
        api.GetAsync<List<CatalogoItemDto>>("api/puestos" + Qs.De(("soloActivos", soloActivos)));

    public Task<ApiResult<CatalogoItemDto>> CrearPuestoAsync(GuardarCatalogoRequest r) => api.PostAsync<CatalogoItemDto>("api/puestos", r);

    public Task<ApiResult<CatalogoItemDto>> ActualizarPuestoAsync(Guid id, GuardarCatalogoRequest r) =>
        api.SendAsync<CatalogoItemDto>(HttpMethod.Put, $"api/puestos/{id}", r);

    public Task<ApiResult<CatalogoItemDto>> EstadoPuestoAsync(Guid id, bool estado) =>
        api.PostAsync<CatalogoItemDto>($"api/puestos/{id}/estado", new CambiarEstadoRolRequest { Estado = estado });
}
