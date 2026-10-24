using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

/// <summary>Inicio y cierre de sesión, perfil y cambio de contraseña contra la API.</summary>
public class AuthClient(ApiClient api, SesionStore store)
{
    public async Task<PerfilDto?> PerfilActualAsync() => (await store.GetAsync())?.Perfil;

    public async Task<ApiResult<PerfilDto>> LoginAsync(string correo, string contrasena)
    {
        var login = await api.PostAsync<TokenResponse>("api/auth/login", new LoginRequest
        {
            Correo = correo.Trim(),
            Contrasena = contrasena,
            Dispositivo = "Navegador web"
        });
        if (!login.Ok || login.Data is null)
            return new ApiResult<PerfilDto>(false, default, login.Error ?? "No se pudo iniciar sesión.", login.Status);

        // Se guarda primero el token (sin avisar) porque el perfil se pide ya autenticado.
        await store.SetAsync(SesionLocal.Desde(login.Data), notificar: false);

        var perfil = await api.GetAsync<PerfilDto>("api/auth/me");
        if (!perfil.Ok || perfil.Data is null)
        {
            await store.ClearAsync();
            return new ApiResult<PerfilDto>(false, default, perfil.Error ?? "No se pudo cargar tu perfil.", perfil.Status);
        }

        await store.SetAsync(SesionLocal.Desde(login.Data, perfil.Data));
        return perfil;
    }

    public async Task LogoutAsync()
    {
        if (await store.GetAsync() is not null)
            await api.SendAsync(HttpMethod.Post, "api/auth/logout");   // si falla, igual se limpia la sesión local
        await store.ClearAsync();
    }

    public async Task<ApiResult> CambiarContrasenaAsync(string actual, string nueva)
    {
        var r = await api.SendAsync(HttpMethod.Post, "api/auth/cambiar-contrasena",
            new CambiarContrasenaRequest { ContrasenaActual = actual, ContrasenaNueva = nueva });
        if (r.Ok) await RecargarPerfilAsync();
        return r;
    }

    public Task<ApiResult<MensajeResponse>> OlvideContrasenaAsync(string correo) =>
        api.PostAsync<MensajeResponse>("api/auth/olvide-contrasena", new OlvideContrasenaRequest { Correo = correo.Trim() });

    public Task<ApiResult> RestablecerContrasenaAsync(string token, string nueva) =>
        api.SendAsync(HttpMethod.Post, "api/auth/restablecer-contrasena", new RestablecerContrasenaRequest { Token = token, ContrasenaNueva = nueva });

    public Task<ApiResult> ConfirmarCorreoAsync(string token) =>
        api.SendAsync(HttpMethod.Post, "api/auth/confirmar-correo", new ConfirmarCorreoRequest { Token = token });

    /// <summary>Vuelve a pedir el perfil (roles y permisos) a la API y actualiza la sesión local.</summary>
    public async Task ActualizarPerfilAsync() => await RecargarPerfilAsync();

    private async Task RecargarPerfilAsync()
    {
        var sesion = await store.GetAsync();
        if (sesion is null) return;
        var perfil = await api.GetAsync<PerfilDto>("api/auth/me");
        if (perfil.Ok && perfil.Data is not null)
        {
            sesion.Perfil = perfil.Data;
            await store.SetAsync(sesion);
        }
    }
}
