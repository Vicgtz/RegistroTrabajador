using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace RegistroTrabajo.Services;

/// <summary>
/// Estado de autenticación de la interfaz, derivado de la sesión guardada.
/// Solo sirve para mostrar u ocultar elementos: la autorización real la valida la API en cada solicitud.
/// </summary>
public class CustomAuthProvider : AuthenticationStateProvider, IDisposable
{
    public const string ClaimPermiso = "permiso";
    private static readonly AuthenticationState Anonimo = new(new ClaimsPrincipal(new ClaimsIdentity()));

    private readonly SesionStore _store;

    public CustomAuthProvider(SesionStore store)
    {
        _store = store;
        _store.Cambio += Notificar;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var sesion = await _store.GetAsync();
        if (sesion?.Perfil is not { } perfil || string.IsNullOrEmpty(sesion.RefreshToken)) return Anonimo;

        // La sesión del servidor ya no es renovable.
        if (sesion.RefreshExpiraUtc <= DateTime.UtcNow)
        {
            await _store.ClearAsync();
            return Anonimo;
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, $"{perfil.Nombre} {perfil.Apellido}".Trim()),
            new(ClaimTypes.Email, perfil.Correo),
            new("sub", perfil.UsuarioId.ToString()),
            new("empresa", perfil.EmpresaNombre)
        };
        claims.AddRange(perfil.Roles.Select(r => new Claim(ClaimTypes.Role, r)));
        claims.AddRange(perfil.Permisos.Select(p => new Claim(ClaimPermiso, p)));
        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt", ClaimTypes.Name, ClaimTypes.Role)));
    }

    private void Notificar() => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

    public void Dispose() => _store.Cambio -= Notificar;
}

public static class ClaimsPrincipalExtensions
{
    public static bool TienePermiso(this ClaimsPrincipal user, string codigo) =>
        user.HasClaim(CustomAuthProvider.ClaimPermiso, codigo);
}
