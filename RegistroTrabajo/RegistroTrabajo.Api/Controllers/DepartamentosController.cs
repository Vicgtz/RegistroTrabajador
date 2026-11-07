using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegistroTrabajo.Api.Infrastructure;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Api.Controllers;

// Ninguna ruta ni cuerpo recibe EmpresaId: la empresa sale siempre del token y los servicios filtran por ella.

[ApiController, Authorize, Route("api/departamentos")]
public class DepartamentosController : ControllerBase
{
    private readonly IDepartamentoService _svc;
    public DepartamentosController(IDepartamentoService svc) => _svc = svc;

    [HttpGet, RequierePermiso(Permisos.DepartamentosConsultar)]
    public async Task<IReadOnlyList<CatalogoItemDto>> Listar([FromQuery] bool soloActivos, CancellationToken ct) =>
        await _svc.ListarAsync(soloActivos, ct);

    [HttpPost, RequierePermiso(Permisos.DepartamentosGestionar)]
    public async Task<CatalogoItemDto> Crear(GuardarCatalogoRequest r, CancellationToken ct) => await _svc.CrearAsync(r, ct);

    [HttpPut("{id:guid}"), RequierePermiso(Permisos.DepartamentosGestionar)]
    public async Task<CatalogoItemDto> Actualizar(Guid id, GuardarCatalogoRequest r, CancellationToken ct) => await _svc.ActualizarAsync(id, r, ct);

    [HttpPost("{id:guid}/estado"), RequierePermiso(Permisos.DepartamentosGestionar)]
    public async Task<CatalogoItemDto> CambiarEstado(Guid id, CambiarEstadoRolRequest r, CancellationToken ct) =>
        await _svc.CambiarEstadoAsync(id, r.Estado, ct);
}
