using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegistroTrabajo.Api.Infrastructure;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Api.Controllers;

// Ninguna ruta ni cuerpo recibe EmpresaId: la empresa sale siempre del token y los servicios filtran por ella.
// La hora de asistencia la genera el servidor. Consultas y correcciones de otros trabajadores validan el alcance dentro del servicio.

[ApiController, Authorize, Route("api/turnos")]
public class TurnosController : ControllerBase
{
    private readonly ITurnosService _svc;
    public TurnosController(ITurnosService svc) => _svc = svc;

    [HttpGet, RequierePermiso(Permisos.HorariosConsultar)]
    public async Task<IReadOnlyList<TurnoDto>> Listar([FromQuery] bool soloActivos, CancellationToken ct) => await _svc.ListarAsync(soloActivos, ct);

    [HttpPost, RequierePermiso(Permisos.HorariosConfigurar)]
    public async Task<TurnoDto> Crear(GuardarTurnoRequest r, CancellationToken ct) => await _svc.CrearAsync(r, ct);

    [HttpPut("{id:guid}"), RequierePermiso(Permisos.HorariosConfigurar)]
    public async Task<TurnoDto> Actualizar(Guid id, GuardarTurnoRequest r, CancellationToken ct) => await _svc.ActualizarAsync(id, r, ct);

    [HttpPost("{id:guid}/estado"), RequierePermiso(Permisos.HorariosConfigurar)]
    public async Task<TurnoDto> CambiarEstado(Guid id, CambiarEstadoRolRequest r, CancellationToken ct) => await _svc.CambiarEstadoAsync(id, r.Estado, ct);

    [HttpPost("iniciales"), RequierePermiso(Permisos.HorariosConfigurar)]
    public async Task<IReadOnlyList<TurnoDto>> SembrarIniciales(CancellationToken ct) => await _svc.SembrarInicialesAsync(ct);
}
