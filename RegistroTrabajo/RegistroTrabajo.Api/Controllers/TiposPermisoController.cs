using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegistroTrabajo.Api.Infrastructure;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Api.Controllers;

// Ninguna ruta ni cuerpo recibe EmpresaId: la empresa sale siempre del token y los servicios filtran por ella.
// Las acciones sin [RequierePermiso] (resolver, bandeja, consultas de otros) validan el alcance dentro del servicio.

[ApiController, Authorize, Route("api/tipos-permiso")]
public class TiposPermisoController : ControllerBase
{
    private readonly ITiposPermisoService _svc;
    public TiposPermisoController(ITiposPermisoService svc) => _svc = svc;

    /// <summary>Catálogo visible para cualquier usuario autenticado (el trabajador lo necesita para solicitar).</summary>
    [HttpGet]
    public async Task<IReadOnlyList<TipoPermisoDto>> Listar([FromQuery] bool soloActivos, CancellationToken ct) => await _svc.ListarAsync(soloActivos, ct);

    [HttpPost, RequierePermiso(Permisos.PermisosConfigurar)]
    public async Task<TipoPermisoDto> Crear(GuardarTipoPermisoRequest r, CancellationToken ct) => await _svc.CrearAsync(r, ct);

    [HttpPut("{id:guid}"), RequierePermiso(Permisos.PermisosConfigurar)]
    public async Task<TipoPermisoDto> Actualizar(Guid id, GuardarTipoPermisoRequest r, CancellationToken ct) => await _svc.ActualizarAsync(id, r, ct);

    [HttpPost("{id:guid}/estado"), RequierePermiso(Permisos.PermisosConfigurar)]
    public async Task<TipoPermisoDto> CambiarEstado(Guid id, CambiarEstadoRolRequest r, CancellationToken ct) => await _svc.CambiarEstadoAsync(id, r.Estado, ct);

    [HttpPost("{id:guid}/reglas"), RequierePermiso(Permisos.PermisosConfigurar)]
    public async Task<ReglaPermisoDto> CrearRegla(Guid id, GuardarReglaPermisoRequest r, CancellationToken ct) => await _svc.CrearReglaAsync(id, r, ct);

    [HttpPut("{id:guid}/reglas/{reglaId:guid}"), RequierePermiso(Permisos.PermisosConfigurar)]
    public async Task<ReglaPermisoDto> ActualizarRegla(Guid id, Guid reglaId, GuardarReglaPermisoRequest r, CancellationToken ct) =>
        await _svc.ActualizarReglaAsync(id, reglaId, r, ct);

    [HttpPost("iniciales"), RequierePermiso(Permisos.PermisosConfigurar)]
    public async Task<IReadOnlyList<TipoPermisoDto>> SembrarIniciales(CancellationToken ct) => await _svc.SembrarInicialesAsync(ct);
}
