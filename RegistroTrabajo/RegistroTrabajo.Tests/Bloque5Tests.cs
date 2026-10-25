using Microsoft.EntityFrameworkCore;
using RegistroTrabajo.Data.Entities;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Tests;

public class Bloque5Tests : IDisposable
{
    private readonly TestEnv _t = new();
    public void Dispose() => _t.Dispose();

    // El FakeClock está en jueves 2026-01-01 12:00 UTC.
    private static async Task RechazaCon(ServiceErrorKind kind, Func<Task> accion) =>
        Assert.Equal(kind, (await Assert.ThrowsAnyAsync<ServiceException>(accion)).Kind);

    private sealed record Esc(EmpresaInfo E, Guid UsuarioSara, Guid UsuarioLuis, Guid UsuarioOtro, Guid Sara, Guid Luis, Guid Otro);

    private static CrearTrabajadorRequest NuevoTrabajador(string nombres, string apellidos, Guid usuario) => new()
    {
        Nombres = nombres, Apellidos = apellidos, FechaIngreso = new DateOnly(2025, 6, 1), UsuarioId = usuario
    };

    /// <summary>Empresa con admin, Sara (supervisora de Luis), Luis y Otro (sin supervisor).</summary>
    private async Task<Esc> EscenarioAsync(string zona = "UTC", bool gps = false)
    {
        var e = await _t.CrearEmpresaAsync("Acme", "admin@a.com");
        var cfg = await _t.Db.ConfiguracionEmpresa.FirstAsync(c => c.EmpresaId == e.EmpresaId);
        cfg.ZonaHorariaId = zona; cfg.PermitirValidacionGps = gps;
        await _t.Db.SaveChangesAsync();
        var uSara = await _t.NuevoUsuarioAsync(e.EmpresaId, "sara@a.com", "Sara", "Sup", e.Roles[NombresRol.Supervisor]);
        var uLuis = await _t.NuevoUsuarioAsync(e.EmpresaId, "luis@a.com", "Luis", "Mora", e.Roles[NombresRol.Trabajador]);
        var uOtro = await _t.NuevoUsuarioAsync(e.EmpresaId, "otro@a.com", "Otro", "Ruiz", e.Roles[NombresRol.Trabajador]);
        _t.Current.As(e, e.AdminId);
        var trab = _t.Svc<ITrabajadorService>();
        var sara = (await trab.CrearAsync(NuevoTrabajador("Sara", "Sup", uSara), default)).TrabajadorId;
        var luis = (await trab.CrearAsync(NuevoTrabajador("Luis", "Mora", uLuis), default)).TrabajadorId;
        var otro = (await trab.CrearAsync(NuevoTrabajador("Otro", "Ruiz", uOtro), default)).TrabajadorId;
        await _t.Svc<ISupervisionService>().AsignarAsync(luis, new AsignarSupervisorRequest { SupervisorTrabajadorId = sara }, default);
        return new Esc(e, uSara, uLuis, uOtro, sara, luis, otro);
    }

    private void Como(Esc s, Guid usuario)
    {
        _t.NewScope();
        _t.Current.As(s.E, usuario);
    }

    private IAsistenciaService Asis => _t.Svc<IAsistenciaService>();
    private ICorreccionesAsistenciaService Corr => _t.Svc<ICorreccionesAsistenciaService>();
    private IHorariosService Hor => _t.Svc<IHorariosService>();

    private static RegistrarAsistenciaRequest SinUbicacion => new();

    private static GuardarHorarioRequest HorarioLV(string nombre, DateOnly desde, int entrada = 9, int salida = 17, Guid? puesto = null, Guid? depto = null)
    {
        var r = new GuardarHorarioRequest { Nombre = nombre, FechaVigenciaDesde = desde, PuestoId = puesto, DepartamentoId = depto };
        for (byte d = 1; d <= 7; d++)
            r.Detalle.Add(new HorarioDetalleDiaDto
            {
                DiaSemana = d, EsDiaLaboral = d <= 5,
                HoraEntrada = d <= 5 ? new TimeOnly(entrada, 0) : null, HoraSalida = d <= 5 ? new TimeOnly(salida, 0) : null
            });
        return r;
    }

    // 1. Entrada y salida ---------------------------------------------------------------
    [Fact]
    public async Task Entrada_y_salida_completan_la_jornada_con_hora_del_servidor()
    {
        var s = await EscenarioAsync();
        Como(s, s.UsuarioLuis);
        Assert.Equal("Entrada", (await Asis.EstadoActualAsync(default)).Siguiente);

        var entrada = await Asis.RegistrarEntradaAsync(SinUbicacion, default);
        Assert.Equal(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc), entrada.EntradaUtc);
        Assert.True(entrada.Incompleta);
        Assert.Equal(EstadoAsistencia.Abierta, entrada.Estado);

        _t.Clock.Advance(TimeSpan.FromHours(8));
        Como(s, s.UsuarioLuis);
        var estado = await Asis.EstadoActualAsync(default);
        Assert.Equal("Salida", estado.Siguiente);
        Assert.NotNull(estado.JornadaAbierta);

        var salida = await Asis.RegistrarSalidaAsync(SinUbicacion, default);
        Assert.False(salida.Incompleta);
        Assert.Equal(EstadoAsistencia.Completa, salida.Estado);
        Assert.Equal(8m, salida.HorasTrabajadas);
        Assert.Equal(entrada.RegistroAsistenciaId, salida.RegistroAsistenciaId);

        Como(s, s.UsuarioLuis);
        Assert.Equal("Entrada", (await Asis.EstadoActualAsync(default)).Siguiente);
        Assert.Single((await Asis.MisRegistrosAsync(new AsistenciaFiltro(), default)).Items);
    }

    [Fact]
    public async Task No_se_permiten_entradas_duplicadas_ni_salidas_sin_entrada()
    {
        var s = await EscenarioAsync();
        Como(s, s.UsuarioLuis);
        await RechazaCon(ServiceErrorKind.Conflict, () => Asis.RegistrarSalidaAsync(SinUbicacion, default));

        await Asis.RegistrarEntradaAsync(SinUbicacion, default);
        Como(s, s.UsuarioLuis);
        await RechazaCon(ServiceErrorKind.Conflict, () => Asis.RegistrarEntradaAsync(SinUbicacion, default));

        Como(s, s.UsuarioLuis);
        await Asis.RegistrarSalidaAsync(SinUbicacion, default);
        Como(s, s.UsuarioLuis);
        await RechazaCon(ServiceErrorKind.Conflict, () => Asis.RegistrarSalidaAsync(SinUbicacion, default));
    }

    [Fact]
    public async Task Las_jornadas_incompletas_se_identifican_y_un_trabajador_inactivo_no_registra()
    {
        var s = await EscenarioAsync();
        Como(s, s.UsuarioLuis);
        await Asis.RegistrarEntradaAsync(SinUbicacion, default);

        Como(s, s.E.AdminId);
        var inc = await Asis.ListarAsync(new AsistenciaFiltro { SoloIncompletas = true }, default);
        Assert.Single(inc.Items);
        Assert.True(inc.Items[0].Incompleta);

        Como(s, s.E.AdminId);
        await _t.Svc<ITrabajadorService>().CambiarEstadoAsync(s.Otro, new CambiarEstadoLaboralRequest { EstadoLaboral = EstadoLaboral.Inactivo }, default);
        Como(s, s.UsuarioOtro);
        await RechazaCon(ServiceErrorKind.Forbidden, () => Asis.RegistrarEntradaAsync(SinUbicacion, default));
    }

    [Fact]
    public async Task Turno_nocturno_conserva_la_fecha_laboral_de_la_entrada()
    {
        var s = await EscenarioAsync();
        _t.Clock.Advance(TimeSpan.FromHours(10)); // 22:00 del 1 de enero
        Como(s, s.UsuarioLuis);
        var entrada = await Asis.RegistrarEntradaAsync(SinUbicacion, default);
        Assert.Equal(new DateOnly(2026, 1, 1), entrada.FechaLaboral);

        _t.Clock.Advance(TimeSpan.FromHours(8)); // 06:00 del 2 de enero
        Como(s, s.UsuarioLuis);
        var salida = await Asis.RegistrarSalidaAsync(SinUbicacion, default);
        Assert.Equal(new DateOnly(2026, 1, 1), salida.FechaLaboral);
        Assert.Equal(8m, salida.HorasTrabajadas);
    }

    [Fact]
    public async Task La_fecha_laboral_usa_la_zona_horaria_de_la_empresa()
    {
        var s = await EscenarioAsync("Asia/Tokyo");
        _t.Clock.Advance(TimeSpan.FromHours(5)); // 17:00 UTC del 1 de enero = 02:00 del 2 de enero en Tokio
        Como(s, s.UsuarioLuis);
        var entrada = await Asis.RegistrarEntradaAsync(SinUbicacion, default);
        Assert.Equal(new DateOnly(2026, 1, 2), entrada.FechaLaboral);
    }

    // 2. GPS ------------------------------------------------------------------------------
    [Fact]
    public async Task Con_gps_exigido_la_ubicacion_es_obligatoria_y_se_guarda()
    {
        var s = await EscenarioAsync(gps: true);
        Como(s, s.UsuarioLuis);
        Assert.True((await Asis.EstadoActualAsync(default)).ExigeUbicacion);
        await RechazaCon(ServiceErrorKind.Validation, () => Asis.RegistrarEntradaAsync(SinUbicacion, default));
        await RechazaCon(ServiceErrorKind.Validation, () => Asis.RegistrarEntradaAsync(new RegistrarAsistenciaRequest { Latitud = 120, Longitud = 10 }, default));

        Como(s, s.UsuarioLuis);
        var r = await Asis.RegistrarEntradaAsync(new RegistrarAsistenciaRequest { Latitud = 19.432608m, Longitud = -99.133209m }, default);
        Assert.Equal(19.432608m, r.EntradaLatitud);
        Assert.Equal(-99.133209m, r.EntradaLongitud);
    }

    [Fact]
    public async Task Sin_gps_exigido_se_descartan_las_coordenadas()
    {
        var s = await EscenarioAsync(gps: false);
        Como(s, s.UsuarioLuis);
        var r = await Asis.RegistrarEntradaAsync(new RegistrarAsistenciaRequest { Latitud = 19.4m, Longitud = -99.1m }, default);
        Assert.Null(r.EntradaLatitud);
        Assert.Null(r.EntradaLongitud);
    }

    // 3. Alcance y aislamiento --------------------------------------------------------------
    [Fact]
    public async Task El_alcance_depende_de_la_supervision_y_las_empresas_estan_aisladas()
    {
        var s = await EscenarioAsync();
        Como(s, s.UsuarioLuis);
        var luisReg = await Asis.RegistrarEntradaAsync(SinUbicacion, default);
        Como(s, s.UsuarioOtro);
        var otroReg = await Asis.RegistrarEntradaAsync(SinUbicacion, default);

        // Sara supervisa a Luis, no a Otro.
        Como(s, s.UsuarioSara);
        var visibles = await Asis.ListarAsync(new AsistenciaFiltro(), default);
        Assert.Contains(visibles.Items, x => x.TrabajadorId == s.Luis);
        Assert.DoesNotContain(visibles.Items, x => x.TrabajadorId == s.Otro);
        Assert.Equal(luisReg.RegistroAsistenciaId, (await Asis.ObtenerAsync(luisReg.RegistroAsistenciaId, default)).RegistroAsistenciaId);
        await RechazaCon(ServiceErrorKind.NotFound, () => Asis.ObtenerAsync(otroReg.RegistroAsistenciaId, default));
        await RechazaCon(ServiceErrorKind.NotFound, () => Asis.ListarAsync(new AsistenciaFiltro { TrabajadorId = s.Otro }, default));

        // El administrador ve todo; un administrador de otra empresa no ve nada.
        Como(s, s.E.AdminId);
        Assert.Equal(2, (await Asis.ListarAsync(new AsistenciaFiltro(), default)).Total);
        var otra = await _t.CrearEmpresaAsync("Otra", "admin@b.com");
        _t.NewScope(); _t.Current.As(otra, otra.AdminId);
        await RechazaCon(ServiceErrorKind.NotFound, () => Asis.ObtenerAsync(luisReg.RegistroAsistenciaId, default));
        Assert.Equal(0, (await Asis.ListarAsync(new AsistenciaFiltro(), default)).Total);
    }

    // 4. Correcciones -----------------------------------------------------------------------
    [Fact]
    public async Task La_correccion_conserva_el_original_y_exige_motivo_y_alcance()
    {
        var s = await EscenarioAsync();
        Como(s, s.UsuarioLuis);
        var r = await Asis.RegistrarEntradaAsync(SinUbicacion, default);
        _t.Clock.Advance(TimeSpan.FromHours(8));
        Como(s, s.UsuarioLuis);
        await Asis.RegistrarSalidaAsync(SinUbicacion, default);

        var nuevaEntrada = new DateTime(2026, 1, 1, 11, 30, 0, DateTimeKind.Utc);
        // El propio trabajador no puede corregir su registro.
        Como(s, s.UsuarioLuis);
        await RechazaCon(ServiceErrorKind.Forbidden, () => Corr.CorregirAsync(r.RegistroAsistenciaId,
            new CorregirAsistenciaRequest { EntradaUtc = nuevaEntrada, Motivo = "Llegué antes" }, default));

        // Quien no supervisa a Luis ni tiene permiso global no ve el registro (404); Sara sí puede corregir.
        Como(s, s.UsuarioOtro);
        await RechazaCon(ServiceErrorKind.NotFound, () => Corr.CorregirAsync(r.RegistroAsistenciaId,
            new CorregirAsistenciaRequest { EntradaUtc = nuevaEntrada, Motivo = "Intento" }, default));

        Como(s, s.UsuarioSara);
        await RechazaCon(ServiceErrorKind.Validation, () => Corr.CorregirAsync(r.RegistroAsistenciaId,
            new CorregirAsistenciaRequest { EntradaUtc = nuevaEntrada, Motivo = "  " }, default));
        var corregido = await Corr.CorregirAsync(r.RegistroAsistenciaId,
            new CorregirAsistenciaRequest { EntradaUtc = nuevaEntrada, Motivo = "Falló el lector" }, default);

        Assert.Equal(nuevaEntrada, corregido.EntradaUtc);
        Assert.True(corregido.Corregida);
        var c = Assert.Single(corregido.Correcciones);
        Assert.Equal(r.EntradaUtc, c.EntradaAnteriorUtc);   // valor original conservado
        Assert.Equal(nuevaEntrada, c.EntradaNuevaUtc);
        Assert.Equal("Falló el lector", c.Motivo);
        Assert.Contains("Sara", c.CorregidoPor);
        Assert.Single(await _t.Db.CorreccionesAsistencia.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task La_correccion_valida_orden_de_horas_y_traslapes()
    {
        var s = await EscenarioAsync();
        Como(s, s.UsuarioLuis);
        var r = await Asis.RegistrarEntradaAsync(SinUbicacion, default);
        _t.Clock.Advance(TimeSpan.FromHours(8));
        Como(s, s.UsuarioLuis);
        await Asis.RegistrarSalidaAsync(SinUbicacion, default);

        Como(s, s.E.AdminId);
        await RechazaCon(ServiceErrorKind.Validation, () => Corr.CorregirAsync(r.RegistroAsistenciaId,
            new CorregirAsistenciaRequest { SalidaUtc = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc), Motivo = "Salida antes de entrada" }, default));
        await RechazaCon(ServiceErrorKind.Validation, () => Corr.CorregirAsync(r.RegistroAsistenciaId,
            new CorregirAsistenciaRequest { Motivo = "Sin cambios" }, default));

        // Un registro manual que se traslapa con el existente se rechaza.
        await RechazaCon(ServiceErrorKind.Conflict, () => Corr.CrearManualAsync(new CrearRegistroManualRequest
        {
            TrabajadorId = s.Luis, EntradaUtc = new DateTime(2026, 1, 1, 13, 0, 0, DateTimeKind.Utc),
            SalidaUtc = new DateTime(2026, 1, 1, 15, 0, 0, DateTimeKind.Utc), Motivo = "Olvidó checar"
        }, default));
    }

    [Fact]
    public async Task Se_puede_anular_un_registro_y_crear_uno_manual_con_motivo()
    {
        var s = await EscenarioAsync();
        Como(s, s.UsuarioLuis);
        var r = await Asis.RegistrarEntradaAsync(SinUbicacion, default);

        Como(s, s.UsuarioSara);
        var anulado = await Corr.AnularAsync(r.RegistroAsistenciaId, new AnularAsistenciaRequest { Motivo = "Registro por error" }, default);
        Assert.Equal(EstadoAsistencia.Anulada, anulado.Estado);
        Assert.StartsWith("Anulación", anulado.Correcciones.Single().Motivo);
        Assert.NotNull(anulado.EntradaUtc);                 // la evidencia original no se borra

        // Anulado ya no bloquea una nueva entrada.
        Como(s, s.UsuarioLuis);
        await Asis.RegistrarEntradaAsync(SinUbicacion, default);

        Como(s, s.UsuarioSara);
        var manual = await Corr.CrearManualAsync(new CrearRegistroManualRequest
        {
            TrabajadorId = s.Luis, EntradaUtc = new DateTime(2025, 12, 30, 9, 0, 0, DateTimeKind.Utc),
            SalidaUtc = new DateTime(2025, 12, 30, 17, 0, 0, DateTimeKind.Utc), Motivo = "No había red"
        }, default);
        Assert.Equal(EstadoAsistencia.Completa, manual.Estado);
        Assert.Equal(new DateOnly(2025, 12, 30), manual.FechaLaboral);
        Assert.Single(manual.Correcciones);
    }

    [Fact]
    public async Task Un_supervisor_no_corrige_fuera_de_la_ventana_pero_RH_si()
    {
        var s = await EscenarioAsync();
        var antiguo = new RegistroAsistencia
        {
            RegistroAsistenciaId = Guid.NewGuid(), EmpresaId = s.E.EmpresaId, TrabajadorId = s.Luis, FechaLaboral = new DateOnly(2025, 10, 1),
            EntradaUtc = new DateTime(2025, 10, 1, 9, 0, 0, DateTimeKind.Utc), SalidaUtc = new DateTime(2025, 10, 1, 17, 0, 0, DateTimeKind.Utc),
            Estado = EstadoAsistencia.Completa, FechaCreacionUtc = new DateTime(2025, 10, 1, 17, 0, 0, DateTimeKind.Utc)
        };
        _t.Db.RegistrosAsistencia.Add(antiguo);
        await _t.Db.SaveChangesAsync();

        var req = new CorregirAsistenciaRequest { SalidaUtc = new DateTime(2025, 10, 1, 18, 0, 0, DateTimeKind.Utc), Motivo = "Se quedó más tiempo" };
        Como(s, s.UsuarioSara);
        await RechazaCon(ServiceErrorKind.Forbidden, () => Corr.CorregirAsync(antiguo.RegistroAsistenciaId, req, default));
        Como(s, s.E.AdminId);
        var ok = await Corr.CorregirAsync(antiguo.RegistroAsistenciaId, req, default);
        Assert.Equal(req.SalidaUtc, ok.SalidaUtc);
    }

    // 5. Horarios ---------------------------------------------------------------------------
    [Fact]
    public async Task Los_turnos_iniciales_incluyen_el_nocturno_que_cruza_la_medianoche()
    {
        var s = await EscenarioAsync();
        var turnos = _t.Svc<ITurnosService>();
        var todos = await turnos.SembrarInicialesAsync(default);
        Assert.Equal(3, todos.Count);
        Assert.Equal(3, (await turnos.SembrarInicialesAsync(default)).Count);
        var noche = todos.Single(t => t.Nombre == "Nocturno");
        Assert.True(noche.CruzaMedianoche);
        Assert.False(todos.Single(t => t.Nombre == "Matutino").CruzaMedianoche);
        await RechazaCon(ServiceErrorKind.Conflict, () => turnos.CrearAsync(new GuardarTurnoRequest { Nombre = "matutino", HoraInicio = new TimeOnly(6, 0), HoraFin = new TimeOnly(14, 0) }, default));
        await RechazaCon(ServiceErrorKind.Validation, () => turnos.CrearAsync(new GuardarTurnoRequest { Nombre = "Raro", HoraInicio = new TimeOnly(6, 0), HoraFin = new TimeOnly(6, 0) }, default));
    }

    [Fact]
    public async Task Horario_con_jornada_nocturna_se_marca_como_cruce_de_medianoche()
    {
        var s = await EscenarioAsync();
        var r = HorarioLV("Noche", new DateOnly(2026, 1, 1), entrada: 22, salida: 6);
        var h = await Hor.CrearAsync(r, default);
        Assert.All(h.Detalle.Where(d => d.EsDiaLaboral), d => Assert.True(d.CruzaMedianoche));
        Assert.False(h.Detalle.Single(d => d.DiaSemana == 6).EsDiaLaboral);
    }

    [Fact]
    public async Task La_excepcion_individual_prevalece_sin_sobrescribir_el_horario_habitual()
    {
        var s = await EscenarioAsync();
        var habitual = await Hor.CrearAsync(HorarioLV("Oficina", new DateOnly(2026, 1, 1)), default);
        var temporal = await Hor.CrearAsync(HorarioLV("Temporal", new DateOnly(2026, 1, 1), entrada: 14, salida: 22), default);

        await Hor.AsignarAsync(new AsignarHorarioRequest { TrabajadorId = s.Luis, HorarioLaboralId = habitual.HorarioLaboralId, FechaDesde = new DateOnly(2026, 1, 1) }, default);
        await Hor.AsignarAsync(new AsignarHorarioRequest
        {
            TrabajadorId = s.Luis, HorarioLaboralId = temporal.HorarioLaboralId, FechaDesde = new DateOnly(2026, 1, 12),
            FechaHasta = new DateOnly(2026, 1, 16), MotivoExcepcion = "Cobertura de turno"
        }, default);

        var antes = await Hor.AplicableAsync(s.Luis, new DateOnly(2026, 1, 9), default);
        Assert.Equal("Oficina", antes.Horario);
        Assert.Equal("Asignacion", antes.Origen);
        var durante = await Hor.AplicableAsync(s.Luis, new DateOnly(2026, 1, 13), default);
        Assert.Equal("Temporal", durante.Horario);
        Assert.Equal("Excepcion", durante.Origen);
        var despues = await Hor.AplicableAsync(s.Luis, new DateOnly(2026, 1, 19), default);
        Assert.Equal("Oficina", despues.Horario);

        // La asignación habitual sigue intacta y no admite otra que se traslape.
        Assert.Equal(2, (await Hor.ListarAsignacionesAsync(s.Luis, default)).Count);
        await RechazaCon(ServiceErrorKind.Conflict, () => Hor.AsignarAsync(new AsignarHorarioRequest
        {
            TrabajadorId = s.Luis, HorarioLaboralId = temporal.HorarioLaboralId, FechaDesde = new DateOnly(2026, 1, 14), FechaHasta = new DateOnly(2026, 1, 15), MotivoExcepcion = "Otra"
        }, default));
        await RechazaCon(ServiceErrorKind.Conflict, () => Hor.AsignarAsync(new AsignarHorarioRequest
        {
            TrabajadorId = s.Luis, HorarioLaboralId = temporal.HorarioLaboralId, FechaDesde = new DateOnly(2026, 2, 1)
        }, default));
    }

    [Fact]
    public async Task El_horario_se_resuelve_por_puesto_y_departamento_cuando_no_hay_asignacion()
    {
        var s = await EscenarioAsync();
        var deptos = _t.Svc<IDepartamentoService>();
        var puestos = _t.Svc<IPuestoService>();
        var depto = (await deptos.CrearAsync(new GuardarCatalogoRequest { Nombre = "Operaciones" }, default)).Id;
        var puesto = (await puestos.CrearAsync(new GuardarCatalogoRequest { Nombre = "Almacenista" }, default)).Id;
        var luisEnt = await _t.Db.Trabajadores.FirstAsync(x => x.TrabajadorId == s.Luis);
        luisEnt.DepartamentoId = depto; luisEnt.PuestoId = puesto;
        await _t.Db.SaveChangesAsync();

        await Hor.CrearAsync(HorarioLV("Depto Operaciones", new DateOnly(2026, 1, 1), depto: depto), default);
        Assert.Equal("Departamento", (await Hor.AplicableAsync(s.Luis, new DateOnly(2026, 1, 5), default)).Origen);
        await Hor.CrearAsync(HorarioLV("Puesto Almacén", new DateOnly(2026, 1, 1), puesto: puesto), default);
        var r = await Hor.AplicableAsync(s.Luis, new DateOnly(2026, 1, 5), default);
        Assert.Equal("Puesto", r.Origen);
        Assert.Equal("Puesto Almacén", r.Horario);
        Assert.True(r.EsDiaLaboral);
        Assert.False((await Hor.AplicableAsync(s.Luis, new DateOnly(2026, 1, 10), default)).EsDiaLaboral); // sábado
    }

    [Fact]
    public async Task Los_cambios_de_horario_no_alteran_el_historial()
    {
        var s = await EscenarioAsync();
        var v1 = await Hor.CrearAsync(HorarioLV("Oficina", new DateOnly(2026, 1, 1)), default);
        await Hor.AsignarAsync(new AsignarHorarioRequest { TrabajadorId = s.Luis, HorarioLaboralId = v1.HorarioLaboralId, FechaDesde = new DateOnly(2026, 1, 1) }, default);

        // Ya rige para alguien: no se edita en sitio.
        await RechazaCon(ServiceErrorKind.Conflict, () => Hor.ActualizarAsync(v1.HorarioLaboralId, HorarioLV("Oficina", new DateOnly(2026, 1, 1), 8, 16), default));

        var v2 = await Hor.NuevaVersionAsync(v1.HorarioLaboralId, HorarioLV("Oficina v2", new DateOnly(2026, 2, 2), 8, 16), default);
        Assert.Equal(new DateOnly(2026, 2, 1), (await Hor.ObtenerAsync(v1.HorarioLaboralId, default)).FechaVigenciaHasta);

        var enero = await Hor.AplicableAsync(s.Luis, new DateOnly(2026, 1, 20), default);
        Assert.Equal("Oficina", enero.Horario);
        Assert.Equal(new TimeOnly(9, 0), enero.Dia!.HoraEntrada);
        var febrero = await Hor.AplicableAsync(s.Luis, new DateOnly(2026, 2, 10), default);
        Assert.Equal("Oficina v2", febrero.Horario);
        Assert.Equal(new TimeOnly(8, 0), febrero.Dia!.HoraEntrada);
        Assert.Equal(v2.HorarioLaboralId, febrero.HorarioLaboralId);
    }

    [Fact]
    public async Task Un_horario_sin_uso_si_se_puede_editar_y_valida_su_contenido()
    {
        var s = await EscenarioAsync();
        var h = await Hor.CrearAsync(HorarioLV("Borrador", new DateOnly(2026, 1, 1)), default);
        var edit = HorarioLV("Borrador editado", new DateOnly(2026, 1, 1), 7, 15);
        var ok = await Hor.ActualizarAsync(h.HorarioLaboralId, edit, default);
        Assert.Equal("Borrador editado", ok.Nombre);
        Assert.Equal(new TimeOnly(7, 0), ok.Detalle.Single(d => d.DiaSemana == 1).HoraEntrada);

        var malo = HorarioLV("Malo", new DateOnly(2026, 1, 1));
        malo.Detalle[0].HoraSalida = null;
        await RechazaCon(ServiceErrorKind.Validation, () => Hor.CrearAsync(malo, default));
        var repetido = HorarioLV("Repetido", new DateOnly(2026, 1, 1));
        repetido.Detalle.Add(new HorarioDetalleDiaDto { DiaSemana = 1, EsDiaLaboral = false });
        await RechazaCon(ServiceErrorKind.Validation, () => Hor.CrearAsync(repetido, default));
        await RechazaCon(ServiceErrorKind.Validation, () => Hor.CrearAsync(new GuardarHorarioRequest { Nombre = "X", FechaVigenciaDesde = new DateOnly(2026, 3, 1), FechaVigenciaHasta = new DateOnly(2026, 2, 1), Detalle = edit.Detalle }, default));
    }

    // 6. Descansos y festivos ---------------------------------------------------------------
    [Fact]
    public async Task Descansos_y_festivos_se_consultan_junto_al_horario_aplicable()
    {
        var s = await EscenarioAsync();
        var cal = _t.Svc<ICalendarioLaboralService>();
        await Hor.CrearAsync(HorarioLV("Oficina", new DateOnly(2026, 1, 1)), default);
        var h = (await Hor.ListarAsync(true, default)).Single();
        await Hor.AsignarAsync(new AsignarHorarioRequest { TrabajadorId = s.Luis, HorarioLaboralId = h.HorarioLaboralId, FechaDesde = new DateOnly(2026, 1, 1) }, default);

        await cal.CrearFestivoAsync(new GuardarDiaFestivoRequest { Fecha = new DateOnly(2026, 1, 6), Nombre = "Día de Reyes" }, default);
        await RechazaCon(ServiceErrorKind.Conflict, () => cal.CrearFestivoAsync(new GuardarDiaFestivoRequest { Fecha = new DateOnly(2026, 1, 6), Nombre = "Día de Reyes" }, default));
        await cal.CrearDescansoAsync(new GuardarDescansoRequest
        {
            TrabajadorId = s.Luis, FechaDesde = new DateOnly(2026, 1, 8), FechaHasta = new DateOnly(2026, 1, 8), Tipo = TipoDescanso.Excepcion, Descripcion = "Cambio de descanso"
        }, default);
        await RechazaCon(ServiceErrorKind.Validation, () => cal.CrearDescansoAsync(new GuardarDescansoRequest
        {
            TrabajadorId = s.Luis, FechaDesde = new DateOnly(2026, 1, 9), FechaHasta = new DateOnly(2026, 1, 8), Tipo = TipoDescanso.Habitual
        }, default));

        var festivo = await Hor.AplicableAsync(s.Luis, new DateOnly(2026, 1, 6), default);
        Assert.Equal("Día de Reyes", festivo.Festivo);
        Assert.False(festivo.EsDiaLaboral);
        var descanso = await Hor.AplicableAsync(s.Luis, new DateOnly(2026, 1, 8), default);
        Assert.Equal("Cambio de descanso", descanso.Descanso);
        Assert.False(descanso.EsDiaLaboral);
        Assert.True((await Hor.AplicableAsync(s.Luis, new DateOnly(2026, 1, 7), default)).EsDiaLaboral);
    }

    // 7. Integración con el Bloque 4 ----------------------------------------------------------
    [Fact]
    public async Task El_calculo_de_permisos_respeta_la_excepcion_individual()
    {
        var s = await EscenarioAsync();
        // Horario habitual lunes a viernes y una excepción que trabaja solo lunes: el cálculo del Bloque 4 debe usar la excepción.
        var habitual = await Hor.CrearAsync(HorarioLV("Oficina", new DateOnly(2026, 1, 1)), default);
        var soloLunes = HorarioLV("Solo lunes", new DateOnly(2026, 1, 1));
        foreach (var d in soloLunes.Detalle.Where(d => d.DiaSemana > 1)) { d.EsDiaLaboral = false; d.HoraEntrada = null; d.HoraSalida = null; }
        var excepcion = await Hor.CrearAsync(soloLunes, default);
        await Hor.AsignarAsync(new AsignarHorarioRequest { TrabajadorId = s.Luis, HorarioLaboralId = habitual.HorarioLaboralId, FechaDesde = new DateOnly(2026, 1, 1) }, default);
        await Hor.AsignarAsync(new AsignarHorarioRequest
        {
            TrabajadorId = s.Luis, HorarioLaboralId = excepcion.HorarioLaboralId, FechaDesde = new DateOnly(2026, 1, 5),
            FechaHasta = new DateOnly(2026, 1, 11), MotivoExcepcion = "Semana reducida"
        }, default);

        var tipos = _t.Svc<ITiposPermisoService>();
        var tipo = await tipos.CrearAsync(new GuardarTipoPermisoRequest { Nombre = "Vacaciones", UnidadBase = UnidadPermiso.Dias }, default);
        await tipos.CrearReglaAsync(tipo.TipoPermisoId, new GuardarReglaPermisoRequest { CantidadAnual = 10 }, default);

        Como(s, s.UsuarioLuis);
        var calc = await _t.Svc<ICalculoDuracionService>().PrevisualizarAsync(new CalculoDuracionRequest
        {
            TipoPermisoId = tipo.TipoPermisoId, FechaInicio = new DateTime(2026, 1, 5), FechaFin = new DateTime(2026, 1, 9)
        }, default);
        Assert.Equal(1m, calc.Cantidad);
    }

    [Fact]
    public async Task Los_permisos_nuevos_estan_en_los_roles_por_defecto()
    {
        Assert.Contains(Permisos.AsistenciaRegistrar, PermisosPorDefecto.PorRol[NombresRol.Trabajador]);
        Assert.DoesNotContain(Permisos.AsistenciaCorregir, PermisosPorDefecto.PorRol[NombresRol.Trabajador]);
        Assert.Contains(Permisos.AsistenciaCorregir, PermisosPorDefecto.PorRol[NombresRol.Supervisor]);
        Assert.DoesNotContain(Permisos.AsistenciaCorregirTodos, PermisosPorDefecto.PorRol[NombresRol.Supervisor]);
        Assert.All(Permisos.Bloque5, p => Assert.Contains(p, PermisosPorDefecto.PorRol[NombresRol.Administrador]));
        await Task.CompletedTask;
    }
}
