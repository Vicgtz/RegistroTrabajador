using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public class ReportesClient(ApiClient api)
{
    private static string Consulta(ReporteFiltro f, bool paginar) => Qs.De(
        ("page", paginar ? f.Page : null), ("pageSize", paginar ? f.PageSize : null),
        ("desde", f.Desde?.ToString("yyyy-MM-dd")), ("hasta", f.Hasta?.ToString("yyyy-MM-dd")),
        ("periodo", f.Periodo), ("anio", f.Anio), ("numero", f.Numero),
        ("trabajadorId", f.TrabajadorId), ("departamentoId", f.DepartamentoId), ("puestoId", f.PuestoId), ("supervisorId", f.SupervisorId),
        ("tipoPermisoId", f.TipoPermisoId), ("proyectoId", f.ProyectoId), ("estadoProyectoId", f.EstadoProyectoId), ("estado", f.Estado),
        ("buscar", f.Buscar), ("ordenarPor", f.OrdenarPor), ("descendente", f.Descendente ? true : null),
        ("soloIncompletos", f.SoloIncompletos ? true : null), ("soloVencidas", f.SoloVencidas ? true : null));

    public Task<ApiResult<ReporteDto>> ConsultarAsync(string tipo, ReporteFiltro f) =>
        api.GetAsync<ReporteDto>($"api/reportes/{Uri.EscapeDataString(tipo)}" + Consulta(f, true));

    /// <summary>Archivo con los mismos filtros que la pantalla; la API vuelve a exigir <c>reportes.exportar</c>.</summary>
    public Task<ApiResult<byte[]>> ExportarAsync(string tipo, string formato, ReporteFiltro f)
    {
        var q = Consulta(f, false);
        var url = $"api/reportes/{Uri.EscapeDataString(tipo)}/exportar" + (q.Length == 0 ? "?" : q + "&") + "formato=" + Uri.EscapeDataString(formato);
        return api.GetBytesAsync(url);
    }
}
