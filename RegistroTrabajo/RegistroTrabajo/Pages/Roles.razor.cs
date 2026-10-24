using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Pages;

public partial class Roles
{
    [Inject] private RolesClient Api { get; set; } = default!;
    [CascadingParameter] private Task<AuthenticationState>? Estado { get; set; }

    private ClaimsPrincipal? user;
    private List<RolDto> roles = new();
    private List<PermisoDto> catalogo = new();
    private RolDto? seleccionado;
    private readonly HashSet<string> permisosRol = new(StringComparer.Ordinal);
    private HashSet<string> permisosOriginales = new(StringComparer.Ordinal);
    private bool cargando = true, cargandoPermisos, guardando, hayCambios;
    private string? error, exito;

    private bool formVisible;
    private Guid? editandoId;
    private FormModel form = new();
    private string? errorForm;

    protected override async Task OnInitializedAsync()
    {
        user = Estado is null ? null : (await Estado).User;
        if (!Puede(Permisos.RolesConsultar)) { cargando = false; return; }
        await Cargar();
        var p = await Api.PermisosAsync();
        if (p.Ok) catalogo = p.Data ?? new(); else error = p.Error;
        cargando = false;
    }

    private bool Puede(string permiso) => user?.TienePermiso(permiso) == true;

    private bool EsRolPropio => seleccionado is not null && user!.IsInRole(seleccionado.Nombre);
    private bool EsRolProtegido => seleccionado is { EsSistema: true } s
        && (s.Nombre == NombresRol.Administrador || s.Nombre == NombresRol.AdministradorPlataforma);
    private bool PuedeEditarPermisos => seleccionado is not null && Puede(Permisos.RolesConfigurar) && !EsRolPropio && !EsRolProtegido;

    private string MotivoSoloLectura =>
        EsRolProtegido ? "Los permisos de este rol de sistema no se pueden modificar."
        : EsRolPropio ? "No puedes modificar los permisos de un rol que tú tienes asignado."
        : "Tu cuenta solo puede consultar los permisos.";

    private async Task Cargar()
    {
        var r = await Api.ListarAsync();
        if (r.Ok) roles = r.Data ?? new(); else error = r.Error;
    }

    private async Task Seleccionar(RolDto rol)
    {
        seleccionado = rol;
        error = exito = null;
        cargandoPermisos = true;
        permisosRol.Clear();
        var r = await Api.PermisosDeRolAsync(rol.RolId);
        if (r.Ok) foreach (var c in r.Data!.Permisos) permisosRol.Add(c); else error = r.Error;
        permisosOriginales = permisosRol.ToHashSet(StringComparer.Ordinal);
        hayCambios = false;
        cargandoPermisos = false;
    }

    private void Alternar(string codigo, bool marcado)
    {
        if (marcado) permisosRol.Add(codigo); else permisosRol.Remove(codigo);
        hayCambios = !permisosRol.SetEquals(permisosOriginales);
    }

    private async Task GuardarPermisos()
    {
        if (seleccionado is null) return;
        guardando = true;
        error = exito = null;
        try
        {
            var r = await Api.ConfigurarPermisosAsync(seleccionado.RolId, permisosRol);
            if (!r.Ok) { error = r.Error; return; }
            permisosOriginales = permisosRol.ToHashSet(StringComparer.Ordinal);
            hayCambios = false;
            exito = "Permisos guardados.";
        }
        finally { guardando = false; }
    }

    private void NuevoRol()
    {
        editandoId = null; form = new(); errorForm = null; formVisible = true;
    }

    private void EditarRol()
    {
        if (seleccionado is null) return;
        editandoId = seleccionado.RolId;
        form = new FormModel { Nombre = seleccionado.Nombre, Descripcion = seleccionado.Descripcion };
        errorForm = null;
        formVisible = true;
    }

    private async Task GuardarRol()
    {
        errorForm = null;
        guardando = true;
        try
        {
            ApiResult<RolDto> r = editandoId is { } id
                ? await Api.ActualizarAsync(id, new ActualizarRolRequest { Nombre = form.Nombre, Descripcion = form.Descripcion })
                : await Api.CrearAsync(new CrearRolRequest { Nombre = form.Nombre, Descripcion = form.Descripcion });
            if (!r.Ok) { errorForm = r.Error; return; }

            formVisible = false;
            exito = editandoId is null ? "Rol creado. Ahora puedes asignarle permisos." : "Rol actualizado.";
            await Cargar();
            await Seleccionar(roles.First(x => x.RolId == r.Data!.RolId));
        }
        finally { guardando = false; }
    }

    private async Task AlternarEstado()
    {
        if (seleccionado is null) return;
        var r = await Api.CambiarEstadoAsync(seleccionado.RolId, !seleccionado.Estado);
        if (!r.Ok) { error = r.Error; return; }
        exito = r.Data!.Estado ? "Rol activado." : "Rol desactivado: sus usuarios dejan de recibir sus permisos.";
        await Cargar();
        seleccionado = roles.FirstOrDefault(x => x.RolId == seleccionado.RolId);
    }

    private class FormModel
    {
        [Required(ErrorMessage = "Ingresa el nombre"), StringLength(80, MinimumLength = 2, ErrorMessage = "Entre 2 y 80 caracteres")]
        public string Nombre { get; set; } = "";

        [StringLength(250)]
        public string? Descripcion { get; set; }
    }
}