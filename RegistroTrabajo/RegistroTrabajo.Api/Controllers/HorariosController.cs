using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegistroTrabajo.Api.Infrastructure;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Api.Controllers;

[ApiController, Authorize, Route("api/horarios")]
public class HorariosController : ControllerBase
{
    private readonly IHorariosService _svc;
    public HorariosController(IHorariosService svc) => _svc = svc;

    [HttpGet, RequierePermiso(Permisos.HorariosConsultar)]
    public async Task<IReadOnlyList<HorarioLaboralDto>> Listar([FromQuery] bool soloActivos, CancellationToken ct) => await _svc.ListarAsync(soloActivos, ct);

    [HttpGet("{id:guid}"), RequierePermiso(Permisos.HorariosConsultar)]
    public async Task<HorarioLaboralDto> Obtener(Guid id, CancellationToken ct) => await _svc.ObtenerAsync(id, ct);

    [HttpPost, RequierePermiso(Permisos.HorariosConfigurar)]
    public async Task<HorarioLaboralDto> Crear(GuardarHorarioRequest r, CancellationToken ct) => await _svc.CrearAsync(r, ct);

    [HttpPut("{id:guid}"), RequierePermiso(Permisos.HorariosConfigurar)]
    public async Task<HorarioLaboralDto> Actualizar(Guid id, GuardarHorarioRequest r, CancellationToken ct) => await _svc.ActualizarAsync(id, r, ct);

    [HttpPost("{id:guid}/nueva-version"), RequierePermiso(Permisos.HorariosConfigurar)]
    public async Task<HorarioLaboralDto> NuevaVersion(Guid id, GuardarHorarioRequest r, CancellationToken ct) => await _svc.NuevaVersionAsync(id, r, ct);

    [HttpPost("{id:guid}/estado"), RequierePermiso(Permisos.HorariosConfigurar)]
    public async Task<HorarioLaboralDto> CambiarEstado(Guid id, CambiarEstadoRolRequest r, CancellationToken ct) => await _svc.CambiarEstadoAsync(id, r.Estado, ct);

    [HttpGet("asignaciones"), RequierePermiso(Permisos.HorariosConsultar)]
    public async Task<IReadOnlyList<AsignacionHorarioDto>> Asignaciones([FromQuery] Guid? trabajadorId, CancellationToken ct) => await _svc.ListarAsignacionesAsync(trabajadorId, ct);

    [HttpPost("asignaciones"), RequierePermiso(Permisos.HorariosConfigurar)]
    public async Task<AsignacionHorarioDto> Asignar(AsignarHorarioRequest r, CancellationToken ct) => await _svc.AsignarAsync(r, ct);

    [HttpPost("asignaciones/{id:guid}/cerrar"), RequierePermiso(Permisos.HorariosConfigurar)]
    public async Task<IActionResult> CerrarAsignacion(Guid id, CerrarAsignacionRequest r, CancellationToken ct)
    {
        await _svc.CerrarAsignacionAsync(id, r.FechaHasta, ct);
        return NoContent();
    }

    [HttpGet("aplicable"), RequierePermiso(Permisos.HorariosConsultar)]
    public async Task<HorarioAplicableDto> Aplicable([FromQuery] Guid trabajadorId, [FromQuery] DateOnly fecha, CancellationToken ct) => await _svc.AplicableAsync(trabajadorId, fecha, ct);
}

public class CerrarAsignacionRequest
{
    public DateOnly FechaHasta { get; set; }
}
