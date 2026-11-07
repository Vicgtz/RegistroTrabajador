using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegistroTrabajo.Api.Infrastructure;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Api.Controllers;

[ApiController]
public class SistemaController : ControllerBase
{
    private readonly IEstadoSistemaService _svc;
    public SistemaController(IEstadoSistemaService svc) => _svc = svc;

    /// <summary>Disponibilidad mínima para monitoreo: solo "ok" o "degradado", sin detalles.</summary>
    [HttpGet("health"), AllowAnonymous]
    public async Task<IActionResult> Health(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var ok = await _svc.DisponibleAsync(ct);
        return StatusCode(ok ? 200 : 503, new { estado = ok ? "ok" : "degradado" });
    }

    [HttpGet("api/sistema/estado"), Authorize, RequierePermiso(Permisos.PlataformaSistemaConsultar)]
    public async Task<EstadoSistemaDto> Estado(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return await _svc.ObtenerAsync(ct);
    }
}
