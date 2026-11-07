using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegistroTrabajo.Api.Infrastructure;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Api.Controllers;

[ApiController, Authorize, Route("api/solicitudes-permiso")]
public class SolicitudesPermisoController : ControllerBase
{
    private readonly ISolicitudesPermisoService _solicitudes;
    private readonly IAprobacionesPermisoService _aprobaciones;
    private readonly ICalculoDuracionService _calculo;

    public SolicitudesPermisoController(ISolicitudesPermisoService solicitudes, IAprobacionesPermisoService aprobaciones, ICalculoDuracionService calculo)
    {
        _solicitudes = solicitudes; _aprobaciones = aprobaciones; _calculo = calculo;
    }

    [HttpPost("calculo"), RequierePermiso(Permisos.PermisosSolicitar)]
    public async Task<CalculoDuracionDto> Calcular(CalculoDuracionRequest r, CancellationToken ct) => await _calculo.PrevisualizarAsync(r, ct);

    [HttpPost, RequierePermiso(Permisos.PermisosSolicitar)]
    public async Task<SolicitudPermisoDto> Crear(CrearSolicitudPermisoRequest r, CancellationToken ct) => await _solicitudes.CrearAsync(r, ct);

    [HttpGet("mias"), RequierePermiso(Permisos.PermisosSolicitar)]
    public async Task<PagedResult<SolicitudPermisoDto>> Mias([FromQuery] SolicitudPermisoFiltro f, CancellationToken ct) => await _solicitudes.MisSolicitudesAsync(f, ct);

    /// <summary>Historial de otros trabajadores (toda la empresa o solo los supervisados, según el permiso).</summary>
    [HttpGet]
    public async Task<PagedResult<SolicitudPermisoDto>> Listar([FromQuery] SolicitudPermisoFiltro f, CancellationToken ct) => await _solicitudes.ListarAsync(f, ct);

    [HttpGet("bandeja")]
    public async Task<PagedResult<SolicitudPermisoDto>> Bandeja([FromQuery] SolicitudPermisoFiltro f, CancellationToken ct) => await _solicitudes.BandejaAsync(f, ct);

    [HttpGet("calendario")]
    public async Task<IReadOnlyList<AusenciaCalendarioDto>> Calendario([FromQuery] DateOnly desde, [FromQuery] DateOnly hasta, CancellationToken ct) =>
        await _solicitudes.CalendarioAsync(desde, hasta, ct);

    [HttpGet("{id:guid}")]
    public async Task<SolicitudPermisoDto> Obtener(Guid id, CancellationToken ct) => await _solicitudes.ObtenerAsync(id, ct);

    [HttpPut("{id:guid}"), RequierePermiso(Permisos.PermisosSolicitar)]
    public async Task<SolicitudPermisoDto> Actualizar(Guid id, ActualizarSolicitudPermisoRequest r, CancellationToken ct) => await _solicitudes.ActualizarAsync(id, r, ct);

    [HttpPost("{id:guid}/cancelar"), RequierePermiso(Permisos.PermisosSolicitar)]
    public async Task<SolicitudPermisoDto> Cancelar(Guid id, CancellationToken ct) => await _solicitudes.CancelarAsync(id, ct);

    [HttpPost("{id:guid}/resolver")]
    public async Task<SolicitudPermisoDto> Resolver(Guid id, ResolverPermisoRequest r, CancellationToken ct) => await _aprobaciones.ResolverAsync(id, r, ct);

    [HttpPost("{id:guid}/solicitar-cancelacion"), RequierePermiso(Permisos.PermisosSolicitar)]
    public async Task<SolicitudCancelacionDto> SolicitarCancelacion(Guid id, SolicitarCancelacionRequest r, CancellationToken ct) =>
        await _aprobaciones.SolicitarCancelacionAsync(id, r, ct);
}
