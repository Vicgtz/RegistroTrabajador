using Microsoft.EntityFrameworkCore;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Tests;

public class Bloque7Tests : IDisposable
{
    private readonly TestEnv _t = new();
    public void Dispose() => _t.Dispose();

    private static async Task RechazaCon(ServiceErrorKind kind, Func<Task> accion) =>
        Assert.Equal(kind, (await Assert.ThrowsAnyAsync<ServiceException>(accion)).Kind);

    private sealed record Esc(EmpresaInfo E, Guid UsuarioSara, Guid UsuarioLuis, Guid UsuarioOtro, Guid Sara, Guid Luis, Guid Otro);

    /// <summary>Empresa con admin (sin expediente), Sara (supervisora de Luis), Luis y Otro (sin supervisor).</summary>
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
    private IComentariosTareaService Com => _t.Svc<IComentariosTareaService>();
    private INotificacionesService Notif => _t.Svc<INotificacionesService>();

    /// <summary>Proyecto de Sara con Luis en el equipo.</summary>
    private async Task<ProyectoDetalleDto> ProyectoConLuisAsync(Esc s, string nombre = "Rediseño web")
    {
        Como(s, s.UsuarioSara);
        var p = await Proy.CrearAsync(new GuardarProyectoRequest { Nombre = nombre }, default);
        return await Proy.AsignarTrabajadoresAsync(p.ProyectoId, new AsignarTrabajadoresRequest { TrabajadorIds = new() { s.Luis } }, default);
    }

    private async Task<TareaDetalleDto> TareaDeLuisAsync(Esc s, Guid proyectoId, string titulo = "Maquetar portada", DateOnly? vence = null)
    {
        Como(s, s.UsuarioSara);
        return await Tar.CrearAsync(proyectoId, new GuardarTareaRequest { Titulo = titulo, FechaVencimiento = vence, TrabajadorIds = new() { s.Luis } }, default);
    }

    // 1. Estados -----------------------------------------------------------------------------
    [Fact]
    public async Task Los_estados_habituales_se_crean_solos_y_solo_administracion_los_configura()
    {
        var s = await EscenarioAsync();
        var estados = await _t.Svc<IEstadosProyectoService>().ListarAsync(false, default);
        Assert.Equal(new[] { "Planeación", "En proceso", "En pausa", "Finalizado", "Cancelado" }, estados.Select(e => e.Nombre));
        Assert.Equal(2, estados.Count(e => e.EsFinal));

        var nuevo = await _t.Svc<IEstadosProyectoService>().CrearAsync(new GuardarEstadoProyectoRequest { Nombre = "En revisión del cliente", Orden = 6 }, default);
        Assert.True(nuevo.Activo);
        await RechazaCon(ServiceErrorKind.Conflict, () => _t.Svc<IEstadosProyectoService>().CrearAsync(new GuardarEstadoProyectoRequest { Nombre = "en pausa", Orden = 7 }, default));

        Como(s, s.UsuarioSara);
        await RechazaCon(ServiceErrorKind.Forbidden, () => _t.Svc<IEstadosProyectoService>().CrearAsync(new GuardarEstadoProyectoRequest { Nombre = "Otro", Orden = 8 }, default));
        Assert.Equal(6, (await _t.Svc<IEstadosProyectoService>().ListarAsync(true, default)).Count);
    }

    // 2. Proyectos y visibilidad -------------------------------------------------------------
    [Fact]
    public async Task Un_proyecto_nuevo_deja_al_creador_como_responsable_y_registra_historial()
    {
        var s = await EscenarioAsync();
        Como(s, s.UsuarioSara);
        var p = await Proy.CrearAsync(new GuardarProyectoRequest { Nombre = "  Migración  ", Prioridad = PrioridadTrabajo.Alta }, default);
        Assert.Equal("Migración", p.Nombre);
        Assert.Equal("Planeación", p.Estado);
        Assert.Equal(s.Sara, p.ResponsableTrabajadorId);
        Assert.Single(p.Equipo);
        Assert.True(p.Equipo[0].EsResponsable);
        Assert.True(p.PuedeModificar && p.PuedeFinalizar && !p.PuedeCambiarResponsable);
        Assert.Single(await Proy.HistorialAsync(p.ProyectoId, default));
    }

    [Fact]
    public async Task Cada_quien_ve_solo_sus_proyectos_y_los_de_sus_supervisados()
    {
        var s = await EscenarioAsync();
        var conLuis = await ProyectoConLuisAsync(s);
        Como(s, s.UsuarioSara);
        var soloSara = await Proy.CrearAsync(new GuardarProyectoRequest { Nombre = "Interno" }, default);

        Como(s, s.UsuarioLuis);
        Assert.Equal(conLuis.ProyectoId, (await Proy.ObtenerAsync(conLuis.ProyectoId, default)).ProyectoId);
        await RechazaCon(ServiceErrorKind.NotFound, () => Proy.ObtenerAsync(soloSara.ProyectoId, default));
        Assert.Single((await Proy.ListarAsync(new ProyectoFiltro(), default)).Items);

        Como(s, s.UsuarioOtro);
        await RechazaCon(ServiceErrorKind.NotFound, () => Proy.ObtenerAsync(conLuis.ProyectoId, default));
        Assert.Empty((await Proy.ListarAsync(new ProyectoFiltro(), default)).Items);

        // Un proyecto liderado por Luis lo ve Sara, que lo supervisa y puede modificar proyectos, aunque no participe.
        _t.NewScope(); _t.Current.As(s.E, s.E.AdminId);
        var deLuis = await Proy.CrearAsync(new GuardarProyectoRequest { Nombre = "Liderado por Luis", ResponsableTrabajadorId = s.Luis }, default);
        Como(s, s.UsuarioSara);
        Assert.Equal(deLuis.ProyectoId, (await Proy.ObtenerAsync(deLuis.ProyectoId, default)).ProyectoId);
        Como(s, s.UsuarioOtro);
        await RechazaCon(ServiceErrorKind.NotFound, () => Proy.ObtenerAsync(deLuis.ProyectoId, default));

        _t.NewScope(); _t.Current.As(s.E, s.E.AdminId);
        Assert.Equal(3, (await Proy.ListarAsync(new ProyectoFiltro(), default)).Total);
    }

    [Fact]
    public async Task Los_proyectos_de_otra_empresa_no_existen_para_nadie()
    {
        var s = await EscenarioAsync();
        var p = await ProyectoConLuisAsync(s);
        var otra = await _t.CrearEmpresaAsync("Otra", "admin@o.com");
        _t.NewScope(); _t.Current.As(otra, otra.AdminId);
        await RechazaCon(ServiceErrorKind.NotFound, () => Proy.ObtenerAsync(p.ProyectoId, default));
        await RechazaCon(ServiceErrorKind.NotFound, () => Tar.ListarPorProyectoAsync(p.ProyectoId, new TareaFiltro(), default));
        Assert.Equal(0, (await Proy.ListarAsync(new ProyectoFiltro(), default)).Total);
    }

    [Fact]
    public async Task Solo_quien_administra_el_proyecto_lo_modifica_y_asigna()
    {
        var s = await EscenarioAsync();
        var p = await ProyectoConLuisAsync(s);

        Como(s, s.UsuarioLuis); // participante sin permisos de gestión
        await RechazaCon(ServiceErrorKind.Forbidden, () => Proy.ActualizarAsync(p.ProyectoId, new GuardarProyectoRequest { Nombre = "Hackeado", RowVersion = p.RowVersion }, default));
        await RechazaCon(ServiceErrorKind.Forbidden, () => Proy.AsignarTrabajadoresAsync(p.ProyectoId, new AsignarTrabajadoresRequest { TrabajadorIds = new() { s.Otro } }, default));

        Como(s, s.UsuarioSara);
        var actual = await Proy.ObtenerAsync(p.ProyectoId, default);
        var editado = await Proy.ActualizarAsync(p.ProyectoId, new GuardarProyectoRequest { Nombre = "Rediseño 2", Prioridad = PrioridadTrabajo.Urgente, RowVersion = actual.RowVersion }, default);
        Assert.Equal("Rediseño 2", editado.Nombre);
        Assert.Equal(PrioridadTrabajo.Urgente, editado.Prioridad);
        await RechazaCon(ServiceErrorKind.Validation, () => Proy.ActualizarAsync(p.ProyectoId, new GuardarProyectoRequest { Nombre = "X", RowVersion = null }, default));

        var conOtro = await Proy.AsignarTrabajadoresAsync(p.ProyectoId, new AsignarTrabajadoresRequest { TrabajadorIds = new() { s.Otro }, RolEnProyecto = "QA" }, default);
        Assert.Equal(3, conOtro.Equipo.Count);
        await RechazaCon(ServiceErrorKind.Conflict, () => Proy.AsignarTrabajadoresAsync(p.ProyectoId, new AsignarTrabajadoresRequest { TrabajadorIds = new() { s.Otro } }, default));
        await RechazaCon(ServiceErrorKind.Conflict, () => Proy.RetirarTrabajadorAsync(p.ProyectoId, s.Sara, default)); // el responsable no se retira
        Assert.Equal(2, (await Proy.RetirarTrabajadorAsync(p.ProyectoId, s.Otro, default)).Equipo.Count);
    }

    [Fact]
    public async Task Cambiar_al_responsable_es_cosa_del_administrador()
    {
        var s = await EscenarioAsync();
        var p = await ProyectoConLuisAsync(s);
        Como(s, s.UsuarioSara);
        await RechazaCon(ServiceErrorKind.Forbidden, () => Proy.CambiarResponsableAsync(p.ProyectoId, new CambiarResponsableRequest { ResponsableTrabajadorId = s.Luis, RowVersion = p.RowVersion }, default));

        _t.NewScope(); _t.Current.As(s.E, s.E.AdminId);
        var actual = await Proy.ObtenerAsync(p.ProyectoId, default);
        var nuevo = await Proy.CambiarResponsableAsync(p.ProyectoId, new CambiarResponsableRequest { ResponsableTrabajadorId = s.Luis, RowVersion = actual.RowVersion }, default);
        Assert.Equal(s.Luis, nuevo.ResponsableTrabajadorId);
        Assert.Contains(nuevo.Equipo, x => x.TrabajadorId == s.Luis && x.EsResponsable);
    }

    // 3. Finalizar y reabrir -----------------------------------------------------------------
    [Fact]
    public async Task Finalizar_lo_hace_el_responsable_y_reabrir_solo_el_administrador()
    {
        var s = await EscenarioAsync();
        var p = await ProyectoConLuisAsync(s);
        var estados = await _t.Svc<IEstadosProyectoService>().ListarAsync(false, default);
        var finalizado = estados.First(e => e.Nombre == "Finalizado").EstadoProyectoId;
        var enProceso = estados.First(e => e.Nombre == "En proceso").EstadoProyectoId;

        Como(s, s.UsuarioLuis);
        await RechazaCon(ServiceErrorKind.Forbidden, () => Proy.CambiarEstadoAsync(p.ProyectoId, new CambiarEstadoProyectoRequest { EstadoProyectoId = finalizado, RowVersion = p.RowVersion }, default));

        Como(s, s.UsuarioSara);
        var actual = await Proy.ObtenerAsync(p.ProyectoId, default);
        var avanzado = await Proy.CambiarEstadoAsync(p.ProyectoId, new CambiarEstadoProyectoRequest { EstadoProyectoId = enProceso, RowVersion = actual.RowVersion }, default);
        Assert.Equal("En proceso", avanzado.Estado);
        var cerrado = await Proy.CambiarEstadoAsync(p.ProyectoId, new CambiarEstadoProyectoRequest { EstadoProyectoId = finalizado, Comentario = "Entregado", RowVersion = avanzado.RowVersion }, default);
        Assert.True(cerrado.EstadoFinal);
        Assert.NotNull(cerrado.FechaCierreUtc);
        Assert.False(cerrado.PuedeModificar);

        // Cerrado: no se edita ni se agregan tareas, pero se conserva todo.
        await RechazaCon(ServiceErrorKind.Conflict, () => Proy.ActualizarAsync(p.ProyectoId, new GuardarProyectoRequest { Nombre = "X", RowVersion = cerrado.RowVersion }, default));
        await RechazaCon(ServiceErrorKind.Conflict, () => Tar.CrearAsync(p.ProyectoId, new GuardarTareaRequest { Titulo = "Tarde" }, default));
        await RechazaCon(ServiceErrorKind.Forbidden, () => Proy.CambiarEstadoAsync(p.ProyectoId, new CambiarEstadoProyectoRequest { EstadoProyectoId = enProceso, RowVersion = cerrado.RowVersion }, default));

        _t.NewScope(); _t.Current.As(s.E, s.E.AdminId);
        var actualAdmin = await Proy.ObtenerAsync(p.ProyectoId, default);
        Assert.True(actualAdmin.PuedeReabrir);
        var reabierto = await Proy.CambiarEstadoAsync(p.ProyectoId, new CambiarEstadoProyectoRequest { EstadoProyectoId = enProceso, Comentario = "Faltaba un entregable", RowVersion = actualAdmin.RowVersion }, default);
        Assert.False(reabierto.EstadoFinal);
        Assert.Null(reabierto.FechaCierreUtc);
        Assert.Equal(4, (await Proy.HistorialAsync(p.ProyectoId, default)).Count);
    }

    // 4. Tareas ------------------------------------------------------------------------------
    [Fact]
    public async Task Solo_se_asigna_a_quien_participa_en_el_proyecto()
    {
        var s = await EscenarioAsync();
        var p = await ProyectoConLuisAsync(s);
        Como(s, s.UsuarioSara);
        await RechazaCon(ServiceErrorKind.Validation, () => Tar.CrearAsync(p.ProyectoId, new GuardarTareaRequest { Titulo = "Tarea", TrabajadorIds = new() { s.Otro } }, default));

        var t = await Tar.CrearAsync(p.ProyectoId, new GuardarTareaRequest { Titulo = "Tarea", TrabajadorIds = new() { s.Luis } }, default);
        Assert.Equal(EstadoTarea.Pendiente, t.Estado);
        Assert.Equal(new[] { "Luis Mora" }, t.Asignados);
        await RechazaCon(ServiceErrorKind.Validation, () => Tar.AsignarAsync(t.TareaId, new AsignarTareaRequest { TrabajadorIds = new() { s.Otro } }, default));
        var con2 = await Tar.AsignarAsync(t.TareaId, new AsignarTareaRequest { TrabajadorIds = new() { s.Sara } }, default);
        Assert.Equal(2, con2.Equipo.Count);
        Assert.Single((await Tar.RetirarAsignadoAsync(t.TareaId, s.Sara, default)).Equipo);
    }

    [Fact]
    public async Task El_trabajador_avanza_su_tarea_y_el_responsable_la_revisa()
    {
        var s = await EscenarioAsync();
        var p = await ProyectoConLuisAsync(s);
        var t = await TareaDeLuisAsync(s, p.ProyectoId);

        Como(s, s.UsuarioLuis);
        var mitad = await Tar.ActualizarAvanceAsync(t.TareaId, new ActualizarAvanceRequest { PorcentajeAvance = 50, Comentario = "Mitad lista", RowVersion = t.RowVersion }, default);
        Assert.Equal(EstadoTarea.EnProceso, mitad.Estado);
        var listo = await Tar.ActualizarAvanceAsync(t.TareaId, new ActualizarAvanceRequest { PorcentajeAvance = 100, RowVersion = mitad.RowVersion }, default);
        Assert.Equal(EstadoTarea.EnRevision, listo.Estado); // no se cierra sola: la revisa el responsable
        await RechazaCon(ServiceErrorKind.Forbidden, () => Tar.RevisarAsync(t.TareaId, new RevisarTareaRequest { Aprobar = true, RowVersion = listo.RowVersion }, default));

        Como(s, s.UsuarioSara);
        var sara = (await Notif.ListarAsync(new NotificacionFiltro(), default)).Items;
        Assert.Contains(sara, n => n.Tipo == TipoNotificacionTrabajo.TareaRevision);
        await RechazaCon(ServiceErrorKind.Validation, () => Tar.RevisarAsync(t.TareaId, new RevisarTareaRequest { Aprobar = false, RowVersion = listo.RowVersion }, default)); // devolver exige observación
        var devuelta = await Tar.RevisarAsync(t.TareaId, new RevisarTareaRequest { Aprobar = false, Comentario = "Falta el menú", RowVersion = listo.RowVersion }, default);
        Assert.Equal(EstadoTarea.Reabierta, devuelta.Estado);

        Como(s, s.UsuarioLuis);
        var otra = await Tar.ActualizarAvanceAsync(t.TareaId, new ActualizarAvanceRequest { PorcentajeAvance = 100, RowVersion = devuelta.RowVersion }, default);
        Assert.Equal(EstadoTarea.EnRevision, otra.Estado);

        Como(s, s.UsuarioSara);
        var hecha = await Tar.RevisarAsync(t.TareaId, new RevisarTareaRequest { Aprobar = true, RowVersion = otra.RowVersion }, default);
        Assert.Equal(EstadoTarea.Completada, hecha.Estado);
        Assert.NotNull(hecha.FechaFinalizacionUtc);

        // Cerrada: nadie la mueve, salvo reabrirla, y la reapertura conserva lo anterior.
        Como(s, s.UsuarioLuis);
        await RechazaCon(ServiceErrorKind.Conflict, () => Tar.ActualizarAvanceAsync(t.TareaId, new ActualizarAvanceRequest { PorcentajeAvance = 80, RowVersion = hecha.RowVersion }, default));
        Como(s, s.UsuarioSara);
        var reabierta = await Tar.ReabrirAsync(t.TareaId, new MotivoTareaRequest { Motivo = "El cliente pidió cambios", RowVersion = hecha.RowVersion }, default);
        Assert.Equal(EstadoTarea.Reabierta, reabierta.Estado);
        Assert.Null(reabierta.FechaFinalizacionUtc);
        var historial = await Tar.HistorialAsync(t.TareaId, default);
        Assert.Equal(7, historial.Count); // creación, 50 %, 100 %, devuelta, 100 %, aprobada, reapertura
        Assert.Equal(EstadoTarea.Reabierta, historial[0].EstadoNuevo);
        Assert.Equal("Luis Mora", historial.First(h => h.PorcentajeNuevo == 50).ActualizadoPor);
    }

    [Fact]
    public async Task Nadie_mueve_tareas_ajenas_ni_con_estados_incoherentes()
    {
        var s = await EscenarioAsync();
        var p = await ProyectoConLuisAsync(s);
        Como(s, s.UsuarioSara);
        await Proy.AsignarTrabajadoresAsync(p.ProyectoId, new AsignarTrabajadoresRequest { TrabajadorIds = new() { s.Otro } }, default);
        var t = await TareaDeLuisAsync(s, p.ProyectoId);

        Como(s, s.UsuarioOtro); // participa en el proyecto pero la tarea no es suya
        await RechazaCon(ServiceErrorKind.Forbidden, () => Tar.ActualizarAvanceAsync(t.TareaId, new ActualizarAvanceRequest { PorcentajeAvance = 10, RowVersion = t.RowVersion }, default));
        await RechazaCon(ServiceErrorKind.Forbidden, () => Tar.ActualizarAsync(t.TareaId, new GuardarTareaRequest { Titulo = "Mía", RowVersion = t.RowVersion }, default));

        Como(s, s.UsuarioLuis);
        await RechazaCon(ServiceErrorKind.Validation, () => Tar.ActualizarAvanceAsync(t.TareaId, new ActualizarAvanceRequest { PorcentajeAvance = 40, Estado = EstadoTarea.Completada, RowVersion = t.RowVersion }, default));
        await RechazaCon(ServiceErrorKind.Validation, () => Tar.ActualizarAvanceAsync(t.TareaId, new ActualizarAvanceRequest { PorcentajeAvance = 40, Estado = EstadoTarea.EnRevision, RowVersion = t.RowVersion }, default));
        await RechazaCon(ServiceErrorKind.Validation, () => Tar.ActualizarAvanceAsync(t.TareaId, new ActualizarAvanceRequest { PorcentajeAvance = 0, RowVersion = t.RowVersion }, default)); // sin cambios
        await RechazaCon(ServiceErrorKind.Validation, () => Tar.ActualizarAvanceAsync(t.TareaId, new ActualizarAvanceRequest { PorcentajeAvance = 20, RowVersion = null! }, default));
        await RechazaCon(ServiceErrorKind.Forbidden, () => Tar.CancelarAsync(t.TareaId, new MotivoTareaRequest { Motivo = "No me gusta", RowVersion = t.RowVersion }, default));
    }

    [Fact]
    public async Task Las_tareas_vencidas_se_detectan_con_la_fecha_de_hoy_y_las_cerradas_no_cuentan()
    {
        var s = await EscenarioAsync();
        var p = await ProyectoConLuisAsync(s);
        var hoy = new DateOnly(2026, 1, 1);
        var vencida = await TareaDeLuisAsync(s, p.ProyectoId, "Atrasada", hoy.AddDays(-3));
        var vigente = await TareaDeLuisAsync(s, p.ProyectoId, "Vigente", hoy.AddDays(5));
        var cancelada = await TareaDeLuisAsync(s, p.ProyectoId, "Descartada", hoy.AddDays(-10));
        Como(s, s.UsuarioSara);
        await Tar.CancelarAsync(cancelada.TareaId, new MotivoTareaRequest { Motivo = "Ya no se necesita", RowVersion = cancelada.RowVersion }, default);

        Como(s, s.UsuarioLuis);
        var mias = await Tar.MisTareasAsync(new MisTareasFiltro(), default);
        Assert.Equal(2, mias.Total); // sin las cerradas
        Assert.Equal(p.Nombre, mias.Items[0].Proyecto);
        Assert.True(mias.Items.Single(x => x.TareaId == vencida.TareaId).Vencida);
        Assert.False(mias.Items.Single(x => x.TareaId == vigente.TareaId).Vencida);
        var soloVencidas = await Tar.MisTareasAsync(new MisTareasFiltro { SoloVencidas = true }, default);
        Assert.Equal(vencida.TareaId, Assert.Single(soloVencidas.Items).TareaId);
        Assert.Equal(1, (await Proy.ResumenAsync(default)).TareasVencidasMias);

        _t.Clock.Advance(TimeSpan.FromDays(10)); // pasa el tiempo: ahora también venció la que estaba vigente
        Assert.Equal(2, (await Tar.MisTareasAsync(new MisTareasFiltro { SoloVencidas = true }, default)).Total);
        Como(s, s.UsuarioOtro);
        Assert.Equal(0, (await Tar.MisTareasAsync(new MisTareasFiltro(), default)).Total);
    }

    [Fact]
    public async Task El_avance_del_proyecto_es_el_promedio_simple_de_sus_tareas_sin_las_canceladas()
    {
        var s = await EscenarioAsync();
        var p = await ProyectoConLuisAsync(s);
        var a = await TareaDeLuisAsync(s, p.ProyectoId, "A");
        var b = await TareaDeLuisAsync(s, p.ProyectoId, "B");
        var c = await TareaDeLuisAsync(s, p.ProyectoId, "C");
        Como(s, s.UsuarioSara);
        await Tar.CancelarAsync(c.TareaId, new MotivoTareaRequest { Motivo = "Fuera de alcance", RowVersion = c.RowVersion }, default);
        await Tar.ActualizarAvanceAsync(a.TareaId, new ActualizarAvanceRequest { PorcentajeAvance = 100, RowVersion = a.RowVersion }, default); // Sara administra: queda completada
        Assert.Equal(EstadoTarea.Completada, (await Tar.ObtenerAsync(a.TareaId, default)).Estado);

        var d = await Proy.ObtenerAsync(p.ProyectoId, default);
        Assert.Equal(2, d.TareasTotal);
        Assert.Equal(1, d.TareasCompletadas);
        Assert.Equal(50, d.Avance);
        Assert.Equal(b.TareaId, Assert.Single((await Tar.ListarPorProyectoAsync(p.ProyectoId, new TareaFiltro { IncluirCerradas = false }, default)).Items).TareaId); // ni la completada ni la cancelada
        Assert.Equal(3, (await Tar.ListarPorProyectoAsync(p.ProyectoId, new TareaFiltro(), default)).Total);
    }

    // 5. Comentarios y notificaciones --------------------------------------------------------
    [Fact]
    public async Task Los_comentarios_conservan_autor_fecha_y_mencionan_solo_a_participantes()
    {
        var s = await EscenarioAsync();
        var p = await ProyectoConLuisAsync(s);
        var t = await TareaDeLuisAsync(s, p.ProyectoId);

        Como(s, s.UsuarioLuis);
        var c1 = await Com.PublicarAsync(t.TareaId, new PublicarComentarioRequest { Contenido = "  Ya subí la maqueta  ", MencionesUsuarioIds = new() { s.UsuarioSara } }, default);
        Assert.Equal("Ya subí la maqueta", c1.Contenido);
        Assert.Equal("Luis Mora", c1.Autor);
        Assert.Equal(new[] { "Sara Sup" }, c1.Menciones);
        Assert.Equal(_t.Clock.GetUtcNow().UtcDateTime, c1.FechaCreacionUtc);
        await RechazaCon(ServiceErrorKind.Validation, () => Com.PublicarAsync(t.TareaId, new PublicarComentarioRequest { Contenido = "Hola", MencionesUsuarioIds = new() { s.UsuarioOtro } }, default));
        await RechazaCon(ServiceErrorKind.Validation, () => Com.PublicarAsync(t.TareaId, new PublicarComentarioRequest { Contenido = "   " }, default));

        Como(s, s.UsuarioSara);
        var bandeja = (await Notif.ListarAsync(new NotificacionFiltro(), default)).Items;
        Assert.Contains(bandeja, n => n.Tipo == TipoNotificacionTrabajo.TareaMencion && !n.Leida);
        _t.Clock.Advance(TimeSpan.FromMinutes(5));
        var respuesta = await Com.PublicarAsync(t.TareaId, new PublicarComentarioRequest { Contenido = "Gracias, reviso hoy", ComentarioPadreId = c1.ComentarioId }, default);
        Assert.Equal(c1.ComentarioId, respuesta.ComentarioPadreId);
        var hilo = await Com.ListarAsync(t.TareaId, default);
        Assert.Equal(2, hilo.Count);
        Assert.Equal(new[] { c1.ComentarioId, respuesta.ComentarioId }, hilo.Select(x => x.ComentarioId));
        Assert.Equal(2, (await Com.MencionablesAsync(t.TareaId, default)).Count(m => m.UsuarioId == s.UsuarioSara || m.UsuarioId == s.UsuarioLuis));

        // Luis (asignado) fue avisado del comentario de Sara.
        Como(s, s.UsuarioLuis);
        Assert.Contains((await Notif.ListarAsync(new NotificacionFiltro(), default)).Items, n => n.Tipo == TipoNotificacionTrabajo.TareaComentario);

        // Un participante sin la tarea asignada no comenta; quien no ve el proyecto ni siquiera la encuentra.
        Como(s, s.UsuarioSara);
        await Proy.AsignarTrabajadoresAsync(p.ProyectoId, new AsignarTrabajadoresRequest { TrabajadorIds = new() { s.Otro } }, default);
        Como(s, s.UsuarioOtro);
        await RechazaCon(ServiceErrorKind.Forbidden, () => Com.PublicarAsync(t.TareaId, new PublicarComentarioRequest { Contenido = "Me meto" }, default));
        Assert.Equal(2, (await Com.ListarAsync(t.TareaId, default)).Count);
    }

    [Fact]
    public async Task Las_asignaciones_notifican_y_cada_usuario_solo_toca_su_bandeja()
    {
        var s = await EscenarioAsync();
        var p = await ProyectoConLuisAsync(s);
        await TareaDeLuisAsync(s, p.ProyectoId);

        Como(s, s.UsuarioLuis);
        var recibidas = (await Notif.ListarAsync(new NotificacionFiltro(), default)).Items;
        Assert.Contains(recibidas, n => n.Tipo == TipoNotificacionTrabajo.ProyectoAsignado);
        Assert.Contains(recibidas, n => n.Tipo == TipoNotificacionTrabajo.TareaAsignada);
        Assert.Equal(recibidas.Count, await Notif.ContarNoLeidasAsync(default));

        await Notif.MarcarLeidaAsync(recibidas[0].NotificacionId, default);
        Assert.Equal(recibidas.Count - 1, await Notif.ContarNoLeidasAsync(default));

        Como(s, s.UsuarioOtro);
        await RechazaCon(ServiceErrorKind.NotFound, () => Notif.MarcarLeidaAsync(recibidas[0].NotificacionId, default));
        await RechazaCon(ServiceErrorKind.NotFound, () => Notif.ArchivarAsync(recibidas[1].NotificacionId, default));
        Assert.Equal(0, await Notif.ContarNoLeidasAsync(default));

        Como(s, s.UsuarioLuis);
        await Notif.ArchivarAsync(recibidas[1].NotificacionId, default);
        Assert.DoesNotContain((await Notif.ListarAsync(new NotificacionFiltro(), default)).Items, n => n.NotificacionId == recibidas[1].NotificacionId);
        await Notif.MarcarTodasLeidasAsync(default);
        Assert.Equal(0, await Notif.ContarNoLeidasAsync(default));
    }

    [Fact]
    public async Task Retirar_a_un_trabajador_del_proyecto_libera_sus_tareas_abiertas_pero_no_las_completadas()
    {
        var s = await EscenarioAsync();
        var p = await ProyectoConLuisAsync(s);
        var abierta = await TareaDeLuisAsync(s, p.ProyectoId, "Abierta");
        var hecha = await TareaDeLuisAsync(s, p.ProyectoId, "Hecha");
        Como(s, s.UsuarioSara);
        await Tar.ActualizarAvanceAsync(hecha.TareaId, new ActualizarAvanceRequest { PorcentajeAvance = 100, RowVersion = hecha.RowVersion }, default);

        await Proy.RetirarTrabajadorAsync(p.ProyectoId, s.Luis, default);
        Assert.Empty((await Tar.ObtenerAsync(abierta.TareaId, default)).Equipo);
        Assert.Single((await Tar.ObtenerAsync(hecha.TareaId, default)).Equipo);

        Como(s, s.UsuarioLuis);
        await RechazaCon(ServiceErrorKind.NotFound, () => Proy.ObtenerAsync(p.ProyectoId, default)); // ya no participa
    }

    [Fact]
    public async Task Las_operaciones_importantes_quedan_en_la_auditoria()
    {
        var s = await EscenarioAsync();
        var p = await ProyectoConLuisAsync(s);
        var t = await TareaDeLuisAsync(s, p.ProyectoId);
        Como(s, s.UsuarioLuis);
        await Com.PublicarAsync(t.TareaId, new PublicarComentarioRequest { Contenido = "Hola" }, default);

        var registros = await _t.Db.Auditoria.AsNoTracking().Where(a => a.EmpresaId == s.E.EmpresaId && (a.Modulo == "Proyectos" || a.Modulo == "Tareas")).ToListAsync();
        Assert.Contains(registros, a => a.Modulo == "Proyectos" && a.Accion == "Crear");
        Assert.Contains(registros, a => a.Modulo == "Proyectos" && a.Accion == "AsignarTrabajadores");
        Assert.Contains(registros, a => a.Modulo == "Tareas" && a.Accion == "Crear");
        Assert.Contains(registros, a => a.Modulo == "Tareas" && a.Accion == "Comentar" && a.UsuarioId == s.UsuarioLuis);
    }
}
