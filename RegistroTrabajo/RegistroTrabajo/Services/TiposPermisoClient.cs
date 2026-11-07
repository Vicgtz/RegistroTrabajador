using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public class TiposPermisoClient(ApiClient api)
{
    public Task<ApiResult<List<TipoPermisoDto>>> ListarAsync(bool soloActivos = false) =>
        api.GetAsync<List<TipoPermisoDto>>("api/tipos-permiso" + Qs.De(("soloActivos", soloActivos)));

    public Task<ApiResult<TipoPermisoDto>> CrearAsync(GuardarTipoPermisoRequest r) => api.PostAsync<TipoPermisoDto>("api/tipos-permiso", r);

    public Task<ApiResult<TipoPermisoDto>> ActualizarAsync(Guid id, GuardarTipoPermisoRequest r) =>
        api.SendAsync<TipoPermisoDto>(HttpMethod.Put, $"api/tipos-permiso/{id}", r);

    public Task<ApiResult<TipoPermisoDto>> EstadoAsync(Guid id, bool estado) =>
        api.PostAsync<TipoPermisoDto>($"api/tipos-permiso/{id}/estado", new CambiarEstadoRolRequest { Estado = estado });

    public Task<ApiResult<ReglaPermisoDto>> CrearReglaAsync(Guid tipoId, GuardarReglaPermisoRequest r) =>
        api.PostAsync<ReglaPermisoDto>($"api/tipos-permiso/{tipoId}/reglas", r);

    public Task<ApiResult<ReglaPermisoDto>> ActualizarReglaAsync(Guid tipoId, Guid reglaId, GuardarReglaPermisoRequest r) =>
        api.SendAsync<ReglaPermisoDto>(HttpMethod.Put, $"api/tipos-permiso/{tipoId}/reglas/{reglaId}", r);

    public Task<ApiResult<List<TipoPermisoDto>>> SembrarInicialesAsync() => api.PostAsync<List<TipoPermisoDto>>("api/tipos-permiso/iniciales");
}
