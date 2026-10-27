using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RegistroTrabajo.Data;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Tests;

public class Bloque9Tests : IDisposable
{
    private readonly TestEnv _t = new();
    public void Dispose() => _t.Dispose();

    private static async Task RechazaCon(ServiceErrorKind kind, Func<Task> accion) =>
        Assert.Equal(kind, (await Assert.ThrowsAnyAsync<ServiceException>(accion)).Kind);

    private sealed record Esc(EmpresaInfo E, Guid UsuarioSara, Guid UsuarioLuis, Guid UsuarioOtro, Guid Sara, Guid Luis, Guid Otro, Guid Vacaciones);

    /// <summary>Empresa con admin, Sara (supervisora de Luis), Luis y Otro, y el tipo de permiso Vacaciones. El reloj está en jueves 2026-01-01.</summary>
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
        var tipos = _t.Svc<ITiposPermisoService>();
        var tipo = await tipos.CrearAsync(new GuardarTipoPermisoRequest { Nombre = "Vacaciones", UnidadBase = UnidadPermiso.Dias }, default);
        await tipos.CrearReglaAsync(tipo.TipoPermisoId, new GuardarReglaPermisoRequest { CantidadAnual = 10 }, default);
        return new Esc(e, uSara, uLuis, uOtro, sara, luis, otro, tipo.TipoPermisoId);
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

    private INotificacionesService Notif => _t.Svc<INotificacionesService>();
    private IPreferenciasNotificacionService Pref => _t.Svc<IPreferenciasNotificacionService>();
    private IProyectosService Proy => _t.Svc<IProyectosService>();
    private ITareasService Tar => _t.Svc<ITareasService>();

    private async Task<ProyectoDetalleDto> ProyectoConLuisAsync(Esc s)
    {
        Como(s, s.UsuarioSara);
        var p = await Proy.CrearAsync(new GuardarProyectoRequest { Nombre = "Rediseño web" }, default);
        return await Proy.AsignarTrabajadoresAsync(p.ProyectoId, new AsignarTrabajadoresRequest { TrabajadorIds = new() { s.Luis } }, default);
    }

    private async Task<TareaDetalleDto> TareaDeLuisAsync(Esc s, Guid proyectoId, string titulo = "Maquetar portada")
    {
        Como(s, s.UsuarioSara);
        return await Tar.CrearAsync(proyectoId, new GuardarTareaRequest { Titulo = titulo, TrabajadorIds = new() { s.Luis } }, default);
    }

    private async Task<List<NotificacionDto>> BandejaDeAsync(Esc s, Guid usuario, NotificacionFiltro? f = null)
    {
        Como(s, usuario);
        return (await Notif.ListarAsync(f ?? new NotificacionFiltro(), default)).Items.ToList();
    }

    // 1. Bandeja personal ----------------------------------------------------------------------
    [Fact]
    public async Task Cada_usuario_ve_y_modifica_solo_su_bandeja()
    {
        var s = await EscenarioAsync();
        var p = await ProyectoConLuisAsync(s);
        await TareaDeLuisAsync(s, p.ProyectoId);

        var deLuis = await BandejaDeAsync(s, s.UsuarioLuis);
        Assert.NotEmpty(deLuis);
        Assert.All(deLuis, n => Assert.False(n.Leida));
        Assert.Empty(await BandejaDeAsync(s, s.UsuarioOtro));

        var ajena = deLuis[0].NotificacionId;
        Como(s, s.UsuarioOtro);
        await RechazaCon(ServiceErrorKind.NotFound, () => Notif.MarcarLeidaAsync(ajena, default));
        await RechazaCon(ServiceErrorKind.NotFound, () => Notif.ArchivarAsync(ajena, default));
        await RechazaCon(ServiceErrorKind.NotFound, () => Notif.DesarchivarAsync(ajena, default));
        await RechazaCon(ServiceErrorKind.NotFound, () => Notif.ResolverDestinoAsync(ajena, default));

        var otra = await _t.CrearEmpresaAsync("Otra", "admin@o.com");
        _t.NewScope(); _t.Current.As(otra, otra.AdminId);
        await RechazaCon(ServiceErrorKind.NotFound, () => Notif.MarcarLeidaAsync(ajena, default));
        Assert.Equal(0, (await Notif.ResumenAsync(default)).NoLeidas);
    }

    [Fact]
    public async Task Leer_archivar_y_desarchivar_cambian_el_estado_sin_tocar_el_recurso()
    {
        var s = await EscenarioAsync();
        var p = await ProyectoConLuisAsync(s);
        var t = await TareaDeLuisAsync(s, p.ProyectoId);
        var tipo = (await BandejaDeAsync(s, s.UsuarioLuis)).Single(n => n.Tipo == TipoNotificacionTrabajo.TareaAsignada);
        Assert.False(tipo.Archivada);

        Como(s, s.UsuarioLuis);
        var antes = (await Notif.ResumenAsync(default)).NoLeidas;
        await Notif.ArchivarAsync(tipo.NotificacionId, default);
        Assert.Equal(antes - 1, (await Notif.ResumenAsync(default)).NoLeidas);
        Assert.DoesNotContain((await Notif.ListarAsync(new NotificacionFiltro(), default)).Items, n => n.NotificacionId == tipo.NotificacionId);

        // El historial conserva lo archivado y no elimina nada.
        var historial = (await Notif.ListarAsync(new NotificacionFiltro { Estado = EstadoNotificacionFiltro.Archivadas }, default)).Items;
        var archivada = Assert.Single(historial);
        Assert.True(archivada.Archivada && archivada.Leida);
        Assert.Equal(tipo.NotificacionId, archivada.NotificacionId);

        await Notif.DesarchivarAsync(tipo.NotificacionId, default);
        Assert.Contains((await Notif.ListarAsync(new NotificacionFiltro(), default)).Items, n => n.NotificacionId == tipo.NotificacionId && !n.Archivada);
        Assert.Equal(EstadoTarea.Pendiente, (await Tar.ObtenerAsync(t.TareaId, default)).Estado);
    }

    [Fact]
    public async Task Marcar_todas_como_leidas_deja_el_contador_en_cero()
    {
        var s = await EscenarioAsync();
        var p = await ProyectoConLuisAsync(s);
        await TareaDeLuisAsync(s, p.ProyectoId);
        Como(s, s.UsuarioLuis);
        Assert.True(await Notif.ContarNoLeidasAsync(default) > 0);
        await Notif.MarcarTodasLeidasAsync(default);
        Assert.Equal(0, await Notif.ContarNoLeidasAsync(default));
        Assert.Equal(0, (await Notif.ResumenAsync(default)).NoLeidas);
        Assert.NotEmpty((await Notif.ListarAsync(new NotificacionFiltro { Estado = EstadoNotificacionFiltro.Leidas }, default)).Items);
    }

    // 2. Categorías y filtros ----------------------------------------------------------------
    [Fact]
    public async Task Las_notificaciones_se_filtran_por_categoria_texto_estado_y_fecha()
    {
        var s = await EscenarioAsync();
        var p = await ProyectoConLuisAsync(s);
        var t = await TareaDeLuisAsync(s, p.ProyectoId, "Maquetar portada");
        Como(s, s.UsuarioSara);
        var menciona = (await _t.Svc<IComentariosTareaService>().MencionablesAsync(t.TareaId, default)).Single(u => u.UsuarioId == s.UsuarioLuis);
        await _t.Svc<IComentariosTareaService>().PublicarAsync(t.TareaId, new PublicarComentarioRequest { Contenido = "Mira esto", MencionesUsuarioIds = new() { menciona.UsuarioId } }, default);

        var todas = await BandejaDeAsync(s, s.UsuarioLuis);
        Assert.Contains(todas, n => n.Categoria == CategoriaNotificacion.Proyectos);
        Assert.Contains(todas, n => n.Categoria == CategoriaNotificacion.Tareas);
        Assert.Contains(todas, n => n.Categoria == CategoriaNotificacion.Menciones);

        var menciones = await BandejaDeAsync(s, s.UsuarioLuis, new NotificacionFiltro { Categoria = CategoriaNotificacion.Menciones });
        Assert.All(menciones, n => Assert.Equal(TipoNotificacionTrabajo.TareaMencion, n.Tipo));
        Assert.Single(menciones);

        Assert.All(await BandejaDeAsync(s, s.UsuarioLuis, new NotificacionFiltro { Buscar = "maquetar" }), n => Assert.Contains("Maquetar", n.Mensaje));
        Assert.NotEmpty(await BandejaDeAsync(s, s.UsuarioLuis, new NotificacionFiltro { Desde = new DateOnly(2026, 1, 1), Hasta = new DateOnly(2026, 1, 1) }));
        Assert.Empty(await BandejaDeAsync(s, s.UsuarioLuis, new NotificacionFiltro { Desde = new DateOnly(2026, 1, 2) }));
        Assert.Empty(await BandejaDeAsync(s, s.UsuarioLuis, new NotificacionFiltro { Hasta = new DateOnly(2025, 12, 31) }));
        Assert.NotEmpty(await BandejaDeAsync(s, s.UsuarioLuis, new NotificacionFiltro { Estado = EstadoNotificacionFiltro.NoLeidas }));

        Como(s, s.UsuarioLuis);
        await RechazaCon(ServiceErrorKind.Validation, () => Notif.ListarAsync(new NotificacionFiltro { Categoria = "otra" }, default));
        await RechazaCon(ServiceErrorKind.Validation, () => Notif.ListarAsync(new NotificacionFiltro { Desde = new DateOnly(2026, 2, 1), Hasta = new DateOnly(2026, 1, 1) }, default));

        var resumen = await Notif.ResumenAsync(default);
        Assert.Equal(todas.Count, resumen.NoLeidas);
        Assert.Equal(1, resumen.NoLeidasPorCategoria[CategoriaNotificacion.Menciones]);
        Assert.Equal(0, resumen.NoLeidasPorCategoria[CategoriaNotificacion.Permisos]);
    }

    [Fact]
    public async Task La_bandeja_se_pagina()
    {
        var s = await EscenarioAsync();
        var p = await ProyectoConLuisAsync(s);
        for (var i = 0; i < 4; i++) await TareaDeLuisAsync(s, p.ProyectoId, $"Tarea {i}");
        Como(s, s.UsuarioLuis);
        var pagina = await Notif.ListarAsync(new NotificacionFiltro { Page = 2, PageSize = 2 }, default);
        Assert.Equal(2, pagina.Items.Count);
        Assert.True(pagina.Total >= 5);
    }

    // 3. Deduplicación, obligatorias y preferencias -----------------------------------------
    [Fact]
    public async Task Un_mismo_evento_no_avisa_dos_veces_al_mismo_usuario()
    {
        var s = await EscenarioAsync();
        Como(s, s.UsuarioSara);
        var db = _t.Svc<RegistroTrabajoDbContext>();
        var ahora = _t.Clock.GetUtcNow().UtcDateTime;
        await NotificacionesHelper.NotificarAsync(db, s.E.EmpresaId, new[] { s.UsuarioLuis }, TipoNotificacionTrabajo.TareaComentario, "T", "M", "Tarea", Guid.NewGuid().ToString(), ahora, default, "c:1");
        await NotificacionesHelper.NotificarAsync(db, s.E.EmpresaId, new[] { s.UsuarioLuis, s.UsuarioOtro }, TipoNotificacionTrabajo.TareaComentario, "T", "M", "Tarea", Guid.NewGuid().ToString(), ahora, default, "c:1");
        await db.SaveChangesAsync();
        // Segundo guardado, ya persistido: tampoco duplica.
        await NotificacionesHelper.NotificarAsync(db, s.E.EmpresaId, new[] { s.UsuarioLuis }, TipoNotificacionTrabajo.TareaComentario, "T", "M", "Tarea", Guid.NewGuid().ToString(), ahora, default, "c:1");
        await db.SaveChangesAsync();

        Assert.Equal(1, await db.Notificaciones.CountAsync(n => n.UsuarioDestinoId == s.UsuarioLuis && n.ClaveDeduplicacion == "c:1"));
        Assert.Equal(1, await db.Notificaciones.CountAsync(n => n.UsuarioDestinoId == s.UsuarioOtro && n.ClaveDeduplicacion == "c:1"));
    }

    [Fact]
    public async Task Las_preferencias_silencian_lo_opcional_pero_nunca_lo_obligatorio()
    {
        var s = await EscenarioAsync();
        Como(s, s.UsuarioLuis);
        var inicial = await Pref.ListarAsync(default);
        Assert.Equal(CategoriaNotificacion.Todas, inicial.Select(c => c.Categoria));
        Assert.All(inicial, c => Assert.True(c.Activa));
        Assert.True(inicial.Single(c => c.Categoria == CategoriaNotificacion.Permisos).TodasObligatorias);
        Assert.False(inicial.Single(c => c.Categoria == CategoriaNotificacion.Menciones).TieneObligatorias);

        // Los permisos son obligatorios: no se pueden desactivar.
        await RechazaCon(ServiceErrorKind.Validation, () => Pref.GuardarAsync(new GuardarPreferenciasRequest
        { Categorias = new() { new GuardarPreferenciaCategoria { Categoria = CategoriaNotificacion.Permisos, Activa = false } } }, default));
        await RechazaCon(ServiceErrorKind.Validation, () => Pref.GuardarAsync(new GuardarPreferenciasRequest
        { Categorias = new() { new GuardarPreferenciaCategoria { Categoria = "inventada", Activa = true } } }, default));

        var guardadas = await Pref.GuardarAsync(new GuardarPreferenciasRequest
        {
            Categorias = new()
            {
                new GuardarPreferenciaCategoria { Categoria = CategoriaNotificacion.Menciones, Activa = false },
                new GuardarPreferenciaCategoria { Categoria = CategoriaNotificacion.Tareas, Activa = false, Prioritaria = true }
            }
        }, default);
        Assert.False(guardadas.Single(c => c.Categoria == CategoriaNotificacion.Menciones).Activa);
        Assert.True(guardadas.Single(c => c.Categoria == CategoriaNotificacion.Tareas).Prioritaria);

        var p = await ProyectoConLuisAsync(s);
        var t = await TareaDeLuisAsync(s, p.ProyectoId);
        Como(s, s.UsuarioSara);
        var menciona = (await _t.Svc<IComentariosTareaService>().MencionablesAsync(t.TareaId, default)).Single(u => u.UsuarioId == s.UsuarioLuis);
        await _t.Svc<IComentariosTareaService>().PublicarAsync(t.TareaId, new PublicarComentarioRequest { Contenido = "Hola", MencionesUsuarioIds = new() { menciona.UsuarioId } }, default);

        var bandeja = await BandejaDeAsync(s, s.UsuarioLuis);
        Assert.DoesNotContain(bandeja, n => n.Tipo == TipoNotificacionTrabajo.TareaMencion);          // opcional: silenciada
        Assert.DoesNotContain(bandeja, n => n.Tipo == TipoNotificacionTrabajo.TareaComentario);       // opcional de Tareas: silenciada
        var asignada = bandeja.Single(n => n.Tipo == TipoNotificacionTrabajo.TareaAsignada);           // obligatoria: llega aunque Tareas esté desactivada
        Assert.True(asignada.Obligatoria);
        Assert.True(asignada.Prioritaria);                                                             // y se marca como prioritaria
        Assert.False(bandeja.Single(n => n.Tipo == TipoNotificacionTrabajo.ProyectoAsignado).Prioritaria);
        Assert.Equal(bandeja.Count(n => n.Prioritaria), (await Notif.ResumenAsync(default)).Prioritarias);
    }

    // 4. Apertura segura del recurso ----------------------------------------------------------
    [Fact]
    public async Task Abrir_una_notificacion_vuelve_a_comprobar_los_permisos_del_usuario()
    {
        var s = await EscenarioAsync();
        var p = await ProyectoConLuisAsync(s);
        var t = await TareaDeLuisAsync(s, p.ProyectoId);
        var asignada = (await BandejaDeAsync(s, s.UsuarioLuis)).Single(n => n.Tipo == TipoNotificacionTrabajo.TareaAsignada);
        var deProyecto = (await BandejaDeAsync(s, s.UsuarioLuis)).Single(n => n.Tipo == TipoNotificacionTrabajo.ProyectoAsignado);

        Como(s, s.UsuarioLuis);
        Assert.Equal($"tareas/{t.TareaId}", (await Notif.ResolverDestinoAsync(asignada.NotificacionId, default)).Ruta);
        Assert.Equal($"proyectos/{p.ProyectoId}", (await Notif.ResolverDestinoAsync(deProyecto.NotificacionId, default)).Ruta);

        // Luis sale del proyecto: la notificación sigue en su bandeja, pero ya no puede abrir el recurso.
        Como(s, s.UsuarioSara);
        await Proy.RetirarTrabajadorAsync(p.ProyectoId, s.Luis, default);
        Como(s, s.UsuarioLuis);
        await RechazaCon(ServiceErrorKind.NotFound, () => Notif.ResolverDestinoAsync(asignada.NotificacionId, default));
        await RechazaCon(ServiceErrorKind.NotFound, () => Notif.ResolverDestinoAsync(deProyecto.NotificacionId, default));
        Assert.Contains((await Notif.ListarAsync(new NotificacionFiltro(), default)).Items, n => n.NotificacionId == asignada.NotificacionId);
    }

    [Fact]
    public async Task Las_notificaciones_de_permisos_llevan_a_la_pantalla_que_le_corresponde_a_cada_quien_y_sin_datos_privados()
    {
        var s = await EscenarioAsync();
        Como(s, s.UsuarioLuis);
        var solicitud = await _t.Svc<ISolicitudesPermisoService>().CrearAsync(new CrearSolicitudPermisoRequest
        {
            TipoPermisoId = s.Vacaciones, FechaInicio = DateTime.Parse("2026-01-12", CultureInfo.InvariantCulture),
            FechaFin = DateTime.Parse("2026-01-13", CultureInfo.InvariantCulture), Motivo = "Cita con el cardiólogo"
        }, default);

        var alSupervisor = Assert.Single(await BandejaDeAsync(s, s.UsuarioSara), n => n.Tipo == TipoNotificacionPermiso.SolicitudPendiente);
        Assert.Equal(CategoriaNotificacion.Permisos, alSupervisor.Categoria);
        Assert.True(alSupervisor.Obligatoria);
        Assert.DoesNotContain("cardi", alSupervisor.Mensaje, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("permisos/aprobaciones", (await Notif.ResolverDestinoAsync(alSupervisor.NotificacionId, default)).Ruta);

        await _t.Svc<IAprobacionesPermisoService>().ResolverAsync(solicitud.SolicitudPermisoId, new ResolverPermisoRequest { Aprobar = true }, default);
        var alTrabajador = Assert.Single(await BandejaDeLuis(s), n => n.Tipo == TipoNotificacionPermiso.Resuelta);
        Assert.DoesNotContain("cardi", alTrabajador.Mensaje, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("permisos", (await Notif.ResolverDestinoAsync(alTrabajador.NotificacionId, default)).Ruta);

        // Otro trabajador no tiene nada que ver con la solicitud.
        Como(s, s.UsuarioOtro);
        Assert.Empty((await Notif.ListarAsync(new NotificacionFiltro(), default)).Items);
    }

    private Task<List<NotificacionDto>> BandejaDeLuis(Esc s) => BandejaDeAsync(s, s.UsuarioLuis);

    // 5. Eventos de proyecto -----------------------------------------------------------------
    [Fact]
    public async Task Cambiar_el_responsable_o_la_fecha_de_termino_avisa_al_equipo()
    {
        var s = await EscenarioAsync();
        var p = await ProyectoConLuisAsync(s);

        Como(s, s.UsuarioSara);
        var actual = await Proy.ObtenerAsync(p.ProyectoId, default);
        await Proy.ActualizarAsync(p.ProyectoId, new GuardarProyectoRequest { Nombre = p.Nombre, FechaFinPrevista = new DateOnly(2026, 3, 1), RowVersion = actual.RowVersion }, default);
        Assert.Contains(await BandejaDeAsync(s, s.UsuarioLuis), n => n.Tipo == TipoNotificacionTrabajo.ProyectoFecha);
        // Quien hizo el cambio no se avisa a sí mismo.
        Assert.DoesNotContain(await BandejaDeAsync(s, s.UsuarioSara), n => n.Tipo == TipoNotificacionTrabajo.ProyectoFecha);

        _t.NewScope(); _t.Current.As(s.E, s.E.AdminId);
        var antes = await Proy.ObtenerAsync(p.ProyectoId, default);
        await Proy.CambiarResponsableAsync(p.ProyectoId, new CambiarResponsableRequest { ResponsableTrabajadorId = s.Luis, RowVersion = antes.RowVersion }, default);
        Assert.Contains(await BandejaDeAsync(s, s.UsuarioLuis), n => n.Tipo == TipoNotificacionTrabajo.ProyectoResponsable && n.Titulo.StartsWith("Eres"));
        Assert.Contains(await BandejaDeAsync(s, s.UsuarioSara), n => n.Tipo == TipoNotificacionTrabajo.ProyectoResponsable);
    }

    [Fact]
    public void El_catalogo_clasifica_los_tipos_y_trata_los_desconocidos_como_obligatorios()
    {
        Assert.Equal(CategoriaNotificacion.Menciones, CatalogoNotificaciones.CategoriaDe(TipoNotificacionTrabajo.TareaMencion));
        Assert.Equal(CategoriaNotificacion.Permisos, CatalogoNotificaciones.CategoriaDe("permiso.futuro"));
        Assert.Equal(CategoriaNotificacion.Proyectos, CatalogoNotificaciones.CategoriaDe("proyecto.futuro"));
        Assert.True(CatalogoNotificaciones.EsObligatoria("permiso.futuro"));
        Assert.False(CatalogoNotificaciones.EsObligatoria(TipoNotificacionTrabajo.TareaComentario));
        Assert.All(CatalogoNotificaciones.Tipos, t => Assert.Contains(t.Categoria, CategoriaNotificacion.Todas));
    }
}
