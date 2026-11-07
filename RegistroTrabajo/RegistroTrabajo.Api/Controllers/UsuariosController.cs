using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegistroTrabajo.Api.Infrastructure;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Api.Controllers;

[ApiController, Authorize, Route("api/usuarios")]
public class UsuariosController : ControllerBase
{
    private readonly IUsuarioService _usuarios;
    private readonly IRolService _roles;
    private readonly ISesionService _sesiones;

    public UsuariosController(IUsuarioService usuarios, IRolService roles, ISesionService sesiones)
    {
        _usuarios = usuarios; _roles = roles; _sesiones = sesiones;
    }

    [HttpGet, RequierePermiso(Permisos.UsuariosConsultar)]
    public async Task<PagedResult<UsuarioDto>> Listar([FromQuery] UsuarioFiltro f, CancellationToken ct) => await _usuarios.ListarAsync(f, ct);

    [HttpGet("{id:guid}"), RequierePermiso(Permisos.UsuariosConsultar)]
    public async Task<UsuarioDto> Obtener(Guid id, CancellationToken ct) => await _usuarios.ObtenerAsync(id, ct);

    [HttpPost, RequierePermiso(Permisos.UsuariosCrear)]
    public async Task<IActionResult> Crear(CrearUsuarioRequest r, CancellationToken ct)
    {
        var u = await _usuarios.CrearAsync(r, ct);
        return CreatedAtAction(nameof(Obtener), new { id = u.UsuarioId }, u);
    }

    [HttpPut("{id:guid}"), RequierePermiso(Permisos.UsuariosEditar)]
    public async Task<UsuarioDto> Actualizar(Guid id, ActualizarUsuarioRequest r, CancellationToken ct) =>
        await _usuarios.ActualizarAsync(id, r, ct);

    [HttpPost("{id:guid}/desactivar"), RequierePermiso(Permisos.UsuariosDesactivar)]
    public async Task<IActionResult> Desactivar(Guid id, CancellationToken ct)
    {
        await _usuarios.DesactivarAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/activar"), RequierePermiso(Permisos.UsuariosEditar)]
    public async Task<IActionResult> Activar(Guid id, CancellationToken ct)
    {
        await _usuarios.ActivarAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/desbloquear"), RequierePermiso(Permisos.UsuariosDesbloquear)]
    public async Task<IActionResult> Desbloquear(Guid id, CancellationToken ct)
    {
        await _usuarios.DesbloquearAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/restablecer-contrasena"), RequierePermiso(Permisos.UsuariosRestablecerContrasena)]
    public async Task<IActionResult> RestablecerContrasena(Guid id, RestablecerContrasenaUsuarioRequest r, CancellationToken ct)
    {
        await _usuarios.RestablecerContrasenaAsync(id, r.ContrasenaNueva, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/roles"), RequierePermiso(Permisos.RolesConsultar)]
    public async Task<IReadOnlyList<RolDto>> RolesDeUsuario(Guid id, CancellationToken ct) => await _roles.ObtenerRolesUsuarioAsync(id, ct);

    [HttpPut("{id:guid}/roles/{rolId:guid}"), RequierePermiso(Permisos.RolesConfigurar)]
    public async Task<IActionResult> AsignarRol(Guid id, Guid rolId, CancellationToken ct)
    {
        await _roles.AsignarRolAsync(id, rolId, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}/roles/{rolId:guid}"), RequierePermiso(Permisos.RolesConfigurar)]
    public async Task<IActionResult> RetirarRol(Guid id, Guid rolId, CancellationToken ct)
    {
        await _roles.RetirarRolAsync(id, rolId, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}/sesiones"), RequierePermiso(Permisos.UsuariosRevocarSesiones)]
    public async Task<IActionResult> RevocarSesiones(Guid id, CancellationToken ct)
    {
        await _sesiones.RevocarTodasDeUsuarioAsync(id, ct);
        return NoContent();
    }
}
