using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegistroTrabajo.Api.Infrastructure;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Api.Controllers;

[ApiController, Authorize, Route("api/solicitudes-perfil")]
public class SolicitudesPerfilController : ControllerBase
{
    private readonly ISolicitudCambioPerfilService _svc;
    public SolicitudesPerfilController(ISolicitudCambioPerfilService svc) => _svc = svc;

    [HttpPost, RequierePermiso(Permisos.PerfilSolicitar)]
    public async Task<SolicitudCambioPerfilDto> Crear(CambioPerfilDatos r, CancellationToken ct) => await _svc.CrearAsync(r, ct);

    [HttpGet("mias"), RequierePermiso(Permisos.PerfilSolicitar)]
    public async Task<IReadOnlyList<SolicitudCambioPerfilDto>> Mias(CancellationToken ct) => await _svc.MisSolicitudesAsync(ct);

    [HttpPost("{id:guid}/cancelar"), RequierePermiso(Permisos.PerfilSolicitar)]
    public async Task<SolicitudCambioPerfilDto> Cancelar(Guid id, CancellationToken ct) => await _svc.CancelarAsync(id, ct);

    [HttpGet, RequierePermiso(Permisos.PerfilResolverSolicitudes)]
    public async Task<PagedResult<SolicitudCambioPerfilDto>> Listar([FromQuery] SolicitudPerfilFiltro f, CancellationToken ct) =>
        await _svc.ListarAsync(f, ct);

    [HttpPost("{id:guid}/resolver"), RequierePermiso(Permisos.PerfilResolverSolicitudes)]
    public async Task<SolicitudCambioPerfilDto> Resolver(Guid id, ResolverSolicitudRequest r, CancellationToken ct) =>
        await _svc.ResolverAsync(id, r, ct);
}
