using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegistroTrabajo.Api.Infrastructure;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Api.Controllers;

// Bloque 13: administración de la clave de registro (la clave en claro solo se devuelve al generarla).
[ApiController, Authorize, Route("api/empresa/clave-registro"), RequierePermiso(Permisos.EmpresaConfigurar)]
public class ClaveRegistroController : ControllerBase
{
    private readonly IClaveRegistroService _svc;
    public ClaveRegistroController(IClaveRegistroService svc) => _svc = svc;

    [HttpGet]
    public async Task<ClaveRegistroEstadoDto> Estado(CancellationToken ct) => await _svc.EstadoAsync(ct);

    [HttpPost]
    public async Task<ClaveRegistroGeneradaDto> Generar(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return await _svc.GenerarAsync(ct);
    }

    [HttpDelete]
    public async Task<IActionResult> Desactivar(CancellationToken ct) { await _svc.DesactivarAsync(ct); return NoContent(); }
}
