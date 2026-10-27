using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegistroTrabajo.Api.Infrastructure;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Api.Controllers;

// Bloque 11. Solo empresa de plataforma (permisos plataforma.respaldos.*). No hay descarga de archivos ni restauración por HTTP:
// los .bak permanecen en la carpeta protegida del servidor y la restauración es un procedimiento técnico controlado.
[ApiController, Authorize, Route("api/respaldos")]
public class RespaldosController : ControllerBase
{
    private readonly IRespaldoService _svc;
    public RespaldosController(IRespaldoService svc) => _svc = svc;

    [HttpGet, RequierePermiso(Permisos.PlataformaRespaldosConsultar)]
    public async Task<PagedResult<RespaldoDto>> Listar([FromQuery] RespaldoFiltro f, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return await _svc.ListarAsync(f, ct);
    }

    [HttpGet("{id:guid}"), RequierePermiso(Permisos.PlataformaRespaldosConsultar)]
    public async Task<RespaldoDto> Obtener(Guid id, CancellationToken ct) => await _svc.ObtenerAsync(id, ct);

    [HttpPost, RequierePermiso(Permisos.PlataformaRespaldosSolicitar)]
    public async Task<RespaldoDto> Solicitar(SolicitarRespaldoRequest r, CancellationToken ct) => await _svc.SolicitarAsync(r, ct);

    [HttpPost("{id:guid}/verificar"), RequierePermiso(Permisos.PlataformaRespaldosSolicitar)]
    public async Task<RespaldoDto> Verificar(Guid id, CancellationToken ct) => await _svc.VerificarAsync(id, ct);
}
