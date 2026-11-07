using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegistroTrabajo.Api.Infrastructure;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Api.Controllers;

[ApiController, Authorize]
public class RolesController : ControllerBase
{
    private readonly IRolService _roles;
    public RolesController(IRolService roles) => _roles = roles;

    [HttpGet("api/roles"), RequierePermiso(Permisos.RolesConsultar)]
    public async Task<IReadOnlyList<RolDto>> Listar(CancellationToken ct) => await _roles.ListarRolesAsync(ct);

    [HttpPost("api/roles"), RequierePermiso(Permisos.RolesConfigurar)]
    public async Task<IActionResult> Crear(CrearRolRequest r, CancellationToken ct)
    {
        var rol = await _roles.CrearRolAsync(r, ct);
        return Created($"api/roles/{rol.RolId}", rol);
    }

    [HttpPut("api/roles/{id:guid}"), RequierePermiso(Permisos.RolesConfigurar)]
    public async Task<RolDto> Actualizar(Guid id, ActualizarRolRequest r, CancellationToken ct) => await _roles.ActualizarRolAsync(id, r, ct);

    [HttpPost("api/roles/{id:guid}/estado"), RequierePermiso(Permisos.RolesConfigurar)]
    public async Task<RolDto> CambiarEstado(Guid id, CambiarEstadoRolRequest r, CancellationToken ct) => await _roles.CambiarEstadoRolAsync(id, r.Estado, ct);

    [HttpGet("api/permisos"), RequierePermiso(Permisos.RolesConsultar)]
    public async Task<IReadOnlyList<PermisoDto>> ListarPermisos(CancellationToken ct) => await _roles.ListarPermisosAsync(ct);

    [HttpGet("api/roles/{id:guid}/permisos"), RequierePermiso(Permisos.RolesConsultar)]
    public async Task<RolPermisosDto> ObtenerPermisos(Guid id, CancellationToken ct) => await _roles.ObtenerPermisosRolAsync(id, ct);

    [HttpPut("api/roles/{id:guid}/permisos"), RequierePermiso(Permisos.RolesConfigurar)]
    public async Task<RolPermisosDto> ConfigurarPermisos(Guid id, ConfigurarPermisosRolRequest r, CancellationToken ct) =>
        await _roles.ConfigurarPermisosRolAsync(id, r.Permisos, ct);
}
