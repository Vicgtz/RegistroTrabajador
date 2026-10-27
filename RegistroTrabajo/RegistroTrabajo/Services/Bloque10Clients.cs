using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public class DashboardClient(ApiClient api)
{
    public Task<ApiResult<DashboardDto>> ObtenerAsync() => api.GetAsync<DashboardDto>("api/dashboard");
}

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

public class BorradoresClient(ApiClient api)
{
    public Task<ApiResult<PagedResult<BorradorDto>>> ListarAsync(BorradorFiltro f) =>
        api.GetAsync<PagedResult<BorradorDto>>("api/borradores" + Qs.De(("page", f.Page), ("pageSize", f.PageSize), ("modulo", f.Modulo)));

    public Task<ApiResult<BorradorDto>> ObtenerAsync(Guid id) => api.GetAsync<BorradorDto>($"api/borradores/{id}");
    public Task<ApiResult<BorradorDto>> CrearAsync(GuardarBorradorRequest r) => api.PostAsync<BorradorDto>("api/borradores", r);
    public Task<ApiResult<BorradorDto>> ActualizarAsync(Guid id, GuardarBorradorRequest r) => api.SendAsync<BorradorDto>(HttpMethod.Put, $"api/borradores/{id}", r);
    public Task<ApiResult> EliminarAsync(Guid id) => api.SendAsync(HttpMethod.Delete, $"api/borradores/{id}");
}

public class ErroresTecnicosClient(ApiClient api)
{
    public Task<ApiResult<PagedResult<ErrorTecnicoDto>>> ListarAsync(ErrorTecnicoFiltro f) =>
        api.GetAsync<PagedResult<ErrorTecnicoDto>>("api/errores-tecnicos" + Qs.De(("page", f.Page), ("pageSize", f.PageSize),
            ("correlationId", f.CorrelationId), ("desde", f.Desde?.ToString("yyyy-MM-dd")), ("hasta", f.Hasta?.ToString("yyyy-MM-dd")), ("buscar", f.Buscar)));
}

/// <summary>Textos de presentación de reportes, dashboard y borradores.</summary>
public static class FormatoReportes
{
    public static string RutaBorrador(string modulo) => modulo switch
    {
        ModuloBorrador.Permisos => "permisos",
        ModuloBorrador.Proyectos => "proyectos",
        ModuloBorrador.Tareas => "proyectos",
        ModuloBorrador.Trabajadores => "trabajadores",
        _ => ""
    };
}

/// <summary>Un tramo de una gráfica (dona o barra apilada).</summary>
public record SegmentoGrafica(string Etiqueta, int Valor, string Color);
