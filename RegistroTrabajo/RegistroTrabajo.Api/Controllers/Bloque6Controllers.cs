using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegistroTrabajo.Api.Infrastructure;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Api.Controllers;

// Ninguna ruta ni cuerpo recibe EmpresaId: la empresa sale siempre del token. El servicio decide qué elementos ve cada usuario
// (eventos por visibilidad, ausencias por alcance, fechas relevantes por configuración y permiso) y valida el acceso a cada evento.

[ApiController, Authorize, Route("api/calendario")]
public class CalendarioController : ControllerBase
{
    private readonly ICalendarioService _svc;
    public CalendarioController(ICalendarioService svc) => _svc = svc;

    [HttpGet, RequierePermiso(Permisos.CalendarioConsultar)]
    public async Task<CalendarioDto> Consultar([FromQuery] DateOnly desde, [FromQuery] DateOnly hasta, [FromQuery] bool incluirCancelados, CancellationToken ct) =>
        await _svc.ConsultarAsync(desde, hasta, incluirCancelados, ct);

    [HttpGet("dia"), RequierePermiso(Permisos.CalendarioConsultar)]
    public async Task<CalendarioDto> Dia([FromQuery] DateOnly fecha, CancellationToken ct) => await _svc.DiaAsync(fecha, ct);

    [HttpGet("configuracion"), RequierePermiso(Permisos.CalendarioConsultar)]
    public async Task<ConfiguracionCalendarioDto> Configuracion(CancellationToken ct) => await _svc.ObtenerConfiguracionAsync(ct);

    [HttpPut("configuracion"), RequierePermiso(Permisos.CalendarioConfigurar)]
    public async Task<ConfiguracionCalendarioDto> GuardarConfiguracion(ConfiguracionCalendarioDto r, CancellationToken ct) => await _svc.GuardarConfiguracionAsync(r, ct);
}

[ApiController, Authorize, Route("api/eventos-empresa")]
public class EventosEmpresaController : ControllerBase
{
    private readonly IEventosEmpresaService _svc;
    public EventosEmpresaController(IEventosEmpresaService svc) => _svc = svc;

    [HttpGet, RequierePermiso(Permisos.CalendarioGestionarEventos)]
    public async Task<PagedResult<EventoEmpresaDto>> Listar([FromQuery] EventoEmpresaFiltro f, CancellationToken ct) => await _svc.ListarAsync(f, ct);

    // El detalle lo puede pedir quien consulta el calendario; la visibilidad del evento se valida dentro del servicio (404 si no le corresponde).
    [HttpGet("{id:guid}"), RequierePermiso(Permisos.CalendarioConsultar)]
    public async Task<EventoEmpresaDto> Obtener(Guid id, CancellationToken ct) => await _svc.ObtenerAsync(id, ct);

    [HttpPost, RequierePermiso(Permisos.CalendarioGestionarEventos)]
    public async Task<EventoEmpresaDto> Crear(GuardarEventoEmpresaRequest r, CancellationToken ct) => await _svc.CrearAsync(r, ct);

    [HttpPut("{id:guid}"), RequierePermiso(Permisos.CalendarioGestionarEventos)]
    public async Task<EventoEmpresaDto> Actualizar(Guid id, GuardarEventoEmpresaRequest r, CancellationToken ct) => await _svc.ActualizarAsync(id, r, ct);

    [HttpPost("{id:guid}/cancelar"), RequierePermiso(Permisos.CalendarioGestionarEventos)]
    public async Task<EventoEmpresaDto> Cancelar(Guid id, CancelarEventoEmpresaRequest r, CancellationToken ct) => await _svc.CancelarAsync(id, r, ct);
}
