using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegistroTrabajo.Api.Infrastructure;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Api.Controllers;

[ApiController, Authorize, Route("api/saldos-permiso")]
public class SaldosPermisoController : ControllerBase
{
    private readonly ISaldosPermisoService _svc;
    public SaldosPermisoController(ISaldosPermisoService svc) => _svc = svc;

    /// <summary>Saldos propios o, con saldos.consultar y dentro del alcance, de otro trabajador (?trabajadorId=).</summary>
    [HttpGet]
    public async Task<IReadOnlyList<SaldoPermisoDto>> Listar([FromQuery] SaldoFiltro f, CancellationToken ct) => await _svc.ListarAsync(f, ct);

    [HttpGet("{id:guid}/movimientos")]
    public async Task<IReadOnlyList<MovimientoSaldoDto>> Movimientos(Guid id, CancellationToken ct) => await _svc.MovimientosAsync(id, ct);

    [HttpPost("ajustes"), RequierePermiso(Permisos.SaldosAjustar)]
    public async Task<SaldoPermisoDto> Ajustar(AjustarSaldoRequest r, CancellationToken ct) => await _svc.AjustarAsync(r, ct);

    [HttpPost("generar/{anio:int}"), RequierePermiso(Permisos.SaldosAjustar)]
    public async Task<IActionResult> Generar(short anio, CancellationToken ct) => Ok(new { creados = await _svc.GenerarAnualAsync(anio, ct) });
}
