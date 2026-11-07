using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public class EventosEmpresaClient(ApiClient api)
{
    private static string Qf(EventoEmpresaFiltro f) => Qs.De(("page", f.Page), ("pageSize", f.PageSize), ("tipo", f.Tipo), ("estado", f.Estado),
        ("desde", f.Desde?.ToString("yyyy-MM-dd")), ("hasta", f.Hasta?.ToString("yyyy-MM-dd")));

    public Task<ApiResult<PagedResult<EventoEmpresaDto>>> ListarAsync(EventoEmpresaFiltro f) =>
        api.GetAsync<PagedResult<EventoEmpresaDto>>("api/eventos-empresa" + Qf(f));

    public Task<ApiResult<EventoEmpresaDto>> ObtenerAsync(Guid id) => api.GetAsync<EventoEmpresaDto>($"api/eventos-empresa/{id}");
    public Task<ApiResult<EventoEmpresaDto>> CrearAsync(GuardarEventoEmpresaRequest r) => api.PostAsync<EventoEmpresaDto>("api/eventos-empresa", r);

    public Task<ApiResult<EventoEmpresaDto>> ActualizarAsync(Guid id, GuardarEventoEmpresaRequest r) =>
        api.SendAsync<EventoEmpresaDto>(HttpMethod.Put, $"api/eventos-empresa/{id}", r);

    public Task<ApiResult<EventoEmpresaDto>> CancelarAsync(Guid id, CancelarEventoEmpresaRequest r) =>
        api.PostAsync<EventoEmpresaDto>($"api/eventos-empresa/{id}/cancelar", r);
}
