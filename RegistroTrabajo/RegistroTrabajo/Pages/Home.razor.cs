using Microsoft.AspNetCore.Components;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Pages;

public partial class Home
{
    [Inject] private AuthClient Auth { get; set; } = default!;
    [Inject] private InicioClient Inicio { get; set; } = default!;
    [Inject] private DashboardClient DashboardApi { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private PerfilDto? perfil;
    private ResumenInicio? resumen;
    private DashboardDto? dashboard;
    private string? errorDashboard;
    private bool cargando = true;
    private string? errorResumen;

    protected override async Task OnInitializedAsync()
    {
        perfil = await Auth.PerfilActualAsync();
        if (perfil is null) return;   // la ruta protegida redirige al login

        if (perfil.DebeCambiarContrasena)
        {
            Navigation.NavigateTo("cuenta/contrasena", replace: true);
            return;
        }

        // El dashboard es independiente del resumen administrativo: si uno falla, el otro se muestra igual.
        try
        {
            var d = await DashboardApi.ObtenerAsync();
            if (d.Ok) dashboard = d.Data; else errorDashboard = d.Error;
        }
        catch (Exception)
        {
            errorDashboard = "No se pudo cargar el panel. Inténtalo de nuevo más tarde.";
        }

        try
        {
            resumen = await Inicio.ObtenerResumenAsync(perfil);
        }
        catch (Exception)
        {
            errorResumen = "No se pudo cargar el resumen. Inténtalo de nuevo más tarde.";
        }
        finally
        {
            cargando = false;
        }
    }

    // Los instantes llegan en UTC sin 'Z'; se muestran en la hora local del navegador.
    private static string Local(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime().ToString("dd/MM/yyyy HH:mm");

    private static string TextoEstadoEmpresa(byte e) => e switch
    {
        EstadoEmpresa.Activa => "Activa",
        EstadoEmpresa.Suspendida => "Suspendida",
        _ => "Inactiva"
    };

    private static string TextoResultado(byte r) => r switch
    {
        ResultadoAcceso.Exitoso => "Exitoso",
        ResultadoAcceso.CredencialesInvalidas => "Credenciales inválidas",
        ResultadoAcceso.CuentaBloqueada => "Cuenta bloqueada",
        ResultadoAcceso.CuentaInactiva => "Cuenta inactiva",
        ResultadoAcceso.RefreshInvalido => "Sesión inválida",
        _ => "Desconocido"
    };
}

