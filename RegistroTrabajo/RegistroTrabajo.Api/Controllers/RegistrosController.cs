using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegistroTrabajo.Api.Infrastructure;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Api.Controllers;

[ApiController, Authorize]
public class RegistrosController : ControllerBase
{
    [HttpGet("api/accesos"), RequierePermiso(Permisos.AccesosConsultar)]
    public async Task<PagedResult<HistorialAccesoDto>> Accesos([FromQuery] FechaFiltro f, [FromServices] IAccesoService svc, CancellationToken ct) =>
        await svc.ListarAsync(f, ct);

    [HttpGet("api/auditoria"), RequierePermiso(Permisos.AuditoriaConsultar)]
    public async Task<PagedResult<AuditoriaDto>> Auditoria([FromQuery] AuditoriaFiltro f, [FromServices] IAuditoriaService svc, CancellationToken ct) =>
        await svc.ListarAsync(f, ct);
}
