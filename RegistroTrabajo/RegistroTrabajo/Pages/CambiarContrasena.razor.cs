using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Pages;

public partial class CambiarContrasena
{
    [Inject] private AuthClient Auth { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private readonly CambioModel modelo = new();
    private bool guardando;
    private bool obligatorio;
    private string? error;
    private string? exito;

    protected override async Task OnInitializedAsync() =>
        obligatorio = (await Auth.PerfilActualAsync())?.DebeCambiarContrasena == true;

    private async Task Guardar()
    {
        error = exito = null;
        guardando = true;
        try
        {
            var r = await Auth.CambiarContrasenaAsync(modelo.Actual, modelo.Nueva);
            if (!r.Ok)
            {
                error = r.Error;
                return;
            }

            modelo.Actual = modelo.Nueva = modelo.Confirmar = "";
            if (obligatorio)
            {
                Navigation.NavigateTo("/", replace: true);
                return;
            }
            exito = "Tu contraseña se actualizó. Se cerraron tus otras sesiones abiertas.";
        }
        finally
        {
            guardando = false;
        }
    }

    private class CambioModel
    {
        [Required(ErrorMessage = "Ingresa tu contraseña actual")]
        public string Actual { get; set; } = "";

        [Required(ErrorMessage = "Ingresa la contraseña nueva")]
        [StringLength(128, MinimumLength = 10, ErrorMessage = "Debe tener entre 10 y 128 caracteres")]
        public string Nueva { get; set; } = "";

        [Required(ErrorMessage = "Confirma la contraseña nueva")]
        [Compare(nameof(Nueva), ErrorMessage = "Las contraseñas no coinciden")]
        public string Confirmar { get; set; } = "";
    }
}
