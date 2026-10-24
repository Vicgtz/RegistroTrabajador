using Microsoft.AspNetCore.Components;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Layout;

public partial class MainLayout
{
    [Inject] private AuthClient Auth { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private async Task CerrarSesion()
    {
        await Auth.LogoutAsync();
        Navigation.NavigateTo("login", replace: true);
    }

    private static string Inicial(string? nombre) =>
        string.IsNullOrWhiteSpace(nombre) ? "?" : nombre.Trim()[..1].ToUpperInvariant();
}
