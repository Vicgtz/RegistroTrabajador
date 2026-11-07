using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegistroTrabajo.Api.Infrastructure;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Api.Controllers;

[ApiController, Authorize, Route("api/calendario-laboral")]
public class CalendarioLaboralController : ControllerBase
{
    private readonly ICalendarioLaboralService _svc;
    public CalendarioLaboralController(ICalendarioLaboralService svc) => _svc = svc;

    [HttpGet("descansos"), RequierePermiso(Permisos.HorariosConsultar)]
    public async Task<IReadOnlyList<DescansoTrabajadorDto>> Descansos([FromQuery] Guid? trabajadorId, [FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta, CancellationToken ct) =>
        await _svc.ListarDescansosAsync(trabajadorId, desde, hasta, ct);

    [HttpPost("descansos"), RequierePermiso(Permisos.HorariosConfigurar)]
    public async Task<DescansoTrabajadorDto> CrearDescanso(GuardarDescansoRequest r, CancellationToken ct) => await _svc.CrearDescansoAsync(r, ct);

    [HttpPut("descansos/{id:guid}"), RequierePermiso(Permisos.HorariosConfigurar)]
    public async Task<DescansoTrabajadorDto> ActualizarDescanso(Guid id, GuardarDescansoRequest r, CancellationToken ct) => await _svc.ActualizarDescansoAsync(id, r, ct);

    [HttpPost("descansos/{id:guid}/terminar"), RequierePermiso(Permisos.HorariosConfigurar)]
    public async Task<DescansoTrabajadorDto> TerminarDescanso(Guid id, CancellationToken ct) => await _svc.TerminarDescansoAsync(id, ct);

    [HttpGet("festivos"), RequierePermiso(Permisos.HorariosConsultar)]
    public async Task<IReadOnlyList<DiaFestivoDto>> Festivos([FromQuery] int? anio, CancellationToken ct) => await _svc.ListarFestivosAsync(anio, ct);

    [HttpPost("festivos"), RequierePermiso(Permisos.HorariosConfigurar)]
    public async Task<DiaFestivoDto> CrearFestivo(GuardarDiaFestivoRequest r, CancellationToken ct) => await _svc.CrearFestivoAsync(r, ct);

    [HttpPut("festivos/{id:guid}"), RequierePermiso(Permisos.HorariosConfigurar)]
    public async Task<DiaFestivoDto> ActualizarFestivo(Guid id, GuardarDiaFestivoRequest r, CancellationToken ct) => await _svc.ActualizarFestivoAsync(id, r, ct);

    [HttpDelete("festivos/{id:guid}"), RequierePermiso(Permisos.HorariosConfigurar)]
    public async Task<IActionResult> EliminarFestivo(Guid id, CancellationToken ct)
    {
        await _svc.EliminarFestivoAsync(id, ct);
        return NoContent();
    }
}
