using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegistroTrabajo.Api.Infrastructure;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Api.Controllers;

// Bloque 14. La empresa sale siempre del token. Los servicios validan el acceso por trabajador (propio, expediente.consultar/editar/aprobar);
// el controlador solo exige permiso en las rutas que no dependen del trabajador.

[ApiController, Authorize]
public class ExpedienteProfesionalController : ControllerBase
{
    private readonly IExpedienteProfesionalService _expediente;
    private readonly ICurriculumService _curriculum;

    public ExpedienteProfesionalController(IExpedienteProfesionalService expediente, ICurriculumService curriculum)
    {
        _expediente = expediente; _curriculum = curriculum;
    }

    // --- Expediente profesional de un trabajador ---
    [HttpGet("api/trabajadores/mi-expediente-profesional")]
    public async Task<ExpedienteProfesionalDto> Mio(CancellationToken ct) => await _expediente.MiExpedienteAsync(ct);

    [HttpGet("api/trabajadores/{id:guid}/expediente-profesional")]
    public async Task<ExpedienteProfesionalDto> Obtener(Guid id, CancellationToken ct) => await _expediente.ObtenerAsync(id, ct);

    // --- Habilidades ---
    [HttpPost("api/trabajadores/{id:guid}/habilidades")]
    public async Task<TrabajadorHabilidadDto> GuardarHabilidad(Guid id, GuardarHabilidadRequest r, CancellationToken ct) =>
        await _expediente.GuardarHabilidadAsync(id, r, ct);

    [HttpDelete("api/trabajadores/{id:guid}/habilidades/{habilidadId:guid}")]
    public async Task<IActionResult> EliminarHabilidad(Guid id, Guid habilidadId, CancellationToken ct)
    {
        await _expediente.EliminarHabilidadAsync(id, habilidadId, ct);
        return NoContent();
    }

    // --- Estudios ---
    [HttpPost("api/trabajadores/{id:guid}/estudios")]
    public async Task<EstudioDto> CrearEstudio(Guid id, GuardarEstudioRequest r, CancellationToken ct) =>
        await _expediente.CrearEstudioAsync(id, r, ct);

    [HttpPut("api/trabajadores/{id:guid}/estudios/{estudioId:guid}")]
    public async Task<EstudioDto> ActualizarEstudio(Guid id, Guid estudioId, GuardarEstudioRequest r, CancellationToken ct) =>
        await _expediente.ActualizarEstudioAsync(id, estudioId, r, ct);

    [HttpDelete("api/trabajadores/{id:guid}/estudios/{estudioId:guid}")]
    public async Task<IActionResult> EliminarEstudio(Guid id, Guid estudioId, CancellationToken ct)
    {
        await _expediente.EliminarEstudioAsync(id, estudioId, ct);
        return NoContent();
    }

    // --- Currículum ---
    [HttpGet("api/trabajadores/{id:guid}/curriculum")]
    public async Task<IActionResult> DescargarCurriculum(Guid id, CancellationToken ct)
    {
        var cv = await _curriculum.ObtenerAsync(id, ct);
        if (cv is null) return NotFound();
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(cv.Contenido, cv.TipoContenido, cv.NombreArchivo);
    }

    [HttpPost("api/trabajadores/{id:guid}/curriculum"), RequestSizeLimit(LimitesCurriculum.MaxBytes + 100_000)]
    public async Task<CurriculumInfoDto> SubirCurriculum(Guid id, IFormFile archivo, CancellationToken ct)
    {
        if (archivo.Length == 0) throw ServiceException.Validation("El archivo está vacío.");
        if (archivo.Length > LimitesCurriculum.MaxBytes) throw ServiceException.Validation("El currículum no puede pesar más de 5 MB.");
        using var ms = new MemoryStream((int)archivo.Length);
        await archivo.CopyToAsync(ms, ct);
        return await _curriculum.GuardarAsync(id, archivo.FileName, ms.ToArray(), ct);
    }

    [HttpDelete("api/trabajadores/{id:guid}/curriculum")]
    public async Task<IActionResult> EliminarCurriculum(Guid id, CancellationToken ct)
    {
        await _curriculum.EliminarAsync(id, ct);
        return NoContent();
    }

    // --- Aprobación de lo propuesto por los trabajadores ---
    [HttpGet("api/expediente/pendientes"), RequierePermiso(Permisos.ExpedienteAprobar)]
    public async Task<IReadOnlyList<PendienteExpedienteDto>> Pendientes(CancellationToken ct) => await _expediente.PendientesAsync(ct);

    [HttpPost("api/expediente/habilidades/{id:guid}/resolver"), RequierePermiso(Permisos.ExpedienteAprobar)]
    public async Task<IActionResult> ResolverHabilidad(Guid id, ResolverExpedienteRequest r, CancellationToken ct)
    {
        await _expediente.ResolverHabilidadAsync(id, r, ct);
        return NoContent();
    }

    [HttpPost("api/expediente/estudios/{id:guid}/resolver"), RequierePermiso(Permisos.ExpedienteAprobar)]
    public async Task<IActionResult> ResolverEstudio(Guid id, ResolverExpedienteRequest r, CancellationToken ct)
    {
        await _expediente.ResolverEstudioAsync(id, r, ct);
        return NoContent();
    }

    // --- Catálogo de habilidades (autocompletado) y búsqueda por perfil ---
    [HttpGet("api/habilidades")]
    public async Task<IReadOnlyList<HabilidadDto>> Habilidades([FromQuery] string? q, [FromQuery] bool incluirInactivas, CancellationToken ct) =>
        await _expediente.ListarHabilidadesAsync(q, incluirInactivas, ct);

    [HttpPut("api/habilidades/{id:guid}"), RequierePermiso(Permisos.ExpedienteEditar)]
    public async Task<HabilidadDto> ActualizarHabilidad(Guid id, ActualizarHabilidadRequest r, CancellationToken ct) =>
        await _expediente.ActualizarHabilidadCatalogoAsync(id, r, ct);

    [HttpGet("api/trabajadores/buscar-perfil"), RequierePermiso(Permisos.ExpedienteBuscar)]
    public async Task<PagedResult<PerfilResultadoDto>> BuscarPerfil([FromQuery] PerfilFiltro f, CancellationToken ct) =>
        await _expediente.BuscarAsync(f, ct);
}
