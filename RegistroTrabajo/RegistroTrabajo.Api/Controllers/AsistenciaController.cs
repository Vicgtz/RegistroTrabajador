using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegistroTrabajo.Api.Infrastructure;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Api.Controllers;

[ApiController, Authorize, Route("api/asistencia")]
public class AsistenciaController : ControllerBase
{
    private readonly IAsistenciaService _svc;
    private readonly ICorreccionesAsistenciaService _correcciones;

    public AsistenciaController(IAsistenciaService svc, ICorreccionesAsistenciaService correcciones)
    {
        _svc = svc; _correcciones = correcciones;
    }

    [HttpGet("estado"), RequierePermiso(Permisos.AsistenciaRegistrar)]
    public async Task<EstadoJornadaDto> Estado(CancellationToken ct) => await _svc.EstadoActualAsync(ct);

    [HttpPost("entrada"), RequierePermiso(Permisos.AsistenciaRegistrar)]
    public async Task<RegistroAsistenciaDto> Entrada(RegistrarAsistenciaRequest r, CancellationToken ct) => await _svc.RegistrarEntradaAsync(r, ct);

    [HttpPost("salida"), RequierePermiso(Permisos.AsistenciaRegistrar)]
    public async Task<RegistroAsistenciaDto> Salida(RegistrarAsistenciaRequest r, CancellationToken ct) => await _svc.RegistrarSalidaAsync(r, ct);

    [HttpGet("mias"), RequierePermiso(Permisos.AsistenciaConsultarPropia)]
    public async Task<PagedResult<RegistroAsistenciaDto>> Mias([FromQuery] AsistenciaFiltro f, CancellationToken ct) => await _svc.MisRegistrosAsync(f, ct);

    [HttpGet]
    public async Task<PagedResult<RegistroAsistenciaDto>> Listar([FromQuery] AsistenciaFiltro f, CancellationToken ct) => await _svc.ListarAsync(f, ct);

    [HttpGet("{id:guid}")]
    public async Task<RegistroAsistenciaDto> Obtener(Guid id, CancellationToken ct) => await _svc.ObtenerAsync(id, ct);

    [HttpPost("{id:guid}/corregir")]
    public async Task<RegistroAsistenciaDto> Corregir(Guid id, CorregirAsistenciaRequest r, CancellationToken ct) => await _correcciones.CorregirAsync(id, r, ct);

    [HttpPost("{id:guid}/anular")]
    public async Task<RegistroAsistenciaDto> Anular(Guid id, AnularAsistenciaRequest r, CancellationToken ct) => await _correcciones.AnularAsync(id, r, ct);

    [HttpPost("manual")]
    public async Task<RegistroAsistenciaDto> Manual(CrearRegistroManualRequest r, CancellationToken ct) => await _correcciones.CrearManualAsync(r, ct);
}
