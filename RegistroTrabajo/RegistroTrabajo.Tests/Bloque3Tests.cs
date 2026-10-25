using Microsoft.EntityFrameworkCore;
using RegistroTrabajo.Data.Entities;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Tests;

public class Bloque3Tests : IDisposable
{
    private readonly TestEnv _t = new();
    public void Dispose() => _t.Dispose();

    private static readonly DateOnly Hoy = new(2026, 1, 1);   // fecha del FakeClock

    private static async Task<ServiceException> Rechaza(Func<Task> accion) =>
        await Assert.ThrowsAnyAsync<ServiceException>(accion);

    private ITrabajadorService Trab => _t.Svc<ITrabajadorService>();
    private ISupervisionService Sup => _t.Svc<ISupervisionService>();

    private static CrearTrabajadorRequest NuevoTrabajador(string nombres, string apellidos, string? numero = null,
        Guid? depto = null, Guid? puesto = null, Guid? usuario = null) => new()
    {
        Nombres = nombres, Apellidos = apellidos, NumeroEmpleado = numero, DepartamentoId = depto, PuestoId = puesto,
        FechaIngreso = new DateOnly(2025, 6, 1), UsuarioId = usuario
    };

    private async Task<(EmpresaInfo E, Guid Depto, Guid Puesto)> EmpresaConCatalogosAsync(string nombre, string correo)
    {
        var e = await _t.CrearEmpresaAsync(nombre, correo);
        _t.Current.As(e, e.AdminId);
        var d = await _t.Svc<IDepartamentoService>().CrearAsync(new GuardarCatalogoRequest { Nombre = "Operaciones" }, default);
        var p = await _t.Svc<IPuestoService>().CrearAsync(new GuardarCatalogoRequest { Nombre = "Analista" }, default);
        return (e, d.Id, p.Id);
    }

    // 1. Catálogos ---------------------------------------------------------------
    [Fact]
    public async Task Departamentos_y_puestos_se_crean_sin_duplicados_y_se_desactivan_sin_borrarse()
    {
        var (_, depto, puesto) = await EmpresaConCatalogosAsync("A", "admin@a.com");
        var ex = await Rechaza(() => _t.Svc<IDepartamentoService>().CrearAsync(new GuardarCatalogoRequest { Nombre = " operaciones " }, default));
        Assert.Equal(ServiceErrorKind.Conflict, ex.Kind);
        ex = await Rechaza(() => _t.Svc<IPuestoService>().CrearAsync(new GuardarCatalogoRequest { Nombre = "ANALISTA" }, default));
        Assert.Equal(ServiceErrorKind.Conflict, ex.Kind);

        var trabajador = await Trab.CrearAsync(NuevoTrabajador("Ana", "Ruiz", depto: depto, puesto: puesto), default);
        await _t.Svc<IDepartamentoService>().CambiarEstadoAsync(depto, false, default);

        Assert.Empty(await _t.Svc<IDepartamentoService>().ListarAsync(true, default));
        var todos = await _t.Svc<IDepartamentoService>().ListarAsync(false, default);
        Assert.False(Assert.Single(todos).Estado);
        // La referencia histórica se conserva y el departamento inactivo no se asigna a nadie nuevo.
        Assert.Equal("Operaciones", (await Trab.ObtenerAsync(trabajador.TrabajadorId, default)).Departamento);
        ex = await Rechaza(() => Trab.CrearAsync(NuevoTrabajador("Beto", "Soto", depto: depto), default));
        Assert.Equal(ServiceErrorKind.Validation, ex.Kind);
    }

    [Fact]
    public async Task Catalogos_de_otra_empresa_no_se_ven_ni_se_modifican_ni_se_asignan()
    {
        var a = await EmpresaConCatalogosAsync("A", "admin@a.com");
        var b = await EmpresaConCatalogosAsync("B", "admin@b.com");   // el contexto queda como admin de B
        Assert.Single(await _t.Svc<IDepartamentoService>().ListarAsync(false, default));

        var ex = await Rechaza(() => _t.Svc<IDepartamentoService>().ActualizarAsync(a.Depto, new GuardarCatalogoRequest { Nombre = "X" }, default));
        Assert.Equal(ServiceErrorKind.NotFound, ex.Kind);
        ex = await Rechaza(() => _t.Svc<IPuestoService>().CambiarEstadoAsync(a.Puesto, false, default));
        Assert.Equal(ServiceErrorKind.NotFound, ex.Kind);
        ex = await Rechaza(() => Trab.CrearAsync(NuevoTrabajador("Ana", "Ruiz", depto: a.Depto), default));
        Assert.Equal(ServiceErrorKind.Validation, ex.Kind);
        ex = await Rechaza(() => Trab.CrearAsync(NuevoTrabajador("Ana", "Ruiz", puesto: a.Puesto), default));
        Assert.Equal(ServiceErrorKind.Validation, ex.Kind);
        Assert.NotEqual(a.E.EmpresaId, b.E.EmpresaId);
    }

    // 2. Trabajadores ---------------------------------------------------------------
    [Fact]
    public async Task Trabajador_se_crea_edita_y_evita_duplicados_en_la_empresa()
    {
        var (_, depto, puesto) = await EmpresaConCatalogosAsync("A", "admin@a.com");
        var t = await Trab.CrearAsync(NuevoTrabajador("Ana", "Ruiz", "E-1", depto, puesto), default);
        Assert.Equal(EstadoLaboral.Activo, t.EstadoLaboral);
        Assert.Contains(await Trab.HistorialAsync(t.TrabajadorId, default), h => h.TipoCambio == TipoCambioLaboral.Ingreso);

        var ex = await Rechaza(() => Trab.CrearAsync(NuevoTrabajador("Otra", "Persona", "E-1"), default));
        Assert.Equal(ServiceErrorKind.Conflict, ex.Kind);
        var conCorreo = NuevoTrabajador("Luis", "Mora"); conCorreo.CorreoContacto = "Luis@x.com";
        await Trab.CrearAsync(conCorreo, default);
        var dup = NuevoTrabajador("Otro", "Mora"); dup.CorreoContacto = "luis@X.com";
        Assert.Equal(ServiceErrorKind.Conflict, (await Rechaza(() => Trab.CrearAsync(dup, default))).Kind);

        var edit = new ActualizarTrabajadorRequest
        {
            Nombres = "Ana María", Apellidos = "Ruiz", NumeroEmpleado = "E-1", DepartamentoId = depto, PuestoId = puesto,
            FechaIngreso = t.FechaIngreso!.Value, RowVersion = t.RowVersion, Telefono = "5551234567"
        };
        var u = await Trab.ActualizarAsync(t.TrabajadorId, edit, default);
        Assert.Equal("Ana María", u.Nombres);
        Assert.Equal("5551234567", u.Telefono);
    }

    [Fact]
    public async Task Editar_con_version_obsoleta_da_conflicto()
    {
        var (_, depto, puesto) = await EmpresaConCatalogosAsync("A", "admin@a.com");
        var t = await Trab.CrearAsync(NuevoTrabajador("Ana", "Ruiz", depto: depto, puesto: puesto), default);
        var ex = await Rechaza(() => Trab.ActualizarAsync(t.TrabajadorId, new ActualizarTrabajadorRequest
        {
            Nombres = "Ana", Apellidos = "Ruiz", DepartamentoId = depto, PuestoId = puesto, FechaIngreso = t.FechaIngreso!.Value,
            RowVersion = Convert.ToBase64String(new byte[] { 9, 9, 9, 9, 9, 9, 9, 9 })
        }, default));
        Assert.Equal(ServiceErrorKind.Conflict, ex.Kind);
    }

    [Fact]
    public async Task Cambios_de_puesto_y_departamento_quedan_en_el_historial_con_antes_y_despues()
    {
        var (_, depto, puesto) = await EmpresaConCatalogosAsync("A", "admin@a.com");
        var depto2 = (await _t.Svc<IDepartamentoService>().CrearAsync(new GuardarCatalogoRequest { Nombre = "Finanzas" }, default)).Id;
        var puesto2 = (await _t.Svc<IPuestoService>().CrearAsync(new GuardarCatalogoRequest { Nombre = "Gerente" }, default)).Id;
        var t = await Trab.CrearAsync(NuevoTrabajador("Ana", "Ruiz", depto: depto, puesto: puesto), default);

        _t.Clock.Advance(TimeSpan.FromDays(30));
        await Trab.ActualizarAsync(t.TrabajadorId, new ActualizarTrabajadorRequest
        {
            Nombres = "Ana", Apellidos = "Ruiz", DepartamentoId = depto2, PuestoId = puesto2, FechaIngreso = t.FechaIngreso!.Value,
            RowVersion = t.RowVersion, MotivoCambio = "Promoción"
        }, default);

        var h = await Trab.HistorialAsync(t.TrabajadorId, default);
        var cambioPuesto = Assert.Single(h, x => x.TipoCambio == TipoCambioLaboral.Puesto);
        Assert.Equal(new DateOnly(2026, 1, 31), cambioPuesto.FechaEfectiva);
        Assert.Contains("Analista", cambioPuesto.DatosAntesJson);
        Assert.Contains("Gerente", cambioPuesto.DatosDespuesJson);
        Assert.Equal("Promoción", cambioPuesto.Motivo);
        Assert.Single(h, x => x.TipoCambio == TipoCambioLaboral.Departamento);
        Assert.Contains(h, x => x.TipoCambio == TipoCambioLaboral.Ingreso);   // el estado anterior no se pierde
    }

    [Fact]
    public async Task Listado_filtra_por_departamento_puesto_estado_y_busqueda_y_muestra_supervisores()
    {
        var (_, depto, puesto) = await EmpresaConCatalogosAsync("A", "admin@a.com");
        var ana = await Trab.CrearAsync(NuevoTrabajador("Ana", "Ruiz", "E-1", depto, puesto), default);
        var beto = await Trab.CrearAsync(NuevoTrabajador("Beto", "Soto", "E-2"), default);
        await Sup.AsignarAsync(beto.TrabajadorId, new AsignarSupervisorRequest { SupervisorTrabajadorId = ana.TrabajadorId, EsPrincipal = true }, default);

        Assert.Equal(1, (await Trab.ListarAsync(new TrabajadorFiltro { DepartamentoId = depto }, default)).Total);
        Assert.Equal(1, (await Trab.ListarAsync(new TrabajadorFiltro { PuestoId = puesto }, default)).Total);
        Assert.Equal(2, (await Trab.ListarAsync(new TrabajadorFiltro { EstadoLaboral = EstadoLaboral.Activo }, default)).Total);
        var b = await Trab.ListarAsync(new TrabajadorFiltro { Buscar = "soto" }, default);
        Assert.Equal("Ana Ruiz", Assert.Single(Assert.Single(b.Items).Supervisores));
        Assert.Single((await Trab.ListarAsync(new TrabajadorFiltro { Buscar = "E-2" }, default)).Items);
    }

    [Fact]
    public async Task Editar_el_expediente_exige_permiso_sobre_datos_personales()
    {
        var (e, _, _) = await EmpresaConCatalogosAsync("A", "admin@a.com");
        var t = await Trab.CrearAsync(NuevoTrabajador("Ana", "Ruiz"), default);
        var cuenta = await _t.NuevoUsuarioAsync(e.EmpresaId, "sup@a.com", "Sup", "Uno", e.Roles[NombresRol.Supervisor]);
        _t.Current.As(e, cuenta);
        var ex = await Rechaza(() => Trab.ActualizarAsync(t.TrabajadorId, new ActualizarTrabajadorRequest
            { Nombres = "X", Apellidos = "Y", FechaIngreso = t.FechaIngreso!.Value, RowVersion = t.RowVersion }, default));
        Assert.Equal(ServiceErrorKind.Forbidden, ex.Kind);
    }

    // 3. Aislamiento entre empresas -----------------------------------------------
    [Fact]
    public async Task Ninguna_operacion_alcanza_trabajadores_de_otra_empresa()
    {
        var a = await EmpresaConCatalogosAsync("A", "admin@a.com");
        var ta = await Trab.CrearAsync(NuevoTrabajador("Ana", "Ruiz"), default);
        var ta2 = await Trab.CrearAsync(NuevoTrabajador("Luis", "Mora"), default);
        var contacto = await _t.Svc<IContactoEmergenciaService>().CrearAsync(ta.TrabajadorId,
            new GuardarContactoEmergenciaRequest { NombreCompleto = "Madre Ruiz", Telefono = "5550001111" }, default);

        var b = await EmpresaConCatalogosAsync("B", "admin@b.com");
        var tb = await Trab.CrearAsync(NuevoTrabajador("Beto", "Soto"), default);   // trabajador de B

        Assert.Single((await Trab.ListarAsync(new TrabajadorFiltro(), default)).Items);
        async Task NoEncuentra(Func<Task> accion) => Assert.Equal(ServiceErrorKind.NotFound, (await Rechaza(accion)).Kind);

        await NoEncuentra(() => Trab.ObtenerAsync(ta.TrabajadorId, default));
        await NoEncuentra(() => Trab.HistorialAsync(ta.TrabajadorId, default));
        await NoEncuentra(() => Trab.DarDeBajaAsync(ta.TrabajadorId, new BajaTrabajadorRequest { FechaBaja = Hoy, Motivo = "Renuncia" }, default));
        await NoEncuentra(() => Trab.ActualizarAsync(ta.TrabajadorId, new ActualizarTrabajadorRequest
            { Nombres = "X", Apellidos = "Y", FechaIngreso = Hoy, RowVersion = ta.RowVersion }, default));
        await NoEncuentra(() => Sup.ListarAsync(ta.TrabajadorId, default));
        await NoEncuentra(() => _t.Svc<IContactoEmergenciaService>().ListarAsync(ta.TrabajadorId, default));
        await NoEncuentra(() => _t.Svc<IContactoEmergenciaService>().EliminarAsync(ta.TrabajadorId, contacto.ContactoEmergenciaId, default));
        // Supervisor de otra empresa: el trabajador propio existe, el supervisor no.
        Assert.Equal(ServiceErrorKind.Validation, (await Rechaza(() => Sup.AsignarAsync(tb.TrabajadorId,
            new AsignarSupervisorRequest { SupervisorTrabajadorId = ta2.TrabajadorId }, default))).Kind);
        // Vincular la cuenta de otra empresa tampoco es posible.
        Assert.Equal(ServiceErrorKind.Validation, (await Rechaza(() => Trab.CrearAsync(
            NuevoTrabajador("Carla", "Paz", usuario: a.E.AdminId), default))).Kind);
        Assert.NotEqual(a.E.EmpresaId, b.E.EmpresaId);
    }

    // 4. Supervisores -----------------------------------------------------------------
    [Fact]
    public async Task Un_trabajador_puede_tener_varios_supervisores_y_un_supervisor_varios_trabajadores()
    {
        await EmpresaConCatalogosAsync("A", "admin@a.com");
        var ana = (await Trab.CrearAsync(NuevoTrabajador("Ana", "Ruiz"), default)).TrabajadorId;
        var luis = (await Trab.CrearAsync(NuevoTrabajador("Luis", "Mora"), default)).TrabajadorId;
        var beto = (await Trab.CrearAsync(NuevoTrabajador("Beto", "Soto"), default)).TrabajadorId;
        var cris = (await Trab.CrearAsync(NuevoTrabajador("Cris", "Paz"), default)).TrabajadorId;

        await Sup.AsignarAsync(cris, new AsignarSupervisorRequest { SupervisorTrabajadorId = ana, EsPrincipal = true }, default);
        await Sup.AsignarAsync(cris, new AsignarSupervisorRequest { SupervisorTrabajadorId = luis, EsPrincipal = true }, default);
        await Sup.AsignarAsync(beto, new AsignarSupervisorRequest { SupervisorTrabajadorId = ana }, default);

        var deCris = await Sup.ListarAsync(cris, default);
        Assert.Equal(2, deCris.Count);
        Assert.Single(deCris, s => s.EsPrincipal);                          // solo el último es principal
        Assert.Equal(2, (await Sup.SupervisadosAsync(ana, default)).Count);  // Ana supervisa a Cris y a Beto
    }

    [Fact]
    public async Task Supervision_rechaza_autosupervision_duplicados_y_ciclos_y_conserva_el_historial_al_finalizar()
    {
        await EmpresaConCatalogosAsync("A", "admin@a.com");
        var ana = (await Trab.CrearAsync(NuevoTrabajador("Ana", "Ruiz"), default)).TrabajadorId;
        var luis = (await Trab.CrearAsync(NuevoTrabajador("Luis", "Mora"), default)).TrabajadorId;

        Assert.Equal(ServiceErrorKind.Validation, (await Rechaza(() => Sup.AsignarAsync(ana, new AsignarSupervisorRequest { SupervisorTrabajadorId = ana }, default))).Kind);
        var rel = await Sup.AsignarAsync(luis, new AsignarSupervisorRequest { SupervisorTrabajadorId = ana }, default);
        Assert.Equal(ServiceErrorKind.Conflict, (await Rechaza(() => Sup.AsignarAsync(luis, new AsignarSupervisorRequest { SupervisorTrabajadorId = ana }, default))).Kind);
        Assert.Equal(ServiceErrorKind.Conflict, (await Rechaza(() => Sup.AsignarAsync(ana, new AsignarSupervisorRequest { SupervisorTrabajadorId = luis }, default))).Kind);

        _t.Clock.Advance(TimeSpan.FromDays(10));
        var fin = await Sup.FinalizarAsync(luis, rel.TrabajadorSupervisorId, new FinalizarSupervisionRequest(), default);
        Assert.Equal(new DateOnly(2026, 1, 11), fin.FechaFin);
        Assert.Equal(ServiceErrorKind.Conflict, (await Rechaza(() => Sup.FinalizarAsync(luis, rel.TrabajadorSupervisorId, new FinalizarSupervisionRequest(), default))).Kind);

        // FechaFin es inclusiva: al día siguiente la relación ya no está vigente, pero sigue visible; el cambio quedó en el historial.
        _t.Clock.Advance(TimeSpan.FromDays(1));
        Assert.Single(await Sup.ListarAsync(luis, default));
        Assert.Empty((await Trab.ListarAsync(new TrabajadorFiltro { Buscar = "Mora" }, default)).Items.Single().Supervisores);
        Assert.Equal(2, (await Trab.HistorialAsync(luis, default)).Count(h => h.TipoCambio == TipoCambioLaboral.Supervisor));
        // Con el supervisor desvinculado, ahora sí puede invertirse la relación.
        await Sup.AsignarAsync(ana, new AsignarSupervisorRequest { SupervisorTrabajadorId = luis }, default);
    }

    // 5. Baja ----------------------------------------------------------------------------
    [Fact]
    public async Task Baja_bloquea_la_cuenta_revoca_sesiones_y_conserva_el_historial()
    {
        var (e, depto, puesto) = await EmpresaConCatalogosAsync("A", "admin@a.com");
        var cuenta = await _t.NuevoUsuarioAsync(e.EmpresaId, "ana@a.com", "Ana", "Ruiz", e.Roles[NombresRol.Trabajador]);
        var ana = await Trab.CrearAsync(NuevoTrabajador("Ana", "Ruiz", depto: depto, puesto: puesto, usuario: cuenta), default);
        var jefe = (await Trab.CrearAsync(NuevoTrabajador("Jefe", "Uno"), default)).TrabajadorId;
        await Sup.AsignarAsync(ana.TrabajadorId, new AsignarSupervisorRequest { SupervisorTrabajadorId = jefe }, default);
        await _t.LoginAsync("ana@a.com");
        _t.NewScope();
        _t.Current.As(e, e.AdminId);

        var baja = await Trab.DarDeBajaAsync(ana.TrabajadorId, new BajaTrabajadorRequest { FechaBaja = Hoy, Motivo = "Renuncia voluntaria" }, default);
        Assert.Equal(EstadoLaboral.Baja, baja.EstadoLaboral);
        Assert.Equal("Renuncia voluntaria", baja.MotivoBaja);

        // El expediente NO se borra y el historial se conserva (con el movimiento de baja).
        _t.NewScope();
        Assert.True(await _t.Db.Trabajadores.AnyAsync(t => t.TrabajadorId == ana.TrabajadorId));
        var historial = await Trab.HistorialAsync(ana.TrabajadorId, default);
        Assert.Contains(historial, h => h.TipoCambio == TipoCambioLaboral.Baja && h.Motivo == "Renuncia voluntaria");
        Assert.Contains(historial, h => h.TipoCambio == TipoCambioLaboral.Ingreso);
        Assert.Equal(Hoy, (await Sup.ListarAsync(ana.TrabajadorId, default)).Single().FechaFin);   // supervisión cerrada, no borrada

        // Acceso bloqueado: cuenta inactiva, sesiones revocadas y el login falla.
        Assert.Equal(EstadoUsuario.Inactivo, (await _t.Db.Usuarios.SingleAsync(u => u.UsuarioId == cuenta)).Estado);
        Assert.All(await _t.Db.SesionesUsuario.Where(s => s.UsuarioId == cuenta).ToListAsync(), s => Assert.NotNull(s.RevocadaUtc));
        await Rechaza(() => _t.LoginAsync("ana@a.com"));
        _t.Current.As(e, e.AdminId);

        // No se puede volver a dar de baja ni editar; el reingreso restablece el expediente pero no reactiva la cuenta.
        Assert.Equal(ServiceErrorKind.Conflict, (await Rechaza(() => Trab.DarDeBajaAsync(ana.TrabajadorId, new BajaTrabajadorRequest { FechaBaja = Hoy, Motivo = "Otra" }, default))).Kind);
        var re = await Trab.ReingresarAsync(ana.TrabajadorId, "Recontratación", default);
        Assert.Equal(EstadoLaboral.Activo, re.EstadoLaboral);
        Assert.Null(re.MotivoBaja);
        Assert.Equal(EstadoUsuario.Inactivo, (await _t.Db.Usuarios.AsNoTracking().SingleAsync(u => u.UsuarioId == cuenta)).Estado);
    }

    [Fact]
    public async Task Baja_valida_fechas_y_no_permite_darse_de_baja_a_uno_mismo()
    {
        var (e, _, _) = await EmpresaConCatalogosAsync("A", "admin@a.com");
        var propio = await Trab.CrearAsync(NuevoTrabajador("Admin", "Uno", usuario: e.AdminId), default);
        var otro = await Trab.CrearAsync(NuevoTrabajador("Ana", "Ruiz"), default);

        Assert.Equal(ServiceErrorKind.Forbidden, (await Rechaza(() => Trab.DarDeBajaAsync(propio.TrabajadorId,
            new BajaTrabajadorRequest { FechaBaja = Hoy, Motivo = "Prueba" }, default))).Kind);
        Assert.Equal(ServiceErrorKind.Validation, (await Rechaza(() => Trab.DarDeBajaAsync(otro.TrabajadorId,
            new BajaTrabajadorRequest { FechaBaja = otro.FechaIngreso!.Value.AddDays(-1), Motivo = "Antes del ingreso" }, default))).Kind);
        Assert.Equal(ServiceErrorKind.Validation, (await Rechaza(() => Trab.DarDeBajaAsync(otro.TrabajadorId,
            new BajaTrabajadorRequest { FechaBaja = Hoy.AddDays(5), Motivo = "Futura" }, default))).Kind);
    }

    // 6. Datos sensibles y contactos de emergencia ---------------------------------
    [Fact]
    public async Task Datos_personales_y_contactos_solo_para_autorizados_o_el_propio_trabajador()
    {
        var (e, _, _) = await EmpresaConCatalogosAsync("A", "admin@a.com");
        var cuentaAna = await _t.NuevoUsuarioAsync(e.EmpresaId, "ana@a.com", "Ana", "Ruiz", e.Roles[NombresRol.Trabajador]);
        var cuentaLuis = await _t.NuevoUsuarioAsync(e.EmpresaId, "luis@a.com", "Luis", "Mora", e.Roles[NombresRol.Trabajador]);
        var req = NuevoTrabajador("Ana", "Ruiz", usuario: cuentaAna); req.Telefono = "5551112222"; req.Direccion = "Calle 1";
        var ana = await Trab.CrearAsync(req, default);
        await Trab.CrearAsync(NuevoTrabajador("Luis", "Mora", usuario: cuentaLuis), default);
        await _t.Svc<IContactoEmergenciaService>().CrearAsync(ana.TrabajadorId,
            new GuardarContactoEmergenciaRequest { NombreCompleto = "Madre Ruiz", Parentesco = "Madre", Telefono = "5550001111" }, default);

        // Administrador (permiso de datos sensibles): ve todo.
        var completo = await Trab.ObtenerAsync(ana.TrabajadorId, default);
        Assert.True(completo.VerSensibles);
        Assert.Equal("5551112222", completo.Telefono);
        Assert.Single(await _t.Svc<IContactoEmergenciaService>().ListarAsync(ana.TrabajadorId, default));

        // Otro trabajador (rol Trabajador): solo los datos básicos.
        _t.Current.As(e, cuentaLuis);
        var basico = await Trab.ObtenerAsync(ana.TrabajadorId, default);
        Assert.False(basico.VerSensibles);
        Assert.Null(basico.Telefono); Assert.Null(basico.Direccion); Assert.Null(basico.CorreoContacto);
        Assert.Null(basico.FechaNacimiento); Assert.Null(basico.UsuarioId); Assert.Null(basico.FechaIngreso);
        Assert.Equal("Ana", basico.Nombres);
        Assert.Equal(ServiceErrorKind.Forbidden, (await Rechaza(() => _t.Svc<IContactoEmergenciaService>().ListarAsync(ana.TrabajadorId, default))).Kind);
        Assert.Equal(ServiceErrorKind.Forbidden, (await Rechaza(() => Trab.HistorialAsync(ana.TrabajadorId, default))).Kind);

        // El propio trabajador ve su expediente completo y sus contactos.
        _t.Current.As(e, cuentaAna);
        var mio = await Trab.MiExpedienteAsync(default);
        Assert.True(mio.VerSensibles);
        Assert.Equal("Calle 1", mio.Direccion);
        Assert.Single(await _t.Svc<IContactoEmergenciaService>().ListarAsync(ana.TrabajadorId, default));
    }

    [Fact]
    public async Task Contactos_de_emergencia_mantienen_un_solo_principal_y_un_maximo()
    {
        await EmpresaConCatalogosAsync("A", "admin@a.com");
        var t = (await Trab.CrearAsync(NuevoTrabajador("Ana", "Ruiz"), default)).TrabajadorId;
        var svc = _t.Svc<IContactoEmergenciaService>();
        GuardarContactoEmergenciaRequest C(string n, bool p = false) => new() { NombreCompleto = n, Telefono = "5550001111", EsPrincipal = p };

        var primero = await svc.CrearAsync(t, C("Contacto Uno"), default);
        Assert.True(primero.EsPrincipal);                           // el primero es principal por defecto
        await svc.CrearAsync(t, C("Contacto Dos", true), default);
        var lista = await svc.ListarAsync(t, default);
        Assert.Equal("Contacto Dos", Assert.Single(lista, c => c.EsPrincipal).NombreCompleto);

        for (var i = 3; i <= 5; i++) await svc.CrearAsync(t, C($"Contacto {i}"), default);
        Assert.Equal(ServiceErrorKind.Conflict, (await Rechaza(() => svc.CrearAsync(t, C("Sobra"), default))).Kind);
        await svc.EliminarAsync(t, primero.ContactoEmergenciaId, default);
        Assert.Equal(4, (await svc.ListarAsync(t, default)).Count);
    }

    // 7. Solicitudes de cambio de perfil -----------------------------------------------
    private async Task<(EmpresaInfo E, Guid CuentaAna, Guid TrabajadorAna, Guid CuentaLuis)> EscenarioSolicitudesAsync()
    {
        var (e, _, _) = await EmpresaConCatalogosAsync("A", "admin@a.com");
        var cuentaAna = await _t.NuevoUsuarioAsync(e.EmpresaId, "ana@a.com", "Ana", "Ruiz", e.Roles[NombresRol.Trabajador]);
        var cuentaLuis = await _t.NuevoUsuarioAsync(e.EmpresaId, "luis@a.com", "Luis", "Mora", e.Roles[NombresRol.Trabajador]);
        var req = NuevoTrabajador("Ana", "Ruiz", usuario: cuentaAna); req.Telefono = "5550000000";
        var ana = await Trab.CrearAsync(req, default);
        await Trab.CrearAsync(NuevoTrabajador("Luis", "Mora", usuario: cuentaLuis), default);
        return (e, cuentaAna, ana.TrabajadorId, cuentaLuis);
    }

    [Fact]
    public async Task Solicitud_pendiente_no_cambia_el_expediente_hasta_ser_aprobada()
    {
        var (e, cuentaAna, ana, _) = await EscenarioSolicitudesAsync();
        var sol = _t.Svc<ISolicitudCambioPerfilService>();

        _t.Current.As(e, cuentaAna);
        var s = await sol.CrearAsync(new CambioPerfilDatos { Telefono = "5559998888" }, default);
        Assert.Equal(EstadoSolicitudPerfil.Pendiente, s.Estado);
        Assert.Equal("5550000000", (await Trab.MiExpedienteAsync(default)).Telefono);   // oficial sin cambios
        Assert.Equal(ServiceErrorKind.Conflict, (await Rechaza(() => sol.CrearAsync(new CambioPerfilDatos { Direccion = "Otra" }, default))).Kind);
        // Con el rol Trabajador no puede resolver (el permiso se exige en la API); ni siquiera ella misma aunque tuviera permiso.
        _t.Current.As(e, e.AdminId);
        var r = await sol.ResolverAsync(s.SolicitudCambioPerfilId, new ResolverSolicitudRequest { Aprobar = true, Comentario = "OK" }, default);
        Assert.Equal(EstadoSolicitudPerfil.Aprobada, r.Estado);
        Assert.Equal(e.AdminId, r.RevisadaPorUsuarioId);
        Assert.NotNull(r.RevisadaUtc);

        Assert.Equal("5559998888", (await Trab.ObtenerAsync(ana, default)).Telefono);
        Assert.Contains(await Trab.HistorialAsync(ana, default), h => h.TipoCambio == TipoCambioLaboral.Perfil);
        Assert.Equal(ServiceErrorKind.Conflict, (await Rechaza(() => sol.ResolverAsync(s.SolicitudCambioPerfilId, new ResolverSolicitudRequest { Aprobar = false, Comentario = "x" }, default))).Kind);
    }

    [Fact]
    public async Task Solicitud_rechazada_exige_motivo_y_no_modifica_datos()
    {
        var (e, cuentaAna, ana, _) = await EscenarioSolicitudesAsync();
        var sol = _t.Svc<ISolicitudCambioPerfilService>();
        _t.Current.As(e, cuentaAna);
        var s = await sol.CrearAsync(new CambioPerfilDatos { Telefono = "5559998888", Direccion = "Nueva 5" }, default);

        _t.Current.As(e, e.AdminId);
        Assert.Equal(ServiceErrorKind.Validation, (await Rechaza(() => sol.ResolverAsync(s.SolicitudCambioPerfilId, new ResolverSolicitudRequest { Aprobar = false }, default))).Kind);
        var r = await sol.ResolverAsync(s.SolicitudCambioPerfilId, new ResolverSolicitudRequest { Aprobar = false, Comentario = "Datos no verificables" }, default);
        Assert.Equal(EstadoSolicitudPerfil.Rechazada, r.Estado);
        Assert.Equal("Datos no verificables", r.ComentarioRevision);
        Assert.Equal("5550000000", (await Trab.ObtenerAsync(ana, default)).Telefono);

        var pendientes = await sol.ListarAsync(new SolicitudPerfilFiltro { Estado = EstadoSolicitudPerfil.Pendiente }, default);
        Assert.Empty(pendientes.Items);
        Assert.Single((await sol.ListarAsync(new SolicitudPerfilFiltro(), default)).Items);
    }

    [Fact]
    public async Task Nadie_resuelve_su_propia_solicitud_ni_cancela_la_de_otro()
    {
        var (e, cuentaAna, _, cuentaLuis) = await EscenarioSolicitudesAsync();
        var sol = _t.Svc<ISolicitudCambioPerfilService>();

        // El administrador también es trabajador: no puede aprobar su propia solicitud.
        var admin = await Trab.CrearAsync(NuevoTrabajador("Admin", "Uno", usuario: e.AdminId), default);
        var propia = await sol.CrearAsync(new CambioPerfilDatos { Telefono = "5551110000" }, default);
        Assert.Equal(admin.TrabajadorId, propia.TrabajadorId);
        Assert.Equal(ServiceErrorKind.Forbidden, (await Rechaza(() => sol.ResolverAsync(propia.SolicitudCambioPerfilId, new ResolverSolicitudRequest { Aprobar = true }, default))).Kind);

        _t.Current.As(e, cuentaAna);
        var deAna = await sol.CrearAsync(new CambioPerfilDatos { Telefono = "5559998888" }, default);
        _t.Current.As(e, cuentaLuis);
        Assert.Equal(ServiceErrorKind.NotFound, (await Rechaza(() => sol.CancelarAsync(deAna.SolicitudCambioPerfilId, default))).Kind);
        Assert.Empty(await sol.MisSolicitudesAsync(default));

        _t.Current.As(e, cuentaAna);
        Assert.Equal(EstadoSolicitudPerfil.Cancelada, (await sol.CancelarAsync(deAna.SolicitudCambioPerfilId, default)).Estado);
    }

    [Fact]
    public async Task Solicitudes_de_otra_empresa_no_se_ven_ni_se_resuelven_y_sin_cambios_no_se_crean()
    {
        var (e, cuentaAna, _, _) = await EscenarioSolicitudesAsync();
        var sol = _t.Svc<ISolicitudCambioPerfilService>();
        _t.Current.As(e, cuentaAna);
        Assert.Equal(ServiceErrorKind.Validation, (await Rechaza(() => sol.CrearAsync(new CambioPerfilDatos(), default))).Kind);
        Assert.Equal(ServiceErrorKind.Validation, (await Rechaza(() => sol.CrearAsync(new CambioPerfilDatos { Telefono = "5550000000" }, default))).Kind);
        var s = await sol.CrearAsync(new CambioPerfilDatos { Telefono = "5559998888" }, default);

        var b = await _t.CrearEmpresaAsync("B", "admin@b.com");
        _t.Current.As(b, b.AdminId);
        Assert.Empty((await sol.ListarAsync(new SolicitudPerfilFiltro(), default)).Items);
        Assert.Equal(ServiceErrorKind.NotFound, (await Rechaza(() => sol.ResolverAsync(s.SolicitudCambioPerfilId, new ResolverSolicitudRequest { Aprobar = true }, default))).Kind);
    }

    [Fact]
    public async Task La_baja_cancela_las_solicitudes_pendientes()
    {
        var (e, cuentaAna, ana, _) = await EscenarioSolicitudesAsync();
        var sol = _t.Svc<ISolicitudCambioPerfilService>();
        _t.Current.As(e, cuentaAna);
        await sol.CrearAsync(new CambioPerfilDatos { Telefono = "5559998888" }, default);

        _t.Current.As(e, e.AdminId);
        await Trab.DarDeBajaAsync(ana, new BajaTrabajadorRequest { FechaBaja = Hoy, Motivo = "Fin de contrato" }, default);
        var todas = await sol.ListarAsync(new SolicitudPerfilFiltro(), default);
        Assert.Equal(EstadoSolicitudPerfil.Cancelada, Assert.Single(todas.Items).Estado);
    }

    // 8. Permisos por defecto -----------------------------------------------------------
    [Fact]
    public void Permisos_del_bloque_3_estan_en_el_catalogo_y_en_los_roles_por_defecto()
    {
        Assert.All(Permisos.Bloque3, p => Assert.Contains(p, Permisos.Todos));
        Assert.Equal(Permisos.Bloque3.Length, Permisos.Bloque3.Distinct().Count());
        var trabajador = PermisosPorDefecto.PorRol[NombresRol.Trabajador];
        Assert.DoesNotContain(Permisos.TrabajadoresVerSensibles, trabajador);   // sensibles solo para roles autorizados
        Assert.DoesNotContain(Permisos.PerfilResolverSolicitudes, trabajador);
        Assert.Contains(Permisos.TrabajadoresVerSensibles, PermisosPorDefecto.PorRol[NombresRol.RecursosHumanos]);
        Assert.DoesNotContain(Permisos.TrabajadoresVerSensibles, PermisosPorDefecto.PorRol[NombresRol.Supervisor]);
    }
}
