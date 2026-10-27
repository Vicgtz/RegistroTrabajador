using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegistroTrabajo.Api.Infrastructure;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Api.Controllers;

// Ninguna ruta ni cuerpo recibe EmpresaId: la empresa sale siempre del token. Los servicios deciden qué proyectos y tareas ve cada usuario
// (participantes, supervisores con permiso y quienes ven todo) y responden 404 ante lo que no le corresponde.

[ApiController, Authorize, Route("api/estados-proyecto")]
public class EstadosProyectoController : ControllerBase
{
    private readonly IEstadosProyectoService _svc;
    public EstadosProyectoController(IEstadosProyectoService svc) => _svc = svc;

    [HttpGet, RequierePermiso(Permisos.ProyectosConsultar)]
    public async Task<IReadOnlyList<EstadoProyectoDto>> Listar([FromQuery] bool incluirInactivos, CancellationToken ct) => await _svc.ListarAsync(incluirInactivos, ct);

    [HttpPost, RequierePermiso(Permisos.ProyectosConfigurarEstados)]
    public async Task<EstadoProyectoDto> Crear(GuardarEstadoProyectoRequest r, CancellationToken ct) => await _svc.CrearAsync(r, ct);

    [HttpPut("{id:guid}"), RequierePermiso(Permisos.ProyectosConfigurarEstados)]
    public async Task<EstadoProyectoDto> Actualizar(Guid id, GuardarEstadoProyectoRequest r, CancellationToken ct) => await _svc.ActualizarAsync(id, r, ct);
}

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

[ApiController, Authorize, Route("api/tareas")]
public class TareasController : ControllerBase
{
    private readonly ITareasService _svc;
    private readonly IComentariosTareaService _comentarios;
    public TareasController(ITareasService svc, IComentariosTareaService comentarios) { _svc = svc; _comentarios = comentarios; }

    [HttpGet("mis-tareas"), RequierePermiso(Permisos.ProyectosConsultar)]
    public async Task<PagedResult<TareaResumenDto>> MisTareas([FromQuery] MisTareasFiltro f, CancellationToken ct) => await _svc.MisTareasAsync(f, ct);

    [HttpGet("{id:guid}"), RequierePermiso(Permisos.ProyectosConsultar)]
    public async Task<TareaDetalleDto> Obtener(Guid id, CancellationToken ct) => await _svc.ObtenerAsync(id, ct);

    [HttpPut("{id:guid}"), RequierePermiso(Permisos.TareasGestionar)]
    public async Task<TareaDetalleDto> Actualizar(Guid id, GuardarTareaRequest r, CancellationToken ct) => await _svc.ActualizarAsync(id, r, ct);

    [HttpPost("{id:guid}/asignados"), RequierePermiso(Permisos.TareasGestionar)]
    public async Task<TareaDetalleDto> Asignar(Guid id, AsignarTareaRequest r, CancellationToken ct) => await _svc.AsignarAsync(id, r, ct);

    [HttpDelete("{id:guid}/asignados/{trabajadorId:guid}"), RequierePermiso(Permisos.TareasGestionar)]
    public async Task<TareaDetalleDto> Retirar(Guid id, Guid trabajadorId, CancellationToken ct) => await _svc.RetirarAsignadoAsync(id, trabajadorId, ct);

    [HttpPost("{id:guid}/avance"), RequierePermiso(Permisos.TareasActualizarAvance)]
    public async Task<TareaDetalleDto> Avance(Guid id, ActualizarAvanceRequest r, CancellationToken ct) => await _svc.ActualizarAvanceAsync(id, r, ct);

    [HttpPost("{id:guid}/revision"), RequierePermiso(Permisos.TareasRevisar)]
    public async Task<TareaDetalleDto> Revisar(Guid id, RevisarTareaRequest r, CancellationToken ct) => await _svc.RevisarAsync(id, r, ct);

    [HttpPost("{id:guid}/reabrir"), RequierePermiso(Permisos.TareasRevisar)]
    public async Task<TareaDetalleDto> Reabrir(Guid id, MotivoTareaRequest r, CancellationToken ct) => await _svc.ReabrirAsync(id, r, ct);

    [HttpPost("{id:guid}/cancelar"), RequierePermiso(Permisos.TareasGestionar)]
    public async Task<TareaDetalleDto> Cancelar(Guid id, MotivoTareaRequest r, CancellationToken ct) => await _svc.CancelarAsync(id, r, ct);

    [HttpGet("{id:guid}/historial"), RequierePermiso(Permisos.ProyectosConsultar)]
    public async Task<IReadOnlyList<HistorialAvanceTareaDto>> Historial(Guid id, CancellationToken ct) => await _svc.HistorialAsync(id, ct);

    [HttpGet("{id:guid}/comentarios"), RequierePermiso(Permisos.ProyectosConsultar)]
    public async Task<IReadOnlyList<ComentarioTareaDto>> Comentarios(Guid id, CancellationToken ct) => await _comentarios.ListarAsync(id, ct);

    // Sin PUT ni DELETE: los comentarios publicados son inmutables.
    [HttpPost("{id:guid}/comentarios"), RequierePermiso(Permisos.TareasComentar)]
    public async Task<ComentarioTareaDto> Comentar(Guid id, PublicarComentarioRequest r, CancellationToken ct) => await _comentarios.PublicarAsync(id, r, ct);

    [HttpGet("{id:guid}/mencionables"), RequierePermiso(Permisos.ProyectosConsultar)]
    public async Task<IReadOnlyList<UsuarioMencionableDto>> Mencionables(Guid id, CancellationToken ct) => await _comentarios.MencionablesAsync(id, ct);
}

[ApiController, Authorize, Route("api/notificaciones")]
public class NotificacionesController : ControllerBase
{
    private readonly INotificacionesService _svc;
    public NotificacionesController(INotificacionesService svc) => _svc = svc;

    [HttpGet, RequierePermiso(Permisos.NotificacionesConsultar)]
    public async Task<PagedResult<NotificacionDto>> Listar([FromQuery] NotificacionFiltro f, CancellationToken ct) => await _svc.ListarAsync(f, ct);

    [HttpGet("no-leidas"), RequierePermiso(Permisos.NotificacionesConsultar)]
    public async Task<int> NoLeidas(CancellationToken ct) => await _svc.ContarNoLeidasAsync(ct);

    [HttpPost("{id:guid}/leida"), RequierePermiso(Permisos.NotificacionesConsultar)]
    public async Task<IActionResult> Leida(Guid id, CancellationToken ct) { await _svc.MarcarLeidaAsync(id, ct); return NoContent(); }

    [HttpPost("leidas"), RequierePermiso(Permisos.NotificacionesConsultar)]
    public async Task<IActionResult> TodasLeidas(CancellationToken ct) { await _svc.MarcarTodasLeidasAsync(ct); return NoContent(); }

    [HttpPost("{id:guid}/archivar"), RequierePermiso(Permisos.NotificacionesConsultar)]
    public async Task<IActionResult> Archivar(Guid id, CancellationToken ct) { await _svc.ArchivarAsync(id, ct); return NoContent(); }
}
