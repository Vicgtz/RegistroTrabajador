using Microsoft.AspNetCore.Components;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Layout;

public partial class NavMenu : IDisposable
{
    [Inject] private TemaEmpresaService Tema { get; set; } = default!;

    private bool collapseNavMenu = true;

    private string? NavMenuCssClass => collapseNavMenu ? "collapse" : null;

    private void ToggleNavMenu() => collapseNavMenu = !collapseNavMenu;

    protected override void OnInitialized() => Tema.Cambio += Redibujar;
    private void Redibujar() => _ = InvokeAsync(StateHasChanged);
    public void Dispose() => Tema.Cambio -= Redibujar;
}
