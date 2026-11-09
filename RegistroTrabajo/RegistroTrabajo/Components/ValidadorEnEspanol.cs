using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace RegistroTrabajo.Components;

/// <summary>
/// Sustituye a <c>&lt;DataAnnotationsValidator /&gt;</c>: valida igual (atributos de DataAnnotations),
/// pero los mensajes estándar de .NET, que vienen en inglés, se muestran traducidos al español.
/// Los mensajes que ya están en español (ErrorMessage propio) no se tocan.
/// </summary>
public sealed class ValidadorEnEspanol : ComponentBase, IDisposable
{
    private EditContext? _contexto;
    private ValidationMessageStore? _mensajes;

    [CascadingParameter] public EditContext? EditContext { get; set; }

    protected override void OnInitialized()
    {
        if (EditContext is null)
            throw new InvalidOperationException("ValidadorEnEspanol debe usarse dentro de un EditForm.");

        _contexto = EditContext;
        _mensajes = new ValidationMessageStore(_contexto);
        _contexto.OnValidationRequested += AlSolicitarValidacion;
        _contexto.OnFieldChanged += AlCambiarCampo;
    }

    private void AlSolicitarValidacion(object? remitente, ValidationRequestedEventArgs e)
    {
        if (_contexto is null || _mensajes is null) return;

        var modelo = _contexto.Model;
        _mensajes.Clear();

        var resultados = new List<ValidationResult>();
        Validator.TryValidateObject(modelo, new ValidationContext(modelo), resultados, validateAllProperties: true);

        foreach (var r in resultados)
        {
            var texto = TraductorValidacion.Traducir(r.ErrorMessage);
            var miembros = r.MemberNames.Where(m => !string.IsNullOrEmpty(m)).ToList();
            if (miembros.Count == 0)
                _mensajes.Add(new FieldIdentifier(modelo, string.Empty), texto);
            else
                foreach (var miembro in miembros)
                    _mensajes.Add(new FieldIdentifier(modelo, miembro), texto);
        }

        _contexto.NotifyValidationStateChanged();
    }

    private void AlCambiarCampo(object? remitente, FieldChangedEventArgs e)
    {
        if (_contexto is null || _mensajes is null) return;

        var campo = e.FieldIdentifier;
        PropertyInfo? propiedad;
        try { propiedad = campo.Model.GetType().GetProperty(campo.FieldName, BindingFlags.Public | BindingFlags.Instance); }
        catch (AmbiguousMatchException) { return; }
        if (propiedad is null || propiedad.GetIndexParameters().Length > 0) return;

        var valor = propiedad.GetValue(campo.Model);
        var contexto = new ValidationContext(campo.Model) { MemberName = campo.FieldName };
        var resultados = new List<ValidationResult>();

        _mensajes.Clear(campo);
        Validator.TryValidateProperty(valor, contexto, resultados);
        _mensajes.Add(campo, resultados.Select(r => TraductorValidacion.Traducir(r.ErrorMessage)));

        _contexto.NotifyValidationStateChanged();
    }

    public void Dispose()
    {
        if (_contexto is null) return;
        _contexto.OnValidationRequested -= AlSolicitarValidacion;
        _contexto.OnFieldChanged -= AlCambiarCampo;
        _mensajes?.Clear();
        _contexto.NotifyValidationStateChanged();
    }
}

/// <summary>Traduce los mensajes estándar de DataAnnotations y de los campos de Blazor (inglés → español).</summary>
public static class TraductorValidacion
{
    private const RegexOptions Op = RegexOptions.CultureInvariant | RegexOptions.Singleline;

    // Grupos "c", "a" y "b" son nombres de campo (se vuelven legibles); el resto se copia tal cual.
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
        (new(@"^The (?<c>.+) field must be a number\.$", Op), "El campo {c} debe ser un número."),
        (new(@"^The field (?<c>.+) must be a number\.$", Op), "El campo {c} debe ser un número."),
        (new(@"^The (?<c>.+) field must be a date\.$", Op), "El campo {c} debe ser una fecha válida."),
        (new(@"^The (?<c>.+) field is not valid\.$", Op), "El campo {c} no es válido."),
        (new(@"^The value '(?<v>.*)' is not valid for (?<c>.+)\.$", Op), "El valor '{v}' no es válido para {c}."),
        (new(@"^The value '(?<v>.*)' is invalid\.$", Op), "El valor '{v}' no es válido."),
    };

    // Palabras de nombres de propiedad que en código van sin tilde ni ñ.
    private static readonly Dictionary<string, string> Palabras = new(StringComparer.OrdinalIgnoreCase)
    {
        ["anio"] = "año", ["anios"] = "años", ["actualizacion"] = "actualización", ["antiguedad"] = "antigüedad",
        ["categoria"] = "categoría", ["categorias"] = "categorías", ["contrasena"] = "contraseña",
        ["descripcion"] = "descripción", ["dia"] = "día", ["dias"] = "días", ["direccion"] = "dirección",
        ["duracion"] = "duración", ["excepcion"] = "excepción", ["institucion"] = "institución",
        ["limite"] = "límite", ["maxima"] = "máxima", ["minima"] = "mínima", ["minimo"] = "mínimo",
        ["modulo"] = "módulo", ["numero"] = "número", ["sesion"] = "sesión", ["telefono"] = "teléfono",
        ["titulo"] = "título", ["version"] = "versión", ["id"] = "id", ["ids"] = "ids", ["url"] = "URL",
    };

    private static readonly Regex Identificador = new(@"^[A-Za-z][A-Za-z0-9]*$", RegexOptions.CultureInvariant);
    private static readonly Regex Partes = new(@"[A-Z]+(?![a-z])|[A-Z]?[a-z0-9]+", RegexOptions.CultureInvariant);

    public static string Traducir(string? mensaje)
    {
        if (string.IsNullOrWhiteSpace(mensaje)) return "Valor no válido.";
        var texto = mensaje.Trim();

        foreach (var (patron, plantilla) in Reglas)
        {
            var m = patron.Match(texto);
            if (!m.Success) continue;

            var salida = plantilla;
            foreach (Group g in m.Groups)
            {
                if (string.IsNullOrEmpty(g.Name) || char.IsDigit(g.Name[0])) continue;
                var valor = g.Name is "c" or "a" or "b" ? Etiqueta(g.Value) : g.Value;
                salida = salida.Replace("{" + g.Name + "}", valor);
            }
            return salida;
        }
        return texto; // ya está en español (o no es un mensaje conocido)
    }

    /// <summary>"AniosExperiencia" → "años experiencia". Si ya es una etiqueta legible, la deja igual.</summary>
    public static string Etiqueta(string nombre)
    {
        if (!Identificador.IsMatch(nombre)) return nombre;      // ya tiene espacios, tildes, etc.
        if (nombre.All(char.IsUpper)) return nombre;             // siglas: RFC

        var palabras = Partes.Matches(nombre).Select(p => p.Value)
            .Select(p => p.All(char.IsUpper) && p.Length > 1 ? p : Palabras.TryGetValue(p, out var es) ? es : p.ToLowerInvariant());
        return string.Join(' ', palabras);
    }
}
