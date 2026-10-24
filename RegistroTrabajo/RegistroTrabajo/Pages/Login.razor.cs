using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Pages;

public partial class Login
{
    [Inject] private AuthClient Auth { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthState { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private readonly LoginModel modelo = new();
    private bool cargando;
    private string? error;

    protected override async Task OnInitializedAsync()
    {
        var estado = await AuthState.GetAuthenticationStateAsync();
        if (estado.User.Identity?.IsAuthenticated == true)
            Navigation.NavigateTo("/", replace: true);
    }

    private async Task IniciarSesion()
    {
        error = null;
        cargando = true;
        try
        {
            var result = await Auth.LoginAsync(modelo.Correo, modelo.Contrasena);
            if (!result.Ok || result.Data is null)
            {
                error = result.Error;
                modelo.Contrasena = "";
                return;
            }

            var destino = result.Data.DebeCambiarContrasena ? "cuenta/contrasena" : "/";
            Navigation.NavigateTo(destino, replace: true);
        }
        finally
        {
            cargando = false;
        }
    }

    private class LoginModel
    {
        [Required(ErrorMessage = "Ingresa tu correo")]
        [EmailAddress(ErrorMessage = "Correo inválido")]
        public string Correo { get; set; } = "";

        [Required(ErrorMessage = "Ingresa tu contraseña")]
        public string Contrasena { get; set; } = "";
    }
}
