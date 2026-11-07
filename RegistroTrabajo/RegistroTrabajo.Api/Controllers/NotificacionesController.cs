using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegistroTrabajo.Api.Infrastructure;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Api.Controllers;

[ApiController, Authorize, Route("api/notificaciones")]
public class NotificacionesController : ControllerBase
{
    private readonly INotificacionesService _svc;
    public NotificacionesController(INotificacionesService svc) => _svc = svc;

    [HttpGet, RequierePermiso(Permisos.NotificacionesConsultar)]
    public async Task<PagedResult<NotificacionDto>> Listar([FromQuery] NotificacionFiltro f, CancellationToken ct) => await _svc.ListarAsync(f, ct);

    [HttpGet("no-leidas"), RequierePermiso(Permisos.NotificacionesConsultar)]
    public async Task<int> NoLeidas(CancellationToken ct) => await _svc.ContarNoLeidasAsync(ct);

    [HttpPost("{id:guid}/leida"), RequierePermiso(Permisos.NotificacionesConsultar)]
    public async Task<IActionResult> Leida(Guid id, CancellationToken ct) { await _svc.MarcarLeidaAsync(id, ct); return NoContent(); }

    [HttpPost("leidas"), RequierePermiso(Permisos.NotificacionesConsultar)]
    public async Task<IActionResult> TodasLeidas(CancellationToken ct) { await _svc.MarcarTodasLeidasAsync(ct); return NoContent(); }

    [HttpPost("{id:guid}/archivar"), RequierePermiso(Permisos.NotificacionesConsultar)]
    public async Task<IActionResult> Archivar(Guid id, CancellationToken ct) { await _svc.ArchivarAsync(id, ct); return NoContent(); }
}
