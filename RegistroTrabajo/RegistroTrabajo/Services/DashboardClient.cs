using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public class DashboardClient(ApiClient api)
{
    public Task<ApiResult<DashboardDto>> ObtenerAsync() => api.GetAsync<DashboardDto>("api/dashboard");
}
