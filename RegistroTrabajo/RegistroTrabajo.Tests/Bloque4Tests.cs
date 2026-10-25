using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RegistroTrabajo.Data.Entities;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Tests;

public class Bloque4Tests : IDisposable
{
    private readonly TestEnv _t = new();
    public void Dispose() => _t.Dispose();

    // El FakeClock está en jueves 2026-01-01. Enero 2026: 2 vie, 3 sáb, 4 dom, 5 lun ... 9 vie, 12 lun ... 16 vie.
    private static async Task<ServiceException> Rechaza(Func<Task> accion) =>
        await Assert.ThrowsAnyAsync<ServiceException>(accion);

    private static async Task RechazaCon(ServiceErrorKind kind, Func<Task> accion) =>
        Assert.Equal(kind, (await Rechaza(accion)).Kind);

    private sealed record Esc(EmpresaInfo E, Guid Admin, Guid UsuarioSara, Guid UsuarioLuis, Guid UsuarioOtro, Guid Sara, Guid Luis, Guid Otro, Guid Vacaciones);

    private static CrearTrabajadorRequest NuevoTrabajador(string nombres, string apellidos, Guid usuario) => new()
    {
        Nombres = nombres, Apellidos = apellidos, FechaIngreso = new DateOnly(2025, 6, 1), UsuarioId = usuario
    };

    private static DateTime F(string s) => DateTime.Parse(s, CultureInfo.InvariantCulture);

    private static CrearSolicitudPermisoRequest Req(Guid tipo, string ini, string fin, bool horas = false, string? clave = null, string? motivo = null) => new()
    {
        TipoPermisoId = tipo, FechaInicio = F(ini), FechaFin = F(fin), EsPorHoras = horas, ClaveIdempotencia = clave, Motivo = motivo
    };

    /// <summary>Empresa con admin (aprobar_todos), Sara (supervisora de Luis), Luis y Otro (sin supervisor) y el tipo Vacaciones con regla anual.</summary>
    private async Task<Esc> EscenarioAsync(decimal anual = 10, bool negativo = false, decimal? limite = null, int? antiguedad = null)
    {
        var e = await _t.CrearEmpresaAsync("Acme", "admin@a.com");
        var uSara = await _t.NuevoUsuarioAsync(e.EmpresaId, "sara@a.com", "Sara", "Sup", e.Roles[NombresRol.Supervisor]);
        var uLuis = await _t.NuevoUsuarioAsync(e.EmpresaId, "luis@a.com", "Luis", "Mora", e.Roles[NombresRol.Trabajador]);
        var uOtro = await _t.NuevoUsuarioAsync(e.EmpresaId, "otro@a.com", "Otro", "Ruiz", e.Roles[NombresRol.Trabajador]);
        _t.Current.As(e, e.AdminId);
        var trab = _t.Svc<ITrabajadorService>();
        var sara = (await trab.CrearAsync(NuevoTrabajador("Sara", "Sup", uSara), default)).TrabajadorId;
        var luis = (await trab.CrearAsync(NuevoTrabajador("Luis", "Mora", uLuis), default)).TrabajadorId;
        var otro = (await trab.CrearAsync(NuevoTrabajador("Otro", "Ruiz", uOtro), default)).TrabajadorId;
        await _t.Svc<ISupervisionService>().AsignarAsync(luis, new AsignarSupervisorRequest { SupervisorTrabajadorId = sara }, default);

        var tipos = _t.Svc<ITiposPermisoService>();
        var tipo = await tipos.CrearAsync(new GuardarTipoPermisoRequest { Nombre = "Vacaciones", UnidadBase = UnidadPermiso.Dias }, default);
        await tipos.CrearReglaAsync(tipo.TipoPermisoId, new GuardarReglaPermisoRequest
        {
            CantidadAnual = anual, PermiteSaldoNegativo = negativo, LimitePorSolicitud = limite, AntiguedadMinimaMeses = antiguedad
        }, default);
        return new Esc(e, e.AdminId, uSara, uLuis, uOtro, sara, luis, otro, tipo.TipoPermisoId);
    }

    /// <summary>Cambia de actor con un scope nuevo (como una solicitud HTTP nueva).</summary>
    private void Como(Esc s, Guid usuario)
    {
        _t.NewScope();
        _t.Current.As(s.E, usuario);
    }

    private ISolicitudesPermisoService Sol => _t.Svc<ISolicitudesPermisoService>();
    private IAprobacionesPermisoService Apr => _t.Svc<IAprobacionesPermisoService>();
    private ISaldosPermisoService Saldos => _t.Svc<ISaldosPermisoService>();

    private Task<SaldoPermiso> SaldoDeAsync(Guid trabajador, Guid tipo) =>
        _t.Db.SaldosPermiso.AsNoTracking().SingleAsync(s => s.TrabajadorId == trabajador && s.TipoPermisoId == tipo && s.PeriodoAnio == 2026);

    private Task<int> MovimientosAsync(Guid solicitud, byte tipo) =>
        _t.Db.MovimientosSaldoPermiso.AsNoTracking().CountAsync(m => m.SolicitudPermisoId == solicitud && m.TipoMovimiento == tipo);

    // 1. Tipos y reglas ---------------------------------------------------------------
    [Fact]
    public async Task Tipos_no_se_duplican_las_reglas_activas_no_se_traslapan_y_los_iniciales_se_siembran_una_vez()
    {
        var s = await EscenarioAsync();
        var tipos = _t.Svc<ITiposPermisoService>();
        await RechazaCon(ServiceErrorKind.Conflict, () => tipos.CrearAsync(new GuardarTipoPermisoRequest { Nombre = " vacaciones " }, default));
        // La regla creada por el escenario no tiene vigencia: otra activa sin vigencia se traslapa.
        await RechazaCon(ServiceErrorKind.Conflict, () => tipos.CrearReglaAsync(s.Vacaciones, new GuardarReglaPermisoRequest { CantidadAnual = 5 }, default));
        await RechazaCon(ServiceErrorKind.Validation, () => tipos.CrearReglaAsync(s.Vacaciones,
            new GuardarReglaPermisoRequest { ActivaDesde = new DateOnly(2026, 5, 1), ActivaHasta = new DateOnly(2026, 4, 1), Estado = false }, default));

        var todos = await tipos.SembrarInicialesAsync(default);
        Assert.Equal(6, todos.Count);                       // Vacaciones ya existía: se crean los otros 5
        Assert.Equal(6, (await tipos.SembrarInicialesAsync(default)).Count);
        Assert.Contains(todos, t => t.Nombre == "Incapacidad" && t.RequiereComprobante);
    }

    // 2. Duración -----------------------------------------------------------------------
    [Fact]
    public async Task La_duracion_cuenta_solo_dias_laborales_sin_festivos_ni_descansos()
    {
        var s = await EscenarioAsync();
        _t.Db.DiasFestivos.Add(new DiaFestivo { DiaFestivoId = Guid.NewGuid(), EmpresaId = s.E.EmpresaId, Fecha = new DateOnly(2026, 1, 5), Nombre = "Festivo" });
        _t.Db.DescansosTrabajador.Add(new DescansoTrabajador
        {
            DescansoTrabajadorId = Guid.NewGuid(), EmpresaId = s.E.EmpresaId, TrabajadorId = s.Luis, FechaDesde = new DateOnly(2026, 1, 7),
            FechaHasta = new DateOnly(2026, 1, 7), Tipo = 1, RegistradoPorUsuarioId = s.Admin
        });
        await _t.Db.SaveChangesAsync();

        Como(s, s.UsuarioLuis);
        // vie 2, (sáb, dom), lun 5 festivo, mar 6, mié 7 descanso, jue 8 => 3 días laborales.
        var c = await _t.Svc<ICalculoDuracionService>().PrevisualizarAsync(new CalculoDuracionRequest
        {
            TipoPermisoId = s.Vacaciones, FechaInicio = F("2026-01-02"), FechaFin = F("2026-01-08")
        }, default);
        Assert.Equal(3m, c.Cantidad);
        Assert.True(c.ControlaSaldo);
        Assert.Equal(7m, c.SaldoRestante);

        await RechazaCon(ServiceErrorKind.Validation, () => Sol.CrearAsync(Req(s.Vacaciones, "2026-01-03", "2026-01-04"), default)); // solo fin de semana
    }

    // 3. Solicitar -----------------------------------------------------------------------
    [Fact]
    public async Task Solicitar_reserva_saldo_y_avisa_al_supervisor_sin_datos_privados()
    {
        var s = await EscenarioAsync();
        Como(s, s.UsuarioLuis);
        var d = await Sol.CrearAsync(Req(s.Vacaciones, "2026-01-05", "2026-01-09", motivo: "Cita con el cardiólogo"), default);

        Assert.Equal(EstadoSolicitudPermiso.Pendiente, d.Estado);
        Assert.Equal(5m, d.Cantidad);
        var saldo = await SaldoDeAsync(s.Luis, s.Vacaciones);
        Assert.Equal((10m, 0m, 5m), (saldo.CantidadAsignada, saldo.CantidadUtilizada, saldo.CantidadReservada));
        Assert.Equal(1, await _t.Db.MovimientosSaldoPermiso.CountAsync(m => m.SaldoPermisoId == saldo.SaldoPermisoId && m.TipoMovimiento == TipoMovimientoSaldo.Asignacion));

        var n = await _t.Db.Notificaciones.AsNoTracking().SingleAsync();
        Assert.Equal(s.UsuarioSara, n.UsuarioDestinoId);
        Assert.DoesNotContain("cardi", n.Mensaje, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Vacaciones", n.Mensaje);
    }

    [Fact]
    public async Task Se_validan_fechas_traslapes_limites_antiguedad_saldo_y_horas()
    {
        var s = await EscenarioAsync(anual: 10, limite: 6);
        Como(s, s.UsuarioLuis);
        await RechazaCon(ServiceErrorKind.Validation, () => Sol.CrearAsync(Req(s.Vacaciones, "2025-12-29", "2025-12-30"), default));   // pasado
        await RechazaCon(ServiceErrorKind.Validation, () => Sol.CrearAsync(Req(s.Vacaciones, "2026-01-09", "2026-01-05"), default));   // fin antes que inicio
        await RechazaCon(ServiceErrorKind.Validation, () => Sol.CrearAsync(Req(s.Vacaciones, "2026-01-12", "2026-01-23"), default));   // 10 días > límite 6
        await RechazaCon(ServiceErrorKind.Validation, () => Sol.CrearAsync(Req(s.Vacaciones, "2026-01-02", "2026-01-02", horas: true), default)); // tipo en días sin fraccionar
        await RechazaCon(ServiceErrorKind.Validation, () => Sol.CrearAsync(Req(Guid.NewGuid(), "2026-01-05", "2026-01-06"), default));  // tipo inexistente

        await Sol.CrearAsync(Req(s.Vacaciones, "2026-01-08", "2026-01-09"), default);
        await RechazaCon(ServiceErrorKind.Conflict, () => Sol.CrearAsync(Req(s.Vacaciones, "2026-01-09", "2026-01-12"), default));     // traslape
        await Sol.CrearAsync(Req(s.Vacaciones, "2026-01-12", "2026-01-16"), default);                                                    // 5 días: reservado total 7
        await RechazaCon(ServiceErrorKind.Validation, () => Sol.CrearAsync(Req(s.Vacaciones, "2026-01-19", "2026-01-22"), default));   // 4 > 3 disponibles
    }

    [Fact]
    public async Task La_antiguedad_minima_se_exige_segun_la_regla()
    {
        var s = await EscenarioAsync(anual: 10, antiguedad: 12);
        Como(s, s.UsuarioLuis);
        await RechazaCon(ServiceErrorKind.Validation, () => Sol.CrearAsync(Req(s.Vacaciones, "2026-01-05", "2026-01-06"), default));    // ingresó hace 7 meses
    }

    [Fact]
    public async Task El_saldo_negativo_solo_se_permite_cuando_la_regla_lo_dice_explicitamente()
    {
        var n = await EscenarioAsync(anual: 3, negativo: true);
        Como(n, n.UsuarioLuis);
        var d = await Sol.CrearAsync(Req(n.Vacaciones, "2026-01-05", "2026-01-09"), default); // 5 > 3 pero la regla lo permite
        Assert.Equal(EstadoSolicitudPermiso.Pendiente, d.Estado);
        var saldo = await SaldoDeAsync(n.Luis, n.Vacaciones);
        Assert.Equal(-2m, saldo.CantidadAsignada - saldo.CantidadReservada);
    }
    [Fact]
    public async Task Un_reintento_con_la_misma_clave_no_duplica_la_solicitud_ni_el_saldo()
    {
        var s = await EscenarioAsync();
        Como(s, s.UsuarioLuis);
        var a = await Sol.CrearAsync(Req(s.Vacaciones, "2026-01-05", "2026-01-09", clave: "abc-1"), default);
        _t.NewScope(); _t.Current.As(s.E, s.UsuarioLuis);
        var b = await Sol.CrearAsync(Req(s.Vacaciones, "2026-01-05", "2026-01-09", clave: "abc-1"), default);

        Assert.Equal(a.SolicitudPermisoId, b.SolicitudPermisoId);
        Assert.Equal(1, await _t.Db.SolicitudesPermiso.CountAsync());
        Assert.Equal(5m, (await SaldoDeAsync(s.Luis, s.Vacaciones)).CantidadReservada);
    }

    // 4. Aprobación --------------------------------------------------------------------
    [Fact]
    public async Task Aprobar_consume_el_saldo_una_sola_vez_y_deja_la_decision_registrada()
    {
        var s = await EscenarioAsync();
        Como(s, s.UsuarioLuis);
        var d = await Sol.CrearAsync(Req(s.Vacaciones, "2026-01-05", "2026-01-09"), default);

        Como(s, s.UsuarioSara);
        var bandeja = await Sol.BandejaAsync(new SolicitudPermisoFiltro(), default);
        Assert.Single(bandeja.Items);
        var r = await Apr.ResolverAsync(d.SolicitudPermisoId, new ResolverPermisoRequest { Aprobar = true, Comentario = "Buen descanso" }, default);
        Assert.Equal(EstadoSolicitudPermiso.Aprobada, r.Estado);
        Assert.Equal(s.UsuarioSara, r.RevisorUsuarioId);

        var saldo = await SaldoDeAsync(s.Luis, s.Vacaciones);
        Assert.Equal((10m, 5m, 0m), (saldo.CantidadAsignada, saldo.CantidadUtilizada, saldo.CantidadReservada));
        Assert.Equal(1, await MovimientosAsync(d.SolicitudPermisoId, TipoMovimientoSaldo.Consumo));

        // Reintentar la resolución no vuelve a descontar.
        Como(s, s.UsuarioSara);
        await RechazaCon(ServiceErrorKind.Conflict, () => Apr.ResolverAsync(d.SolicitudPermisoId, new ResolverPermisoRequest { Aprobar = true }, default));
        Assert.Equal(5m, (await SaldoDeAsync(s.Luis, s.Vacaciones)).CantidadUtilizada);
        Assert.Equal(1, await MovimientosAsync(d.SolicitudPermisoId, TipoMovimientoSaldo.Consumo));

        var luis = await _t.Db.Notificaciones.AsNoTracking().Where(n => n.UsuarioDestinoId == s.UsuarioLuis).ToListAsync();
        Assert.Single(luis);
    }

    [Fact]
    public async Task Solo_resuelve_un_supervisor_vigente_o_quien_tiene_aprobar_todos_y_nunca_lo_propio()
    {
        var s = await EscenarioAsync();
        Como(s, s.UsuarioLuis);
        var deLuis = await Sol.CrearAsync(Req(s.Vacaciones, "2026-01-05", "2026-01-06"), default);
        Como(s, s.UsuarioOtro);
        var deOtro = await Sol.CrearAsync(Req(s.Vacaciones, "2026-01-05", "2026-01-06"), default);
        Como(s, s.UsuarioSara);
        var deSara = await Sol.CrearAsync(Req(s.Vacaciones, "2026-01-05", "2026-01-06"), default);

        // Sara no supervisa a Otro; tampoco puede resolver su propia solicitud.
        await RechazaCon(ServiceErrorKind.Forbidden, () => Apr.ResolverAsync(deOtro.SolicitudPermisoId, new ResolverPermisoRequest { Aprobar = true }, default));
        await RechazaCon(ServiceErrorKind.Forbidden, () => Apr.ResolverAsync(deSara.SolicitudPermisoId, new ResolverPermisoRequest { Aprobar = true }, default));
        // Un trabajador sin permisos de revisión no puede resolver ni ver la bandeja.
        Como(s, s.UsuarioOtro);
        await RechazaCon(ServiceErrorKind.Forbidden, () => Apr.ResolverAsync(deLuis.SolicitudPermisoId, new ResolverPermisoRequest { Aprobar = true }, default));
        await RechazaCon(ServiceErrorKind.Forbidden, () => Sol.BandejaAsync(new SolicitudPermisoFiltro(), default));
        // La bandeja de Sara solo trae a su supervisado.
        Como(s, s.UsuarioSara);
        Assert.Equal(deLuis.SolicitudPermisoId, (await Sol.BandejaAsync(new SolicitudPermisoFiltro(), default)).Items.Single().SolicitudPermisoId);
        // RH/Admin (aprobar_todos) sí puede resolver a cualquiera.
        Como(s, s.Admin);
        Assert.Equal(3, (await Sol.BandejaAsync(new SolicitudPermisoFiltro(), default)).Total);
        var r = await Apr.ResolverAsync(deOtro.SolicitudPermisoId, new ResolverPermisoRequest { Aprobar = true }, default);
        Assert.Equal(EstadoSolicitudPermiso.Aprobada, r.Estado);
    }

    [Fact]
    public async Task Rechazar_exige_motivo_y_libera_el_saldo_reservado()
    {
        var s = await EscenarioAsync();
        Como(s, s.UsuarioLuis);
        var d = await Sol.CrearAsync(Req(s.Vacaciones, "2026-01-05", "2026-01-09"), default);
        Como(s, s.UsuarioSara);
        await RechazaCon(ServiceErrorKind.Validation, () => Apr.ResolverAsync(d.SolicitudPermisoId, new ResolverPermisoRequest { Aprobar = false }, default));
        var r = await Apr.ResolverAsync(d.SolicitudPermisoId, new ResolverPermisoRequest { Aprobar = false, Comentario = "Cierre de mes" }, default);
        Assert.Equal(EstadoSolicitudPermiso.Rechazada, r.Estado);
        Assert.Equal("Cierre de mes", r.ComentarioDecision);
        var saldo = await SaldoDeAsync(s.Luis, s.Vacaciones);
        Assert.Equal((0m, 0m), (saldo.CantidadReservada, saldo.CantidadUtilizada));
        Assert.Equal(0, await MovimientosAsync(d.SolicitudPermisoId, TipoMovimientoSaldo.Consumo));
    }

    [Fact]
    public async Task Cancelar_o_editar_una_pendiente_ajusta_la_reserva_y_las_resueltas_no_se_editan()
    {
        var s = await EscenarioAsync();
        Como(s, s.UsuarioLuis);
        var d = await Sol.CrearAsync(Req(s.Vacaciones, "2026-01-05", "2026-01-09"), default);

        Como(s, s.UsuarioLuis);
        var e = await Sol.ActualizarAsync(d.SolicitudPermisoId, new ActualizarSolicitudPermisoRequest
        {
            TipoPermisoId = s.Vacaciones, FechaInicio = F("2026-01-05"), FechaFin = F("2026-01-06"), RowVersion = d.RowVersion
        }, default);
        Assert.Equal(2m, e.Cantidad);
        Assert.Equal(2m, (await SaldoDeAsync(s.Luis, s.Vacaciones)).CantidadReservada);

        // Otro trabajador no puede tocarla (404).
        Como(s, s.UsuarioOtro);
        await RechazaCon(ServiceErrorKind.NotFound, () => Sol.CancelarAsync(d.SolicitudPermisoId, default));

        Como(s, s.UsuarioLuis);
        var c = await Sol.CancelarAsync(d.SolicitudPermisoId, default);
        Assert.Equal(EstadoSolicitudPermiso.Cancelada, c.Estado);
        Assert.Equal(0m, (await SaldoDeAsync(s.Luis, s.Vacaciones)).CantidadReservada);
        await RechazaCon(ServiceErrorKind.Conflict, () => Sol.CancelarAsync(d.SolicitudPermisoId, default));
        await RechazaCon(ServiceErrorKind.Conflict, () => Sol.ActualizarAsync(d.SolicitudPermisoId, new ActualizarSolicitudPermisoRequest
        {
            TipoPermisoId = s.Vacaciones, FechaInicio = F("2026-01-05"), FechaFin = F("2026-01-06"), RowVersion = d.RowVersion
        }, default));
    }

    // 5. Cancelación de permisos aprobados ------------------------------------------------
    [Fact]
    public async Task Cancelar_un_permiso_aprobado_pasa_por_revision_devuelve_el_saldo_una_vez_y_conserva_la_solicitud()
    {
        var s = await EscenarioAsync();
        Como(s, s.UsuarioLuis);
        var d = await Sol.CrearAsync(Req(s.Vacaciones, "2026-01-05", "2026-01-09"), default);
        await RechazaCon(ServiceErrorKind.Conflict, () => Apr.SolicitarCancelacionAsync(d.SolicitudPermisoId, new SolicitarCancelacionRequest(), default)); // aún pendiente
        Como(s, s.UsuarioSara);
        await Apr.ResolverAsync(d.SolicitudPermisoId, new ResolverPermisoRequest { Aprobar = true }, default);

        Como(s, s.UsuarioLuis);
        await RechazaCon(ServiceErrorKind.Conflict, () => Sol.CancelarAsync(d.SolicitudPermisoId, default));   // aprobada: debe pedir cancelación
        var pedida = await Apr.SolicitarCancelacionAsync(d.SolicitudPermisoId, new SolicitarCancelacionRequest { Motivo = "Cambio de planes" }, default);
        Assert.Equal(EstadoCancelacionPermiso.Pendiente, pedida.Estado);
        await RechazaCon(ServiceErrorKind.Conflict, () => Apr.SolicitarCancelacionAsync(d.SolicitudPermisoId, new SolicitarCancelacionRequest(), default)); // ya hay una
        Assert.True((await Sol.ObtenerAsync(d.SolicitudPermisoId, default)).CancelacionPendiente);

        Como(s, s.UsuarioOtro);
        await RechazaCon(ServiceErrorKind.Forbidden, () => Apr.ListarCancelacionesAsync(new CancelacionPermisoFiltro(), default));

        Como(s, s.UsuarioSara);
        Assert.Single((await Apr.ListarCancelacionesAsync(new CancelacionPermisoFiltro(), default)).Items);
        var ok = await Apr.ResolverCancelacionAsync(pedida.SolicitudCancelacionId, new ResolverPermisoRequest { Aprobar = true }, default);
        Assert.Equal(EstadoCancelacionPermiso.Aprobada, ok.Estado);

        var saldo = await SaldoDeAsync(s.Luis, s.Vacaciones);
        Assert.Equal((10m, 0m, 0m), (saldo.CantidadAsignada, saldo.CantidadUtilizada, saldo.CantidadReservada));
        Assert.Equal(1, await MovimientosAsync(d.SolicitudPermisoId, TipoMovimientoSaldo.Consumo));
        Assert.Equal(1, await MovimientosAsync(d.SolicitudPermisoId, TipoMovimientoSaldo.Devolucion));
        Como(s, s.UsuarioLuis);
        Assert.Equal(EstadoSolicitudPermiso.Cancelada, (await Sol.ObtenerAsync(d.SolicitudPermisoId, default)).Estado);   // la solicitud original sigue ahí

        Como(s, s.UsuarioSara);
        await RechazaCon(ServiceErrorKind.Conflict, () => Apr.ResolverCancelacionAsync(pedida.SolicitudCancelacionId, new ResolverPermisoRequest { Aprobar = true }, default));
        Assert.Equal(1, await MovimientosAsync(d.SolicitudPermisoId, TipoMovimientoSaldo.Devolucion));
    }

    [Fact]
    public async Task Rechazar_la_cancelacion_deja_el_permiso_aprobado_y_el_saldo_consumido()
    {
        var s = await EscenarioAsync();
        Como(s, s.UsuarioLuis);
        var d = await Sol.CrearAsync(Req(s.Vacaciones, "2026-01-05", "2026-01-06"), default);
        Como(s, s.UsuarioSara);
        await Apr.ResolverAsync(d.SolicitudPermisoId, new ResolverPermisoRequest { Aprobar = true }, default);
        Como(s, s.UsuarioLuis);
        var pedida = await Apr.SolicitarCancelacionAsync(d.SolicitudPermisoId, new SolicitarCancelacionRequest(), default);
        Como(s, s.UsuarioSara);
        await RechazaCon(ServiceErrorKind.Validation, () => Apr.ResolverCancelacionAsync(pedida.SolicitudCancelacionId, new ResolverPermisoRequest { Aprobar = false }, default));
        var r = await Apr.ResolverCancelacionAsync(pedida.SolicitudCancelacionId, new ResolverPermisoRequest { Aprobar = false, Comentario = "Ya está programado" }, default);
        Assert.Equal(EstadoCancelacionPermiso.Rechazada, r.Estado);
        Assert.Equal(EstadoSolicitudPermiso.Aprobada, (await Sol.ObtenerAsync(d.SolicitudPermisoId, default)).Estado);
        Assert.Equal(2m, (await SaldoDeAsync(s.Luis, s.Vacaciones)).CantidadUtilizada);
        Assert.Equal(0, await MovimientosAsync(d.SolicitudPermisoId, TipoMovimientoSaldo.Devolucion));
    }

    // 6. Tipos sin aprobación y permisos por horas ---------------------------------------
    [Fact]
    public async Task Un_tipo_sin_aprobacion_queda_aprobado_y_sin_saldo_no_lo_controla()
    {
        var s = await EscenarioAsync();
        var tipo = await _t.Svc<ITiposPermisoService>().CrearAsync(new GuardarTipoPermisoRequest { Nombre = "Aviso", RequiereAprobacion = false }, default);
        Como(s, s.UsuarioLuis);
        var d = await Sol.CrearAsync(Req(tipo.TipoPermisoId, "2026-01-05", "2026-01-05"), default);
        Assert.Equal(EstadoSolicitudPermiso.Aprobada, d.Estado);
        Assert.Equal(1, await _t.Db.AprobacionesPermiso.CountAsync(a => a.SolicitudPermisoId == d.SolicitudPermisoId));
        Assert.False(await _t.Db.SaldosPermiso.AnyAsync(x => x.TipoPermisoId == tipo.TipoPermisoId));
    }

    [Fact]
    public async Task Un_permiso_por_horas_se_calcula_en_horas_y_debe_ser_de_un_dia_laboral()
    {
        var s = await EscenarioAsync();
        var tipos = _t.Svc<ITiposPermisoService>();
        var cita = await tipos.CrearAsync(new GuardarTipoPermisoRequest { Nombre = "Cita médica", UnidadBase = UnidadPermiso.Horas }, default);
        await tipos.CrearReglaAsync(cita.TipoPermisoId, new GuardarReglaPermisoRequest { CantidadAnual = 16 }, default);
        Como(s, s.UsuarioLuis);

        var d = await Sol.CrearAsync(Req(cita.TipoPermisoId, "2026-01-02T09:00", "2026-01-02T11:30", horas: true), default);
        Assert.Equal(2.5m, d.Cantidad);
        Assert.True(d.EsPorHoras);
        Assert.Equal(2.5m, (await SaldoDeAsync(s.Luis, cita.TipoPermisoId)).CantidadReservada);

        await RechazaCon(ServiceErrorKind.Validation, () => Sol.CrearAsync(Req(cita.TipoPermisoId, "2026-01-05T09:00", "2026-01-06T11:00", horas: true), default)); // dos días
        await RechazaCon(ServiceErrorKind.Validation, () => Sol.CrearAsync(Req(cita.TipoPermisoId, "2026-01-03T09:00", "2026-01-03T11:00", horas: true), default)); // sábado
        await RechazaCon(ServiceErrorKind.Validation, () => Sol.CrearAsync(Req(cita.TipoPermisoId, "2026-01-06T11:00", "2026-01-06T09:00", horas: true), default)); // intervalo inválido
        await RechazaCon(ServiceErrorKind.Conflict, () => Sol.CrearAsync(Req(cita.TipoPermisoId, "2026-01-02T10:00", "2026-01-02T12:00", horas: true), default));  // traslapa con la anterior
    }

    // 7. Privacidad y aislamiento -----------------------------------------------------
    [Fact]
    public async Task El_motivo_privado_solo_lo_ven_el_trabajador_y_quien_tiene_el_permiso_y_el_calendario_no_expone_tipo()
    {
        var s = await EscenarioAsync();
        Como(s, s.UsuarioLuis);
        var d = await Sol.CrearAsync(Req(s.Vacaciones, "2026-01-05", "2026-01-06", motivo: "Tratamiento médico"), default);
        Assert.Equal("Tratamiento médico", (await Sol.ObtenerAsync(d.SolicitudPermisoId, default)).Motivo);

        Como(s, s.UsuarioSara);
        Assert.Null((await Sol.ObtenerAsync(d.SolicitudPermisoId, default)).Motivo);            // supervisora sin ver_motivo_privado
        Assert.All((await Sol.BandejaAsync(new SolicitudPermisoFiltro(), default)).Items, i => Assert.Null(i.Motivo));
        await Apr.ResolverAsync(d.SolicitudPermisoId, new ResolverPermisoRequest { Aprobar = true }, default);
        var ausencias = await Sol.CalendarioAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), default);
        Assert.Equal("Luis Mora", ausencias.Single().TrabajadorNombre);

        Como(s, s.Admin);
        Assert.Equal("Tratamiento médico", (await Sol.ObtenerAsync(d.SolicitudPermisoId, default)).Motivo);

        Como(s, s.UsuarioOtro);
        await RechazaCon(ServiceErrorKind.NotFound, () => Sol.ObtenerAsync(d.SolicitudPermisoId, default));
        await RechazaCon(ServiceErrorKind.Forbidden, () => Sol.CalendarioAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), default));
        await RechazaCon(ServiceErrorKind.Forbidden, () => Sol.ListarAsync(new SolicitudPermisoFiltro(), default));
    }

    [Fact]
    public async Task Los_datos_de_otra_empresa_no_se_ven_ni_se_resuelven()
    {
        var s = await EscenarioAsync();
        Como(s, s.UsuarioLuis);
        var d = await Sol.CrearAsync(Req(s.Vacaciones, "2026-01-05", "2026-01-06"), default);
        var saldo = await SaldoDeAsync(s.Luis, s.Vacaciones);

        var b = await _t.CrearEmpresaAsync("Beta", "admin@b.com");
        _t.NewScope();
        _t.Current.As(b, b.AdminId);
        await RechazaCon(ServiceErrorKind.NotFound, () => Sol.ObtenerAsync(d.SolicitudPermisoId, default));
        await RechazaCon(ServiceErrorKind.NotFound, () => Apr.ResolverAsync(d.SolicitudPermisoId, new ResolverPermisoRequest { Aprobar = true }, default));
        await RechazaCon(ServiceErrorKind.NotFound, () => Saldos.MovimientosAsync(saldo.SaldoPermisoId, default));
        Assert.Equal(0, (await Sol.ListarAsync(new SolicitudPermisoFiltro(), default)).Total);
        Assert.Empty(await _t.Svc<ITiposPermisoService>().ListarAsync(false, default));
        await RechazaCon(ServiceErrorKind.NotFound, () => _t.Svc<ITiposPermisoService>().CambiarEstadoAsync(s.Vacaciones, false, default));
        // El saldo ajeno tampoco se puede ajustar creando un trabajador/tipo inexistentes en esta empresa.
        await RechazaCon(ServiceErrorKind.NotFound, () => Saldos.AjustarAsync(new AjustarSaldoRequest
        {
            TrabajadorId = s.Luis, TipoPermisoId = s.Vacaciones, PeriodoAnio = 2026, Cantidad = 5, Motivo = "Intento"
        }, default));
    }

    // 8. Saldos --------------------------------------------------------------------------
    [Fact]
    public async Task Los_ajustes_quedan_en_el_libro_con_motivo_y_no_dejan_el_saldo_por_debajo_de_lo_usado()
    {
        var s = await EscenarioAsync();
        Como(s, s.UsuarioLuis);
        var d = await Sol.CrearAsync(Req(s.Vacaciones, "2026-01-05", "2026-01-09"), default);
        Como(s, s.UsuarioSara);
        await Apr.ResolverAsync(d.SolicitudPermisoId, new ResolverPermisoRequest { Aprobar = true }, default);

        Como(s, s.Admin);
        AjustarSaldoRequest Aj(decimal c, string m = "Bono por antigüedad") => new() { TrabajadorId = s.Luis, TipoPermisoId = s.Vacaciones, PeriodoAnio = 2026, Cantidad = c, Motivo = m };
        await RechazaCon(ServiceErrorKind.Validation, () => Saldos.AjustarAsync(Aj(2, " "), default));
        await RechazaCon(ServiceErrorKind.Validation, () => Saldos.AjustarAsync(Aj(0), default));
        await RechazaCon(ServiceErrorKind.Validation, () => Saldos.AjustarAsync(Aj(-6), default));     // 10 - 6 = 4 < 5 utilizados

        var mas = await Saldos.AjustarAsync(Aj(2), default);
        Assert.Equal(12m, mas.Asignada);
        Assert.Equal(7m, mas.Disponible);
        var menos = await Saldos.AjustarAsync(Aj(-3, "Corrección"), default);
        Assert.Equal(9m, menos.Asignada);

        var libro = await Saldos.MovimientosAsync(mas.SaldoPermisoId, default);
        Assert.Equal(new byte[] { TipoMovimientoSaldo.Asignacion, TipoMovimientoSaldo.Consumo, TipoMovimientoSaldo.Asignacion, TipoMovimientoSaldo.Ajuste },
            libro.Select(m => m.TipoMovimiento).ToArray());
        Assert.All(libro.Where(m => m.TipoMovimiento is TipoMovimientoSaldo.Asignacion or TipoMovimientoSaldo.Ajuste).Skip(1), m =>
        {
            Assert.StartsWith("Ajuste:", m.Descripcion);
            Assert.Equal(s.Admin, m.RegistradoPorUsuarioId);
        });
        Assert.Contains(await _t.Db.Auditoria.AsNoTracking().ToListAsync(), a => a.Accion == "AjustarSaldoPermiso");
    }

    [Fact]
    public async Task Cada_quien_ve_sus_saldos_y_solo_con_permiso_y_alcance_los_de_otros()
    {
        var s = await EscenarioAsync();
        Como(s, s.UsuarioLuis);
        await Sol.CrearAsync(Req(s.Vacaciones, "2026-01-05", "2026-01-06"), default);
        var propios = await Saldos.ListarAsync(new SaldoFiltro(), default);
        Assert.Equal(8m, propios.Single().Disponible);   // 10 asignados - 2 reservados
        await RechazaCon(ServiceErrorKind.Forbidden, () => Saldos.ListarAsync(new SaldoFiltro { TrabajadorId = s.Otro }, default));

        Como(s, s.UsuarioSara);                                    // saldos.consultar, pero solo de sus supervisados
        Assert.Single(await Saldos.ListarAsync(new SaldoFiltro { TrabajadorId = s.Luis }, default));
        await RechazaCon(ServiceErrorKind.Forbidden, () => Saldos.ListarAsync(new SaldoFiltro { TrabajadorId = s.Otro }, default));

        Como(s, s.Admin);
        Assert.Single(await Saldos.ListarAsync(new SaldoFiltro { TrabajadorId = s.Luis }, default));
    }

    [Fact]
    public async Task Generar_los_saldos_anuales_es_idempotente_y_el_saldo_se_acumula_si_la_regla_no_reinicia()
    {
        var s = await EscenarioAsync();
        Como(s, s.Admin);
        // 3 trabajadores activos × tipo con cantidad anual.
        Assert.Equal(3, await Saldos.GenerarAnualAsync(2026, default));
        _t.NewScope(); _t.Current.As(s.E, s.Admin);
        Assert.Equal(0, await Saldos.GenerarAnualAsync(2026, default));

        // Acumulación: la regla del tipo deja de reiniciar y 2027 hereda lo no usado de 2026.
        var tipos = _t.Svc<ITiposPermisoService>();
        var regla = (await tipos.ListarAsync(false, default)).Single().Reglas.Single();
        await tipos.ActualizarReglaAsync(s.Vacaciones, regla.ReglaPermisoId, new GuardarReglaPermisoRequest { CantidadAnual = 10, ReiniciaAnualmente = false }, default);
        _t.NewScope(); _t.Current.As(s.E, s.Admin);
        Assert.Equal(3, await Saldos.GenerarAnualAsync(2027, default));
        var s2027 = await _t.Db.SaldosPermiso.AsNoTracking().FirstAsync(x => x.TrabajadorId == s.Luis && x.PeriodoAnio == 2027);
        Assert.Equal(20m, s2027.CantidadAsignada);
    }
}
