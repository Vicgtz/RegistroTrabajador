using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Layout;

public partial class MainLayout : IDisposable
{
    [Inject] private AuthClient Auth { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private TemaEmpresaService Tema { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthState { get; set; } = default!;

    protected override void OnInitialized() => Tema.Cambio += Redibujar;

    // Se ejecuta en cada navegación: tras iniciar sesión (el layout se conserva) carga el tema una sola vez.
    protected override async Task OnParametersSetAsync()
    {
        if (Tema.Apariencia is not null) return;
        var estado = await AuthState.GetAuthenticationStateAsync();
        if (estado.User.Identity?.IsAuthenticated == true) await Tema.CargarAsync();
    }

    private void Redibujar() => _ = InvokeAsync(StateHasChanged);

    public void Dispose() => Tema.Cambio -= Redibujar;

    private async Task CerrarSesion()
    {
        await Auth.LogoutAsync();
        Tema.Limpiar();
        await Tema.AplicarAsync(null, null, "Registro de Trabajo");
        Navigation.NavigateTo("login", replace: true);
    }

    private static string Inicial(string? nombre) =>
        string.IsNullOrWhiteSpace(nombre) ? "?" : nombre.Trim()[..1].ToUpperInvariant();
}
