using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

/// <summary>
/// Envía a la API (api/cliente/errores) los errores del navegador: excepciones no controladas de los componentes y cualquier mensaje de nivel Error.
/// Usa su propio HttpClient (sin sesión ni renovación de token) para no generar bucles, nunca lanza excepciones y limita la frecuencia de envío.
/// </summary>
public sealed class ErroresClienteLoggerProvider : ILoggerProvider
{
    private const int MaxPorMinuto = 10;
    private readonly HttpClient _http;
    private readonly Queue<DateTime> _envios = new();
    private string? _ultimo;

    /// <summary>Se asigna al arrancar la aplicación para incluir la página actual en el aviso.</summary>
    public IServiceProvider? Servicios { get; set; }

    public ErroresClienteLoggerProvider(string apiUrl) => _http = new HttpClient { BaseAddress = new Uri(apiUrl) };

    public ILogger CreateLogger(string categoryName) => new Registrador(this, categoryName);

    public void Dispose() => _http.Dispose();

    private void Enviar(string categoria, string mensaje, Exception? ex)
    {
        // Los errores del propio transporte HTTP no se reenvían (la API puede ser justo lo que falla).
        if (categoria.StartsWith("System.Net.Http", StringComparison.Ordinal)) return;

        var clave = categoria + mensaje;
        if (clave == _ultimo) return;   // el mismo error en cadena (p. ej. un componente que falla al volver a dibujarse)
        _ultimo = clave;

        var ahora = DateTime.UtcNow;
        while (_envios.Count > 0 && ahora - _envios.Peek() > TimeSpan.FromMinutes(1)) _envios.Dequeue();
        if (_envios.Count >= MaxPorMinuto) return;
        _envios.Enqueue(ahora);

        string? url = null;
        try { url = Servicios?.GetService(typeof(NavigationManager)) is NavigationManager n ? n.Uri : null; } catch (Exception) { }

        var cuerpo = new ErrorClienteRequest
        {
            Mensaje = Cortar(ex?.Message is { Length: > 0 } m ? $"{mensaje}: {m}" : mensaje, 500),
            Detalle = ex is null ? null : Cortar(ex.ToString(), 4000),
            Url = url is null ? null : Cortar(url, 300),
            Origen = Cortar(categoria, 150)
        };
        _ = PublicarAsync(cuerpo);
    }

    private async Task PublicarAsync(ErrorClienteRequest cuerpo)
    {
        try { using var _ = await _http.PostAsJsonAsync("api/cliente/errores", cuerpo); }
        catch (Exception) { /* sin conexión o API caída: el aviso se pierde, la interfaz no debe verse afectada */ }
    }

    private static string Cortar(string t, int max) => t.Length > max ? t[..max] : t;

    private sealed class Registrador(ErroresClienteLoggerProvider proveedor, string categoria) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            try { proveedor.Enviar(categoria, formatter(state, exception), exception); } catch (Exception) { }
        }
    }
}
