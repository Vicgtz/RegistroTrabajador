using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegistroTrabajo.Api.Infrastructure;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Api.Controllers;

[ApiController, Authorize, Route("api/puestos")]
public class PuestosController : ControllerBase
{
    private readonly IPuestoService _svc;
    public PuestosController(IPuestoService svc) => _svc = svc;

    [HttpGet, RequierePermiso(Permisos.PuestosConsultar)]
    public async Task<IReadOnlyList<CatalogoItemDto>> Listar([FromQuery] bool soloActivos, CancellationToken ct) =>
        await _svc.ListarAsync(soloActivos, ct);

    [HttpPost, RequierePermiso(Permisos.PuestosGestionar)]
    public async Task<CatalogoItemDto> Crear(GuardarCatalogoRequest r, CancellationToken ct) => await _svc.CrearAsync(r, ct);

    [HttpPut("{id:guid}"), RequierePermiso(Permisos.PuestosGestionar)]
    public async Task<CatalogoItemDto> Actualizar(Guid id, GuardarCatalogoRequest r, CancellationToken ct) => await _svc.ActualizarAsync(id, r, ct);

    [HttpPost("{id:guid}/estado"), RequierePermiso(Permisos.PuestosGestionar)]
    public async Task<CatalogoItemDto> CambiarEstado(Guid id, CambiarEstadoRolRequest r, CancellationToken ct) =>
        await _svc.CambiarEstadoAsync(id, r.Estado, ct);
}
