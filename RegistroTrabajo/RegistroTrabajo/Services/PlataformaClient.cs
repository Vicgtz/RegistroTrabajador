using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public class PlataformaClient(ApiClient api)
{
    public Task<ApiResult<PagedResult<EmpresaPlataformaDto>>> ListarAsync(int page, int pageSize, string? buscar) =>
        api.GetAsync<PagedResult<EmpresaPlataformaDto>>("api/plataforma/empresas" + Qs.De(("page", page), ("pageSize", pageSize), ("buscar", buscar)));

    public Task<ApiResult<EmpresaPlataformaDto>> CrearAsync(CrearEmpresaPlataformaRequest r) =>
        api.PostAsync<EmpresaPlataformaDto>("api/plataforma/empresas", r);

    public Task<ApiResult<EmpresaPlataformaDto>> CambiarEstadoAsync(Guid id, byte estado) =>
        api.PostAsync<EmpresaPlataformaDto>($"api/plataforma/empresas/{id}/estado", new CambiarEstadoEmpresaRequest { Estado = estado });
}
