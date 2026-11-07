using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegistroTrabajo.Api.Infrastructure;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Api.Controllers;

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
