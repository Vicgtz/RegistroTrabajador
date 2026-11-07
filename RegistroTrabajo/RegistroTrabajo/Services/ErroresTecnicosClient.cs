using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public class ErroresTecnicosClient(ApiClient api)
{
    public Task<ApiResult<PagedResult<ErrorTecnicoDto>>> ListarAsync(ErrorTecnicoFiltro f) =>
        api.GetAsync<PagedResult<ErrorTecnicoDto>>("api/errores-tecnicos" + Qs.De(("page", f.Page), ("pageSize", f.PageSize),
            ("correlationId", f.CorrelationId), ("desde", f.Desde?.ToString("yyyy-MM-dd")), ("hasta", f.Hasta?.ToString("yyyy-MM-dd")), ("buscar", f.Buscar)));
}
