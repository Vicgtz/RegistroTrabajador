using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegistroTrabajo.Api.Infrastructure;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Api.Controllers;

[ApiController, Authorize, Route("api/proyectos")]
public class ProyectosController : ControllerBase
{
    private readonly IProyectosService _svc;
    private readonly ITareasService _tareas;
    public ProyectosController(IProyectosService svc, ITareasService tareas) { _svc = svc; _tareas = tareas; }

    [HttpGet("resumen"), RequierePermiso(Permisos.ProyectosConsultar)]
    public async Task<ProyectosResumenDto> Resumen(CancellationToken ct) => await _svc.ResumenAsync(ct);

    [HttpGet, RequierePermiso(Permisos.ProyectosConsultar)]
    public async Task<PagedResult<ProyectoResumenDto>> Listar([FromQuery] ProyectoFiltro f, CancellationToken ct) => await _svc.ListarAsync(f, ct);

    [HttpGet("{id:guid}"), RequierePermiso(Permisos.ProyectosConsultar)]
    public async Task<ProyectoDetalleDto> Obtener(Guid id, CancellationToken ct) => await _svc.ObtenerAsync(id, ct);

    [HttpPost, RequierePermiso(Permisos.ProyectosCrear)]
    public async Task<ProyectoDetalleDto> Crear(GuardarProyectoRequest r, CancellationToken ct) => await _svc.CrearAsync(r, ct);

    [HttpPut("{id:guid}"), RequierePermiso(Permisos.ProyectosModificar)]
    public async Task<ProyectoDetalleDto> Actualizar(Guid id, GuardarProyectoRequest r, CancellationToken ct) => await _svc.ActualizarAsync(id, r, ct);

    [HttpPost("{id:guid}/trabajadores"), RequierePermiso(Permisos.ProyectosAsignar)]
    public async Task<ProyectoDetalleDto> Asignar(Guid id, AsignarTrabajadoresRequest r, CancellationToken ct) => await _svc.AsignarTrabajadoresAsync(id, r, ct);

    [HttpDelete("{id:guid}/trabajadores/{trabajadorId:guid}"), RequierePermiso(Permisos.ProyectosAsignar)]
    public async Task<ProyectoDetalleDto> Retirar(Guid id, Guid trabajadorId, CancellationToken ct) => await _svc.RetirarTrabajadorAsync(id, trabajadorId, ct);

    [HttpPost("{id:guid}/responsable"), RequierePermiso(Permisos.ProyectosCambiarResponsable)]
    public async Task<ProyectoDetalleDto> CambiarResponsable(Guid id, CambiarResponsableRequest r, CancellationToken ct) => await _svc.CambiarResponsableAsync(id, r, ct);

    // Finalizar, cancelar, pausar o reabrir: el servicio exige el permiso que corresponda según el estado de origen y de destino.
    [HttpPost("{id:guid}/estado"), RequierePermiso(Permisos.ProyectosConsultar)]
    public async Task<ProyectoDetalleDto> CambiarEstado(Guid id, CambiarEstadoProyectoRequest r, CancellationToken ct) => await _svc.CambiarEstadoAsync(id, r, ct);

    [HttpGet("{id:guid}/historial"), RequierePermiso(Permisos.ProyectosConsultar)]
    public async Task<IReadOnlyList<HistorialEstadoProyectoDto>> Historial(Guid id, CancellationToken ct) => await _svc.HistorialAsync(id, ct);

    [HttpGet("{id:guid}/tareas"), RequierePermiso(Permisos.ProyectosConsultar)]
    public async Task<PagedResult<TareaResumenDto>> Tareas(Guid id, [FromQuery] TareaFiltro f, CancellationToken ct) => await _tareas.ListarPorProyectoAsync(id, f, ct);

    [HttpPost("{id:guid}/tareas"), RequierePermiso(Permisos.TareasGestionar)]
    public async Task<TareaDetalleDto> CrearTarea(Guid id, GuardarTareaRequest r, CancellationToken ct) => await _tareas.CrearAsync(id, r, ct);
}
