using System.Globalization;
using Microsoft.JSInterop;
using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

/// <summary>Formato de fechas y horas de la empresa. Valores por defecto = los de siempre; se actualizan al cargar la apariencia.</summary>
public static class FormatoEmpresa
{
    private static string _fecha = "dd/MM/yyyy", _hora = "HH:mm";
    private static CultureInfo _cultura = new("es-MX");
    private static TimeZoneInfo? _zona;

    public static void Aplicar(AparienciaEmpresaDto? a)
    {
        _fecha = a is not null && FormatosRegionales.Fecha.Contains(a.FormatoFecha) ? a.FormatoFecha : "dd/MM/yyyy";
        _hora = a is not null && FormatosRegionales.Hora.Contains(a.FormatoHora) ? a.FormatoHora : "HH:mm";
        try { _cultura = a is null ? new CultureInfo("es-MX") : CultureInfo.GetCultureInfo(a.Cultura); } catch (CultureNotFoundException) { _cultura = new CultureInfo("es-MX"); }
        try { _zona = a is null ? null : TimeZoneInfo.FindSystemTimeZoneById(a.ZonaHorariaId); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException) { _zona = null; }   // sin datos de zona en el navegador: hora local del equipo
    }

    private static DateTime ALocal(DateTime utc)
    {
        utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return _zona is null ? utc.ToLocalTime() : TimeZoneInfo.ConvertTimeFromUtc(utc, _zona);
    }

    public static string Fecha(DateTime utc) => ALocal(utc).ToString(_fecha, _cultura);
    public static string Hora(DateTime utc) => ALocal(utc).ToString(_hora, _cultura);
    public static string FechaHora(DateTime utc) => ALocal(utc).ToString(_fecha + " " + _hora, _cultura);
}

/// <summary>Carga la apariencia de la empresa (colores, logotipo, nombre, formatos) y la aplica. Si algo falla, quedan los valores por defecto.</summary>
public class TemaEmpresaService(ConfiguracionClient api, IJSRuntime js)
{
    public AparienciaEmpresaDto? Apariencia { get; private set; }
    public string? LogoDataUrl { get; private set; }
    public event Action? Cambio;

    public async Task CargarAsync()
    {
        var r = await api.AparienciaAsync();
        if (!r.Ok || r.Data is null) return;
        Apariencia = r.Data;
        LogoDataUrl = null;
        if (r.Data.TieneLogo)
        {
            var logo = await api.LogoAsync();
            if (logo is not null) LogoDataUrl = logo;
        }
        await AplicarAsync(Apariencia.ColorPrimario, Apariencia.ColorSecundario, Apariencia.Nombre);
        FormatoEmpresa.Aplicar(Apariencia);
        Cambio?.Invoke();
    }

    /// <summary>También sirve para la vista previa: aplica colores sin guardar.</summary>
    public async Task AplicarAsync(string? primario, string? secundario, string? nombre)
    {
        try { await js.InvokeVoidAsync("rtTema.aplicar", primario, secundario, nombre); }
        catch (JSException) { }
        catch (InvalidOperationException) { }
    }

    public void Limpiar()
    {
        Apariencia = null; LogoDataUrl = null;
        FormatoEmpresa.Aplicar(null);
        Cambio?.Invoke();
    }
}

public class ConfiguracionClient(ApiClient api)
{
    public Task<ApiResult<AparienciaEmpresaDto>> AparienciaAsync() => api.GetAsync<AparienciaEmpresaDto>("api/empresa/apariencia");

    /// <summary>Logotipo como data URL (el endpoint exige el JWT, por eso no se puede usar directamente en un &lt;img&gt;).</summary>
    public async Task<string?> LogoAsync()
    {
        var r = await api.GetBytesAsync("api/empresa/logo");
        if (!r.Ok || r.Data is null || r.Data.Length == 0) return null;
        return $"data:{TipoImagen(r.Data)};base64,{Convert.ToBase64String(r.Data)}";
    }

    public static string TipoImagen(byte[] b) =>
        b.Length > 3 && b[0] == 0x89 ? "image/png" : b.Length > 3 && b[0] == 0xFF ? "image/jpeg" : "image/webp";

    public Task<ApiResult> SubirLogoAsync(string nombre, byte[] contenido, string tipo) => api.SubirArchivoAsync("api/empresa/logo", nombre, contenido, tipo);
    public Task<ApiResult> EliminarLogoAsync() => api.SendAsync(HttpMethod.Delete, "api/empresa/logo");

    public Task<ApiResult<PermisosEfectivosDto>> PermisosEfectivosAsync(Guid usuarioId) =>
        api.GetAsync<PermisosEfectivosDto>($"api/usuarios/{usuarioId}/permisos-efectivos");

    public Task<ApiResult<EstadoSistemaDto>> EstadoSistemaAsync() => api.GetAsync<EstadoSistemaDto>("api/sistema/estado");
}
