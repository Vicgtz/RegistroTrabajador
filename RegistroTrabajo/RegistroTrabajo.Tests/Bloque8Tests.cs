using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Tests;

public class Bloque8Tests : IDisposable
{
    private readonly TestEnv _t = new();
    public void Dispose() => _t.Dispose();

    private static async Task RechazaCon(ServiceErrorKind kind, Func<Task> accion) =>
        Assert.Equal(kind, (await Assert.ThrowsAnyAsync<ServiceException>(accion)).Kind);

    private sealed record Esc(EmpresaInfo E, Guid UsuarioSara, Guid UsuarioLuis, Guid UsuarioOtro, Guid Sara, Guid Luis, Guid Otro);

    /// <summary>Empresa con admin, Sara (supervisora de Luis), Luis y Otro. La fecha de prueba es el jueves 1 de enero de 2026 (zona UTC).</summary>
    private async Task<Esc> EscenarioAsync()
    {
        var e = await _t.CrearEmpresaAsync("Acme", "admin@a.com");
        var uSara = await _t.NuevoUsuarioAsync(e.EmpresaId, "sara@a.com", "Sara", "Sup", e.Roles[NombresRol.Supervisor]);
        var uLuis = await _t.NuevoUsuarioAsync(e.EmpresaId, "luis@a.com", "Luis", "Mora", e.Roles[NombresRol.Trabajador]);
        var uOtro = await _t.NuevoUsuarioAsync(e.EmpresaId, "otro@a.com", "Otro", "Ruiz", e.Roles[NombresRol.Trabajador]);
        _t.Current.As(e, e.AdminId);
        var trab = _t.Svc<ITrabajadorService>();
        var sara = (await trab.CrearAsync(Nuevo("Sara", "Sup", uSara), default)).TrabajadorId;
        var luis = (await trab.CrearAsync(Nuevo("Luis", "Mora", uLuis), default)).TrabajadorId;
        var otro = (await trab.CrearAsync(Nuevo("Otro", "Ruiz", uOtro), default)).TrabajadorId;
        await _t.Svc<ISupervisionService>().AsignarAsync(luis, new AsignarSupervisorRequest { SupervisorTrabajadorId = sara }, default);
        return new Esc(e, uSara, uLuis, uOtro, sara, luis, otro);
    }

    private static CrearTrabajadorRequest Nuevo(string nombres, string apellidos, Guid usuario) => new()
    {
        Nombres = nombres, Apellidos = apellidos, FechaIngreso = new DateOnly(2020, 6, 1), UsuarioId = usuario
    };

    private void Como(Esc s, Guid usuario)
    {
        _t.NewScope();
        _t.Current.As(s.E, usuario);
    }

    private IProyectosService Proy => _t.Svc<IProyectosService>();
    private ITareasService Tar => _t.Svc<ITareasService>();
    private ISeguimientoService Seg => _t.Svc<ISeguimientoService>();
    private INotificacionesService Notif => _t.Svc<INotificacionesService>();

    private async Task<ProyectoDetalleDto> ProyectoConLuisAsync(Esc s)
    {
        Como(s, s.UsuarioSara);
        var p = await Proy.CrearAsync(new GuardarProyectoRequest { Nombre = "Rediseño web" }, default);
        return await Proy.AsignarTrabajadoresAsync(p.ProyectoId, new AsignarTrabajadoresRequest { TrabajadorIds = new() { s.Luis } }, default);
    }

    private async Task<TareaDetalleDto> TareaDeLuisAsync(Esc s, Guid proyectoId, string titulo = "Módulo de empleados", DateOnly? vence = null)
    {
        Como(s, s.UsuarioSara);
        return await Tar.CrearAsync(proyectoId, new GuardarTareaRequest { Titulo = titulo, FechaVencimiento = vence, TrabajadorIds = new() { s.Luis } }, default);
    }

    private void Pasa(TimeSpan t) => _t.Clock.Advance(t);

    // 1. Línea de tiempo ----------------------------------------------------------------------
    [Fact]
    public async Task La_linea_de_tiempo_registra_cada_operacion_con_autor_y_valores_anterior_y_nuevo()
    {
        var s = await EscenarioAsync();
        var p = await ProyectoConLuisAsync(s);
        var t = await TareaDeLuisAsync(s, p.ProyectoId, vence: new DateOnly(2026, 1, 20));

        Pasa(TimeSpan.FromMinutes(1));
        Como(s, s.UsuarioSara);
        var editada = await Tar.ActualizarAsync(t.TareaId, new GuardarTareaRequest
        {
            Titulo = t.Titulo, FechaVencimiento = new DateOnly(2026, 1, 25), Prioridad = PrioridadTrabajo.Alta, RowVersion = t.RowVersion
        }, default);

        Pasa(TimeSpan.FromMinutes(1));
        Como(s, s.UsuarioLuis);
        var mitad = await Tar.ActualizarAvanceAsync(t.TareaId, new ActualizarAvanceRequest { PorcentajeAvance = 60, Comentario = "Formulario terminado", RowVersion = editada.RowVersion }, default);
        Pasa(TimeSpan.FromMinutes(1));
        var listo = await Tar.ActualizarAvanceAsync(t.TareaId, new ActualizarAvanceRequest { PorcentajeAvance = 100, Comentario = "Validaciones listas", RowVersion = mitad.RowVersion }, default);

        Pasa(TimeSpan.FromMinutes(1));
        Como(s, s.UsuarioSara);
        var devuelta = await Tar.RevisarAsync(t.TareaId, new RevisarTareaRequest { Aprobar = false, Comentario = "Falta validar el RFC", RowVersion = listo.RowVersion }, default);

        Pasa(TimeSpan.FromMinutes(1));
        Como(s, s.UsuarioLuis);
        var otra = await Tar.ActualizarAvanceAsync(t.TareaId, new ActualizarAvanceRequest { PorcentajeAvance = 100, RowVersion = devuelta.RowVersion }, default);

        Pasa(TimeSpan.FromMinutes(1));
        Como(s, s.UsuarioSara);
        var hecha = await Tar.RevisarAsync(t.TareaId, new RevisarTareaRequest { Aprobar = true, RowVersion = otra.RowVersion }, default);
        Pasa(TimeSpan.FromMinutes(1));
        await Tar.ReabrirAsync(t.TareaId, new MotivoTareaRequest { Motivo = "El cliente pidió cambios", RowVersion = hecha.RowVersion }, default);

        var linea = await Seg.LineaDeTiempoAsync(t.TareaId, default);

        var fecha = Assert.Single(linea, e => e.Tipo == TipoEventoTarea.FechaLimiteCambiada);
        Assert.Equal("2026-01-20", fecha.ValorAnterior);
        Assert.Equal("2026-01-25", fecha.ValorNuevo);
        Assert.Equal("Sara Sup", fecha.Usuario);
        var prioridad = Assert.Single(linea, e => e.Tipo == TipoEventoTarea.PrioridadCambiada);
        Assert.Equal(("2", "3"), (prioridad.ValorAnterior, prioridad.ValorNuevo));
        Assert.Contains("Luis", Assert.Single(linea, e => e.Tipo == TipoEventoTarea.AsignacionAgregada).ValorNuevo);
        Assert.Equal(2, linea.Count(e => e.Tipo == TipoEventoTarea.EnviadaARevision));

        var rechazo = Assert.Single(linea, e => e.Tipo == TipoEventoTarea.RevisionDevuelta);
        Assert.Equal("Falta validar el RFC", rechazo.Detalle);
        Assert.Equal(EstadoTarea.Reabierta, rechazo.EstadoNuevo);   // se fusiona con su fila de avance
        Assert.Equal((byte)100, rechazo.PorcentajeNuevo);
        Assert.Single(linea, e => e.Tipo == TipoEventoTarea.RevisionAprobada);
        var reapertura = Assert.Single(linea, e => e.Tipo == TipoEventoTarea.Reabierta);
        Assert.Equal("El cliente pidió cambios", reapertura.Detalle);

        // Sin duplicados: solo quedan como "avance" la creación y el 60 %.
        Assert.Equal(2, linea.Count(e => e.Tipo == TipoEventoTarea.AvanceActualizado));
        Assert.Equal(linea.OrderByDescending(e => e.FechaUtc).Select(e => e.FechaUtc), linea.Select(e => e.FechaUtc));
        Assert.Equal(TipoEventoTarea.Reabierta, linea[0].Tipo);

        var revisiones = await Seg.RevisionesAsync(t.TareaId, default);
        Assert.Equal(new[] { ResultadoRevision.Aprobada, ResultadoRevision.Devuelta }, revisiones.Select(r => r.Resultado));
        Assert.Equal("Falta validar el RFC", revisiones[1].Observacion);
        Assert.Equal("Sara Sup", revisiones[0].Revisor);
    }

    [Fact]
    public async Task El_historial_se_conserva_aunque_cambie_el_responsable_de_la_tarea()
    {
        var s = await EscenarioAsync();
        var p = await ProyectoConLuisAsync(s);
        var t = await TareaDeLuisAsync(s, p.ProyectoId);
        Como(s, s.UsuarioSara);
        await Proy.AsignarTrabajadoresAsync(p.ProyectoId, new AsignarTrabajadoresRequest { TrabajadorIds = new() { s.Otro } }, default);

        Como(s, s.UsuarioLuis);
        var mitad = await Tar.ActualizarAvanceAsync(t.TareaId, new ActualizarAvanceRequest { PorcentajeAvance = 40, RowVersion = t.RowVersion }, default);

        Pasa(TimeSpan.FromMinutes(1));
        Como(s, s.UsuarioSara);
        await Tar.AsignarAsync(t.TareaId, new AsignarTareaRequest { TrabajadorIds = new() { s.Otro } }, default);
        Pasa(TimeSpan.FromMinutes(1));
        var sinLuis = await Tar.RetirarAsignadoAsync(t.TareaId, s.Luis, default);
        Assert.Contains("Otro", Assert.Single(sinLuis.Asignados));

        var linea = await Seg.LineaDeTiempoAsync(t.TareaId, default);
        Assert.Contains(linea, e => e.Tipo == TipoEventoTarea.AvanceActualizado && e.PorcentajeNuevo == 40 && e.Usuario == "Luis Mora");
        Assert.Equal(2, linea.Count(e => e.Tipo == TipoEventoTarea.AsignacionAgregada));
        Assert.Contains("Luis", Assert.Single(linea, e => e.Tipo == TipoEventoTarea.AsignacionRetirada).ValorAnterior);

        // Luis ya no la tiene asignada: recibe el aviso, no la mueve, pero sigue viendo el historial por participar en el proyecto.
        Como(s, s.UsuarioLuis);
        Assert.Contains((await Notif.ListarAsync(new NotificacionFiltro(), default)).Items, n => n.Tipo == TipoNotificacionTrabajo.TareaRetirada);
        await RechazaCon(ServiceErrorKind.Forbidden, () => Tar.ActualizarAvanceAsync(t.TareaId, new ActualizarAvanceRequest { PorcentajeAvance = 50, RowVersion = mitad.RowVersion }, default));
        Assert.NotEmpty(await Seg.LineaDeTiempoAsync(t.TareaId, default));
    }

    [Fact]
    public async Task Retirar_a_alguien_del_proyecto_deja_constancia_en_sus_tareas_abiertas()
    {
        var s = await EscenarioAsync();
        var p = await ProyectoConLuisAsync(s);
        var t = await TareaDeLuisAsync(s, p.ProyectoId);
        Como(s, s.UsuarioSara);
        await Proy.RetirarTrabajadorAsync(p.ProyectoId, s.Luis, default);
        var linea = await Seg.LineaDeTiempoAsync(t.TareaId, default);
        var retiro = Assert.Single(linea, e => e.Tipo == TipoEventoTarea.AsignacionRetirada);
        Assert.Equal("Retirado del proyecto", retiro.Detalle);
    }

    [Fact]
    public async Task Los_comentarios_quedan_en_la_linea_de_tiempo_y_las_menciones_notifican()
    {
        var s = await EscenarioAsync();
        var p = await ProyectoConLuisAsync(s);
        var t = await TareaDeLuisAsync(s, p.ProyectoId);
        Como(s, s.UsuarioSara);
        var menciona = (await _t.Svc<IComentariosTareaService>().MencionablesAsync(t.TareaId, default)).First(u => u.UsuarioId == s.UsuarioLuis);
        await _t.Svc<IComentariosTareaService>().PublicarAsync(t.TareaId, new PublicarComentarioRequest { Contenido = "Revisa el diseño", MencionesUsuarioIds = new() { menciona.UsuarioId } }, default);
        Assert.Contains(await Seg.LineaDeTiempoAsync(t.TareaId, default), e => e.Tipo == TipoEventoTarea.Comentario && e.Detalle == "Revisa el diseño");
        Como(s, s.UsuarioLuis);
        Assert.Contains((await Notif.ListarAsync(new NotificacionFiltro(), default)).Items, n => n.Tipo == TipoNotificacionTrabajo.TareaMencion);
    }

    [Fact]
    public async Task Cambiar_la_fecha_limite_avisa_a_los_asignados()
    {
        var s = await EscenarioAsync();
        var p = await ProyectoConLuisAsync(s);
        var t = await TareaDeLuisAsync(s, p.ProyectoId, vence: new DateOnly(2026, 1, 20));
        Como(s, s.UsuarioSara);
        await Tar.ActualizarAsync(t.TareaId, new GuardarTareaRequest { Titulo = t.Titulo, FechaVencimiento = new DateOnly(2026, 2, 1), RowVersion = t.RowVersion }, default);
        Como(s, s.UsuarioLuis);
        Assert.Contains((await Notif.ListarAsync(new NotificacionFiltro(), default)).Items, n => n.Tipo == TipoNotificacionTrabajo.TareaFechaLimite);
    }

    [Fact]
    public async Task Quien_no_tiene_acceso_al_proyecto_no_ve_la_linea_de_tiempo_ni_las_revisiones()
    {
        var s = await EscenarioAsync();
        var p = await ProyectoConLuisAsync(s);
        var t = await TareaDeLuisAsync(s, p.ProyectoId);

        Como(s, s.UsuarioOtro); // no participa
        await RechazaCon(ServiceErrorKind.NotFound, () => Seg.LineaDeTiempoAsync(t.TareaId, default));
        await RechazaCon(ServiceErrorKind.NotFound, () => Seg.RevisionesAsync(t.TareaId, default));

        var otra = await _t.CrearEmpresaAsync("Otra", "admin@o.com");
        _t.NewScope(); _t.Current.As(otra, otra.AdminId);
        await RechazaCon(ServiceErrorKind.NotFound, () => Seg.LineaDeTiempoAsync(t.TareaId, default));
        var panel = await Seg.PanelAsync(new SeguimientoFiltro(), default);
        Assert.Equal(0, panel.TotalVencidas + panel.TotalPendientesRevision + panel.TotalReabiertas);
    }

    // 2. Transiciones --------------------------------------------------------------------------
    [Fact]
    public void La_matriz_de_transiciones_solo_deja_cerrar_a_quien_revisa()
    {
        Assert.DoesNotContain(EstadoTarea.Completada, TransicionesTarea.DestinosAvance(EstadoTarea.EnProceso, revisa: false));
        Assert.Contains(EstadoTarea.Completada, TransicionesTarea.DestinosAvance(EstadoTarea.EnProceso, revisa: true));
        Assert.Empty(TransicionesTarea.DestinosAvance(EstadoTarea.Completada, revisa: true));
        Assert.Empty(TransicionesTarea.DestinosAvance(EstadoTarea.Cancelada, revisa: true));

        Assert.Equal(new[] { EstadoTarea.Completada, EstadoTarea.Reabierta }, TransicionesTarea.Destinos(EstadoTarea.EnRevision, puedeAvance: false, revisa: true, gestiona: false));
        Assert.Equal(new[] { EstadoTarea.Reabierta }, TransicionesTarea.Destinos(EstadoTarea.Completada, puedeAvance: false, revisa: true, gestiona: true));
        Assert.Empty(TransicionesTarea.Destinos(EstadoTarea.EnRevision, puedeAvance: false, revisa: false, gestiona: false));
    }

    [Fact]
    public async Task El_detalle_de_la_tarea_expone_solo_las_transiciones_que_el_usuario_puede_hacer()
    {
        var s = await EscenarioAsync();
        var p = await ProyectoConLuisAsync(s);
        var t = await TareaDeLuisAsync(s, p.ProyectoId);

        Como(s, s.UsuarioLuis);
        var luis = await Tar.ObtenerAsync(t.TareaId, default);
        Assert.Contains(EstadoTarea.EnRevision, luis.TransicionesPermitidas);
        Assert.DoesNotContain(EstadoTarea.Completada, luis.TransicionesPermitidas);
        Assert.DoesNotContain(EstadoTarea.Cancelada, luis.TransicionesPermitidas);

        var listo = await Tar.ActualizarAvanceAsync(t.TareaId, new ActualizarAvanceRequest { PorcentajeAvance = 100, RowVersion = luis.RowVersion }, default);
        Como(s, s.UsuarioSara);
        var sara = await Tar.ObtenerAsync(t.TareaId, default);
        Assert.Contains(EstadoTarea.Completada, sara.TransicionesPermitidas);
        Assert.Contains(EstadoTarea.Reabierta, sara.TransicionesPermitidas);
        Assert.Equal(EstadoTarea.EnRevision, listo.Estado);
    }

    // 3. Retrasos y pendientes ----------------------------------------------------------------
    [Fact]
    public async Task El_panel_clasifica_vencidas_proximas_en_revision_y_reabiertas()
    {
        var s = await EscenarioAsync();
        var p = await ProyectoConLuisAsync(s);
        var vencida = await TareaDeLuisAsync(s, p.ProyectoId, "Atrasada", new DateOnly(2025, 12, 29));
        var proxima = await TareaDeLuisAsync(s, p.ProyectoId, "Próxima", new DateOnly(2026, 1, 5));   // lunes: 2 días hábiles
        var lejana = await TareaDeLuisAsync(s, p.ProyectoId, "Lejana", new DateOnly(2026, 1, 30));
        var enRevision = await TareaDeLuisAsync(s, p.ProyectoId, "Lista");
        var rechazada = await TareaDeLuisAsync(s, p.ProyectoId, "Rechazada");

        Como(s, s.UsuarioLuis);
        var l = await Tar.ActualizarAvanceAsync(enRevision.TareaId, new ActualizarAvanceRequest { PorcentajeAvance = 100, RowVersion = enRevision.RowVersion }, default);
        var r = await Tar.ActualizarAvanceAsync(rechazada.TareaId, new ActualizarAvanceRequest { PorcentajeAvance = 100, RowVersion = rechazada.RowVersion }, default);
        Como(s, s.UsuarioSara);
        await Tar.RevisarAsync(rechazada.TareaId, new RevisarTareaRequest { Aprobar = false, Comentario = "Rehacer", RowVersion = r.RowVersion }, default);

        var panel = await Seg.PanelAsync(new SeguimientoFiltro(), default);
        Assert.True(panel.VerEquipo);
        Assert.Equal(vencida.TareaId, Assert.Single(panel.Vencidas).TareaId);
        Assert.Equal(3, panel.Vencidas[0].DiasRetraso);                // 30 y 31 de diciembre y 1 de enero
        Assert.Equal(proxima.TareaId, Assert.Single(panel.ProximasAVencer).TareaId);
        Assert.Equal(2, panel.ProximasAVencer[0].DiasParaVencer);
        Assert.DoesNotContain(panel.ProximasAVencer, x => x.TareaId == lejana.TareaId);
        Assert.Equal(enRevision.TareaId, Assert.Single(panel.PendientesRevision).TareaId);
        Assert.True(panel.PendientesRevision[0].PuedeRevisar);
        Assert.Equal(rechazada.TareaId, Assert.Single(panel.Reabiertas).TareaId);
        Assert.Equal(l.RowVersion, panel.PendientesRevision[0].RowVersion);
    }

    [Fact]
    public async Task Una_fecha_limite_en_fin_de_semana_no_vence_hasta_pasado_el_siguiente_dia_habil()
    {
        var s = await EscenarioAsync();
        var p = await ProyectoConLuisAsync(s);
        var t = await TareaDeLuisAsync(s, p.ProyectoId, "Entrega del sábado", new DateOnly(2026, 1, 3)); // sábado

        Pasa(TimeSpan.FromDays(3)); // domingo 4
        Como(s, s.UsuarioSara);
        Assert.False((await Tar.ObtenerAsync(t.TareaId, default)).Vencida);
        Pasa(TimeSpan.FromDays(1)); // lunes 5: es su día hábil efectivo
        Assert.False((await Tar.ObtenerAsync(t.TareaId, default)).Vencida);
        Pasa(TimeSpan.FromDays(1)); // martes 6
        var vencida = await Tar.ObtenerAsync(t.TareaId, default);
        Assert.True(vencida.Vencida);
        Assert.Equal(EstadoTarea.Pendiente, vencida.Estado);   // vencer no cambia el estado real
        Assert.Equal(t.TareaId, Assert.Single((await Seg.PanelAsync(new SeguimientoFiltro(), default)).Vencidas).TareaId);
    }

    [Fact]
    public async Task Los_festivos_de_la_empresa_no_cuentan_como_dias_habiles()
    {
        var s = await EscenarioAsync();
        var p = await ProyectoConLuisAsync(s);
        var t = await TareaDeLuisAsync(s, p.ProyectoId, "Entrega del festivo", new DateOnly(2026, 1, 5)); // lunes
        _t.NewScope(); _t.Current.As(s.E, s.E.AdminId);
        await _t.Svc<ICalendarioLaboralService>().CrearFestivoAsync(new GuardarDiaFestivoRequest { Fecha = new DateOnly(2026, 1, 5), Nombre = "Festivo de prueba" }, default);

        Pasa(TimeSpan.FromDays(5)); // martes 6: el lunes era festivo, el vencimiento efectivo es hoy
        Como(s, s.UsuarioSara);
        Assert.False((await Tar.ObtenerAsync(t.TareaId, default)).Vencida);
        Pasa(TimeSpan.FromDays(1)); // miércoles 7
        Assert.True((await Tar.ObtenerAsync(t.TareaId, default)).Vencida);
    }

    [Fact]
    public async Task La_alerta_de_sin_actualizacion_solo_aparece_si_la_empresa_la_configura()
    {
        var s = await EscenarioAsync();
        var p = await ProyectoConLuisAsync(s);
        var t = await TareaDeLuisAsync(s, p.ProyectoId);
        Pasa(TimeSpan.FromDays(5)); // martes 6: 3 días hábiles desde el jueves 1

        Como(s, s.UsuarioSara);
        var sin = await Seg.PanelAsync(new SeguimientoFiltro(), default);
        Assert.Null(sin.DiasSinActualizacion);
        Assert.Empty(sin.SinActualizacion);

        await RechazaCon(ServiceErrorKind.Forbidden, () => Seg.GuardarConfiguracionAsync(new ConfiguracionSeguimientoDto { DiasSinActualizacion = 2 }, default));
        _t.NewScope(); _t.Current.As(s.E, s.E.AdminId);
        await RechazaCon(ServiceErrorKind.Validation, () => Seg.GuardarConfiguracionAsync(new ConfiguracionSeguimientoDto { DiasSinActualizacion = 400 }, default));
        await Seg.GuardarConfiguracionAsync(new ConfiguracionSeguimientoDto { DiasAlertaVencimiento = 1, DiasSinActualizacion = 2 }, default);
        Assert.Equal((byte)1, (await Seg.ObtenerConfiguracionAsync(default)).DiasAlertaVencimiento);

        Como(s, s.UsuarioSara);
        var con = await Seg.PanelAsync(new SeguimientoFiltro(), default);
        Assert.Equal(t.TareaId, Assert.Single(con.SinActualizacion).TareaId);
        Assert.Equal(3, con.SinActualizacion[0].DiasHabilesSinActualizar);

        // Actualizar la tarea reinicia el conteo.
        Como(s, s.UsuarioLuis);
        await Tar.ActualizarAvanceAsync(t.TareaId, new ActualizarAvanceRequest { PorcentajeAvance = 10, RowVersion = t.RowVersion }, default);
        Como(s, s.UsuarioSara);
        Assert.Empty((await Seg.PanelAsync(new SeguimientoFiltro(), default)).SinActualizacion);
    }

    [Fact]
    public async Task El_trabajador_ve_solo_su_seguimiento_y_el_equipo_solo_con_el_permiso()
    {
        var s = await EscenarioAsync();
        var p = await ProyectoConLuisAsync(s);
        Como(s, s.UsuarioSara);
        await Proy.AsignarTrabajadoresAsync(p.ProyectoId, new AsignarTrabajadoresRequest { TrabajadorIds = new() { s.Otro } }, default);
        var deLuis = await TareaDeLuisAsync(s, p.ProyectoId, "De Luis", new DateOnly(2025, 12, 29));
        Como(s, s.UsuarioSara);
        await Tar.CrearAsync(p.ProyectoId, new GuardarTareaRequest { Titulo = "De Otro", FechaVencimiento = new DateOnly(2025, 12, 29), TrabajadorIds = new() { s.Otro } }, default);

        Assert.Equal(2, (await Seg.PanelAsync(new SeguimientoFiltro(), default)).TotalVencidas);
        Assert.Equal(deLuis.TareaId, Assert.Single((await Seg.PanelAsync(new SeguimientoFiltro { TrabajadorId = s.Luis }, default)).Vencidas).TareaId);

        Como(s, s.UsuarioLuis);
        var propio = await Seg.PanelAsync(new SeguimientoFiltro { TrabajadorId = s.Otro }, default); // el filtro no amplía su alcance
        Assert.False(propio.VerEquipo);
        Assert.Equal(deLuis.TareaId, Assert.Single(propio.Vencidas).TareaId);
    }

    // 4. Revisión -------------------------------------------------------------------------------
    [Fact]
    public async Task La_bandeja_de_revision_muestra_lo_pendiente_y_solo_el_responsable_puede_actuar()
    {
        var s = await EscenarioAsync();
        var p = await ProyectoConLuisAsync(s);
        var t = await TareaDeLuisAsync(s, p.ProyectoId);
        Como(s, s.UsuarioLuis);
        await Tar.ActualizarAvanceAsync(t.TareaId, new ActualizarAvanceRequest { PorcentajeAvance = 100, RowVersion = t.RowVersion }, default);

        var propia = await Seg.PendientesRevisionAsync(new SeguimientoFiltro(), default);
        Assert.False(Assert.Single(propia).PuedeRevisar);

        Como(s, s.UsuarioSara);
        var pendiente = Assert.Single(await Seg.PendientesRevisionAsync(new SeguimientoFiltro(), default));
        Assert.True(pendiente.PuedeRevisar);
        await Tar.RevisarAsync(t.TareaId, new RevisarTareaRequest { Aprobar = true, RowVersion = pendiente.RowVersion }, default);
        Assert.Empty(await Seg.PendientesRevisionAsync(new SeguimientoFiltro(), default));
        Assert.Equal(ResultadoRevision.Aprobada, Assert.Single(await Seg.RevisionesAsync(t.TareaId, default)).Resultado);

        Como(s, s.UsuarioOtro);
        Assert.Empty(await Seg.PendientesRevisionAsync(new SeguimientoFiltro(), default));
    }
}

