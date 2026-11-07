using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public class RolesClient(ApiClient api)
{
    public Task<ApiResult<List<RolDto>>> ListarAsync() => api.GetAsync<List<RolDto>>("api/roles");
    public Task<ApiResult<List<PermisoDto>>> PermisosAsync() => api.GetAsync<List<PermisoDto>>("api/permisos");
    public Task<ApiResult<RolPermisosDto>> PermisosDeRolAsync(Guid id) => api.GetAsync<RolPermisosDto>($"api/roles/{id}/permisos");

    public Task<ApiResult<RolPermisosDto>> ConfigurarPermisosAsync(Guid id, IEnumerable<string> codigos) =>
        api.SendAsync<RolPermisosDto>(HttpMethod.Put, $"api/roles/{id}/permisos", new ConfigurarPermisosRolRequest { Permisos = codigos.ToList() });

    public Task<ApiResult<RolDto>> CrearAsync(CrearRolRequest r) => api.PostAsync<RolDto>("api/roles", r);
    public Task<ApiResult<RolDto>> ActualizarAsync(Guid id, ActualizarRolRequest r) => api.SendAsync<RolDto>(HttpMethod.Put, $"api/roles/{id}", r);

    public Task<ApiResult<RolDto>> CambiarEstadoAsync(Guid id, bool estado) =>
        api.PostAsync<RolDto>($"api/roles/{id}/estado", new CambiarEstadoRolRequest { Estado = estado });
}
