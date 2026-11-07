using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegistroTrabajo.Api.Infrastructure;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Api.Controllers;

[ApiController, Authorize, Route("api/cancelaciones-permiso")]
public class CancelacionesPermisoController : ControllerBase
{
    private readonly IAprobacionesPermisoService _svc;
    public CancelacionesPermisoController(IAprobacionesPermisoService svc) => _svc = svc;

    [HttpGet]
    public async Task<PagedResult<SolicitudCancelacionDto>> Listar([FromQuery] CancelacionPermisoFiltro f, CancellationToken ct) => await _svc.ListarCancelacionesAsync(f, ct);

    [HttpPost("{id:guid}/resolver")]
    public async Task<SolicitudCancelacionDto> Resolver(Guid id, ResolverPermisoRequest r, CancellationToken ct) => await _svc.ResolverCancelacionAsync(id, r, ct);
}
