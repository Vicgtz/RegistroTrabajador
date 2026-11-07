using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public class ConfiguracionClient(ApiClient api)
{
    public Task<ApiResult<AparienciaEmpresaDto>> AparienciaAsync() => api.GetAsync<AparienciaEmpresaDto>("api/empresa/apariencia");

    /// <summary>Logotipo como data URL (el endpoint exige el JWT, por eso no se puede usar directamente en un &lt;img&gt;).</summary>
    public async Task<string?> LogoAsync()
    {
        var r = await api.GetBytesAsync("api/empresa/logo");
        if (!r.Ok || r.Data is null || r.Data.Length == 0) return null;
        return $"data:{TipoImagen(r.Data)};base64,{Convert.ToBase64String(r.Data)}";
    }

    public static string TipoImagen(byte[] b) =>
        b.Length > 3 && b[0] == 0x89 ? "image/png" : b.Length > 3 && b[0] == 0xFF ? "image/jpeg" : "image/webp";

    public Task<ApiResult> SubirLogoAsync(string nombre, byte[] contenido, string tipo) => api.SubirArchivoAsync("api/empresa/logo", nombre, contenido, tipo);
    public Task<ApiResult<ClaveRegistroEstadoDto>> ClaveRegistroAsync() => api.GetAsync<ClaveRegistroEstadoDto>("api/empresa/clave-registro");
    public Task<ApiResult<ClaveRegistroGeneradaDto>> GenerarClaveRegistroAsync() => api.PostAsync<ClaveRegistroGeneradaDto>("api/empresa/clave-registro", new { });
    public Task<ApiResult> DesactivarClaveRegistroAsync() => api.SendAsync(HttpMethod.Delete, "api/empresa/clave-registro");
    public Task<ApiResult> EliminarLogoAsync() => api.SendAsync(HttpMethod.Delete, "api/empresa/logo");

    public Task<ApiResult<PermisosEfectivosDto>> PermisosEfectivosAsync(Guid usuarioId) =>
        api.GetAsync<PermisosEfectivosDto>($"api/usuarios/{usuarioId}/permisos-efectivos");

    public Task<ApiResult<EstadoSistemaDto>> EstadoSistemaAsync() => api.GetAsync<EstadoSistemaDto>("api/sistema/estado");
}
