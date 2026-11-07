using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public static class Formato
{
    /// <summary>Los instantes llegan en UTC sin 'Z'; se muestran en la hora local del navegador.</summary>
    public static string Local(DateTime? utc) => utc is null
        ? "—"
        : DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc).ToLocalTime().ToString("dd/MM/yyyy HH:mm");

    public static string EstadoUsuario(byte e) => e switch
    {
        Models.EstadoUsuario.Activo => "Activo", Models.EstadoUsuario.Inactivo => "Inactivo", _ => "Suspendido"
    };

    public static string EstadoEmpresa(byte e) => e switch
    {
        Models.EstadoEmpresa.Activa => "Activa", Models.EstadoEmpresa.Suspendida => "Suspendida", _ => "Inactiva"
    };

    public static string ResultadoAcceso(byte r) => r switch
    {
        Models.ResultadoAcceso.Exitoso => "Exitoso",
        Models.ResultadoAcceso.CredencialesInvalidas => "Credenciales inválidas",
        Models.ResultadoAcceso.CuentaBloqueada => "Cuenta bloqueada",
        Models.ResultadoAcceso.CuentaInactiva => "Cuenta inactiva",
        Models.ResultadoAcceso.RefreshInvalido => "Sesión inválida",
        _ => "Desconocido"
    };

    /// <summary>Muestra un JSON con sangría; si no es JSON válido devuelve el texto tal cual.</summary>
    public static string Json(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return "—";
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return System.Text.Json.JsonSerializer.Serialize(doc.RootElement, new System.Text.Json.JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        }
        catch (System.Text.Json.JsonException) { return json; }
    }
}
