using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegistroTrabajo.Api.Infrastructure;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Api.Controllers;

// Bloque 8: seguimiento de tareas. Igual que el Bloque 7, la empresa sale del token y lo que el usuario no puede ver responde 404.
// Las acciones de revisión (aprobar, devolver, reabrir) siguen en api/tareas/{id}/revision y /reabrir.

[ApiController, Authorize]
public class SeguimientoTareasController : ControllerBase
{
    private readonly ISeguimientoService _svc;
    public SeguimientoTareasController(ISeguimientoService svc) => _svc = svc;

    [HttpGet("api/tareas/{id:guid}/linea-de-tiempo"), RequierePermiso(Permisos.ProyectosConsultar)]
    public async Task<IReadOnlyList<EventoSeguimientoDto>> LineaDeTiempo(Guid id, CancellationToken ct) => await _svc.LineaDeTiempoAsync(id, ct);

    [HttpGet("api/tareas/{id:guid}/revisiones"), RequierePermiso(Permisos.ProyectosConsultar)]
    public async Task<IReadOnlyList<RevisionTareaDto>> Revisiones(Guid id, CancellationToken ct) => await _svc.RevisionesAsync(id, ct);

    [HttpGet("api/seguimiento/panel"), RequierePermiso(Permisos.ProyectosConsultar)]
    public async Task<PanelSeguimientoDto> Panel([FromQuery] SeguimientoFiltro f, CancellationToken ct) => await _svc.PanelAsync(f, ct);

    [HttpGet("api/seguimiento/revisiones"), RequierePermiso(Permisos.ProyectosConsultar)]
    public async Task<IReadOnlyList<TareaSeguimientoDto>> PendientesRevision([FromQuery] SeguimientoFiltro f, CancellationToken ct) => await _svc.PendientesRevisionAsync(f, ct);

    /// <summary>Seguimiento de un trabajador: el panel filtrado por sus tareas (solo con <c>tareas.ver_seguimiento</c>).</summary>
    [HttpGet("api/seguimiento/trabajador/{trabajadorId:guid}"), RequierePermiso(Permisos.TareasVerSeguimiento)]
    public async Task<PanelSeguimientoDto> PorTrabajador(Guid trabajadorId, CancellationToken ct) => await _svc.PanelAsync(new SeguimientoFiltro { TrabajadorId = trabajadorId }, ct);

    [HttpGet("api/seguimiento/configuracion"), RequierePermiso(Permisos.EmpresaConsultar)]
    public async Task<ConfiguracionSeguimientoDto> Configuracion(CancellationToken ct) => await _svc.ObtenerConfiguracionAsync(ct);

    [HttpPut("api/seguimiento/configuracion"), RequierePermiso(Permisos.EmpresaConfigurar)]
    public async Task<ConfiguracionSeguimientoDto> GuardarConfiguracion(ConfiguracionSeguimientoDto r, CancellationToken ct) => await _svc.GuardarConfiguracionAsync(r, ct);
}
