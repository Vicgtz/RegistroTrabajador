using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Pages;

public partial class Catalogos
{
    [Inject] private CatalogosClient Api { get; set; } = default!;
    [CascadingParameter] private Task<AuthenticationState>? Estado { get; set; }

    private ClaimsPrincipal? user;
    private List<CatalogoItemDto> departamentos = new(), puestos = new();
    private bool cargando = true, guardando, formVisible, formEsDepto;
    private Guid? editandoId;
    private string? error, exito, errorForm;
    private FormModel form = new();

    private string TituloForm =>
        (editandoId is null ? "Nuevo " : "Editar ") + (formEsDepto ? "departamento" : "puesto");

    protected override async Task OnInitializedAsync()
    {
        user = Estado is null ? null : (await Estado).User;
        await Cargar();
        cargando = false;
    }

    private bool Puede(string permiso) => user?.TienePermiso(permiso) == true;

    private async Task Cargar()
    {
        if (Puede(Permisos.DepartamentosConsultar))
        {
            var d = await Api.DepartamentosAsync();
            if (d.Ok) departamentos = d.Data ?? new(); else error = d.Error;
        }
        if (Puede(Permisos.PuestosConsultar))
        {
            var p = await Api.PuestosAsync();
            if (p.Ok) puestos = p.Data ?? new(); else error = p.Error;
        }
    }

    private void Nuevo(bool esDepto)
    {
        formEsDepto = esDepto; editandoId = null; form = new(); errorForm = null; formVisible = true;
    }

    private void Editar(CatalogoItemDto i, bool esDepto)
    {
        formEsDepto = esDepto; editandoId = i.Id;
        form = new FormModel { Nombre = i.Nombre, Descripcion = i.Descripcion };
        errorForm = null; formVisible = true;
    }

    private async Task Guardar()
    {
        errorForm = null;
        guardando = true;
        try
        {
            var req = new GuardarCatalogoRequest { Nombre = form.Nombre, Descripcion = form.Descripcion };
            ApiResult<CatalogoItemDto> r = (formEsDepto, editandoId) switch
            {
                (true, { } id) => await Api.ActualizarDepartamentoAsync(id, req),
                (true, null) => await Api.CrearDepartamentoAsync(req),
                (false, { } id) => await Api.ActualizarPuestoAsync(id, req),
                _ => await Api.CrearPuestoAsync(req)
            };
            if (!r.Ok) { errorForm = r.Error; return; }
            formVisible = false;
            exito = "Cambios guardados.";
            await Cargar();
        }
        finally { guardando = false; }
    }

    private async Task AlternarEstado(CatalogoItemDto i, bool esDepto)
    {
        error = exito = null;
        var r = esDepto ? await Api.EstadoDepartamentoAsync(i.Id, !i.Estado) : await Api.EstadoPuestoAsync(i.Id, !i.Estado);
        if (!r.Ok) { error = r.Error; return; }
        exito = r.Data!.Estado ? "Activado." : "Desactivado: los trabajadores actuales conservan su asignación, pero no se podrá asignar a nuevos.";
        await Cargar();
    }

    private class FormModel
    {
        [Required(ErrorMessage = "Ingresa el nombre"), StringLength(120, MinimumLength = 2, ErrorMessage = "Entre 2 y 120 caracteres")]
        public string Nombre { get; set; } = "";

        [StringLength(300)]
        public string? Descripcion { get; set; }
    }
}
