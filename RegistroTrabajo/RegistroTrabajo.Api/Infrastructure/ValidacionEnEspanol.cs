using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace RegistroTrabajo.Api.Infrastructure;

/// <summary>
/// Los mensajes de validación automática de ASP.NET Core (DataAnnotations, enlace de modelo, JSON) vienen en inglés.
/// Aquí se traducen para que la API responda siempre en español.
/// </summary>
public static class ValidacionEnEspanol
{
    private const RegexOptions Op = RegexOptions.CultureInvariant | RegexOptions.Singleline;

    // (patrón inglés estándar de DataAnnotations → plantilla en español). Los grupos nombrados se sustituyen en la plantilla.
    private static readonly (Regex Patron, string Plantilla)[] Reglas =
    {
        (new(@"^The (?<c>.+) field is required\.$", Op), "El campo {c} es obligatorio."),
        (new(@"^The field (?<c>.+) must be a string with a minimum length of (?<min>.+) and a maximum length of (?<max>.+)\.$", Op), "El campo {c} debe tener entre {min} y {max} caracteres."),
        (new(@"^The field (?<c>.+) must be a string with a maximum length of (?<max>.+)\.$", Op), "El campo {c} no puede superar los {max} caracteres."),
        (new(@"^The field (?<c>.+) must be a string or array type with a maximum length of '(?<max>.+)'\.$", Op), "El campo {c} no puede superar los {max} elementos o caracteres."),
        (new(@"^The field (?<c>.+) must be a string or array type with a minimum length of '(?<min>.+)'\.$", Op), "El campo {c} debe tener al menos {min} elementos o caracteres."),
        (new(@"^The field (?<c>.+) must be between (?<min>.+) and (?<max>.+)\.$", Op), "El campo {c} debe estar entre {min} y {max}."),
        (new(@"^The (?<c>.+) field is not a valid e-?mail address\.$", Op), "El campo {c} no es un correo electrónico válido."),
        (new(@"^The (?<c>.+) field is not a valid phone number\.$", Op), "El campo {c} no es un teléfono válido."),
        (new(@"^The (?<c>.+) field is not a valid fully-qualified http, https, or ftp URL\.$", Op), "El campo {c} no es una dirección web válida."),
        (new(@"^The field (?<c>.+) must match the regular expression '.+'\.$", Op), "El campo {c} no tiene un formato válido."),
        (new(@"^The field (?<c>.+) is invalid\.$", Op), "El campo {c} no es válido."),
        (new(@"^'(?<a>.+)' and '(?<b>.+)' do not match\.$", Op), "Los campos {a} y {b} no coinciden."),
        (new(@"^The field (?<c>.+) must be a number\.$", Op), "El campo {c} debe ser un número."),
        (new(@"^The value '(?<v>.*)' is not valid for (?<c>.+)\.$", Op), "El valor '{v}' no es válido para {c}."),
        (new(@"^The value '(?<v>.*)' is invalid\.$", Op), "El valor '{v}' no es válido."),
        (new(@"^The supplied value is invalid for (?<c>.+)\.$", Op), "El valor indicado no es válido para {c}."),
        (new(@"^A value for the '(?<c>.+)' parameter or property was not provided\.$", Op), "Falta indicar un valor para {c}."),
        (new(@"^The (?<c>.+) field is not valid\.$", Op), "El campo {c} no es válido."),
        (new(@"^The JSON value could not be converted to .+\. Path: \$\.(?<c>[^ |]+).*$", Op), "El valor del campo {c} no tiene un formato válido."),
        (new(@"^The JSON value could not be converted to .+$", Op), "Algún valor de la solicitud no tiene un formato válido."),
        (new(@"^JSON deserialization for type .+ was missing required properties.*$", Op), "Faltan campos obligatorios en la solicitud."),
        (new(@"^'(?<v>.*)' is not valid\.$", Op), "El valor '{v}' no es válido."),
    };

    private static readonly Dictionary<string, string> Exactos = new(StringComparer.Ordinal)
    {
        ["A non-empty request body is required."] = "La solicitud no puede estar vacía.",
        ["The request field is required."] = "La solicitud no puede estar vacía.",
        ["The supplied value is invalid."] = "El valor indicado no es válido.",
        ["Null value is invalid."] = "El valor no puede ser nulo.",
        ["A value is required."] = "Falta indicar un valor.",
        ["The value provided is invalid."] = "El valor indicado no es válido.",
        ["The field must be a number."] = "El campo debe ser un número.",
        ["The value '' is invalid."] = "El valor indicado no es válido.",
        ["One or more validation errors occurred."] = "Hay errores de validación en los datos enviados.",
        ["Bad Request"] = "Solicitud no válida",
        ["Unauthorized"] = "No autenticado",
        ["Forbidden"] = "Acceso denegado",
        ["Not Found"] = "No encontrado",
        ["Conflict"] = "Conflicto",
        ["Unsupported Media Type"] = "Tipo de contenido no admitido",
        ["Method Not Allowed"] = "Método no permitido",
        ["Internal Server Error"] = "Error interno",
    };

    /// <summary>Traduce un mensaje conocido; si no lo reconoce lo devuelve igual (puede que ya esté en español).</summary>
    public static string Traducir(string mensaje)
    {
        if (string.IsNullOrWhiteSpace(mensaje)) return mensaje;
        mensaje = mensaje.Trim();
        if (!mensaje.EndsWith('.') && !Exactos.ContainsKey(mensaje)) mensaje += ".";
        if (Exactos.TryGetValue(mensaje, out var directo)) return directo;
        foreach (var (patron, plantilla) in Reglas)
        {
            var m = patron.Match(mensaje);
            if (!m.Success) continue;
            var salida = plantilla;
            foreach (Group g in m.Groups)
                if (!string.IsNullOrEmpty(g.Name) && !char.IsDigit(g.Name[0]))
                    salida = salida.Replace("{" + g.Name + "}", g.Value);
            return salida;
        }
        return mensaje;
    }

    /// <summary>Respuesta 400 de validación de modelo, con los mensajes traducidos y el mismo formato que ya lee el cliente (<c>errors</c> + <c>detail</c>).</summary>
    public static IActionResult RespuestaInvalida(ActionContext ctx)
    {
        var errores = new Dictionary<string, string[]>();
        foreach (var (clave, entrada) in ctx.ModelState)
        {
            if (entrada.Errors.Count == 0) continue;
            errores[LimpiarClave(clave)] = entrada.Errors
                .Select(e => Traducir(e.ErrorMessage.Length > 0 ? e.ErrorMessage : "Valor no válido."))
                .ToArray();
        }

        var problema = new ValidationProblemDetails(errores)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Solicitud no válida",
            Detail = string.Join(" ", errores.Values.SelectMany(v => v).Distinct())
        };
        problema.Extensions["correlationId"] = ctx.HttpContext.Items[HttpCurrentUser.CorrelationItem]?.ToString();
        return new ObjectResult(problema) { StatusCode = StatusCodes.Status400BadRequest, ContentTypes = { "application/problem+json" } };
    }

    private static string LimpiarClave(string clave) => clave.StartsWith("$.", StringComparison.Ordinal) ? clave[2..] : clave;

    /// <summary>Mensajes del enlace de modelo (valores con formato incorrecto, cuerpo vacío…) en español.</summary>
    public static void ConfigurarEnlaceDeModelo(DefaultModelBindingMessageProvider p)
    {
        p.SetValueMustNotBeNullAccessor(_ => "El campo es obligatorio.");
        p.SetMissingBindRequiredValueAccessor(c => $"Falta indicar un valor para {c}.");
        p.SetMissingKeyOrValueAccessor(() => "Falta indicar un valor.");
        p.SetMissingRequestBodyRequiredValueAccessor(() => "La solicitud no puede estar vacía.");
        p.SetAttemptedValueIsInvalidAccessor((v, c) => $"El valor '{v}' no es válido para {c}.");
        p.SetNonPropertyAttemptedValueIsInvalidAccessor(v => $"El valor '{v}' no es válido.");
        p.SetUnknownValueIsInvalidAccessor(c => $"El valor indicado no es válido para {c}.");
        p.SetNonPropertyUnknownValueIsInvalidAccessor(() => "El valor indicado no es válido.");
        p.SetValueIsInvalidAccessor(v => $"El valor '{v}' no es válido.");
        p.SetValueMustBeANumberAccessor(c => $"El campo {c} debe ser un número.");
        p.SetNonPropertyValueMustBeANumberAccessor(() => "El campo debe ser un número.");
    }
}
