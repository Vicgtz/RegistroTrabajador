using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public class CalendarioClient(ApiClient api)
{
    private static string Dia(DateOnly d) => d.ToString("yyyy-MM-dd");

    public Task<ApiResult<CalendarioDto>> ConsultarAsync(DateOnly desde, DateOnly hasta, bool incluirCancelados = false) =>
        api.GetAsync<CalendarioDto>("api/calendario" + Qs.De(("desde", Dia(desde)), ("hasta", Dia(hasta)), ("incluirCancelados", incluirCancelados ? true : null)));

    public Task<ApiResult<ConfiguracionCalendarioDto>> ConfiguracionAsync() => api.GetAsync<ConfiguracionCalendarioDto>("api/calendario/configuracion");

    public Task<ApiResult<ConfiguracionCalendarioDto>> GuardarConfiguracionAsync(ConfiguracionCalendarioDto r) =>
        api.SendAsync<ConfiguracionCalendarioDto>(HttpMethod.Put, "api/calendario/configuracion", r);
}
