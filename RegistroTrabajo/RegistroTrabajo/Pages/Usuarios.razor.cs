using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Pages;

public partial class Usuarios
{
    [Inject] private UsuariosClient Api { get; set; } = default!;
    [Inject] private RolesClient RolesApi { get; set; } = default!;
    [CascadingParameter] private Task<AuthenticationState>? Estado { get; set; }

    private ClaimsPrincipal? user;
    private Guid? yoId;
    private readonly UsuarioFiltro filtro = new() { PageSize = 15 };
    private PagedResult<UsuarioDto>? datos;
    private bool cargando;
    private string? error, exito;
    private Guid? MenuAbierto;

    // crear / editar
    private bool formVisible, guardando;
    private Guid? editandoId;
    private Guid? editandoTrabajadorId;
    private FormModel form = new();
    private string? errorForm;
    private List<RolDto> rolesDisponibles = new();
    private readonly HashSet<Guid> rolesSeleccionados = new();

    // roles de un usuario
    private bool rolesVisible, cargandoRoles;
    private UsuarioDto? usuarioRoles;
    private readonly HashSet<Guid> rolesDelUsuario = new();
    private string? errorRoles;

    // restablecer contraseña
    private bool resetVisible;
    private UsuarioDto? usuarioReset;
    private string resetContrasena = "";
    private bool verReset;
    private string? errorReset;

    // confirmación
    private bool confirmVisible;
    private string confirmTitulo = "", confirmMensaje = "", confirmExito = "";
    private Func<Task<ApiResult>>? confirmAccion;

    protected override async Task OnInitializedAsync()
    {
        user = Estado is null ? null : (await Estado).User;
        yoId = Guid.TryParse(user?.FindFirst("sub")?.Value, out var id) ? id : null;
        if (Puede(Permisos.UsuariosConsultar)) await Cargar();
    }

    private bool Puede(string permiso) => user?.TienePermiso(permiso) == true;

    private async Task Cargar()
    {
        cargando = true;
        error = null;
        MenuAbierto = null;
        var r = await Api.ListarAsync(filtro);
        if (r.Ok) datos = r.Data; else error = r.Error;
        cargando = false;
    }

    private async Task Buscar() { filtro.Page = 1; await Cargar(); }
    private async Task TeclaBuscar(KeyboardEventArgs e) { if (e.Key == "Enter") await Buscar(); }
    private async Task IrAPagina(int p) { filtro.Page = p; await Cargar(); }

    // ---------------- crear / editar ----------------
    private async Task CargarRolesDisponibles()
    {
        if (rolesDisponibles.Count > 0 || !Puede(Permisos.RolesConsultar)) return;
        var r = await RolesApi.ListarAsync();
        if (r.Ok) rolesDisponibles = r.Data ?? new();
    }

    private async Task NuevoUsuario()
    {
        editandoId = null; editandoTrabajadorId = null;
        form = new FormModel { DebeCambiarContrasena = true };
        rolesSeleccionados.Clear();
        errorForm = null;
        await CargarRolesDisponibles();
        formVisible = true;
    }

    private void Editar(UsuarioDto u)
    {
        MenuAbierto = null;
        editandoId = u.UsuarioId;
        editandoTrabajadorId = u.TrabajadorId;
        form = new FormModel { Nombre = u.Nombre, Apellido = u.Apellido, Correo = u.Correo, DebeCambiarContrasena = u.DebeCambiarContrasena };
        errorForm = null;
        formVisible = true;
    }

    private void AlternarRolNuevo(Guid rolId, bool marcado)
    {
        if (marcado) rolesSeleccionados.Add(rolId); else rolesSeleccionados.Remove(rolId);
    }

    private async Task Guardar()
    {
        errorForm = null;
        guardando = true;
        try
        {
            ApiResult resultado;
            if (editandoId is { } id)
            {
                var r = await Api.ActualizarAsync(id, new ActualizarUsuarioRequest
                {
                    Correo = form.Correo, Nombre = form.Nombre, Apellido = form.Apellido,
                    DebeCambiarContrasena = form.DebeCambiarContrasena, TrabajadorId = editandoTrabajadorId
                });
                resultado = r.Sin();
            }
            else
            {
                if (string.IsNullOrWhiteSpace(form.Contrasena))
                {
                    errorForm = "Escribe una contraseña inicial.";
                    return;
                }
                var r = await Api.CrearAsync(new CrearUsuarioRequest
                {
                    Correo = form.Correo, Nombre = form.Nombre, Apellido = form.Apellido, Contrasena = form.Contrasena,
                    DebeCambiarContrasena = form.DebeCambiarContrasena, RolIds = rolesSeleccionados.ToList()
                });
                resultado = r.Sin();
            }

            if (!resultado.Ok) { errorForm = resultado.Error; return; }
            formVisible = false;
            exito = editandoId is null ? "Usuario creado. Se envió un enlace para confirmar su correo." : "Usuario actualizado.";
            await Cargar();
        }
        finally { guardando = false; }
    }

    // ---------------- roles ----------------
    private async Task AbrirRoles(UsuarioDto u)
    {
        MenuAbierto = null;
        usuarioRoles = u;
        errorRoles = null;
        rolesDelUsuario.Clear();
        rolesVisible = true;
        cargandoRoles = true;
        rolesDisponibles = new();
        await CargarRolesDisponibles();
        var r = await Api.RolesAsync(u.UsuarioId);
        if (r.Ok) foreach (var rol in r.Data ?? new()) rolesDelUsuario.Add(rol.RolId); else errorRoles = r.Error;
        cargandoRoles = false;
    }

    private async Task CambiarRol(RolDto rol, bool asignar)
    {
        if (usuarioRoles is null) return;
        errorRoles = null;
        var r = asignar ? await Api.AsignarRolAsync(usuarioRoles.UsuarioId, rol.RolId) : await Api.RetirarRolAsync(usuarioRoles.UsuarioId, rol.RolId);
        if (!r.Ok) { errorRoles = r.Error; return; }
        if (asignar) rolesDelUsuario.Add(rol.RolId); else rolesDelUsuario.Remove(rol.RolId);
        exito = null;
        await Cargar();   // refresca las etiquetas de roles de la tabla
    }

    // ---------------- restablecer contraseña ----------------
    private void AbrirReset(UsuarioDto u)
    {
        MenuAbierto = null;
        usuarioReset = u;
        resetContrasena = "";
        errorReset = null;
        resetVisible = true;
    }

    private async Task Restablecer()
    {
        if (usuarioReset is null) return;
        errorReset = null;
        guardando = true;
        try
        {
            var r = await Api.RestablecerContrasenaAsync(usuarioReset.UsuarioId, resetContrasena);
            if (!r.Ok) { errorReset = r.Error; return; }
            resetVisible = false;
            exito = $"Contraseña de {usuarioReset.Nombre} restablecida. Deberá cambiarla al iniciar sesión.";
            resetContrasena = "";
            await Cargar();
        }
        finally { guardando = false; }
    }

    // ---------------- confirmaciones ----------------
    private void PedirDesbloquear(UsuarioDto u) =>
        Confirmar($"Desbloquear a {u.Nombre} {u.Apellido}", "Se quitará el bloqueo temporal de la cuenta.", () => Api.DesbloquearAsync(u.UsuarioId), "Cuenta desbloqueada.");

    private void PedirCerrarSesiones(UsuarioDto u) =>
        Confirmar($"Cerrar sesiones de {u.Nombre} {u.Apellido}", "Se cerrarán todas sus sesiones abiertas.", () => Api.RevocarSesionesAsync(u.UsuarioId), "Sesiones cerradas.");

    private void PedirDesactivar(UsuarioDto u) =>
        Confirmar($"Desactivar a {u.Nombre} {u.Apellido}", "No podrá iniciar sesión y se cerrarán sus sesiones.", () => Api.DesactivarAsync(u.UsuarioId), "Usuario desactivado.");

    private void PedirActivar(UsuarioDto u) =>
        Confirmar($"Activar a {u.Nombre} {u.Apellido}", "Podrá volver a iniciar sesión.", () => Api.ActivarAsync(u.UsuarioId), "Usuario activado.");

    private void Confirmar(string titulo, string mensaje, Func<Task<ApiResult>> accion, string textoExito)
    {
        MenuAbierto = null;
        confirmTitulo = titulo; confirmMensaje = mensaje; confirmAccion = accion; confirmExito = textoExito;
        confirmVisible = true;
    }

    private async Task EjecutarConfirmacion()
    {
        if (confirmAccion is null) return;
        guardando = true;
        try
        {
            var r = await confirmAccion();
            confirmVisible = false;
            if (r.Ok) { exito = confirmExito; error = null; } else { error = r.Error; exito = null; }
            await Cargar();
        }
        finally { guardando = false; }
    }

    private class FormModel
    {
        [Required(ErrorMessage = "Ingresa el nombre"), StringLength(100)]
        public string Nombre { get; set; } = "";

        [Required(ErrorMessage = "Ingresa el apellido"), StringLength(150)]
        public string Apellido { get; set; } = "";

        [Required(ErrorMessage = "Ingresa el correo"), EmailAddress(ErrorMessage = "Correo inválido"), StringLength(254)]
        public string Correo { get; set; } = "";

        [StringLength(128, MinimumLength = 10, ErrorMessage = "Debe tener entre 10 y 128 caracteres")]
        public string? Contrasena { get; set; }

        public bool DebeCambiarContrasena { get; set; }
    }
}
