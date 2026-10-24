using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

/// <summary>Arma la cadena de consulta omitiendo los valores vacíos.</summary>
public static class Qs
{
    public static string De(params (string Clave, object? Valor)[] pares)
    {
        var partes = pares
            .Where(p => p.Valor is not null && !(p.Valor is string s && string.IsNullOrWhiteSpace(s)))
            .Select(p => $"{Uri.EscapeDataString(p.Clave)}={Uri.EscapeDataString(Texto(p.Valor!))}");
        var qs = string.Join("&", partes);
        return qs.Length == 0 ? "" : "?" + qs;
    }

    private static string Texto(object v) => v switch
    {
        DateTime d => d.ToString("o"),
        bool b => b ? "true" : "false",
        IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
        _ => v.ToString() ?? ""
    };
}

public class UsuariosClient(ApiClient api)
{
    public Task<ApiResult<PagedResult<UsuarioDto>>> ListarAsync(UsuarioFiltro f) =>
        api.GetAsync<PagedResult<UsuarioDto>>("api/usuarios" + Qs.De(("page", f.Page), ("pageSize", f.PageSize), ("buscar", f.Buscar), ("estado", f.Estado)));

    public Task<ApiResult<UsuarioDto>> CrearAsync(CrearUsuarioRequest r) => api.PostAsync<UsuarioDto>("api/usuarios", r);

    public Task<ApiResult<UsuarioDto>> ActualizarAsync(Guid id, ActualizarUsuarioRequest r) =>
        api.SendAsync<UsuarioDto>(HttpMethod.Put, $"api/usuarios/{id}", r);

    public Task<ApiResult> ActivarAsync(Guid id) => api.SendAsync(HttpMethod.Post, $"api/usuarios/{id}/activar");
    public Task<ApiResult> DesactivarAsync(Guid id) => api.SendAsync(HttpMethod.Post, $"api/usuarios/{id}/desactivar");
    public Task<ApiResult> DesbloquearAsync(Guid id) => api.SendAsync(HttpMethod.Post, $"api/usuarios/{id}/desbloquear");

    public Task<ApiResult> RestablecerContrasenaAsync(Guid id, string nueva) =>
        api.SendAsync(HttpMethod.Post, $"api/usuarios/{id}/restablecer-contrasena", new RestablecerContrasenaUsuarioRequest { ContrasenaNueva = nueva });

    public Task<ApiResult<List<RolDto>>> RolesAsync(Guid id) => api.GetAsync<List<RolDto>>($"api/usuarios/{id}/roles");
    public Task<ApiResult> AsignarRolAsync(Guid id, Guid rolId) => api.SendAsync(HttpMethod.Put, $"api/usuarios/{id}/roles/{rolId}");
    public Task<ApiResult> RetirarRolAsync(Guid id, Guid rolId) => api.SendAsync(HttpMethod.Delete, $"api/usuarios/{id}/roles/{rolId}");
    public Task<ApiResult> RevocarSesionesAsync(Guid id) => api.SendAsync(HttpMethod.Delete, $"api/usuarios/{id}/sesiones");
}

public class RolesClient(ApiClient api)
{
    public Task<ApiResult<List<RolDto>>> ListarAsync() => api.GetAsync<List<RolDto>>("api/roles");
    public Task<ApiResult<List<PermisoDto>>> PermisosAsync() => api.GetAsync<List<PermisoDto>>("api/permisos");
    public Task<ApiResult<RolPermisosDto>> PermisosDeRolAsync(Guid id) => api.GetAsync<RolPermisosDto>($"api/roles/{id}/permisos");

    public Task<ApiResult<RolPermisosDto>> ConfigurarPermisosAsync(Guid id, IEnumerable<string> codigos) =>
        api.SendAsync<RolPermisosDto>(HttpMethod.Put, $"api/roles/{id}/permisos", new ConfigurarPermisosRolRequest { Permisos = codigos.ToList() });

    public Task<ApiResult<RolDto>> CrearAsync(CrearRolRequest r) => api.PostAsync<RolDto>("api/roles", r);
    public Task<ApiResult<RolDto>> ActualizarAsync(Guid id, ActualizarRolRequest r) => api.SendAsync<RolDto>(HttpMethod.Put, $"api/roles/{id}", r);

    public Task<ApiResult<RolDto>> CambiarEstadoAsync(Guid id, bool estado) =>
        api.PostAsync<RolDto>($"api/roles/{id}/estado", new CambiarEstadoRolRequest { Estado = estado });
}

public class SesionesClient(ApiClient api)
{
    public Task<ApiResult<PagedResult<SesionDto>>> ListarAsync(int page, int pageSize) =>
        api.GetAsync<PagedResult<SesionDto>>("api/sesiones" + Qs.De(("page", page), ("pageSize", pageSize)));

    public Task<ApiResult> RevocarAsync(Guid id) => api.SendAsync(HttpMethod.Delete, $"api/sesiones/{id}");

    public Task<ApiResult<PagedResult<SesionDto>>> MiasAsync(int page, int pageSize) =>
        api.GetAsync<PagedResult<SesionDto>>("api/sesiones/mias" + Qs.De(("page", page), ("pageSize", pageSize)));

    public Task<ApiResult> RevocarMiaAsync(Guid id) => api.SendAsync(HttpMethod.Delete, $"api/sesiones/mias/{id}");
}

public class RegistrosClient(ApiClient api)
{
    public Task<ApiResult<PagedResult<HistorialAccesoDto>>> AccesosAsync(FechaFiltro f) =>
        api.GetAsync<PagedResult<HistorialAccesoDto>>("api/accesos" + Qs.De(("page", f.Page), ("pageSize", f.PageSize), ("desde", f.Desde), ("hasta", f.Hasta)));

    public Task<ApiResult<PagedResult<AuditoriaDto>>> AuditoriaAsync(AuditoriaFiltro f) =>
        api.GetAsync<PagedResult<AuditoriaDto>>("api/auditoria" + Qs.De(("page", f.Page), ("pageSize", f.PageSize),
            ("desde", f.Desde), ("hasta", f.Hasta), ("entidad", f.Entidad), ("entidadId", f.EntidadId)));
}

public class EmpresaClient(ApiClient api)
{
    public Task<ApiResult<EmpresaDto>> ObtenerAsync() => api.GetAsync<EmpresaDto>("api/empresa");
    public Task<ApiResult<EmpresaDto>> ActualizarAsync(ActualizarEmpresaRequest r) => api.SendAsync<EmpresaDto>(HttpMethod.Put, "api/empresa", r);
    public Task<ApiResult<ConfiguracionEmpresaDto>> ConfiguracionAsync() => api.GetAsync<ConfiguracionEmpresaDto>("api/empresa/configuracion");

    public Task<ApiResult<ConfiguracionEmpresaDto>> ActualizarConfiguracionAsync(ConfiguracionEmpresaDto r) =>
        api.SendAsync<ConfiguracionEmpresaDto>(HttpMethod.Put, "api/empresa/configuracion", r);
}

public class PlataformaClient(ApiClient api)
{
    public Task<ApiResult<PagedResult<EmpresaPlataformaDto>>> ListarAsync(int page, int pageSize, string? buscar) =>
        api.GetAsync<PagedResult<EmpresaPlataformaDto>>("api/plataforma/empresas" + Qs.De(("page", page), ("pageSize", pageSize), ("buscar", buscar)));

    public Task<ApiResult<EmpresaPlataformaDto>> CrearAsync(CrearEmpresaPlataformaRequest r) =>
        api.PostAsync<EmpresaPlataformaDto>("api/plataforma/empresas", r);

    public Task<ApiResult<EmpresaPlataformaDto>> CambiarEstadoAsync(Guid id, byte estado) =>
        api.PostAsync<EmpresaPlataformaDto>($"api/plataforma/empresas/{id}/estado", new CambiarEstadoEmpresaRequest { Estado = estado });
}

public static class Formato
{
    /// <summary>Los instantes llegan en UTC sin 'Z'; se muestran en la hora local del navegador.</summary>
    public static string Local(DateTime? utc) => utc is null
        ? "—"
        : DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc).ToLocalTime().ToString("dd/MM/yyyy HH:mm");

    public static string EstadoUsuario(byte e) => e switch
    {
        Models.EstadoUsuario.Activo => "Activo", Models.EstadoUsuario.Inactivo => "Inactivo", _ => "Suspendido"
    };

    public static string EstadoEmpresa(byte e) => e switch
    {
        Models.EstadoEmpresa.Activa => "Activa", Models.EstadoEmpresa.Suspendida => "Suspendida", _ => "Inactiva"
    };

    public static string ResultadoAcceso(byte r) => r switch
    {
        Models.ResultadoAcceso.Exitoso => "Exitoso",
        Models.ResultadoAcceso.CredencialesInvalidas => "Credenciales inválidas",
        Models.ResultadoAcceso.CuentaBloqueada => "Cuenta bloqueada",
        Models.ResultadoAcceso.CuentaInactiva => "Cuenta inactiva",
        Models.ResultadoAcceso.RefreshInvalido => "Sesión inválida",
        _ => "Desconocido"
    };

    /// <summary>Muestra un JSON con sangría; si no es JSON válido devuelve el texto tal cual.</summary>
    public static string Json(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return "—";
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return System.Text.Json.JsonSerializer.Serialize(doc.RootElement, new System.Text.Json.JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        }
        catch (System.Text.Json.JsonException) { return json; }
    }
}
