using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Pages;

public partial class TrabajadorExpediente
{
    [Parameter] public Guid Id { get; set; }
    [Inject] private TrabajadoresClient Api { get; set; } = default!;
    [Inject] private CatalogosClient Catalogos { get; set; } = default!;
    [CascadingParameter] private Task<AuthenticationState>? Estado { get; set; }

    private ClaimsPrincipal? user;
    private TrabajadorDetalleDto? t;
    private List<SupervisorAsignadoDto> supervisores = new();
    private List<ContactoEmergenciaDto> contactos = new();
    private List<HistorialLaboralDto> historial = new();
    private List<CatalogoItemDto> departamentos = new(), puestos = new();
    private List<TrabajadorResumenDto> candidatos = new();
    private bool cargando = true, guardando;
    private string? error, exito, errorForm;

    // Edición
    private bool editarVisible;
    private FormModel form = new();

    // Baja / reingreso
    private bool bajaVisible, reingresoVisible;
    private DateOnly bajaFecha;
    private string? bajaMotivo, reingresoMotivo;

    // Supervisores
    private bool supervisorVisible, supervisorPrincipal;
    private Guid? supervisorId;
    private DateOnly supervisorInicio;

    // Contactos
    private bool contactoVisible;
    private Guid? contactoId;
    private ContactoForm contactoForm = new();

    // Confirmación genérica
    private bool confirmarVisible;
    private string confirmarTexto = "";
    private Func<Task>? confirmarAccion;

    protected override async Task OnParametersSetAsync()
    {
        user = Estado is null ? null : (await Estado).User;
        if (!Puede(Permisos.TrabajadoresConsultar)) { cargando = false; return; }
        await Cargar();
    }

    private bool Puede(string permiso) => user?.TienePermiso(permiso) == true;

    private async Task Cargar()
    {
        cargando = true;
        error = null;
        var r = await Api.ObtenerAsync(Id);
        if (!r.Ok) { error = r.Error; t = null; cargando = false; return; }
        t = r.Data;
        await CargarRelacionadosAsync();
        cargando = false;
    }

    private async Task CargarRelacionadosAsync()
    {
        var s = await Api.SupervisoresAsync(Id);
        if (s.Ok) supervisores = s.Data ?? new(); else error = s.Error;
        if (t is { VerSensibles: true })
        {
            var c = await Api.ContactosAsync(Id);
            if (c.Ok) contactos = c.Data ?? new(); else error = c.Error;
            var h = await Api.HistorialAsync(Id);
            if (h.Ok) historial = h.Data ?? new(); else error = h.Error;
        }
    }

    private void Aplicar(TrabajadorDetalleDto nuevo) => t = nuevo;

    // ---------------- Edición ----------------
    private async Task AbrirEditar()
    {
        if (t is null) return;
        if (departamentos.Count == 0 && Puede(Permisos.DepartamentosConsultar))
        {
            var d = await Catalogos.DepartamentosAsync();
            if (d.Ok) departamentos = d.Data ?? new();
        }
        if (puestos.Count == 0 && Puede(Permisos.PuestosConsultar))
        {
            var p = await Catalogos.PuestosAsync();
            if (p.Ok) puestos = p.Data ?? new();
        }
        form = new FormModel
        {
            Nombres = t.Nombres, Apellidos = t.Apellidos, NumeroEmpleado = t.NumeroEmpleado, FechaIngreso = t.FechaIngreso ?? DateOnly.FromDateTime(DateTime.Today),
            FechaNacimiento = t.FechaNacimiento, CorreoContacto = t.CorreoContacto, Telefono = t.Telefono, Direccion = t.Direccion,
            DepartamentoId = t.DepartamentoId, PuestoId = t.PuestoId, FechaEfectiva = DateOnly.FromDateTime(DateTime.Today)
        };
        errorForm = null;
        editarVisible = true;
    }

    private async Task GuardarEdicion()
    {
        if (t is null) return;
        errorForm = null;
        guardando = true;
        try
        {
            var r = await Api.ActualizarAsync(Id, new ActualizarTrabajadorRequest
            {
                Nombres = form.Nombres, Apellidos = form.Apellidos, NumeroEmpleado = form.NumeroEmpleado, FechaIngreso = form.FechaIngreso,
                FechaNacimiento = form.FechaNacimiento, CorreoContacto = form.CorreoContacto, Telefono = form.Telefono, Direccion = form.Direccion,
                DepartamentoId = form.DepartamentoId, PuestoId = form.PuestoId, UsuarioId = t.UsuarioId,
                FechaEfectiva = form.FechaEfectiva, MotivoCambio = form.MotivoCambio, RowVersion = t.RowVersion
            });
            if (!r.Ok) { errorForm = r.Error; return; }
            Aplicar(r.Data!);
            editarVisible = false;
            exito = "Expediente actualizado.";
            await CargarRelacionadosAsync();
        }
        finally { guardando = false; }
    }

    private async Task AlternarEstadoLaboral()
    {
        if (t is null) return;
        error = exito = null;
        var nuevo = t.EstadoLaboral == EstadoLaboral.Activo ? EstadoLaboral.Inactivo : EstadoLaboral.Activo;
        var r = await Api.CambiarEstadoAsync(Id, new CambiarEstadoLaboralRequest { EstadoLaboral = nuevo });
        if (!r.Ok) { error = r.Error; return; }
        Aplicar(r.Data!);
        exito = "Estado laboral actualizado.";
        await CargarRelacionadosAsync();
    }

    // ---------------- Baja y reingreso ----------------
    private void AbrirBaja()
    {
        bajaFecha = DateOnly.FromDateTime(DateTime.Today); bajaMotivo = null; errorForm = null; bajaVisible = true;
    }

    private async Task ConfirmarBaja()
    {
        errorForm = null;
        guardando = true;
        try
        {
            var r = await Api.BajaAsync(Id, new BajaTrabajadorRequest { FechaBaja = bajaFecha, Motivo = bajaMotivo ?? "" });
            if (!r.Ok) { errorForm = r.Error; return; }
            Aplicar(r.Data!);
            bajaVisible = false;
            exito = "Baja registrada. Su cuenta ya no puede acceder al sistema.";
            await CargarRelacionadosAsync();
        }
        finally { guardando = false; }
    }

    private void AbrirReingreso() { reingresoMotivo = null; errorForm = null; reingresoVisible = true; }

    private async Task ConfirmarReingreso()
    {
        errorForm = null;
        guardando = true;
        try
        {
            var r = await Api.ReingresoAsync(Id, reingresoMotivo);
            if (!r.Ok) { errorForm = r.Error; return; }
            Aplicar(r.Data!);
            reingresoVisible = false;
            exito = "Trabajador reingresado.";
            await CargarRelacionadosAsync();
        }
        finally { guardando = false; }
    }

    // ---------------- Supervisores ----------------
    private async Task AbrirSupervisor()
    {
        supervisorId = null; supervisorPrincipal = supervisores.All(s => s.FechaFin is not null);
        supervisorInicio = DateOnly.FromDateTime(DateTime.Today); errorForm = null;
        var r = await Api.ListarAsync(new TrabajadorFiltro { PageSize = 200, EstadoLaboral = EstadoLaboral.Activo });
        candidatos = r.Ok ? r.Data!.Items.Where(c => c.TrabajadorId != Id).ToList() : new();
        if (!r.Ok) error = r.Error;
        supervisorVisible = true;
    }

    private async Task ConfirmarSupervisor()
    {
        if (supervisorId is not { } sid) return;
        errorForm = null;
        guardando = true;
        try
        {
            var r = await Api.AsignarSupervisorAsync(Id, new AsignarSupervisorRequest
            {
                SupervisorTrabajadorId = sid, FechaInicio = supervisorInicio, EsPrincipal = supervisorPrincipal
            });
            if (!r.Ok) { errorForm = r.Error; return; }
            supervisorVisible = false;
            exito = "Supervisor asignado.";
            await Cargar();
        }
        finally { guardando = false; }
    }

    private void FinalizarSupervision(SupervisorAsignadoDto s)
    {
        confirmarTexto = $"¿Finalizar hoy la supervisión de {s.SupervisorNombre}? La relación queda en el historial.";
        confirmarAccion = async () =>
        {
            var r = await Api.FinalizarSupervisionAsync(Id, s.TrabajadorSupervisorId, null);
            if (!r.Ok) { error = r.Error; return; }
            exito = "Supervisión finalizada.";
            await Cargar();
        };
        confirmarVisible = true;
    }

    // ---------------- Contactos ----------------
    private void NuevoContacto() { contactoId = null; contactoForm = new(); errorForm = null; contactoVisible = true; }

    private void EditarContacto(ContactoEmergenciaDto c)
    {
        contactoId = c.ContactoEmergenciaId;
        contactoForm = new ContactoForm
        {
            NombreCompleto = c.NombreCompleto, Parentesco = c.Parentesco, Telefono = c.Telefono,
            TelefonoAlternativo = c.TelefonoAlternativo, EsPrincipal = c.EsPrincipal
        };
        errorForm = null;
        contactoVisible = true;
    }

    private async Task GuardarContacto()
    {
        errorForm = null;
        guardando = true;
        try
        {
            var req = new GuardarContactoEmergenciaRequest
            {
                NombreCompleto = contactoForm.NombreCompleto, Parentesco = contactoForm.Parentesco, Telefono = contactoForm.Telefono,
                TelefonoAlternativo = contactoForm.TelefonoAlternativo, EsPrincipal = contactoForm.EsPrincipal
            };
            var r = contactoId is { } cid ? await Api.ActualizarContactoAsync(Id, cid, req) : await Api.CrearContactoAsync(Id, req);
            if (!r.Ok) { errorForm = r.Error; return; }
            contactoVisible = false;
            exito = "Contacto guardado.";
            await CargarRelacionadosAsync();
        }
        finally { guardando = false; }
    }

    private void PedirEliminarContacto(ContactoEmergenciaDto c)
    {
        confirmarTexto = $"¿Quitar el contacto de emergencia {c.NombreCompleto}?";
        confirmarAccion = async () =>
        {
            var r = await Api.EliminarContactoAsync(Id, c.ContactoEmergenciaId);
            if (!r.Ok) { error = r.Error; return; }
            exito = "Contacto eliminado.";
            await CargarRelacionadosAsync();
        };
        confirmarVisible = true;
    }

    private async Task EjecutarConfirmacion()
    {
        if (confirmarAccion is null) return;
        error = exito = null;
        guardando = true;
        try { await confirmarAccion(); }
        finally { guardando = false; confirmarVisible = false; confirmarAccion = null; }
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
        public DateOnly? FechaEfectiva { get; set; }
        [StringLength(500)] public string? MotivoCambio { get; set; }
    }

    private class ContactoForm
    {
        [Required(ErrorMessage = "Ingresa el nombre"), StringLength(180, MinimumLength = 2)] public string NombreCompleto { get; set; } = "";
        [StringLength(80)] public string? Parentesco { get; set; }
        [Required(ErrorMessage = "Ingresa el teléfono"), StringLength(30, MinimumLength = 5, ErrorMessage = "Teléfono no válido")] public string Telefono { get; set; } = "";
        [StringLength(30)] public string? TelefonoAlternativo { get; set; }
        public bool EsPrincipal { get; set; }
    }
}
