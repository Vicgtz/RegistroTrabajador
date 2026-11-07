using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public class EmpresaClient(ApiClient api)
{
    public Task<ApiResult<EmpresaDto>> ObtenerAsync() => api.GetAsync<EmpresaDto>("api/empresa");
    public Task<ApiResult<EmpresaDto>> ActualizarAsync(ActualizarEmpresaRequest r) => api.SendAsync<EmpresaDto>(HttpMethod.Put, "api/empresa", r);
    public Task<ApiResult<ConfiguracionEmpresaDto>> ConfiguracionAsync() => api.GetAsync<ConfiguracionEmpresaDto>("api/empresa/configuracion");

    public Task<ApiResult<ConfiguracionEmpresaDto>> ActualizarConfiguracionAsync(ConfiguracionEmpresaDto r) =>
        api.SendAsync<ConfiguracionEmpresaDto>(HttpMethod.Put, "api/empresa/configuracion", r);
}
