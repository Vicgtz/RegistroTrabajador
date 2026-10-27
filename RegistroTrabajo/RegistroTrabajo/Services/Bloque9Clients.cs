using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public class NotificacionesClient(ApiClient api)
{
    public Task<ApiResult<PagedResult<NotificacionDto>>> ListarAsync(NotificacionFiltro f) =>
        api.GetAsync<PagedResult<NotificacionDto>>("api/notificaciones" + Qs.De(("page", f.Page), ("pageSize", f.PageSize),
            ("estado", f.Estado == EstadoNotificacionFiltro.Bandeja ? null : (int)f.Estado), ("categoria", f.Categoria),
            ("desde", f.Desde?.ToString("yyyy-MM-dd")), ("hasta", f.Hasta?.ToString("yyyy-MM-dd")), ("buscar", f.Buscar)));

    public Task<ApiResult<int>> NoLeidasAsync() => api.GetAsync<int>("api/notificaciones/no-leidas");
    public Task<ApiResult<ResumenNotificacionesDto>> ResumenAsync() => api.GetAsync<ResumenNotificacionesDto>("api/notificaciones/resumen");
    public Task<ApiResult> LeidaAsync(Guid id) => api.SendAsync(HttpMethod.Post, $"api/notificaciones/{id}/leida");
    public Task<ApiResult> TodasLeidasAsync() => api.SendAsync(HttpMethod.Post, "api/notificaciones/leidas");
    public Task<ApiResult> ArchivarAsync(Guid id) => api.SendAsync(HttpMethod.Post, $"api/notificaciones/{id}/archivar");
    public Task<ApiResult> DesarchivarAsync(Guid id) => api.SendAsync(HttpMethod.Post, $"api/notificaciones/{id}/desarchivar");
    public Task<ApiResult<DestinoNotificacionDto>> DestinoAsync(Guid id) => api.GetAsync<DestinoNotificacionDto>($"api/notificaciones/{id}/destino");

    public Task<ApiResult<List<PreferenciaCategoriaDto>>> PreferenciasAsync() => api.GetAsync<List<PreferenciaCategoriaDto>>("api/notificaciones/preferencias");

    public Task<ApiResult<List<PreferenciaCategoriaDto>>> GuardarPreferenciasAsync(GuardarPreferenciasRequest r) =>
        api.SendAsync<List<PreferenciaCategoriaDto>>(HttpMethod.Put, "api/notificaciones/preferencias", r);
}

/// <summary>Presentación del centro de notificaciones.</summary>
public static class FormatoNotificaciones
{
    public static string ClaseCategoria(string categoria) => categoria switch
    {
        CategoriaNotificacion.Permisos => "info",
        CategoriaNotificacion.Proyectos => "primary",
        CategoriaNotificacion.Tareas => "warning",
        CategoriaNotificacion.Menciones => "success",
        _ => "secondary"
    };
}
