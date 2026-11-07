using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegistroTrabajo.Api.Infrastructure;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Api.Controllers;

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
