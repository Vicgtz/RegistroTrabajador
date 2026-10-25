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

[ApiController, Authorize, Route("api/cancelaciones-permiso")]
public class CancelacionesPermisoController : ControllerBase
{
    private readonly IAprobacionesPermisoService _svc;
    public CancelacionesPermisoController(IAprobacionesPermisoService svc) => _svc = svc;

    [HttpGet]
    public async Task<PagedResult<SolicitudCancelacionDto>> Listar([FromQuery] CancelacionPermisoFiltro f, CancellationToken ct) => await _svc.ListarCancelacionesAsync(f, ct);

    [HttpPost("{id:guid}/resolver")]
    public async Task<SolicitudCancelacionDto> Resolver(Guid id, ResolverPermisoRequest r, CancellationToken ct) => await _svc.ResolverCancelacionAsync(id, r, ct);
}

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
