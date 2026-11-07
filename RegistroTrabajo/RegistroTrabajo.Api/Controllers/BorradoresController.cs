using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegistroTrabajo.Api.Infrastructure;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Api.Controllers;

[ApiController, Authorize, Route("api/borradores")]
public class BorradoresController : ControllerBase
{
    private readonly IBorradoresService _svc;
    public BorradoresController(IBorradoresService svc) => _svc = svc;

    [HttpGet]
    public async Task<PagedResult<BorradorDto>> Listar([FromQuery] BorradorFiltro f, CancellationToken ct) => await _svc.ListarAsync(f, ct);

    [HttpGet("{id:guid}")]
    public async Task<BorradorDto> Obtener(Guid id, CancellationToken ct) => await _svc.ObtenerAsync(id, ct);

    [HttpPost]
    public async Task<BorradorDto> Crear(GuardarBorradorRequest r, CancellationToken ct) => await _svc.CrearAsync(r, ct);

    [HttpPut("{id:guid}")]
    public async Task<BorradorDto> Actualizar(Guid id, GuardarBorradorRequest r, CancellationToken ct) => await _svc.ActualizarAsync(id, r, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken ct) { await _svc.EliminarAsync(id, ct); return NoContent(); }
}
