using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegistroTrabajo.Api.Infrastructure;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Api.Controllers;

[ApiController, Authorize, Route("api/trabajadores")]
public class TrabajadoresController : ControllerBase
{
    private readonly ITrabajadorService _trabajadores;
    private readonly ISupervisionService _supervision;
    private readonly IContactoEmergenciaService _contactos;

    public TrabajadoresController(ITrabajadorService trabajadores, ISupervisionService supervision, IContactoEmergenciaService contactos)
    {
        _trabajadores = trabajadores; _supervision = supervision; _contactos = contactos;
    }

    // --- Directorio y expediente ---
    [HttpGet, RequierePermiso(Permisos.TrabajadoresConsultar)]
    public async Task<PagedResult<TrabajadorResumenDto>> Listar([FromQuery] TrabajadorFiltro f, CancellationToken ct) =>
        await _trabajadores.ListarAsync(f, ct);

    /// <summary>Expediente propio (cualquier usuario vinculado a un trabajador).</summary>
    [HttpGet("mi-expediente")]
    public async Task<TrabajadorDetalleDto> MiExpediente(CancellationToken ct) => await _trabajadores.MiExpedienteAsync(ct);

    [HttpGet("{id:guid}"), RequierePermiso(Permisos.TrabajadoresConsultar)]
    public async Task<TrabajadorDetalleDto> Obtener(Guid id, CancellationToken ct) => await _trabajadores.ObtenerAsync(id, ct);

    [HttpPost, RequierePermiso(Permisos.TrabajadoresCrear)]
    public async Task<IActionResult> Crear(CrearTrabajadorRequest r, CancellationToken ct)
    {
        // Todo trabajador nuevo tiene cuenta de acceso: se crea junto con el expediente (o se vincula una existente).
        if (r.Cuenta is null && r.UsuarioId is null)
            throw ServiceException.Validation("Indique el correo y la contraseña de la cuenta del trabajador.");
        var t = await _trabajadores.CrearAsync(r, ct);
        return CreatedAtAction(nameof(Obtener), new { id = t.TrabajadorId }, t);
    }

    [HttpPut("{id:guid}"), RequierePermiso(Permisos.TrabajadoresEditar)]
    public async Task<TrabajadorDetalleDto> Actualizar(Guid id, ActualizarTrabajadorRequest r, CancellationToken ct) =>
        await _trabajadores.ActualizarAsync(id, r, ct);

    [HttpPost("{id:guid}/estado"), RequierePermiso(Permisos.TrabajadoresEditar)]
    public async Task<TrabajadorDetalleDto> CambiarEstado(Guid id, CambiarEstadoLaboralRequest r, CancellationToken ct) =>
        await _trabajadores.CambiarEstadoAsync(id, r, ct);

    [HttpPost("{id:guid}/baja"), RequierePermiso(Permisos.TrabajadoresBaja)]
    public async Task<TrabajadorDetalleDto> Baja(Guid id, BajaTrabajadorRequest r, CancellationToken ct) =>
        await _trabajadores.DarDeBajaAsync(id, r, ct);

    [HttpPost("{id:guid}/reingreso"), RequierePermiso(Permisos.TrabajadoresBaja)]
    public async Task<TrabajadorDetalleDto> Reingreso(Guid id, [FromBody] ReingresoRequest r, CancellationToken ct) =>
        await _trabajadores.ReingresarAsync(id, r.Motivo, ct);

    // El servicio exige permiso de datos sensibles o ser el propio trabajador.
    [HttpGet("{id:guid}/historial")]
    public async Task<IReadOnlyList<HistorialLaboralDto>> Historial(Guid id, CancellationToken ct) =>
        await _trabajadores.HistorialAsync(id, ct);

    // --- Supervisión ---
    [HttpGet("{id:guid}/supervisores"), RequierePermiso(Permisos.TrabajadoresConsultar)]
    public async Task<IReadOnlyList<SupervisorAsignadoDto>> Supervisores(Guid id, CancellationToken ct) =>
        await _supervision.ListarAsync(id, ct);

    [HttpGet("{id:guid}/supervisados"), RequierePermiso(Permisos.TrabajadoresConsultar)]
    public async Task<IReadOnlyList<TrabajadorResumenDto>> Supervisados(Guid id, CancellationToken ct) =>
        await _supervision.SupervisadosAsync(id, ct);

    [HttpPost("{id:guid}/supervisores"), RequierePermiso(Permisos.SupervisoresGestionar)]
    public async Task<SupervisorAsignadoDto> AsignarSupervisor(Guid id, AsignarSupervisorRequest r, CancellationToken ct) =>
        await _supervision.AsignarAsync(id, r, ct);

    [HttpPost("{id:guid}/supervisores/{supervisionId:long}/finalizar"), RequierePermiso(Permisos.SupervisoresGestionar)]
    public async Task<SupervisorAsignadoDto> FinalizarSupervision(Guid id, long supervisionId, FinalizarSupervisionRequest r, CancellationToken ct) =>
        await _supervision.FinalizarAsync(id, supervisionId, r, ct);

    // --- Contactos de emergencia (lectura: permiso de datos sensibles o propio trabajador, validado en el servicio) ---
    [HttpGet("{id:guid}/contactos-emergencia")]
    public async Task<IReadOnlyList<ContactoEmergenciaDto>> Contactos(Guid id, CancellationToken ct) =>
        await _contactos.ListarAsync(id, ct);

    [HttpPost("{id:guid}/contactos-emergencia"), RequierePermiso(Permisos.TrabajadoresEditar)]
    public async Task<ContactoEmergenciaDto> CrearContacto(Guid id, GuardarContactoEmergenciaRequest r, CancellationToken ct) =>
        await _contactos.CrearAsync(id, r, ct);

    [HttpPut("{id:guid}/contactos-emergencia/{contactoId:guid}"), RequierePermiso(Permisos.TrabajadoresEditar)]
    public async Task<ContactoEmergenciaDto> ActualizarContacto(Guid id, Guid contactoId, GuardarContactoEmergenciaRequest r, CancellationToken ct) =>
        await _contactos.ActualizarAsync(id, contactoId, r, ct);

    [HttpDelete("{id:guid}/contactos-emergencia/{contactoId:guid}"), RequierePermiso(Permisos.TrabajadoresEditar)]
    public async Task<IActionResult> EliminarContacto(Guid id, Guid contactoId, CancellationToken ct)
    {
        await _contactos.EliminarAsync(id, contactoId, ct);
        return NoContent();
    }
}
