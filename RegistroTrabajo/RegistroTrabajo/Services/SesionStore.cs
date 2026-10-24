using System.Text.Json;
using Blazored.LocalStorage;
using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

/// <summary>Sesión guardada en el navegador. El JWT y el refresh token viven aquí (localStorage).</summary>
public class SesionLocal
{
    public string AccessToken { get; set; } = "";
    public DateTime AccessExpiraUtc { get; set; }
    public string RefreshToken { get; set; } = "";
    public DateTime RefreshExpiraUtc { get; set; }
    public PerfilDto? Perfil { get; set; }

    public static SesionLocal Desde(TokenResponse t, PerfilDto? perfil = null) => new()
    {
        AccessToken = t.AccessToken,
        AccessExpiraUtc = Utc(t.AccessTokenExpiraUtc),
        RefreshToken = t.RefreshToken,
        RefreshExpiraUtc = Utc(t.RefreshTokenExpiraUtc),
        Perfil = perfil
    };

    // La API serializa fechas UTC sin 'Z'; se marcan como UTC para comparar correctamente.
    private static DateTime Utc(DateTime d) => DateTime.SpecifyKind(d, DateTimeKind.Utc);
}

public class SesionStore(ILocalStorageService storage)
{
    private const string Key = "rt.sesion";

    /// <summary>Se dispara cuando la sesión cambia (inicio, cierre o expiración).</summary>
    public event Action? Cambio;

    public async ValueTask<SesionLocal?> GetAsync()
    {
        try
        {
            return await storage.GetItemAsync<SesionLocal>(Key);
        }
        catch (JsonException)
        {
            await storage.RemoveItemAsync(Key);
            return null;
        }
    }

    public async Task SetAsync(SesionLocal sesion, bool notificar = true)
    {
        await storage.SetItemAsync(Key, sesion);
        if (notificar) Cambio?.Invoke();
    }

    public async Task ClearAsync()
    {
        await storage.RemoveItemAsync(Key);
        Cambio?.Invoke();
    }
}
