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

/// <summary>Textos y clases de presentación del calendario (las fechas ya llegan en hora local de la empresa).</summary>
public static class FormatoCalendario
{
    public static readonly string[] Meses =
    {
        "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"
    };

    public static readonly string[] DiasCortos = { "L", "M", "X", "J", "V", "S", "D" };

    public static string Mes(DateOnly d) => $"{Meses[d.Month - 1]} {d.Year}";

    public static string Categoria(byte c) => c switch
    {
        CategoriaCalendario.Evento => "Evento",
        CategoriaCalendario.Festivo => "Festivo",
        CategoriaCalendario.Ausencia => "Ausencia",
        CategoriaCalendario.Cumpleanos => "Cumpleaños",
        CategoriaCalendario.Aniversario => "Aniversario laboral",
        _ => "—"
    };

    /// <summary>Clase CSS (app.css) que fija el color de cada categoría.</summary>
    public static string ClaseCategoria(byte c) => $"cal-cat-{c}";

    public static string TipoEvento(byte t) => t switch
    {
        TipoEventoEmpresa.ReunionGeneral => "Reunión general",
        TipoEventoEmpresa.Capacitacion => "Capacitación",
        TipoEventoEmpresa.ActividadInterna => "Actividad interna",
        TipoEventoEmpresa.FechaImportante => "Fecha importante",
        TipoEventoEmpresa.Otro => "Otro",
        _ => "—"
    };

    public static string Visibilidad(EventoEmpresaDto e) =>
        e.Visibilidad == VisibilidadEvento.Departamento ? $"Departamento: {e.Departamento ?? "—"}" : "Toda la empresa";

    public static string Estado(byte e) => e == EstadoEventoEmpresa.Cancelado ? "Cancelado" : "Activo";

    private static string Corta(DateOnly d) => d.ToString("dd/MM");

    /// <summary>"Todo el día", "09:00 – 10:30", "25/10 – 28/10"... según el tipo de elemento.</summary>
    public static string Periodo(CalendarioItemDto i)
    {
        var varios = i.FechaFin > i.FechaInicio;
        if (i.TodoElDia) return varios ? $"{Corta(i.FechaInicio)} – {Corta(i.FechaFin)}" : "Todo el día";
        var ini = i.HoraInicio is { } hi ? hi.ToString("HH\\:mm") : "";
        var fin = i.HoraFin is { } hf ? hf.ToString("HH\\:mm") : null;
        if (!varios) return fin is null ? ini : $"{ini} – {fin}";
        return $"{Corta(i.FechaInicio)} {ini} – {Corta(i.FechaFin)} {fin}".TrimEnd();
    }

    /// <summary>Periodo de un evento ya completo (administración y detalle).</summary>
    public static string Periodo(EventoEmpresaDto e)
    {
        var ini = e.FechaInicio;
        var fin = e.FechaFin;
        if (e.TodoElDia)
            return fin is { } f && f.Date > ini.Date ? $"{ini:dd/MM/yyyy} – {f:dd/MM/yyyy} (todo el día)" : $"{ini:dd/MM/yyyy} (todo el día)";
        if (fin is not { } g) return $"{ini:dd/MM/yyyy HH:mm}";
        return g.Date == ini.Date ? $"{ini:dd/MM/yyyy HH:mm} – {g:HH:mm}" : $"{ini:dd/MM/yyyy HH:mm} – {g:dd/MM/yyyy HH:mm}";
    }

    public static string FechaCompleta(DateOnly d)
    {
        var s = d.ToString("dddd d 'de' MMMM 'de' yyyy");
        return char.ToUpper(s[0]) + s[1..];
    }
}
