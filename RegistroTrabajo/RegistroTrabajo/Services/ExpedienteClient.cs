using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

/// <summary>Bloque 14: expediente profesional (estudios, habilidades, currículum), aprobación y búsqueda por perfil.</summary>
public class ExpedienteClient(ApiClient api)
{
    public Task<ApiResult<ExpedienteProfesionalDto>> ObtenerAsync(Guid trabajadorId) =>
        api.GetAsync<ExpedienteProfesionalDto>($"api/trabajadores/{trabajadorId}/expediente-profesional");

    public Task<ApiResult<TrabajadorHabilidadDto>> GuardarHabilidadAsync(Guid trabajadorId, GuardarHabilidadRequest r) =>
        api.PostAsync<TrabajadorHabilidadDto>($"api/trabajadores/{trabajadorId}/habilidades", r);

    public Task<ApiResult> EliminarHabilidadAsync(Guid trabajadorId, Guid id) =>
        api.SendAsync(HttpMethod.Delete, $"api/trabajadores/{trabajadorId}/habilidades/{id}");

    public Task<ApiResult<EstudioDto>> CrearEstudioAsync(Guid trabajadorId, GuardarEstudioRequest r) =>
        api.PostAsync<EstudioDto>($"api/trabajadores/{trabajadorId}/estudios", r);

    public Task<ApiResult<EstudioDto>> ActualizarEstudioAsync(Guid trabajadorId, Guid id, GuardarEstudioRequest r) =>
        api.SendAsync<EstudioDto>(HttpMethod.Put, $"api/trabajadores/{trabajadorId}/estudios/{id}", r);

    public Task<ApiResult> EliminarEstudioAsync(Guid trabajadorId, Guid id) =>
        api.SendAsync(HttpMethod.Delete, $"api/trabajadores/{trabajadorId}/estudios/{id}");

    public Task<ApiResult> SubirCurriculumAsync(Guid trabajadorId, string nombre, byte[] contenido, string tipo) =>
        api.SubirArchivoAsync($"api/trabajadores/{trabajadorId}/curriculum", nombre, contenido, tipo);

    public Task<ApiResult<byte[]>> DescargarCurriculumAsync(Guid trabajadorId) =>
        api.GetBytesAsync($"api/trabajadores/{trabajadorId}/curriculum");

    public Task<ApiResult> EliminarCurriculumAsync(Guid trabajadorId) =>
        api.SendAsync(HttpMethod.Delete, $"api/trabajadores/{trabajadorId}/curriculum");

    public Task<ApiResult<List<PendienteExpedienteDto>>> PendientesAsync() =>
        api.GetAsync<List<PendienteExpedienteDto>>("api/expediente/pendientes");

    public Task<ApiResult> ResolverAsync(PendienteExpedienteDto p, bool aprobar, string? motivo) =>
        api.SendAsync(HttpMethod.Post,
            $"api/expediente/{(p.Clase == "Habilidad" ? "habilidades" : "estudios")}/{p.Id}/resolver",
            new ResolverExpedienteRequest { Aprobar = aprobar, MotivoRechazo = motivo });

    public Task<ApiResult<List<HabilidadDto>>> HabilidadesAsync(string? texto = null) =>
        api.GetAsync<List<HabilidadDto>>("api/habilidades" + Qs.De(("q", texto)));

    public Task<ApiResult<PagedResult<PerfilResultadoDto>>> BuscarAsync(PerfilFiltro f)
    {
        var q = Qs.De(("page", f.Page), ("pageSize", f.PageSize), ("todasLasHabilidades", f.TodasLasHabilidades),
            ("nivelMinimo", f.NivelMinimo), ("tipoEstudio", f.TipoEstudio), ("estudio", f.Estudio), ("soloActivos", f.SoloActivos));
        // Las habilidades se envían como parámetro repetido: habilidadIds=a&habilidadIds=b.
        var ids = string.Join("&", (f.HabilidadIds ?? new()).Select(i => "habilidadIds=" + i));
        if (ids.Length > 0) q = q.Length == 0 ? "?" + ids : q + "&" + ids;
        return api.GetAsync<PagedResult<PerfilResultadoDto>>("api/trabajadores/buscar-perfil" + q);
    }
}
