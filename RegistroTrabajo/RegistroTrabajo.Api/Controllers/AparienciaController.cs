using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegistroTrabajo.Api.Infrastructure;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Api.Controllers;

// Bloque 12. La empresa sale siempre del token. La apariencia la puede leer cualquier usuario de la empresa (no contiene datos sensibles);
// cambiarla exige empresa.configurar (en el controlador y en el servicio).

[ApiController, Authorize, Route("api/empresa")]
public class AparienciaController : ControllerBase
{
    private readonly IAparienciaService _apariencia;
    private readonly ILogoEmpresaService _logo;
    public AparienciaController(IAparienciaService apariencia, ILogoEmpresaService logo) { _apariencia = apariencia; _logo = logo; }

    [HttpGet("apariencia")]
    public async Task<AparienciaEmpresaDto> Apariencia(CancellationToken ct) => await _apariencia.ObtenerAsync(ct);

    [HttpGet("logo")]
    public async Task<IActionResult> ObtenerLogo(CancellationToken ct)
    {
        var logo = await _logo.ObtenerAsync(ct);
        if (logo is null) return NotFound();
        Response.Headers.CacheControl = "private, max-age=3600";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(logo.Contenido, logo.TipoContenido);
    }

    [HttpPost("logo"), RequierePermiso(Permisos.EmpresaConfigurar), RequestSizeLimit(FormatosRegionales.MaxBytesLogo + 100_000)]
    public async Task<IActionResult> SubirLogo(IFormFile archivo, CancellationToken ct)
    {
        if (archivo.Length == 0) throw ServiceException.Validation("El archivo está vacío.");
        if (archivo.Length > FormatosRegionales.MaxBytesLogo) throw ServiceException.Validation("El logotipo no puede pesar más de 512 KB.");
        using var ms = new MemoryStream((int)archivo.Length);
        await archivo.CopyToAsync(ms, ct);
        await _logo.GuardarAsync(ms.ToArray(), ct);
        return NoContent();
    }

    [HttpDelete("logo"), RequierePermiso(Permisos.EmpresaConfigurar)]
    public async Task<IActionResult> EliminarLogo(CancellationToken ct) { await _logo.EliminarAsync(ct); return NoContent(); }
}
