using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;

namespace RegistroTrabajo.Services;

public class EmailOptions
{
    /// <summary>"Dev" escribe los correos en archivos y en el log; "Smtp" los envía de verdad.</summary>
    public string Mode { get; set; } = "Dev";
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string? User { get; set; }
    public string? Password { get; set; }
    public string From { get; set; } = "no-responder@registrotrabajo.local";
    public string FromName { get; set; } = "Registro de Trabajo";
    /// <summary>Dirección base del cliente Blazor; se usa para armar los enlaces de los correos.</summary>
    public string ClientBaseUrl { get; set; } = "http://localhost:5118";
    /// <summary>Carpeta de salida del modo Dev (por defecto, "correos-dev" junto al ejecutable).</summary>
    public string? DevDirectory { get; set; }
}

public interface IEmailSender
{
    Task EnviarAsync(string para, string asunto, string cuerpo, CancellationToken ct);
}

/// <summary>Solo desarrollo: no envía nada; guarda el mensaje (con sus enlaces) en un archivo y lo registra en el log.</summary>
public class DevEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<DevEmailSender> _logger;

    public DevEmailSender(EmailOptions options, ILogger<DevEmailSender> logger)
    {
        _options = options; _logger = logger;
    }

    public async Task EnviarAsync(string para, string asunto, string cuerpo, CancellationToken ct)
    {
        var dir = _options.DevDirectory ?? Path.Combine(AppContext.BaseDirectory, "correos-dev");
        Directory.CreateDirectory(dir);
        var archivo = Path.Combine(dir, $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(archivo, $"Para: {para}\nAsunto: {asunto}\n\n{cuerpo}\n", ct);
        _logger.LogInformation("Correo (modo Dev) para {Para}: {Asunto}. Contenido en {Archivo}", para, asunto, archivo);
    }
}

public class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    public SmtpEmailSender(EmailOptions options) => _options = options;

    public async Task EnviarAsync(string para, string asunto, string cuerpo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.Host))
            throw new InvalidOperationException("Email:Host no está configurado.");

        using var mensaje = new MailMessage
        {
            From = new MailAddress(_options.From, _options.FromName),
            Subject = asunto,
            Body = cuerpo,
            IsBodyHtml = false
        };
        mensaje.To.Add(para);

        using var cliente = new SmtpClient(_options.Host, _options.Port) { EnableSsl = _options.UseSsl };
        if (!string.IsNullOrEmpty(_options.User))
            cliente.Credentials = new NetworkCredential(_options.User, _options.Password);
        await cliente.SendMailAsync(mensaje, ct);
    }
}

internal static class PlantillasCorreo
{
    public static (string Asunto, string Cuerpo) Recuperacion(EmailOptions o, string token, int minutos) =>
        ("Restablece tu contraseña",
         $"Recibimos una solicitud para restablecer tu contraseña.\n\nAbre este enlace (válido por {minutos} minutos y de un solo uso):\n{Enlace(o, "restablecer-contrasena", token)}\n\nSi no fuiste tú, ignora este mensaje: tu contraseña no cambiará.");

    public static (string Asunto, string Cuerpo) Confirmacion(EmailOptions o, string token, int horas) =>
        ("Confirma tu correo electrónico",
         $"Confirma tu correo electrónico para activar todas las funciones de tu cuenta.\n\nAbre este enlace (válido por {horas} horas):\n{Enlace(o, "confirmar-correo", token)}\n\nSi no esperabas este mensaje, ignóralo.");

    private static string Enlace(EmailOptions o, string ruta, string token) =>
        $"{o.ClientBaseUrl.TrimEnd('/')}/{ruta}?token={Uri.EscapeDataString(token)}";
}
