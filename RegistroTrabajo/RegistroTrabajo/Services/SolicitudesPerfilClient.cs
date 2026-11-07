using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public class SolicitudesPerfilClient(ApiClient api)
{
    public Task<ApiResult<SolicitudCambioPerfilDto>> CrearAsync(CambioPerfilDatos r) => api.PostAsync<SolicitudCambioPerfilDto>("api/solicitudes-perfil", r);
    public Task<ApiResult<List<SolicitudCambioPerfilDto>>> MiasAsync() => api.GetAsync<List<SolicitudCambioPerfilDto>>("api/solicitudes-perfil/mias");
    public Task<ApiResult<SolicitudCambioPerfilDto>> CancelarAsync(Guid id) => api.PostAsync<SolicitudCambioPerfilDto>($"api/solicitudes-perfil/{id}/cancelar");

    public Task<ApiResult<PagedResult<SolicitudCambioPerfilDto>>> ListarAsync(SolicitudPerfilFiltro f) =>
        api.GetAsync<PagedResult<SolicitudCambioPerfilDto>>("api/solicitudes-perfil" + Qs.De(("page", f.Page), ("pageSize", f.PageSize), ("estado", f.Estado)));

    public Task<ApiResult<SolicitudCambioPerfilDto>> ResolverAsync(Guid id, bool aprobar, string? comentario) =>
        api.PostAsync<SolicitudCambioPerfilDto>($"api/solicitudes-perfil/{id}/resolver", new ResolverSolicitudRequest { Aprobar = aprobar, Comentario = comentario });
}
