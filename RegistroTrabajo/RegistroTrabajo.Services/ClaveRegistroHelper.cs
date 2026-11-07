using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RegistroTrabajo.Data;
using RegistroTrabajo.Data.Entities;
using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

internal static class ClaveRegistroHelper
{
    // Sin caracteres ambiguos (0/O, 1/I/L) para poder dictar o copiar la clave sin errores.
    private const string Alfabeto = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    /// <summary>16 caracteres (≈79 bits) en grupos de 4: XXXX-XXXX-XXXX-XXXX.</summary>
    public static string Nueva()
    {
        var chars = new char[19];
        for (int i = 0, j = 0; i < 16; i++)
        {
            if (i > 0 && i % 4 == 0) chars[j++] = '-';
            chars[j++] = Alfabeto[RandomNumberGenerator.GetInt32(Alfabeto.Length)];
        }
        return new string(chars);
    }

    /// <summary>Ignora guiones, espacios y mayúsculas/minúsculas.</summary>
    public static byte[] Hash(string clave) =>
        TokenHelper.Hash(new string(clave.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant());
}
