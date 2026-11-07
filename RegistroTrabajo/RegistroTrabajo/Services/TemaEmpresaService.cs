using Microsoft.JSInterop;
using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

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
