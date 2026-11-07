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
