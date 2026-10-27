using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RegistroTrabajo.Data;
using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public class ExportacionService : IExportacionService
{
    private const int MaxFilasPdf = 2000;

    private readonly ReporteService _reportes;
    private readonly RegistroTrabajoDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IPermisoService _permisos;
    private readonly IAuditoriaService _auditoria;

    public ExportacionService(ReporteService reportes, RegistroTrabajoDbContext db, ICurrentUser current, IPermisoService permisos, IAuditoriaService auditoria)
    {
        _reportes = reportes; _db = db; _current = current; _permisos = permisos; _auditoria = auditoria;
    }

    public async Task<ArchivoExportadoDto> ExportarAsync(string tipo, string formato, ReporteFiltro filtro, CancellationToken ct)
    {
        // El permiso se comprueba aquí y en el controlador: ocultar el botón en la interfaz no basta.
        await _permisos.AsegurarPermisoAsync(Permisos.ReportesExportar, ct);
        formato = (formato ?? "").Trim().ToLowerInvariant();
        if (formato is not (FormatoExportacion.Excel or FormatoExportacion.Pdf)) throw ServiceException.Validation("El formato debe ser xlsx o pdf.");

        var (r, filtros, titulo, c) = await _reportes.TodoAsync(tipo, filtro, ct);
        if (formato == FormatoExportacion.Pdf && r.Total > MaxFilasPdf)
            throw ServiceException.Validation($"El PDF admite hasta {MaxFilasPdf} filas y el reporte tiene {r.Total}. Acote los filtros o exporte a Excel.");

        var empresaId = _current.RequireEmpresaId();
        var empresa = await _db.Empresas.AsNoTracking().Where(e => e.EmpresaId == empresaId).Select(e => e.Nombre).FirstAsync(ct);
        var ahoraUtc = DateTime.UtcNow;
        var local = TimeZoneInfo.ConvertTimeFromUtc(ahoraUtc, c.Zona);
        var generado = local.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

        var contenido = formato == FormatoExportacion.Excel
            ? ExportadorExcel.Generar(empresa, titulo, local, filtros, r.Resumen, r.Columnas, r.Filas, r.Nota)
            : ExportadorPdf.Generar(empresa, titulo, generado, filtros, r.Resumen, r.Columnas, r.Filas, r.Nota);

        // Se audita la operación y sus filtros, nunca el contenido del reporte.
        _auditoria.Registrar("Reportes", "Reporte", tipo, "Exportar", null, new { tipo, formato, filas = r.Filas.Count, filtros });
        await _db.SaveSafeAsync(ct);

        return new ArchivoExportadoDto
        {
            NombreArchivo = $"reporte-{tipo}-{local:yyyyMMdd-HHmm}.{formato}",
            TipoContenido = formato == FormatoExportacion.Excel
                ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" : "application/pdf",
            Contenido = contenido
        };
    }
}
