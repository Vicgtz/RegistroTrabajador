using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Pages;

public partial class Trabajadores
{
    [Inject] private TrabajadoresClient Api { get; set; } = default!;
    [Inject] private CatalogosClient Catalogos { get; set; } = default!;
    [Inject] private NavigationManager Navegacion { get; set; } = default!;
    [CascadingParameter] private Task<AuthenticationState>? Estado { get; set; }

    private ClaimsPrincipal? user;
    private PagedResult<TrabajadorResumenDto>? datos;
    private List<CatalogoItemDto> departamentos = new(), puestos = new();
    private readonly TrabajadorFiltro filtro = new() { PageSize = 20 };
    private bool cargando = true, guardando, formVisible;
    private string? error, errorForm;
    private FormModel form = new();

    protected override async Task OnInitializedAsync()
    {
        user = Estado is null ? null : (await Estado).User;
        if (!Puede(Permisos.TrabajadoresConsultar)) { cargando = false; return; }
        if (Puede(Permisos.DepartamentosConsultar))
        {
            var d = await Catalogos.DepartamentosAsync();
            if (d.Ok) departamentos = d.Data ?? new();
        }
        if (Puede(Permisos.PuestosConsultar))
        {
            var p = await Catalogos.PuestosAsync();
            if (p.Ok) puestos = p.Data ?? new();
        }
        await Cargar();
    }

    private bool Puede(string permiso) => user?.TienePermiso(permiso) == true;

    /// <summary>Quien solo tiene el rol Trabajador consulta el directorio, pero no abre expedientes (su información está en «Mi perfil»).</summary>
    private bool PuedeVerExpedientes => user is not null
        && user.Claims.Where(c => c.Type == ClaimTypes.Role).Any(c => c.Value != NombresRol.Trabajador);

    private async Task Cargar()
    {
        cargando = true;
        error = null;
        var r = await Api.ListarAsync(filtro);
        if (r.Ok) datos = r.Data; else error = r.Error;
        cargando = false;
    }

    private async Task Buscar() { filtro.Page = 1; await Cargar(); }
    private async Task IrAPagina(int p) { filtro.Page = p; await Cargar(); }
    private async Task TeclaBuscar(KeyboardEventArgs e) { if (e.Key == "Enter") await Buscar(); }

    private void Nuevo()
    {
        form = new FormModel { FechaIngreso = DateOnly.FromDateTime(DateTime.Today) };
        errorForm = null;
        formVisible = true;
    }

    private async Task Guardar()
    {
        errorForm = null;
        guardando = true;
        try
        {
            var r = await Api.CrearAsync(new CrearTrabajadorRequest
            {
                NumeroEmpleado = form.NumeroEmpleado, Nombres = form.Nombres, Apellidos = form.Apellidos,
                FechaNacimiento = form.FechaNacimiento, CorreoContacto = form.CorreoContacto, Telefono = form.Telefono,
                Direccion = form.Direccion, DepartamentoId = form.DepartamentoId, PuestoId = form.PuestoId,
                FechaIngreso = form.FechaIngreso
            });
            if (!r.Ok) { errorForm = r.Error; return; }
            Navegacion.NavigateTo($"trabajadores/{r.Data!.TrabajadorId}");
        }
        finally { guardando = false; }
    }

    private class FormModel
    {
        [Required(ErrorMessage = "Ingresa los nombres"), StringLength(120)] public string Nombres { get; set; } = "";
        [Required(ErrorMessage = "Ingresa los apellidos"), StringLength(160)] public string Apellidos { get; set; } = "";
        [StringLength(40)] public string? NumeroEmpleado { get; set; }
        public DateOnly FechaIngreso { get; set; }
        public DateOnly? FechaNacimiento { get; set; }
        [EmailAddress(ErrorMessage = "Correo no válido"), StringLength(254)] public string? CorreoContacto { get; set; }
        [StringLength(30)] public string? Telefono { get; set; }
        [StringLength(300)] public string? Direccion { get; set; }
        public Guid? DepartamentoId { get; set; }
        public Guid? PuestoId { get; set; }
    }
}
