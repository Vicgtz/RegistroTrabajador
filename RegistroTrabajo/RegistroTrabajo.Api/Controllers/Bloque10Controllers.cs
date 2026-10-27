using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegistroTrabajo.Api.Infrastructure;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Api.Controllers;

// Bloque 10. La empresa y el usuario salen siempre del token. El dashboard y los reportes se acotan al alcance de cada usuario;
// los indicadores no abren registros: al consultar un elemento, su módulo vuelve a validar el acceso.

[ApiController, Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _svc;
    public DashboardController(IDashboardService svc) => _svc = svc;

    [HttpGet("api/dashboard")]
    public async Task<DashboardDto> Obtener(CancellationToken ct) => await _svc.ObtenerAsync(ct);
}

[ApiController, Authorize, Route("api/reportes")]
public class ReportesController : ControllerBase
{
    private readonly IReporteService _reportes;
    private readonly IExportacionService _exportacion;
    public ReportesController(IReporteService reportes, IExportacionService exportacion) { _reportes = reportes; _exportacion = exportacion; }

    [HttpGet("{tipo}"), RequierePermiso(Permisos.ReportesConsultar)]
    public async Task<ReporteDto> Consultar(string tipo, [FromQuery] ReporteFiltro f, CancellationToken ct) => await _reportes.ConsultarAsync(tipo, f, ct);

    /// <summary>Archivo con los mismos filtros y restricciones que la consulta. Exige <c>reportes.exportar</c> aquí y en el servicio.</summary>
    [HttpGet("{tipo}/exportar"), RequierePermiso(Permisos.ReportesExportar)]
    public async Task<IActionResult> Exportar(string tipo, [FromQuery] string formato, [FromQuery] ReporteFiltro f, CancellationToken ct)
    {
        var archivo = await _exportacion.ExportarAsync(tipo, formato, f, ct);
        Response.Headers.CacheControl = "no-store";
        return File(archivo.Contenido, archivo.TipoContenido, archivo.NombreArchivo);
    }
}

[ApiController, Authorize, Route("api/borradores")]
public class BorradoresController : ControllerBase
{
    private readonly IBorradoresService _svc;
    public BorradoresController(IBorradoresService svc) => _svc = svc;

    [HttpGet]
    public async Task<PagedResult<BorradorDto>> Listar([FromQuery] BorradorFiltro f, CancellationToken ct) => await _svc.ListarAsync(f, ct);

    [HttpGet("{id:guid}")]
    public async Task<BorradorDto> Obtener(Guid id, CancellationToken ct) => await _svc.ObtenerAsync(id, ct);

    [HttpPost]
    public async Task<BorradorDto> Crear(GuardarBorradorRequest r, CancellationToken ct) => await _svc.CrearAsync(r, ct);

    [HttpPut("{id:guid}")]
    public async Task<BorradorDto> Actualizar(Guid id, GuardarBorradorRequest r, CancellationToken ct) => await _svc.ActualizarAsync(id, r, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken ct) { await _svc.EliminarAsync(id, ct); return NoContent(); }
}

[ApiController, Authorize, Route("api/errores-tecnicos")]
public class ErroresTecnicosController : ControllerBase
{
    private readonly IErrorTecnicoService _svc;
    public ErroresTecnicosController(IErrorTecnicoService svc) => _svc = svc;

    [HttpGet, RequierePermiso(Permisos.ErroresConsultar)]
    public async Task<PagedResult<ErrorTecnicoDto>> Listar([FromQuery] ErrorTecnicoFiltro f, CancellationToken ct) => await _svc.ListarAsync(f, ct);
}
