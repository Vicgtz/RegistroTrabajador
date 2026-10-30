using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RegistroTrabajo.Api.Infrastructure;
using RegistroTrabajo.Models;

namespace RegistroTrabajo.Api.Controllers;

/// <summary>
/// Recibe los errores del navegador para dejarlos en el registro de errores (archivo diario de la API). Es público porque un error puede ocurrir
/// antes de iniciar sesión, así que está limitado por IP, acota el tamaño de cada campo y neutraliza saltos de línea para que nadie pueda falsear entradas del log.
/// </summary>
[ApiController, AllowAnonymous, EnableRateLimiting("cliente-errores"), Route("api/cliente/errores")]
public class ErroresClienteController : ControllerBase
{
    private readonly ILogger<ErroresClienteController> _logger;
    public ErroresClienteController(ILogger<ErroresClienteController> logger) => _logger = logger;

    [HttpPost]
    public IActionResult Registrar(ErrorClienteRequest r)
    {
        var usuario = User.FindFirst(ClaimNames.UsuarioId)?.Value;
        var empresa = User.FindFirst(ClaimNames.EmpresaId)?.Value;
        _logger.LogError("[CLIENTE] {Mensaje} | Origen={Origen} | Url={Url} | Usuario={Usuario} | Empresa={Empresa}\n{Detalle}",
            Limpiar(r.Mensaje, 500), Limpiar(r.Origen, 150), Limpiar(r.Url, 300), usuario ?? "anónimo", empresa ?? "-", Limpiar(r.Detalle, 4000, true));
        return NoContent();
    }

    // Quita los saltos de línea de los campos de una sola línea; en el detalle los conserva pero con sangría para que no parezcan entradas nuevas.
    private static string Limpiar(string? texto, int max, bool multilinea = false)
    {
        if (string.IsNullOrEmpty(texto)) return "-";
        var t = texto.Length > max ? texto[..max] : texto;
        t = multilinea ? t.Replace("\r", "").Replace("\n", "\n    ") : t.Replace("\r", " ").Replace("\n", " ");
        return string.Concat(t.Where(c => !char.IsControl(c) || c == '\n'));
    }
}
