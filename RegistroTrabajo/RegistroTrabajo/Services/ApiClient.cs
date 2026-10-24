using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;

namespace RegistroTrabajo.Services;

public record ApiResult(bool Ok, string? Error, HttpStatusCode Status);

public record ApiResult<T>(bool Ok, T? Data, string? Error, HttpStatusCode Status)
{
    public ApiResult Sin() => new(Ok, Error, Status);
}

/// <summary>Cliente HTTP único hacia la API. Convierte las respuestas (ProblemDetails incluido) en <see cref="ApiResult{T}"/>.</summary>
public class ApiClient(HttpClient http, JsonSerializerOptions json, NavigationManager navigation)
{
    private const string SinConexion = "No se pudo conectar con el servidor. Inténtalo de nuevo.";

    public Task<ApiResult<T>> GetAsync<T>(string url, CancellationToken ct = default) =>
        SendAsync<T>(HttpMethod.Get, url, null, ct);

    public Task<ApiResult<T>> PostAsync<T>(string url, object? body = null, CancellationToken ct = default) =>
        SendAsync<T>(HttpMethod.Post, url, body, ct);

    public async Task<ApiResult> SendAsync(HttpMethod method, string url, object? body = null, CancellationToken ct = default) =>
        (await SendAsync<JsonElement?>(method, url, body, ct)).Sin();

    public async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string url, object? body, CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(method, url);
            if (body is not null) request.Content = JsonContent.Create(body, body.GetType(), options: json);

            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var error = await LeerErrorAsync(response, ct);
                // La API bloquea todo salvo el cambio de contraseña mientras la cuenta lo requiera.
                if (response.StatusCode == HttpStatusCode.Forbidden && error.Contains("cambiar tu contraseña", StringComparison.OrdinalIgnoreCase)
                    && !navigation.Uri.Contains("cuenta/contrasena", StringComparison.OrdinalIgnoreCase))
                    navigation.NavigateTo("cuenta/contrasena");
                return new ApiResult<T>(false, default, error, response.StatusCode);
            }

            if (response.StatusCode == HttpStatusCode.NoContent || response.Content.Headers.ContentLength == 0)
                return new ApiResult<T>(true, default, null, response.StatusCode);

            var data = await response.Content.ReadFromJsonAsync<T>(json, ct);
            return new ApiResult<T>(true, data, null, response.StatusCode);
        }
        catch (HttpRequestException)
        {
            return new ApiResult<T>(false, default, SinConexion, 0);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return new ApiResult<T>(false, default, SinConexion, 0);
        }
        catch (JsonException)
        {
            return new ApiResult<T>(false, default, "La respuesta del servidor no es válida.", HttpStatusCode.OK);
        }
    }

    private static async Task<string> LeerErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            return "Demasiados intentos. Espera un momento e inténtalo de nuevo.";

        try
        {
            var texto = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(texto);
            var raiz = doc.RootElement;

            // Validación de modelo de ASP.NET: { errors: { Campo: ["mensaje"] } }
            if (raiz.TryGetProperty("errors", out var errores) && errores.ValueKind == JsonValueKind.Object)
            {
                var mensajes = errores.EnumerateObject()
                    .SelectMany(p => p.Value.EnumerateArray().Select(e => e.GetString()))
                    .Where(m => !string.IsNullOrWhiteSpace(m));
                var unidos = string.Join(" ", mensajes);
                if (unidos.Length > 0) return unidos;
            }

            if (raiz.TryGetProperty("detail", out var detalle) && detalle.GetString() is { Length: > 0 } d) return d;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException) { }

        return response.StatusCode switch
        {
            HttpStatusCode.Forbidden => "No tienes permiso para realizar esta acción.",
            HttpStatusCode.NotFound => "No se encontró el recurso solicitado.",
            HttpStatusCode.Unauthorized => "Tu sesión no es válida. Inicia sesión de nuevo.",
            _ => "No se pudo completar la operación."
        };
    }
}
