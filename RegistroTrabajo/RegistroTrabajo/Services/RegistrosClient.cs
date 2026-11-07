using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public class RegistrosClient(ApiClient api)
{
    public Task<ApiResult<PagedResult<HistorialAccesoDto>>> AccesosAsync(FechaFiltro f) =>
        api.GetAsync<PagedResult<HistorialAccesoDto>>("api/accesos" + Qs.De(("page", f.Page), ("pageSize", f.PageSize), ("desde", f.Desde), ("hasta", f.Hasta)));

    public Task<ApiResult<PagedResult<AuditoriaDto>>> AuditoriaAsync(AuditoriaFiltro f) =>
        api.GetAsync<PagedResult<AuditoriaDto>>("api/auditoria" + Qs.De(("page", f.Page), ("pageSize", f.PageSize),
            ("desde", f.Desde), ("hasta", f.Hasta), ("entidad", f.Entidad), ("entidadId", f.EntidadId)));
}
