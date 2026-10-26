using Microsoft.EntityFrameworkCore;
using RegistroTrabajo.Data.Entities;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Tests;

public class Bloque6Tests : IDisposable
{
    private readonly TestEnv _t = new();
    public void Dispose() => _t.Dispose();

    private static async Task RechazaCon(ServiceErrorKind kind, Func<Task> accion) =>
        Assert.Equal(kind, (await Assert.ThrowsAnyAsync<ServiceException>(accion)).Kind);

    private sealed record Esc(EmpresaInfo E, Guid UsuarioSara, Guid UsuarioLuis, Guid UsuarioOtro, Guid Sara, Guid Luis, Guid Otro, Guid Ventas);

    /// <summary>Empresa con admin, Sara (supervisora de Luis), Luis (departamento Ventas) y Otro (sin departamento ni supervisor).</summary>
    private async Task<Esc> EscenarioAsync(string zona = "UTC")
    {
        var e = await _t.CrearEmpresaAsync("Acme", "admin@a.com");
        var cfg = await _t.Db.ConfiguracionEmpresa.FirstAsync(c => c.EmpresaId == e.EmpresaId);
        cfg.ZonaHorariaId = zona;
        await _t.Db.SaveChangesAsync();
        var uSara = await _t.NuevoUsuarioAsync(e.EmpresaId, "sara@a.com", "Sara", "Sup", e.Roles[NombresRol.Supervisor]);
        var uLuis = await _t.NuevoUsuarioAsync(e.EmpresaId, "luis@a.com", "Luis", "Mora", e.Roles[NombresRol.Trabajador]);
        var uOtro = await _t.NuevoUsuarioAsync(e.EmpresaId, "otro@a.com", "Otro", "Ruiz", e.Roles[NombresRol.Trabajador]);
        _t.Current.As(e, e.AdminId);
        var ventas = (await _t.Svc<IDepartamentoService>().CrearAsync(new GuardarCatalogoRequest { Nombre = "Ventas" }, default)).Id;
        var trab = _t.Svc<ITrabajadorService>();
        var sara = (await trab.CrearAsync(Nuevo("Sara", "Sup", uSara), default)).TrabajadorId;
        var luis = (await trab.CrearAsync(Nuevo("Luis", "Mora", uLuis, ventas), default)).TrabajadorId;
        var otro = (await trab.CrearAsync(Nuevo("Otro", "Ruiz", uOtro), default)).TrabajadorId;
        await _t.Svc<ISupervisionService>().AsignarAsync(luis, new AsignarSupervisorRequest { SupervisorTrabajadorId = sara }, default);
        return new Esc(e, uSara, uLuis, uOtro, sara, luis, otro, ventas);
    }

    private static CrearTrabajadorRequest Nuevo(string nombres, string apellidos, Guid usuario, Guid? depto = null, DateOnly? nacimiento = null, DateOnly? ingreso = null) => new()
    {
        Nombres = nombres, Apellidos = apellidos, FechaIngreso = ingreso ?? new DateOnly(2020, 6, 1), UsuarioId = usuario,
        DepartamentoId = depto, FechaNacimiento = nacimiento
    };

    private void Como(Esc s, Guid usuario)
    {
        _t.NewScope();
        _t.Current.As(s.E, usuario);
    }

    private IEventosEmpresaService Eventos => _t.Svc<IEventosEmpresaService>();
    private ICalendarioService Cal => _t.Svc<ICalendarioService>();

    private static GuardarEventoEmpresaRequest DiaCompleto(string titulo, DateTime inicio, DateTime? fin = null, byte visibilidad = VisibilidadEvento.Empresa, Guid? depto = null) => new()
    {
        Titulo = titulo, TodoElDia = true, FechaInicio = inicio, FechaFin = fin, Visibilidad = visibilidad, DepartamentoId = depto
    };

    private static GuardarEventoEmpresaRequest ConHora(string titulo, DateTime inicio, DateTime? fin) => new()
    {
        Titulo = titulo, TodoElDia = false, FechaInicio = inicio, FechaFin = fin
    };

    private async Task<CalendarioDto> Mes(int anio, int mes, bool cancelados = false) =>
        await Cal.ConsultarAsync(new DateOnly(anio, mes, 1), new DateOnly(anio, mes, DateTime.DaysInMonth(anio, mes)), cancelados, default);

    private async Task<Guid> TipoPermisoAsync(Guid empresaId)
    {
        var tipo = new TipoPermiso { TipoPermisoId = Guid.NewGuid(), EmpresaId = empresaId, Nombre = "Vacaciones", UnidadBase = UnidadPermiso.Dias, RequiereAprobacion = true, Estado = true };
        _t.Db.TiposPermiso.Add(tipo);
        await _t.Db.SaveChangesAsync();
        return tipo.TipoPermisoId;
    }

    private async Task<Guid> PermisoAsync(Esc s, Guid tipo, Guid trabajador, DateTime inicio, DateTime fin, byte estado, bool porHoras = false)
    {
        var p = new SolicitudPermiso
        {
            SolicitudPermisoId = Guid.NewGuid(), EmpresaId = s.E.EmpresaId, TrabajadorId = trabajador, TipoPermisoId = tipo, FechaInicio = inicio,
            FechaFin = fin, EsPorHoras = porHoras, CantidadSolicitada = 1, Estado = estado, SolicitadaUtc = _t.Clock.GetUtcNow().UtcDateTime,
            MotivoPrivado = "Cita médica privada"
        };
        _t.Db.SolicitudesPermiso.Add(p);
        await _t.Db.SaveChangesAsync();
        return p.SolicitudPermisoId;
    }

    // 1. Calendario mensual y eventos ------------------------------------------------------
    [Fact]
    public async Task Un_evento_de_dia_completo_conserva_su_dia_sin_importar_la_zona_horaria()
    {
        var s = await EscenarioAsync("Tokyo Standard Time");
        Como(s, s.E.AdminId);
        var ev = await Eventos.CrearAsync(DiaCompleto("Aniversario de la empresa", new DateTime(2026, 10, 25)), default);
        Assert.Equal(new DateTime(2026, 10, 25), ev.FechaInicio);
        Assert.Equal(new DateTime(2026, 10, 25), ev.FechaFin);

        Como(s, s.UsuarioOtro);
        var mes = await Mes(2026, 10);
        var item = Assert.Single(mes.Items, i => i.Categoria == CategoriaCalendario.Evento);
        Assert.Equal(new DateOnly(2026, 10, 25), item.FechaInicio);
        Assert.Equal(new DateOnly(2026, 10, 25), item.FechaFin);
        Assert.True(item.TodoElDia);
        Assert.Null(item.HoraInicio);
    }

    [Fact]
    public async Task Un_evento_con_hora_respeta_la_zona_horaria_de_la_empresa()
    {
        var s = await EscenarioAsync("Tokyo Standard Time"); // UTC+9
        Como(s, s.E.AdminId);
        var ev = await Eventos.CrearAsync(ConHora("Junta", new DateTime(2026, 10, 25, 8, 0, 0), new DateTime(2026, 10, 25, 9, 30, 0)), default);
        Assert.Equal(new DateTime(2026, 10, 24, 23, 0, 0), (await _t.Db.EventosEmpresa.AsNoTracking().SingleAsync()).FechaInicio); // guardado en UTC
        Assert.Equal(new DateTime(2026, 10, 25, 8, 0, 0), ev.FechaInicio);                                                    // devuelto en hora local

        Como(s, s.UsuarioLuis);
        var dia25 = await Cal.DiaAsync(new DateOnly(2026, 10, 25), default);
        var item = Assert.Single(dia25.Items);
        Assert.Equal(new TimeOnly(8, 0), item.HoraInicio);
        Assert.Equal(new TimeOnly(9, 30), item.HoraFin);
        Assert.Equal(new DateOnly(2026, 10, 25), item.FechaInicio);
        Assert.Empty((await Cal.DiaAsync(new DateOnly(2026, 10, 24), default)).Items); // en UTC sería del 24: no se desplaza
    }

    [Fact]
    public async Task Los_eventos_de_varios_dias_cruzan_mes_y_anio()
    {
        var s = await EscenarioAsync();
        Como(s, s.E.AdminId);
        await Eventos.CrearAsync(DiaCompleto("Cierre anual", new DateTime(2026, 12, 30), new DateTime(2027, 1, 2)), default);

        Como(s, s.UsuarioLuis);
        foreach (var (anio, mes) in new[] { (2026, 12), (2027, 1) })
        {
            var item = Assert.Single((await Mes(anio, mes)).Items);
            Assert.Equal(new DateOnly(2026, 12, 30), item.FechaInicio);
            Assert.Equal(new DateOnly(2027, 1, 2), item.FechaFin);
        }
        Assert.Empty((await Mes(2027, 2)).Items);
        // Rango que abarca dos años.
        var rango = await Cal.ConsultarAsync(new DateOnly(2026, 12, 28), new DateOnly(2027, 1, 5), false, default);
        Assert.Single(rango.Items);
    }

    [Fact]
    public async Task Un_evento_con_hora_que_termina_a_medianoche_no_invade_el_dia_siguiente()
    {
        var s = await EscenarioAsync();
        Como(s, s.E.AdminId);
        await Eventos.CrearAsync(ConHora("Cena", new DateTime(2026, 5, 10, 22, 0, 0), new DateTime(2026, 5, 11, 0, 0, 0)), default);
        Como(s, s.UsuarioLuis);
        Assert.Single((await Cal.DiaAsync(new DateOnly(2026, 5, 10), default)).Items);
        Assert.Empty((await Cal.DiaAsync(new DateOnly(2026, 5, 11), default)).Items);
    }

    [Fact]
    public async Task Un_evento_que_cruza_la_medianoche_aparece_en_ambos_dias()
    {
        var s = await EscenarioAsync();
        Como(s, s.E.AdminId);
        await Eventos.CrearAsync(ConHora("Guardia", new DateTime(2026, 5, 10, 20, 0, 0), new DateTime(2026, 5, 11, 6, 0, 0)), default);
        Como(s, s.UsuarioLuis);
        Assert.Single((await Cal.DiaAsync(new DateOnly(2026, 5, 10), default)).Items);
        Assert.Single((await Cal.DiaAsync(new DateOnly(2026, 5, 11), default)).Items);
    }

    [Fact]
    public async Task Una_hora_inexistente_por_cambio_de_horario_se_rechaza()
    {
        var s = await EscenarioAsync("Pacific Standard Time");
        Como(s, s.E.AdminId);
        // 8 de marzo de 2026: a las 02:00 los relojes saltan a las 03:00.
        await RechazaCon(ServiceErrorKind.Validation, () => Eventos.CrearAsync(ConHora("Junta", new DateTime(2026, 3, 8, 2, 30, 0), null), default));
        await Eventos.CrearAsync(ConHora("Junta", new DateTime(2026, 3, 8, 3, 30, 0), null), default);
    }

    [Fact]
    public async Task Valida_fechas_titulo_y_rango_de_consulta()
    {
        var s = await EscenarioAsync();
        Como(s, s.E.AdminId);
        await RechazaCon(ServiceErrorKind.Validation, () => Eventos.CrearAsync(DiaCompleto("X", new DateTime(2026, 5, 10), new DateTime(2026, 5, 9)), default));
        await RechazaCon(ServiceErrorKind.Validation, () => Eventos.CrearAsync(DiaCompleto(" ", new DateTime(2026, 5, 10)), default));
        await RechazaCon(ServiceErrorKind.Validation, () => Eventos.CrearAsync(DiaCompleto("Largo", new DateTime(2026, 1, 1), new DateTime(2027, 6, 1)), default));
        await RechazaCon(ServiceErrorKind.Validation, () => Eventos.CrearAsync(ConHora("Y", new DateTime(2026, 5, 10, 10, 0, 0), new DateTime(2026, 5, 10, 9, 0, 0)), default));

        await RechazaCon(ServiceErrorKind.Validation, () => Cal.ConsultarAsync(new DateOnly(2026, 2, 1), new DateOnly(2026, 1, 1), false, default));
        await RechazaCon(ServiceErrorKind.Validation, () => Cal.ConsultarAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 1), false, default));
        Assert.NotNull(await Cal.ConsultarAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 4, 3), false, default)); // 93 días
    }

    // 2. Visibilidad, permisos y aislamiento ------------------------------------------------
    [Fact]
    public async Task Un_evento_de_departamento_solo_lo_ven_sus_miembros_y_quien_administra()
    {
        var s = await EscenarioAsync();
        Como(s, s.E.AdminId);
        var ev = await Eventos.CrearAsync(DiaCompleto("Meta de ventas", new DateTime(2026, 5, 10), null, VisibilidadEvento.Departamento, s.Ventas), default);
        await Eventos.CrearAsync(DiaCompleto("Comida general", new DateTime(2026, 5, 10)), default);
        Assert.Equal("Ventas", ev.Departamento);

        Como(s, s.UsuarioLuis); // pertenece a Ventas
        Assert.Equal(2, (await Mes(2026, 5)).Items.Count);
        Assert.Equal(ev.EventoEmpresaId, (await Eventos.ObtenerAsync(ev.EventoEmpresaId, default)).EventoEmpresaId);

        Como(s, s.UsuarioOtro); // sin departamento: aunque conozca el identificador, no puede abrirlo
        Assert.Equal("Comida general", Assert.Single((await Mes(2026, 5)).Items).Titulo);
        await RechazaCon(ServiceErrorKind.NotFound, () => Eventos.ObtenerAsync(ev.EventoEmpresaId, default));

        Como(s, s.E.AdminId);
        Assert.Equal(2, (await Mes(2026, 5)).Items.Count);
    }

    [Fact]
    public async Task Solo_quien_administra_eventos_puede_crear_editar_cancelar_listar_o_configurar()
    {
        var s = await EscenarioAsync();
        Como(s, s.E.AdminId);
        var ev = await Eventos.CrearAsync(DiaCompleto("Junta", new DateTime(2026, 5, 10)), default);

        foreach (var usuario in new[] { s.UsuarioLuis, s.UsuarioSara })
        {
            Como(s, usuario);
            await RechazaCon(ServiceErrorKind.Forbidden, () => Eventos.CrearAsync(DiaCompleto("Otra", new DateTime(2026, 5, 11)), default));
            await RechazaCon(ServiceErrorKind.Forbidden, () => Eventos.ActualizarAsync(ev.EventoEmpresaId, new GuardarEventoEmpresaRequest { Titulo = "X", FechaInicio = new DateTime(2026, 5, 10), RowVersion = ev.RowVersion }, default));
            await RechazaCon(ServiceErrorKind.Forbidden, () => Eventos.CancelarAsync(ev.EventoEmpresaId, new CancelarEventoEmpresaRequest { Motivo = "No", RowVersion = ev.RowVersion }, default));
            await RechazaCon(ServiceErrorKind.Forbidden, () => Eventos.ListarAsync(new EventoEmpresaFiltro(), default));
            await RechazaCon(ServiceErrorKind.Forbidden, () => Cal.GuardarConfiguracionAsync(new ConfiguracionCalendarioDto { MostrarCumpleanos = true }, default));
        }
    }

    [Fact]
    public async Task Otra_empresa_no_ve_ni_abre_los_eventos_ajenos()
    {
        var s = await EscenarioAsync();
        Como(s, s.E.AdminId);
        var ev = await Eventos.CrearAsync(DiaCompleto("Secreto", new DateTime(2026, 5, 10)), default);

        var otra = await _t.CrearEmpresaAsync("Beta", "admin@b.com");
        _t.NewScope();
        _t.Current.As(otra, otra.AdminId);
        Assert.Empty((await Mes(2026, 5)).Items);
        await RechazaCon(ServiceErrorKind.NotFound, () => Eventos.ObtenerAsync(ev.EventoEmpresaId, default));
        await RechazaCon(ServiceErrorKind.NotFound, () => Eventos.CancelarAsync(ev.EventoEmpresaId, new CancelarEventoEmpresaRequest { Motivo = "Intento", RowVersion = ev.RowVersion }, default));
        Assert.Empty((await Eventos.ListarAsync(new EventoEmpresaFiltro(), default)).Items);
    }

    [Fact]
    public async Task El_departamento_del_evento_debe_existir_en_la_empresa_y_estar_activo()
    {
        var s = await EscenarioAsync();
        Como(s, s.E.AdminId);
        await RechazaCon(ServiceErrorKind.Validation, () => Eventos.CrearAsync(DiaCompleto("Evento", new DateTime(2026, 5, 10), null, VisibilidadEvento.Departamento, Guid.NewGuid()), default));
        await RechazaCon(ServiceErrorKind.Validation, () => Eventos.CrearAsync(DiaCompleto("Evento", new DateTime(2026, 5, 10), null, VisibilidadEvento.Departamento, null), default));

        var otra = await _t.CrearEmpresaAsync("Beta", "admin@b.com");
        _t.NewScope();
        _t.Current.As(otra, otra.AdminId);
        var ajeno = (await _t.Svc<IDepartamentoService>().CrearAsync(new GuardarCatalogoRequest { Nombre = "Ajeno" }, default)).Id;
        Como(s, s.E.AdminId);
        await RechazaCon(ServiceErrorKind.Validation, () => Eventos.CrearAsync(DiaCompleto("Evento", new DateTime(2026, 5, 10), null, VisibilidadEvento.Departamento, ajeno), default));

        // Con visibilidad para toda la empresa el departamento se ignora.
        var ok = await Eventos.CrearAsync(DiaCompleto("Evento 2", new DateTime(2026, 5, 10), null, VisibilidadEvento.Empresa, s.Ventas), default);
        Assert.Null(ok.DepartamentoId);
    }

    // 3. Edición, cancelación e historial -----------------------------------------------------
    [Fact]
    public async Task Editar_registra_quien_y_cuando_y_detecta_ediciones_concurrentes()
    {
        var s = await EscenarioAsync();
        Como(s, s.E.AdminId);
        var ev = await Eventos.CrearAsync(DiaCompleto("Junta", new DateTime(2026, 5, 10)), default);
        Assert.Null(ev.FechaModificacionUtc);

        _t.Clock.Advance(TimeSpan.FromHours(3));
        Como(s, s.E.AdminId);
        var req = DiaCompleto("Junta trimestral", new DateTime(2026, 5, 12), new DateTime(2026, 5, 13));
        req.RowVersion = ev.RowVersion;
        var editado = await Eventos.ActualizarAsync(ev.EventoEmpresaId, req, default);
        Assert.Equal("Junta trimestral", editado.Titulo);
        Assert.Equal(new DateTime(2026, 1, 1, 15, 0, 0), editado.FechaModificacionUtc);
        Assert.Equal("Admin Uno", editado.ModificadoPor);
        Assert.Equal("Admin Uno", editado.CreadoPor);

        Como(s, s.E.AdminId);
        var obsoleta = DiaCompleto("Otro cambio", new DateTime(2026, 5, 12));
        obsoleta.RowVersion = Convert.ToBase64String(new byte[] { 9, 9, 9, 9, 9, 9, 9, 9 });
        await RechazaCon(ServiceErrorKind.Conflict, () => Eventos.ActualizarAsync(ev.EventoEmpresaId, obsoleta, default));
        obsoleta.RowVersion = null;
        await RechazaCon(ServiceErrorKind.Validation, () => Eventos.ActualizarAsync(ev.EventoEmpresaId, obsoleta, default));

        // La edición quedó en la auditoría.
        Assert.Contains(await _t.Db.Auditoria.AsNoTracking().Select(a => a.Accion).ToListAsync(), a => a == "Actualizar");
    }

    [Fact]
    public async Task Un_evento_cancelado_se_conserva_pero_sale_de_la_vista_habitual()
    {
        var s = await EscenarioAsync();
        Como(s, s.E.AdminId);
        var ev = await Eventos.CrearAsync(DiaCompleto("Capacitación", new DateTime(2026, 5, 10)), default);
        await RechazaCon(ServiceErrorKind.Validation, () => Eventos.CancelarAsync(ev.EventoEmpresaId, new CancelarEventoEmpresaRequest { Motivo = " ", RowVersion = ev.RowVersion }, default));

        _t.Clock.Advance(TimeSpan.FromDays(1));
        Como(s, s.E.AdminId);
        var cancelado = await Eventos.CancelarAsync(ev.EventoEmpresaId, new CancelarEventoEmpresaRequest { Motivo = "Se pospone", RowVersion = ev.RowVersion }, default);
        Assert.Equal(EstadoEventoEmpresa.Cancelado, cancelado.Estado);
        Assert.Equal("Se pospone", cancelado.MotivoCancelacion);
        Assert.Equal("Admin Uno", cancelado.CanceladoPor);
        Assert.NotNull(cancelado.FechaCancelacionUtc);
        Assert.Equal(1, await _t.Db.EventosEmpresa.CountAsync()); // no se borra

        Como(s, s.E.AdminId);
        Assert.Empty((await Mes(2026, 5)).Items);                                         // fuera de la vista habitual
        var conCancelados = Assert.Single((await Mes(2026, 5, cancelados: true)).Items);  // el administrador puede verlo
        Assert.True(conCancelados.Cancelado);
        Assert.Single((await Eventos.ListarAsync(new EventoEmpresaFiltro { Estado = EstadoEventoEmpresa.Cancelado }, default)).Items);
        Assert.Empty((await Eventos.ListarAsync(new EventoEmpresaFiltro { Estado = EstadoEventoEmpresa.Activo }, default)).Items);

        Como(s, s.UsuarioLuis);
        Assert.Empty((await Mes(2026, 5, cancelados: true)).Items);                       // un trabajador nunca ve cancelados
        await RechazaCon(ServiceErrorKind.NotFound, () => Eventos.ObtenerAsync(ev.EventoEmpresaId, default));

        Como(s, s.E.AdminId);
        await RechazaCon(ServiceErrorKind.Conflict, () => Eventos.CancelarAsync(ev.EventoEmpresaId, new CancelarEventoEmpresaRequest { Motivo = "Otra vez", RowVersion = cancelado.RowVersion }, default));
        var edicion = DiaCompleto("Reabrir", new DateTime(2026, 5, 10));
        edicion.RowVersion = cancelado.RowVersion;
        await RechazaCon(ServiceErrorKind.Conflict, () => Eventos.ActualizarAsync(ev.EventoEmpresaId, edicion, default));
    }

    [Fact]
    public async Task El_listado_de_administracion_filtra_por_rango_y_tipo()
    {
        var s = await EscenarioAsync("Tokyo Standard Time");
        Como(s, s.E.AdminId);
        var capacitacion = DiaCompleto("Curso", new DateTime(2026, 5, 10));
        capacitacion.Tipo = TipoEventoEmpresa.Capacitacion;
        await Eventos.CrearAsync(capacitacion, default);
        await Eventos.CrearAsync(DiaCompleto("Junio", new DateTime(2026, 6, 10)), default);
        await Eventos.CrearAsync(ConHora("Madrugada", new DateTime(2026, 5, 20, 0, 30, 0), null), default); // 19 may 15:30 UTC

        Como(s, s.E.AdminId);
        var mayo = await Eventos.ListarAsync(new EventoEmpresaFiltro { Desde = new DateOnly(2026, 5, 1), Hasta = new DateOnly(2026, 5, 31) }, default);
        Assert.Equal(new[] { "Madrugada", "Curso" }, mayo.Items.Select(i => i.Titulo));
        Assert.Equal(new DateTime(2026, 5, 20, 0, 30, 0), mayo.Items[0].FechaInicio);
        var solo20 = await Eventos.ListarAsync(new EventoEmpresaFiltro { Desde = new DateOnly(2026, 5, 20), Hasta = new DateOnly(2026, 5, 20) }, default);
        Assert.Equal("Madrugada", Assert.Single(solo20.Items).Titulo);
        Assert.Equal("Curso", Assert.Single((await Eventos.ListarAsync(new EventoEmpresaFiltro { Tipo = TipoEventoEmpresa.Capacitacion }, default)).Items).Titulo);
        Assert.Equal(3, (await Eventos.ListarAsync(new EventoEmpresaFiltro(), default)).Total);
    }

    // 4. Festivos ----------------------------------------------------------------------------------
    [Fact]
    public async Task Los_festivos_del_bloque_5_se_muestran_incluidos_los_recurrentes()
    {
        var s = await EscenarioAsync();
        Como(s, s.E.AdminId);
        var cal = _t.Svc<ICalendarioLaboralService>();
        await cal.CrearFestivoAsync(new GuardarDiaFestivoRequest { Fecha = new DateOnly(2026, 12, 25), Nombre = "Navidad", EsRecurrenteAnual = true }, default);
        await cal.CrearFestivoAsync(new GuardarDiaFestivoRequest { Fecha = new DateOnly(2026, 5, 15), Nombre = "Cierre corporativo" }, default);
        await cal.CrearFestivoAsync(new GuardarDiaFestivoRequest { Fecha = new DateOnly(2024, 2, 29), Nombre = "Bisiesto", EsRecurrenteAnual = true }, default);

        Como(s, s.UsuarioOtro);
        var navidad2027 = await Cal.ConsultarAsync(new DateOnly(2027, 12, 1), new DateOnly(2027, 12, 31), false, default);
        var f = Assert.Single(navidad2027.Items);
        Assert.Equal((CategoriaCalendario.Festivo, "Navidad", new DateOnly(2027, 12, 25)), (f.Categoria, f.Titulo, f.FechaInicio));
        Assert.Null(f.RecursoId);

        Assert.Equal("Cierre corporativo", Assert.Single((await Mes(2026, 5)).Items).Titulo);
        Assert.Empty((await Mes(2027, 5)).Items); // no recurrente: solo ese año
        Assert.Empty((await Mes(2027, 2)).Items); // 29 de febrero recurrente: no existe en 2027
        Assert.Single((await Mes(2028, 2)).Items);
        // Rango que cruza el año: el recurrente aparece en cada año.
        var cruce = await Cal.ConsultarAsync(new DateOnly(2026, 12, 20), new DateOnly(2027, 1, 10), false, default);
        Assert.Single(cruce.Items, i => i.Titulo == "Navidad");
    }

    // 5. Ausencias ---------------------------------------------------------------------------------
    [Fact]
    public async Task Solo_los_permisos_aprobados_son_ausencias_y_dejan_de_serlo_al_cancelarse()
    {
        var s = await EscenarioAsync();
        Como(s, s.E.AdminId);
        var tipo = await TipoPermisoAsync(s.E.EmpresaId);
        var aprobada = await PermisoAsync(s, tipo, s.Luis, new DateTime(2026, 5, 11), new DateTime(2026, 5, 13, 23, 59, 59), EstadoSolicitudPermiso.Aprobada);
        await PermisoAsync(s, tipo, s.Luis, new DateTime(2026, 5, 18), new DateTime(2026, 5, 18, 23, 59, 59), EstadoSolicitudPermiso.Pendiente);
        await PermisoAsync(s, tipo, s.Luis, new DateTime(2026, 5, 19), new DateTime(2026, 5, 19, 23, 59, 59), EstadoSolicitudPermiso.Rechazada);

        Como(s, s.E.AdminId);
        var item = Assert.Single((await Mes(2026, 5)).Items);
        Assert.Equal(CategoriaCalendario.Ausencia, item.Categoria);
        Assert.Equal("Luis Mora", item.Titulo);
        Assert.Equal((new DateOnly(2026, 5, 11), new DateOnly(2026, 5, 13)), (item.FechaInicio, item.FechaFin));
        Assert.True(item.TodoElDia);
        Assert.Equal(aprobada, item.RecursoId);

        // Al cancelarse el permiso aprobado, deja de mostrarse como ausencia vigente.
        var p = await _t.Db.SolicitudesPermiso.FirstAsync(x => x.SolicitudPermisoId == aprobada);
        p.Estado = EstadoSolicitudPermiso.Cancelada;
        await _t.Db.SaveChangesAsync();
        Como(s, s.E.AdminId);
        Assert.Empty((await Mes(2026, 5)).Items);
    }

    [Fact]
    public async Task Por_defecto_todos_ven_las_ausencias_de_la_empresa_pero_solo_abren_las_de_su_alcance()
    {
        var s = await EscenarioAsync();
        Como(s, s.E.AdminId);
        var tipo = await TipoPermisoAsync(s.E.EmpresaId);
        await PermisoAsync(s, tipo, s.Luis, new DateTime(2026, 5, 11), new DateTime(2026, 5, 11, 23, 59, 59), EstadoSolicitudPermiso.Aprobada);
        await PermisoAsync(s, tipo, s.Otro, new DateTime(2026, 5, 12), new DateTime(2026, 5, 12, 23, 59, 59), EstadoSolicitudPermiso.Aprobada);
        await PermisoAsync(s, tipo, s.Sara, new DateTime(2026, 5, 13), new DateTime(2026, 5, 13, 23, 59, 59), EstadoSolicitudPermiso.Aprobada);

        foreach (var usuario in new[] { s.E.AdminId, s.UsuarioSara, s.UsuarioLuis, s.UsuarioOtro })
        {
            Como(s, usuario);
            Assert.Equal(new[] { "Luis Mora", "Otro Ruiz", "Sara Sup" }, (await Mes(2026, 5)).Items.Select(i => i.Titulo));
        }

        // Ver una ausencia ajena no da acceso a su solicitud: solo se puede abrir la propia o las del alcance de permisos.
        Como(s, s.UsuarioLuis);
        var items = (await Mes(2026, 5)).Items;
        Assert.NotNull(items.Single(i => i.Titulo == "Luis Mora").RecursoId);
        Assert.Null(items.Single(i => i.Titulo == "Otro Ruiz").RecursoId);
        Como(s, s.UsuarioSara);
        items = (await Mes(2026, 5)).Items;
        Assert.NotNull(items.Single(i => i.Titulo == "Luis Mora").RecursoId);   // supervisada
        Assert.Null(items.Single(i => i.Titulo == "Otro Ruiz").RecursoId);
        Como(s, s.E.AdminId);
        Assert.All((await Mes(2026, 5)).Items, i => Assert.NotNull(i.RecursoId));
    }

    [Fact]
    public async Task Sin_el_permiso_ver_ausencias_todos_cada_quien_ve_solo_su_alcance()
    {
        var s = await EscenarioAsync();
        Como(s, s.E.AdminId);
        var tipo = await TipoPermisoAsync(s.E.EmpresaId);
        await PermisoAsync(s, tipo, s.Luis, new DateTime(2026, 5, 11), new DateTime(2026, 5, 11, 23, 59, 59), EstadoSolicitudPermiso.Aprobada);
        await PermisoAsync(s, tipo, s.Otro, new DateTime(2026, 5, 12), new DateTime(2026, 5, 12, 23, 59, 59), EstadoSolicitudPermiso.Aprobada);
        await PermisoAsync(s, tipo, s.Sara, new DateTime(2026, 5, 13), new DateTime(2026, 5, 13, 23, 59, 59), EstadoSolicitudPermiso.Aprobada);

        // La empresa retira el permiso a los roles de supervisor y trabajador.
        var permisoId = await _t.Db.PermisosSistema.Where(p => p.Codigo == Permisos.CalendarioVerAusenciasTodos).Select(p => p.PermisoId).SingleAsync();
        var quitar = await _t.Db.RolPermisos.Where(rp => rp.PermisoId == permisoId
            && (rp.RolId == s.E.Roles[NombresRol.Supervisor] || rp.RolId == s.E.Roles[NombresRol.Trabajador])).ToListAsync();
        _t.Db.RolPermisos.RemoveRange(quitar);
        await _t.Db.SaveChangesAsync();

        Como(s, s.E.AdminId);
        Assert.Equal(3, (await Mes(2026, 5)).Items.Count);                                                 // RH/admin conservan toda la empresa
        Como(s, s.UsuarioSara);
        Assert.Equal(new[] { "Luis Mora", "Sara Sup" }, (await Mes(2026, 5)).Items.Select(i => i.Titulo)); // supervisados + propia
        Como(s, s.UsuarioLuis);
        Assert.Equal("Luis Mora", Assert.Single((await Mes(2026, 5)).Items).Titulo);
        Como(s, s.UsuarioOtro);
        Assert.Equal("Otro Ruiz", Assert.Single((await Mes(2026, 5)).Items).Titulo);
    }
    [Fact]
    public async Task Las_ausencias_por_horas_aparecen_en_su_dia_con_la_hora_y_sin_datos_privados()
    {
        var s = await EscenarioAsync();
        Como(s, s.E.AdminId);
        var tipo = await TipoPermisoAsync(s.E.EmpresaId);
        await PermisoAsync(s, tipo, s.Luis, new DateTime(2026, 5, 11, 9, 0, 0), new DateTime(2026, 5, 11, 11, 30, 0), EstadoSolicitudPermiso.Aprobada, porHoras: true);

        Como(s, s.UsuarioLuis);
        var item = Assert.Single((await Cal.DiaAsync(new DateOnly(2026, 5, 11), default)).Items);
        Assert.False(item.TodoElDia);
        Assert.Equal((new TimeOnly(9, 0), new TimeOnly(11, 30)), (item.HoraInicio, item.HoraFin));
        // El elemento del calendario no incluye motivo ni tipo: solo trabajador, periodo y el vínculo autorizado.
        var json = System.Text.Json.JsonSerializer.Serialize(item);
        Assert.DoesNotContain("médica", json);
        Assert.DoesNotContain("Vacaciones", json);
    }

    // 6. Fechas relevantes -----------------------------------------------------------------------------
    [Fact]
    public async Task Cumpleanos_y_aniversarios_solo_aparecen_si_la_empresa_los_habilita()
    {
        var s = await EscenarioAsync();
        Como(s, s.E.AdminId);
        var luis = await _t.Svc<ITrabajadorService>().ObtenerAsync(s.Luis, default);
        await _t.Svc<ITrabajadorService>().ActualizarAsync(s.Luis, new ActualizarTrabajadorRequest
        {
            Nombres = "Luis", Apellidos = "Mora", FechaIngreso = new DateOnly(2020, 6, 1), FechaNacimiento = new DateOnly(1990, 5, 20),
            DepartamentoId = s.Ventas, UsuarioId = s.UsuarioLuis, RowVersion = luis.RowVersion
        }, default);

        Como(s, s.UsuarioOtro);
        var apagado = await Mes(2026, 5);
        Assert.False(apagado.MuestraCumpleanos);
        Assert.Empty(apagado.Items);
        Assert.Empty((await Mes(2026, 6)).Items);

        Como(s, s.E.AdminId);
        await Cal.GuardarConfiguracionAsync(new ConfiguracionCalendarioDto { MostrarCumpleanos = true, MostrarAniversarios = false }, default);
        Como(s, s.UsuarioOtro);
        var cumple = Assert.Single((await Mes(2026, 5)).Items);
        Assert.Equal((CategoriaCalendario.Cumpleanos, "Luis Mora", new DateOnly(2026, 5, 20)), (cumple.Categoria, cumple.Titulo, cumple.FechaInicio));
        Assert.Null(cumple.Descripcion);   // sin edad ni datos adicionales
        Assert.Null(cumple.RecursoId);
        Assert.Empty((await Mes(2026, 6)).Items); // aniversarios aún apagados

        Como(s, s.E.AdminId);
        await Cal.GuardarConfiguracionAsync(new ConfiguracionCalendarioDto { MostrarCumpleanos = false, MostrarAniversarios = true }, default);
        Como(s, s.UsuarioOtro);
        Assert.Empty((await Mes(2026, 5)).Items);
        var aniv = Assert.Single((await Mes(2026, 6)).Items, i => i.Titulo == "Luis Mora");
        Assert.Equal((CategoriaCalendario.Aniversario, 6, "6 años en la empresa"), (aniv.Categoria, aniv.Anios, aniv.Descripcion));
        Assert.Equal(new DateOnly(2026, 6, 1), aniv.FechaInicio);
    }

    [Fact]
    public async Task Los_aniversarios_excluyen_el_primer_dia_y_a_los_inactivos_y_el_29_de_febrero_se_celebra_el_28()
    {
        var s = await EscenarioAsync();
        Como(s, s.E.AdminId);
        var trab = _t.Svc<ITrabajadorService>();
        var uNuevo = await _t.NuevoUsuarioAsync(s.E.EmpresaId, "nuevo@a.com", "Nuevo", "Ing", s.E.Roles[NombresRol.Trabajador]);
        var uBis = await _t.NuevoUsuarioAsync(s.E.EmpresaId, "bis@a.com", "Bis", "Ciesto", s.E.Roles[NombresRol.Trabajador]);
        // El FakeClock está en 2026: "Nuevo" ingresa en 2026 (0 años) y "Bis" nació un 29 de febrero.
        await trab.CrearAsync(Nuevo("Nuevo", "Ing", uNuevo, null, null, new DateOnly(2026, 3, 10)), default);
        await trab.CrearAsync(Nuevo("Bis", "Ciesto", uBis, null, new DateOnly(2000, 2, 29), new DateOnly(2019, 2, 28)), default);
        await Cal.GuardarConfiguracionAsync(new ConfiguracionCalendarioDto { MostrarCumpleanos = true, MostrarAniversarios = true }, default);
        await trab.CambiarEstadoAsync(s.Otro, new CambiarEstadoLaboralRequest { EstadoLaboral = EstadoLaboral.Inactivo }, default);

        Como(s, s.E.AdminId);
        var marzo = await Mes(2026, 3);
        Assert.DoesNotContain(marzo.Items, i => i.Titulo == "Nuevo Ing"); // 0 años: no es aniversario
        var feb2027 = (await Mes(2027, 2)).Items;
        Assert.Contains(feb2027, i => i.Categoria == CategoriaCalendario.Cumpleanos && i.Titulo == "Bis Ciesto" && i.FechaInicio == new DateOnly(2027, 2, 28));
        var feb2028 = (await Mes(2028, 2)).Items;
        Assert.Contains(feb2028, i => i.Categoria == CategoriaCalendario.Cumpleanos && i.Titulo == "Bis Ciesto" && i.FechaInicio == new DateOnly(2028, 2, 29));

        var junio = (await Mes(2026, 6)).Items; // Otro (inactivo) ingresó el 2020-06-01: no aparece
        Assert.DoesNotContain(junio, i => i.Titulo == "Otro Ruiz");
        Assert.Contains(junio, i => i.Titulo == "Sara Sup");
    }

    [Fact]
    public async Task La_configuracion_del_calendario_se_audita_y_la_lee_cualquiera_que_consulte()
    {
        var s = await EscenarioAsync();
        Como(s, s.UsuarioLuis);
        Assert.False((await Cal.ObtenerConfiguracionAsync(default)).MostrarCumpleanos);

        Como(s, s.E.AdminId);
        var guardada = await Cal.GuardarConfiguracionAsync(new ConfiguracionCalendarioDto { MostrarCumpleanos = true, MostrarAniversarios = true }, default);
        Assert.True(guardada.MostrarCumpleanos && guardada.MostrarAniversarios);
        Assert.Contains(await _t.Db.Auditoria.AsNoTracking().Select(a => a.Accion).ToListAsync(), a => a == "ConfigurarCalendario");

        Como(s, s.UsuarioLuis);
        Assert.True((await Cal.ObtenerConfiguracionAsync(default)).MostrarAniversarios);
    }

    // 7. Catálogo de permisos ---------------------------------------------------------------------
    [Fact]
    public void Los_permisos_del_bloque_6_estan_en_el_catalogo_y_en_los_roles_por_defecto()
    {
        Assert.All(Permisos.Bloque6, p => Assert.Contains(p, Permisos.Todos));
        Assert.Equal(Permisos.Bloque6.Length, Permisos.Bloque6.Distinct().Count());
        Assert.All(Permisos.Bloque6, p => Assert.Contains(p, PermisosPorDefecto.PorRol[NombresRol.Administrador]));
        Assert.All(Permisos.Bloque6, p => Assert.Contains(p, PermisosPorDefecto.PorRol[NombresRol.RecursosHumanos]));
        foreach (var rol in new[] { NombresRol.Supervisor, NombresRol.Trabajador })
        {
            Assert.Contains(Permisos.CalendarioConsultar, PermisosPorDefecto.PorRol[rol]);
            Assert.DoesNotContain(Permisos.CalendarioGestionarEventos, PermisosPorDefecto.PorRol[rol]);
            Assert.DoesNotContain(Permisos.CalendarioConfigurar, PermisosPorDefecto.PorRol[rol]);
        }
    }
}
