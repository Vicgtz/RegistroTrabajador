using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public class UsuariosClient(ApiClient api)
{
    public Task<ApiResult<PagedResult<UsuarioDto>>> ListarAsync(UsuarioFiltro f) =>
        api.GetAsync<PagedResult<UsuarioDto>>("api/usuarios" + Qs.De(("page", f.Page), ("pageSize", f.PageSize), ("buscar", f.Buscar), ("estado", f.Estado)));

    public Task<ApiResult<UsuarioDto>> CrearAsync(CrearUsuarioRequest r) => api.PostAsync<UsuarioDto>("api/usuarios", r);

    public Task<ApiResult<UsuarioDto>> ActualizarAsync(Guid id, ActualizarUsuarioRequest r) =>
        api.SendAsync<UsuarioDto>(HttpMethod.Put, $"api/usuarios/{id}", r);

    public Task<ApiResult> ActivarAsync(Guid id) => api.SendAsync(HttpMethod.Post, $"api/usuarios/{id}/activar");
    public Task<ApiResult> DesactivarAsync(Guid id) => api.SendAsync(HttpMethod.Post, $"api/usuarios/{id}/desactivar");
    public Task<ApiResult> DesbloquearAsync(Guid id) => api.SendAsync(HttpMethod.Post, $"api/usuarios/{id}/desbloquear");

    public Task<ApiResult> RestablecerContrasenaAsync(Guid id, string nueva) =>
        api.SendAsync(HttpMethod.Post, $"api/usuarios/{id}/restablecer-contrasena", new RestablecerContrasenaUsuarioRequest { ContrasenaNueva = nueva });

    public Task<ApiResult<List<RolDto>>> RolesAsync(Guid id) => api.GetAsync<List<RolDto>>($"api/usuarios/{id}/roles");
    public Task<ApiResult> AsignarRolAsync(Guid id, Guid rolId) => api.SendAsync(HttpMethod.Put, $"api/usuarios/{id}/roles/{rolId}");
    public Task<ApiResult> RetirarRolAsync(Guid id, Guid rolId) => api.SendAsync(HttpMethod.Delete, $"api/usuarios/{id}/roles/{rolId}");
    public Task<ApiResult> RevocarSesionesAsync(Guid id) => api.SendAsync(HttpMethod.Delete, $"api/usuarios/{id}/sesiones");
}
