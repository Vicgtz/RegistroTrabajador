using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegistroTrabajo.Api.Infrastructure;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Api.Controllers;

// Bloque 9: centro de notificaciones. Complementa a NotificacionesController (Bloque 7). Cada usuario solo accede a su propia bandeja;
// la empresa y el usuario salen del token y una notificación ajena responde 404.

[ApiController, Authorize, Route("api/notificaciones")]
public class CentroNotificacionesController : ControllerBase
{
    private readonly INotificacionesService _svc;
    private readonly IPreferenciasNotificacionService _preferencias;
    public CentroNotificacionesController(INotificacionesService svc, IPreferenciasNotificacionService preferencias) { _svc = svc; _preferencias = preferencias; }

    [HttpGet("resumen"), RequierePermiso(Permisos.NotificacionesConsultar)]
    public async Task<ResumenNotificacionesDto> Resumen(CancellationToken ct) => await _svc.ResumenAsync(ct);

    [HttpPost("{id:guid}/desarchivar"), RequierePermiso(Permisos.NotificacionesConsultar)]
    public async Task<IActionResult> Desarchivar(Guid id, CancellationToken ct) { await _svc.DesarchivarAsync(id, ct); return NoContent(); }

    /// <summary>Ruta del recurso relacionado, validada otra vez contra los permisos actuales del usuario.</summary>
    [HttpGet("{id:guid}/destino"), RequierePermiso(Permisos.NotificacionesConsultar)]
    public async Task<DestinoNotificacionDto> Destino(Guid id, CancellationToken ct) => await _svc.ResolverDestinoAsync(id, ct);

    [HttpGet("preferencias"), RequierePermiso(Permisos.NotificacionesConsultar)]
    public async Task<IReadOnlyList<PreferenciaCategoriaDto>> Preferencias(CancellationToken ct) => await _preferencias.ListarAsync(ct);

    [HttpPut("preferencias"), RequierePermiso(Permisos.NotificacionesConsultar)]
    public async Task<IReadOnlyList<PreferenciaCategoriaDto>> GuardarPreferencias(GuardarPreferenciasRequest r, CancellationToken ct) => await _preferencias.GuardarAsync(r, ct);
}
