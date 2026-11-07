using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public class SolicitudesPermisoClient(ApiClient api)
{
    private static string Qf(SolicitudPermisoFiltro f) => Qs.De(("page", f.Page), ("pageSize", f.PageSize), ("trabajadorId", f.TrabajadorId),
        ("tipoPermisoId", f.TipoPermisoId), ("estado", f.Estado), ("desde", f.Desde?.ToString("yyyy-MM-dd")), ("hasta", f.Hasta?.ToString("yyyy-MM-dd")));

    public Task<ApiResult<CalculoDuracionDto>> CalcularAsync(CalculoDuracionRequest r) => api.PostAsync<CalculoDuracionDto>("api/solicitudes-permiso/calculo", r);
    public Task<ApiResult<SolicitudPermisoDto>> CrearAsync(CrearSolicitudPermisoRequest r) => api.PostAsync<SolicitudPermisoDto>("api/solicitudes-permiso", r);

    public Task<ApiResult<SolicitudPermisoDto>> ActualizarAsync(Guid id, ActualizarSolicitudPermisoRequest r) =>
        api.SendAsync<SolicitudPermisoDto>(HttpMethod.Put, $"api/solicitudes-permiso/{id}", r);

    public Task<ApiResult<SolicitudPermisoDto>> CancelarAsync(Guid id) => api.PostAsync<SolicitudPermisoDto>($"api/solicitudes-permiso/{id}/cancelar");

    public Task<ApiResult<PagedResult<SolicitudPermisoDto>>> MiasAsync(SolicitudPermisoFiltro f) =>
        api.GetAsync<PagedResult<SolicitudPermisoDto>>("api/solicitudes-permiso/mias" + Qf(f));

    public Task<ApiResult<PagedResult<SolicitudPermisoDto>>> ListarAsync(SolicitudPermisoFiltro f) =>
        api.GetAsync<PagedResult<SolicitudPermisoDto>>("api/solicitudes-permiso" + Qf(f));

    public Task<ApiResult<PagedResult<SolicitudPermisoDto>>> BandejaAsync(SolicitudPermisoFiltro f) =>
        api.GetAsync<PagedResult<SolicitudPermisoDto>>("api/solicitudes-permiso/bandeja" + Qf(f));

    public Task<ApiResult<SolicitudPermisoDto>> ResolverAsync(Guid id, bool aprobar, string? comentario) =>
        api.PostAsync<SolicitudPermisoDto>($"api/solicitudes-permiso/{id}/resolver", new ResolverPermisoRequest { Aprobar = aprobar, Comentario = comentario });

    public Task<ApiResult<SolicitudCancelacionDto>> SolicitarCancelacionAsync(Guid id, string? motivo) =>
        api.PostAsync<SolicitudCancelacionDto>($"api/solicitudes-permiso/{id}/solicitar-cancelacion", new SolicitarCancelacionRequest { Motivo = motivo });

    public Task<ApiResult<PagedResult<SolicitudCancelacionDto>>> CancelacionesAsync(CancelacionPermisoFiltro f) =>
        api.GetAsync<PagedResult<SolicitudCancelacionDto>>("api/cancelaciones-permiso" + Qs.De(("page", f.Page), ("pageSize", f.PageSize), ("estado", f.Estado)));

    public Task<ApiResult<SolicitudCancelacionDto>> ResolverCancelacionAsync(Guid id, bool aprobar, string? comentario) =>
        api.PostAsync<SolicitudCancelacionDto>($"api/cancelaciones-permiso/{id}/resolver", new ResolverPermisoRequest { Aprobar = aprobar, Comentario = comentario });
}
