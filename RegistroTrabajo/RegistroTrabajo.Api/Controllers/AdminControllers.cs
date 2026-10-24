using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegistroTrabajo.Api.Infrastructure;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Api.Controllers;

[ApiController, Authorize, Route("api/empresa")]
public class EmpresaController : ControllerBase
{
    private readonly IEmpresaService _svc;
    public EmpresaController(IEmpresaService svc) => _svc = svc;

    [HttpGet, RequierePermiso(Permisos.EmpresaConsultar)]
    public async Task<EmpresaDto> Obtener(CancellationToken ct) => await _svc.ObtenerAsync(ct);

    [HttpPut, RequierePermiso(Permisos.EmpresaConfigurar)]
    public async Task<EmpresaDto> Actualizar(ActualizarEmpresaRequest r, CancellationToken ct) => await _svc.ActualizarAsync(r, ct);

    [HttpGet("configuracion"), RequierePermiso(Permisos.EmpresaConsultar)]
    public async Task<ConfiguracionEmpresaDto> ObtenerConfiguracion(CancellationToken ct) => await _svc.ObtenerConfiguracionAsync(ct);

    [HttpPut("configuracion"), RequierePermiso(Permisos.EmpresaConfigurar)]
    public async Task<ConfiguracionEmpresaDto> ActualizarConfiguracion(ConfiguracionEmpresaDto r, CancellationToken ct) =>
        await _svc.ActualizarConfiguracionAsync(r, ct);
}

/// <summary>Administración de empresas: solo para usuarios de la empresa de plataforma.</summary>
[ApiController, Authorize, Route("api/plataforma/empresas")]
public class PlataformaController : ControllerBase
{
    private readonly IPlataformaService _svc;
    public PlataformaController(IPlataformaService svc) => _svc = svc;

    [HttpGet, RequierePermiso(Permisos.PlataformaEmpresasConsultar)]
    public async Task<PagedResult<EmpresaPlataformaDto>> Listar([FromQuery] PageQuery q, [FromQuery] string? buscar, CancellationToken ct) =>
        await _svc.ListarEmpresasAsync(q, buscar, ct);

    [HttpGet("{id:guid}"), RequierePermiso(Permisos.PlataformaEmpresasConsultar)]
    public async Task<EmpresaPlataformaDto> Obtener(Guid id, CancellationToken ct) => await _svc.ObtenerEmpresaAsync(id, ct);

    [HttpPost, RequierePermiso(Permisos.PlataformaEmpresasCrear)]
    public async Task<IActionResult> Crear(CrearEmpresaPlataformaRequest r, CancellationToken ct)
    {
        var e = await _svc.CrearEmpresaAsync(r, ct);
        return CreatedAtAction(nameof(Obtener), new { id = e.EmpresaId }, e);
    }

    [HttpPost("{id:guid}/estado"), RequierePermiso(Permisos.PlataformaEmpresasCambiarEstado)]
    public async Task<EmpresaPlataformaDto> CambiarEstado(Guid id, CambiarEstadoEmpresaRequest r, CancellationToken ct) =>
        await _svc.CambiarEstadoAsync(id, r.Estado, ct);
}

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

[ApiController, Authorize, Route("api/sesiones")]
public class SesionesController : ControllerBase
{
    private readonly ISesionService _sesiones;
    public SesionesController(ISesionService sesiones) => _sesiones = sesiones;

    [HttpGet, RequierePermiso(Permisos.SesionesConsultar)]
    public async Task<PagedResult<SesionDto>> Listar([FromQuery] PageQuery q, CancellationToken ct) => await _sesiones.ListarActivasAsync(q, ct);

    [HttpDelete("{id:guid}"), RequierePermiso(Permisos.SesionesRevocar)]
    public async Task<IActionResult> Revocar(Guid id, CancellationToken ct)
    {
        await _sesiones.RevocarAsync(id, ct);
        return NoContent();
    }

    // Sesiones del propio usuario: no requieren permiso adicional.
    [HttpGet("mias")]
    public async Task<PagedResult<SesionDto>> Mias([FromQuery] PageQuery q, CancellationToken ct) => await _sesiones.ListarMiasAsync(q, ct);

    [HttpDelete("mias/{id:guid}")]
    public async Task<IActionResult> RevocarMia(Guid id, CancellationToken ct)
    {
        await _sesiones.RevocarMiaAsync(id, ct);
        return NoContent();
    }
}

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
