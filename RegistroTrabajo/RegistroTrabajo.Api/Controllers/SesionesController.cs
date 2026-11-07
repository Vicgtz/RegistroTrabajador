using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegistroTrabajo.Api.Infrastructure;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Api.Controllers;

[ApiController, Authorize, Route("api/sesiones")]
public class SesionesController : ControllerBase
{
    private readonly ISesionService _sesiones;
    public SesionesController(ISesionService sesiones) => _sesiones = sesiones;

    [HttpGet, RequierePermiso(Permisos.SesionesConsultar)]
    public async Task<PagedResult<SesionDto>> Listar([FromQuery] PageQuery q, CancellationToken ct) => await _sesiones.ListarActivasAsync(q, ct);

    [HttpDelete("{id:guid}"), RequierePermiso(Permisos.SesionesRevocar)]
    public async Task<IActionResult> Revocar(Guid id, CancellationToken ct)
    {
        await _sesiones.RevocarAsync(id, ct);
        return NoContent();
    }

    // Sesiones del propio usuario: no requieren permiso adicional.
    [HttpGet("mias")]
    public async Task<PagedResult<SesionDto>> Mias([FromQuery] PageQuery q, CancellationToken ct) => await _sesiones.ListarMiasAsync(q, ct);

    [HttpDelete("mias/{id:guid}")]
    public async Task<IActionResult> RevocarMia(Guid id, CancellationToken ct)
    {
        await _sesiones.RevocarMiaAsync(id, ct);
        return NoContent();
    }
}
