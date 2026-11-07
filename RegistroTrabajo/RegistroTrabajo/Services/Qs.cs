using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

/// <summary>Arma la cadena de consulta omitiendo los valores vacíos.</summary>
public static class Qs
{
    public static string De(params (string Clave, object? Valor)[] pares)
    {
        var partes = pares
            .Where(p => p.Valor is not null && !(p.Valor is string s && string.IsNullOrWhiteSpace(s)))
            .Select(p => $"{Uri.EscapeDataString(p.Clave)}={Uri.EscapeDataString(Texto(p.Valor!))}");
        var qs = string.Join("&", partes);
        return qs.Length == 0 ? "" : "?" + qs;
    }

    private static string Texto(object v) => v switch
    {
        DateTime d => d.ToString("o"),
        bool b => b ? "true" : "false",
        IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
        _ => v.ToString() ?? ""
    };
}
