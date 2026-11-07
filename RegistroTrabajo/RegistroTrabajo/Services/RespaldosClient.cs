using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public class RespaldosClient(ApiClient api)
{
    public Task<ApiResult<PagedResult<RespaldoDto>>> ListarAsync(RespaldoFiltro f) =>
        api.GetAsync<PagedResult<RespaldoDto>>("api/respaldos" + Qs.De(("page", f.Page), ("pageSize", f.PageSize), ("tipo", f.Tipo), ("estado", f.Estado),
            ("desde", f.Desde?.ToString("yyyy-MM-dd")), ("hasta", f.Hasta?.ToString("yyyy-MM-dd"))));

    public Task<ApiResult<RespaldoDto>> SolicitarAsync(byte tipo) => api.PostAsync<RespaldoDto>("api/respaldos", new SolicitarRespaldoRequest { Tipo = tipo });

    public Task<ApiResult<RespaldoDto>> VerificarAsync(Guid id) => api.PostAsync<RespaldoDto>($"api/respaldos/{id}/verificar");
}
