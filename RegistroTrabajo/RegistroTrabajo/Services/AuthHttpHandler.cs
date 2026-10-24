using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

/// <summary>
/// Agrega el JWT a cada solicitud hacia la API. Si está por vencer lo renueva con el refresh token
/// (cliente "ApiAnon", sin este handler, para no entrar en recursión). Ante un 401 cierra la sesión local.
/// </summary>
public class AuthHttpHandler(SesionStore store, IHttpClientFactory factory, JsonSerializerOptions json) : DelegatingHandler
{
    public const string ClienteAnonimo = "ApiAnon";
    private static readonly SemaphoreSlim Candado = new(1, 1);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var sesion = await store.GetAsync();
        if (sesion is not null && sesion.AccessExpiraUtc <= DateTime.UtcNow.AddSeconds(30))
            sesion = await RenovarAsync(ct);

        if (sesion is not null && !string.IsNullOrEmpty(sesion.AccessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sesion.AccessToken);

        var response = await base.SendAsync(request, ct);

        // Sesión revocada, expirada o cuenta desactivada en el servidor.
        if (response.StatusCode == HttpStatusCode.Unauthorized && sesion is not null)
            await store.ClearAsync();

        return response;
    }

    private async Task<SesionLocal?> RenovarAsync(CancellationToken ct)
    {
        await Candado.WaitAsync(ct);
        try
        {
            var actual = await store.GetAsync();
            if (actual is null) return null;
            if (actual.AccessExpiraUtc > DateTime.UtcNow.AddSeconds(30)) return actual;   // otra solicitud ya renovó

            if (actual.RefreshExpiraUtc <= DateTime.UtcNow)
            {
                await store.ClearAsync();
                return null;
            }

            try
            {
                var http = factory.CreateClient(ClienteAnonimo);
                using var resp = await http.PostAsJsonAsync("api/auth/refresh",
                    new RefreshRequest { RefreshToken = actual.RefreshToken }, json, ct);

                if (!resp.IsSuccessStatusCode)
                {
                    if (resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest)
                        await store.ClearAsync();
                    return null;
                }

                var token = await resp.Content.ReadFromJsonAsync<TokenResponse>(json, ct);
                if (token is null) return null;

                var nueva = SesionLocal.Desde(token, actual.Perfil);
                await store.SetAsync(nueva, notificar: false);
                return nueva;
            }
            catch (HttpRequestException)
            {
                return actual;   // sin red: se intenta con el token actual y la API decidirá
            }
        }
        finally
        {
            Candado.Release();
        }
    }
}
