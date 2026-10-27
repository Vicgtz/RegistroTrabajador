using System.Globalization;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.EntityFrameworkCore;
using RegistroTrabajo.Data;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Tests;

public class Bloque10Tests : IDisposable
{
    private readonly TestEnv _t = new();
    public void Dispose() => _t.Dispose();

    private static async Task RechazaCon(ServiceErrorKind kind, Func<Task> accion) =>
        Assert.Equal(kind, (await Assert.ThrowsAnyAsync<ServiceException>(accion)).Kind);

    private sealed record Esc(EmpresaInfo E, Guid UsuarioSara, Guid UsuarioLuis, Guid UsuarioOtro, Guid UsuarioRita, Guid Sara, Guid Luis, Guid Otro, Guid Operaciones, Guid Vacaciones);

    /// <summary>
    /// Empresa con admin, Rita (Recursos Humanos), Sara (supervisora de Luis), Luis y Otro. Sara y Luis son del departamento Operaciones.
    /// El reloj está en jueves 2026-01-01 (zona UTC).
    /// </summary>
    private async Task<Esc> EscenarioAsync()
    {
        var e = await _t.CrearEmpresaAsync("Acme", "admin@a.com");
        var uSara = await _t.NuevoUsuarioAsync(e.EmpresaId, "sara@a.com", "Sara", "Sup", e.Roles[NombresRol.Supervisor]);
        var uLuis = await _t.NuevoUsuarioAsync(e.EmpresaId, "luis@a.com", "Luis", "Mora", e.Roles[NombresRol.Trabajador]);
        var uOtro = await _t.NuevoUsuarioAsync(e.EmpresaId, "otro@a.com", "Otro", "Ruiz", e.Roles[NombresRol.Trabajador]);
        var uRita = await _t.NuevoUsuarioAsync(e.EmpresaId, "rita@a.com", "Rita", "Gomez", e.Roles[NombresRol.RecursosHumanos]);
        _t.Current.As(e, e.AdminId);
        var depto = (await _t.Svc<IDepartamentoService>().CrearAsync(new GuardarCatalogoRequest { Nombre = "Operaciones" }, default)).Id;
        var trab = _t.Svc<ITrabajadorService>();
        var sara = (await trab.CrearAsync(Nuevo("Sara", "Sup", uSara, depto), default)).TrabajadorId;
        var luis = (await trab.CrearAsync(Nuevo("Luis", "Mora", uLuis, depto), default)).TrabajadorId;
        var otro = (await trab.CrearAsync(Nuevo("Otro", "Ruiz", uOtro, null), default)).TrabajadorId;
        await _t.Svc<ISupervisionService>().AsignarAsync(luis, new AsignarSupervisorRequest { SupervisorTrabajadorId = sara }, default);
        var tipos = _t.Svc<ITiposPermisoService>();
        var tipo = await tipos.CrearAsync(new GuardarTipoPermisoRequest { Nombre = "Vacaciones", UnidadBase = UnidadPermiso.Dias }, default);
        await tipos.CrearReglaAsync(tipo.TipoPermisoId, new GuardarReglaPermisoRequest { CantidadAnual = 10 }, default);
        return new Esc(e, uSara, uLuis, uOtro, uRita, sara, luis, otro, depto, tipo.TipoPermisoId);
    }

    private static CrearTrabajadorRequest Nuevo(string nombres, string apellidos, Guid usuario, Guid? depto) => new()
    {
        Nombres = nombres, Apellidos = apellidos, FechaIngreso = new DateOnly(2020, 6, 1), UsuarioId = usuario, DepartamentoId = depto
    };

    private void Como(Esc s, Guid usuario)
    {
        _t.NewScope();
        _t.Current.As(s.E, usuario);
    }

    private void ComoAdmin(Esc s) => Como(s, s.E.AdminId);

    private IReporteService Rep => _t.Svc<IReporteService>();
    private IExportacionService Exp => _t.Svc<IExportacionService>();
    private IBorradoresService Bor => _t.Svc<IBorradoresService>();
    private IErrorTecnicoService Err => _t.Svc<IErrorTecnicoService>();
    private IDashboardService Dash => _t.Svc<IDashboardService>();

    private async Task<List<string?>[]> FilasAsync(string tipo, ReporteFiltro? f = null) =>
        (await Rep.ConsultarAsync(tipo, f ?? new ReporteFiltro(), default)).Filas.ToArray();

    private static int Indice(ReporteDto r, string clave) => r.Columnas.FindIndex(c => c.Clave == clave);

    // 1. Reportes de trabajadores -------------------------------------------------------------
    [Fact]
    public async Task El_reporte_de_trabajadores_respeta_el_alcance_de_cada_rol()
    {
        var s = await EscenarioAsync();
        ComoAdmin(s);
        var admin = await Rep.ConsultarAsync(TipoReporte.Trabajadores, new ReporteFiltro(), default);
        Assert.Equal(3, admin.Total);
        Assert.Equal("Activos", admin.Resumen[1].Etiqueta);
        Assert.Equal("3", admin.Resumen[1].Valor);
        Assert.Equal(new[] { "Luis Mora", "Otro Ruiz", "Sara Sup" }, admin.Filas.Select(f => f[Indice(admin, "nombre")]!).OrderBy(x => x).ToArray());

        Como(s, s.UsuarioRita);
        Assert.Equal(3, (await Rep.ConsultarAsync(TipoReporte.Trabajadores, new ReporteFiltro(), default)).Total);

        // El supervisor ve a su equipo y a sí mismo; el trabajador, solo lo suyo.
        Como(s, s.UsuarioSara);
        var sara = await Rep.ConsultarAsync(TipoReporte.Trabajadores, new ReporteFiltro(), default);
        Assert.Equal(2, sara.Total);
        Assert.DoesNotContain(sara.Filas, f => f[Indice(sara, "nombre")]!.Contains("Otro"));
        await RechazaCon(ServiceErrorKind.NotFound, () => Rep.ConsultarAsync(TipoReporte.Trabajadores, new ReporteFiltro { TrabajadorId = s.Otro }, default));

        Como(s, s.UsuarioLuis);
        var luis = await Rep.ConsultarAsync(TipoReporte.Trabajadores, new ReporteFiltro(), default);
        Assert.Equal(1, luis.Total);
        Assert.Contains("Luis", luis.Filas[0][Indice(luis, "nombre")]);
    }

    [Fact]
    public async Task El_reporte_de_trabajadores_filtra_y_conserva_las_bajas_en_el_historico()
    {
        var s = await EscenarioAsync();
        ComoAdmin(s);
        await _t.Svc<ITrabajadorService>().DarDeBajaAsync(s.Otro, new BajaTrabajadorRequest { FechaBaja = new DateOnly(2026, 1, 1), Motivo = "Renuncia voluntaria" }, default);

        var todos = await Rep.ConsultarAsync(TipoReporte.Trabajadores, new ReporteFiltro(), default);
        Assert.Equal(3, todos.Total);                                    // la baja no elimina al trabajador
        var otro = Assert.Single(todos.Filas, f => f[Indice(todos, "nombre")]!.Contains("Otro"));
        Assert.Equal("Baja", otro[Indice(todos, "estado")]);
        Assert.Equal("01/01/2026", otro[Indice(todos, "baja")]);
        Assert.DoesNotContain(todos.Filas.SelectMany(f => f), c => c is not null && c.Contains("Renuncia"));   // el motivo de la baja no se expone

        Assert.Equal(2, (await Rep.ConsultarAsync(TipoReporte.Trabajadores, new ReporteFiltro { DepartamentoId = s.Operaciones }, default)).Total);
        Assert.Equal(1, (await Rep.ConsultarAsync(TipoReporte.Trabajadores, new ReporteFiltro { Estado = EstadoLaboral.Baja }, default)).Total);
        Assert.Equal(1, (await Rep.ConsultarAsync(TipoReporte.Trabajadores, new ReporteFiltro { SupervisorId = s.Sara }, default)).Total);
        Assert.Equal(1, (await Rep.ConsultarAsync(TipoReporte.Trabajadores, new ReporteFiltro { Buscar = "mora" }, default)).Total);

        // Altas y bajas del periodo: en 2026 solo hay una baja y ninguna alta (todos ingresaron en 2020).
        var periodo = await Rep.ConsultarAsync(TipoReporte.Trabajadores, new ReporteFiltro { Periodo = "anio", Anio = 2026 }, default);
        Assert.Equal(1, periodo.Total);
        Assert.Equal("1", periodo.Resumen.Single(x => x.Etiqueta == "Bajas en el periodo").Valor);
        Assert.Equal("0", periodo.Resumen.Single(x => x.Etiqueta == "Altas en el periodo").Valor);

        // Orden y paginación en servidor.
        var pagina = await Rep.ConsultarAsync(TipoReporte.Trabajadores, new ReporteFiltro { Page = 2, PageSize = 2, OrdenarPor = "ingreso" }, default);
        Assert.Single(pagina.Filas);
        Assert.Equal(3, pagina.Total);
    }

    [Fact]
    public async Task Los_filtros_de_periodo_y_fechas_se_validan()
    {
        var s = await EscenarioAsync();
        ComoAdmin(s);
        await RechazaCon(ServiceErrorKind.Validation, () => Rep.ConsultarAsync(TipoReporte.Trabajadores, new ReporteFiltro { Periodo = "siglo" }, default));
        await RechazaCon(ServiceErrorKind.Validation, () => Rep.ConsultarAsync(TipoReporte.Trabajadores, new ReporteFiltro { Periodo = "mes", Numero = 13 }, default));
        await RechazaCon(ServiceErrorKind.Validation, () => Rep.ConsultarAsync(TipoReporte.Trabajadores, new ReporteFiltro { Desde = new DateOnly(2026, 2, 1), Hasta = new DateOnly(2026, 1, 1) }, default));
        await RechazaCon(ServiceErrorKind.NotFound, () => Rep.ConsultarAsync("inexistente", new ReporteFiltro(), default));
        var trimestre = await Rep.ConsultarAsync(TipoReporte.Trabajadores, new ReporteFiltro { Periodo = "trimestre", Anio = 2026, Numero = 1 }, default);
        Assert.Contains("Desde: 01/01/2026", trimestre.FiltrosAplicados);
        Assert.Contains("Hasta: 31/03/2026", trimestre.FiltrosAplicados);
    }

    [Fact]
    public async Task El_reporte_de_cambios_laborales_muestra_el_historial_sin_datos_personales()
    {
        var s = await EscenarioAsync();
        ComoAdmin(s);
        await _t.Svc<ITrabajadorService>().DarDeBajaAsync(s.Otro, new BajaTrabajadorRequest { FechaBaja = new DateOnly(2026, 1, 1), Motivo = "Renuncia voluntaria" }, default);
        var r = await Rep.ConsultarAsync(TipoReporte.Movimientos, new ReporteFiltro { Buscar = "baja" }, default);
        var fila = Assert.Single(r.Filas);
        Assert.Equal("Baja", fila[Indice(r, "tipo")]);
        Assert.NotEmpty(await FilasAsync(TipoReporte.Movimientos));
        Como(s, s.UsuarioLuis);
        Assert.All(await FilasAsync(TipoReporte.Movimientos), f => Assert.Contains("Luis", f[1]));
    }

    // 2. Reportes de permisos ------------------------------------------------------------------
    private async Task<SolicitudPermisoDto> SolicitarAsync(Esc s, Guid usuario, string ini, string fin, string? motivo = null)
    {
        Como(s, usuario);
        return await _t.Svc<ISolicitudesPermisoService>().CrearAsync(new CrearSolicitudPermisoRequest
        {
            TipoPermisoId = s.Vacaciones, FechaInicio = DateTime.Parse(ini, CultureInfo.InvariantCulture), FechaFin = DateTime.Parse(fin, CultureInfo.InvariantCulture), Motivo = motivo
        }, default);
    }

    [Fact]
    public async Task El_reporte_de_permisos_respeta_el_alcance_resume_cantidades_y_nunca_incluye_motivos()
    {
        var s = await EscenarioAsync();
        var deLuis = await SolicitarAsync(s, s.UsuarioLuis, "2026-01-12", "2026-01-13", motivo: "Cita con el cardiólogo");
        await SolicitarAsync(s, s.UsuarioOtro, "2026-01-19", "2026-01-19", motivo: "Tratamiento médico");

        Como(s, s.UsuarioSara);
        await _t.Svc<IAprobacionesPermisoService>().ResolverAsync(deLuis.SolicitudPermisoId, new ResolverPermisoRequest { Aprobar = true }, default);

        ComoAdmin(s);
        var admin = await Rep.ConsultarAsync(TipoReporte.Permisos, new ReporteFiltro(), default);
        Assert.Equal(2, admin.Total);
        Assert.DoesNotContain(admin.Columnas, c => c.Titulo.Contains("Motivo", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(admin.Filas.SelectMany(f => f), c => c is not null && (c.Contains("cardi", StringComparison.OrdinalIgnoreCase) || c.Contains("médico", StringComparison.OrdinalIgnoreCase)));
        Assert.Equal("2", admin.Resumen.Single(x => x.Etiqueta == "Días autorizados").Valor);
        Assert.Equal("1", admin.Resumen.Single(x => x.Etiqueta == "Días pendientes de aprobar").Valor);
        var aprobada = Assert.Single(admin.Filas, f => f[Indice(admin, "estado")] == "Aprobada");
        Assert.Equal("Sara Sup", aprobada[Indice(admin, "resuelto")]);

        Assert.Equal(1, (await Rep.ConsultarAsync(TipoReporte.Permisos, new ReporteFiltro { Estado = EstadoSolicitudPermiso.Pendiente }, default)).Total);
        Assert.Equal(1, (await Rep.ConsultarAsync(TipoReporte.Permisos, new ReporteFiltro { DepartamentoId = s.Operaciones }, default)).Total);
        Assert.Equal(1, (await Rep.ConsultarAsync(TipoReporte.Permisos, new ReporteFiltro { Desde = new DateOnly(2026, 1, 15), Hasta = new DateOnly(2026, 1, 31) }, default)).Total);
        Assert.Equal(0, (await Rep.ConsultarAsync(TipoReporte.Permisos, new ReporteFiltro { TipoPermisoId = Guid.NewGuid() }, default)).Total);

        Como(s, s.UsuarioSara);   // supervisa a Luis, no a Otro
        var sara = await Rep.ConsultarAsync(TipoReporte.Permisos, new ReporteFiltro(), default);
        Assert.Equal(1, sara.Total);
        await RechazaCon(ServiceErrorKind.NotFound, () => Rep.ConsultarAsync(TipoReporte.Permisos, new ReporteFiltro { TrabajadorId = s.Otro }, default));

        Como(s, s.UsuarioLuis);
        Assert.Equal(1, (await Rep.ConsultarAsync(TipoReporte.Permisos, new ReporteFiltro(), default)).Total);
    }

    // 3. Reportes de asistencia ----------------------------------------------------------------
    [Fact]
    public async Task El_reporte_de_asistencia_muestra_jornadas_incompletas_dentro_del_alcance()
    {
        var s = await EscenarioAsync();
        Como(s, s.UsuarioLuis);
        await _t.Svc<IAsistenciaService>().RegistrarEntradaAsync(new RegistrarAsistenciaRequest(), default);

        ComoAdmin(s);
        var admin = await Rep.ConsultarAsync(TipoReporte.Asistencia, new ReporteFiltro { SoloIncompletos = true }, default);
        Assert.Equal(1, admin.Total);
        Assert.Equal("Incompleta (sin salida)", admin.Filas[0][Indice(admin, "estado")]);
        Assert.Equal(1, (await Rep.ConsultarAsync(TipoReporte.Asistencia, new ReporteFiltro { DepartamentoId = s.Operaciones }, default)).Total);
        Assert.Equal(0, (await Rep.ConsultarAsync(TipoReporte.Asistencia, new ReporteFiltro { DepartamentoId = Guid.NewGuid() }, default)).Total);

        Como(s, s.UsuarioSara);
        Assert.Equal(1, (await Rep.ConsultarAsync(TipoReporte.Asistencia, new ReporteFiltro(), default)).Total);
        Como(s, s.UsuarioLuis);
        Assert.Equal(1, (await Rep.ConsultarAsync(TipoReporte.Asistencia, new ReporteFiltro(), default)).Total);
        Como(s, s.UsuarioOtro);
        Assert.Equal(0, (await Rep.ConsultarAsync(TipoReporte.Asistencia, new ReporteFiltro(), default)).Total);
        await RechazaCon(ServiceErrorKind.NotFound, () => Rep.ConsultarAsync(TipoReporte.Asistencia, new ReporteFiltro { TrabajadorId = s.Luis }, default));
    }

    // 4. Proyectos y tareas --------------------------------------------------------------------
    private async Task<(ProyectoDetalleDto P, TareaDetalleDto T)> ProyectoConTareaAsync(Esc s, DateOnly? vence = null)
    {
        Como(s, s.UsuarioSara);
        var p = await _t.Svc<IProyectosService>().CrearAsync(new GuardarProyectoRequest { Nombre = "Rediseño web" }, default);
        p = await _t.Svc<IProyectosService>().AsignarTrabajadoresAsync(p.ProyectoId, new AsignarTrabajadoresRequest { TrabajadorIds = new() { s.Luis } }, default);
        var t = await _t.Svc<ITareasService>().CrearAsync(p.ProyectoId, new GuardarTareaRequest { Titulo = "Maquetar", FechaVencimiento = vence, TrabajadorIds = new() { s.Luis } }, default);
        return (p, t);
    }

    [Fact]
    public async Task Los_reportes_de_proyectos_tareas_y_carga_usan_la_visibilidad_del_modulo()
    {
        var s = await EscenarioAsync();
        var (p, t) = await ProyectoConTareaAsync(s, new DateOnly(2025, 12, 29));
        Como(s, s.UsuarioLuis);
        var listo = await _t.Svc<ITareasService>().ActualizarAvanceAsync(t.TareaId, new ActualizarAvanceRequest { PorcentajeAvance = 100, RowVersion = t.RowVersion }, default);
        Assert.Equal(EstadoTarea.EnRevision, listo.Estado);

        Como(s, s.UsuarioSara);
        var proyectos = await Rep.ConsultarAsync(TipoReporte.Proyectos, new ReporteFiltro(), default);
        var fila = Assert.Single(proyectos.Filas);
        Assert.Equal("Rediseño web", fila[0]);
        Assert.Equal("1", fila[Indice(proyectos, "revision")]);
        Assert.Equal("1", fila[Indice(proyectos, "vencidas")]);
        Assert.Equal("100", fila[Indice(proyectos, "avance")]);
        Assert.Equal("Sí", fila[Indice(proyectos, "atencion")]);
        Assert.Contains("productividad", proyectos.Nota);

        var tareas = await Rep.ConsultarAsync(TipoReporte.Tareas, new ReporteFiltro { SoloVencidas = true }, default);
        Assert.Equal(1, tareas.Total);
        Assert.Equal("En revisión", tareas.Filas[0][Indice(tareas, "estado")]);
        Assert.Equal("1", tareas.Resumen.Single(x => x.Etiqueta == "Vencidas").Valor);

        var carga = await Rep.ConsultarAsync(TipoReporte.CargaTrabajo, new ReporteFiltro(), default);
        var luis = Assert.Single(carga.Filas);
        Assert.Contains("Luis", luis[0]);
        Assert.Equal("1", luis[Indice(carga, "abiertas")]);

        // Quien no participa en el proyecto no lo ve en ningún reporte.
        Como(s, s.UsuarioOtro);
        Assert.Empty((await Rep.ConsultarAsync(TipoReporte.Proyectos, new ReporteFiltro(), default)).Filas);
        Assert.Empty((await Rep.ConsultarAsync(TipoReporte.Tareas, new ReporteFiltro(), default)).Filas);
        Assert.Empty((await Rep.ConsultarAsync(TipoReporte.CargaTrabajo, new ReporteFiltro(), default)).Filas);

        var otra = await _t.CrearEmpresaAsync("Otra", "admin@o.com");
        _t.NewScope(); _t.Current.As(otra, otra.AdminId);
        Assert.Equal(0, (await Rep.ConsultarAsync(TipoReporte.Proyectos, new ReporteFiltro { ProyectoId = p.ProyectoId }, default)).Total);
        Assert.Equal(0, (await Rep.ConsultarAsync(TipoReporte.Trabajadores, new ReporteFiltro(), default)).Total);
    }

    // 5. Exportación ----------------------------------------------------------------------------
    [Fact]
    public async Task Solo_administrador_y_recursos_humanos_exportan_y_la_api_lo_valida()
    {
        var s = await EscenarioAsync();
        foreach (var usuario in new[] { s.UsuarioSara, s.UsuarioLuis, s.UsuarioOtro })
        {
            Como(s, usuario);
            await RechazaCon(ServiceErrorKind.Forbidden, () => Exp.ExportarAsync(TipoReporte.Trabajadores, FormatoExportacion.Excel, new ReporteFiltro(), default));
        }
        Como(s, s.UsuarioRita);
        Assert.NotEmpty((await Exp.ExportarAsync(TipoReporte.Trabajadores, FormatoExportacion.Excel, new ReporteFiltro(), default)).Contenido);
        ComoAdmin(s);
        await RechazaCon(ServiceErrorKind.Validation, () => Exp.ExportarAsync(TipoReporte.Trabajadores, "docx", new ReporteFiltro(), default));
        await RechazaCon(ServiceErrorKind.NotFound, () => Exp.ExportarAsync("inexistente", FormatoExportacion.Pdf, new ReporteFiltro(), default));
    }

    [Fact]
    public async Task El_excel_y_el_pdf_respetan_los_mismos_filtros_que_la_pantalla_y_se_auditan()
    {
        var s = await EscenarioAsync();
        await SolicitarAsync(s, s.UsuarioLuis, "2026-01-12", "2026-01-13", motivo: "Cita con el cardiólogo");
        ComoAdmin(s);
        var filtro = new ReporteFiltro { DepartamentoId = s.Operaciones };
        var pantalla = await Rep.ConsultarAsync(TipoReporte.Trabajadores, filtro, default);

        var xlsx = await Exp.ExportarAsync(TipoReporte.Trabajadores, FormatoExportacion.Excel, filtro, default);
        Assert.EndsWith(".xlsx", xlsx.NombreArchivo);
        Assert.Contains("spreadsheetml", xlsx.TipoContenido);
        using (var doc = SpreadsheetDocument.Open(new MemoryStream(xlsx.Contenido), false))
        {
            var hoja = doc.WorkbookPart!.WorksheetParts.First().Worksheet;
            var filas = hoja.Descendants<Row>().ToList();
            var textos = filas.SelectMany(f => f.Descendants<Text>()).Select(x => x.Text).ToList();
            Assert.Contains("Acme", textos);
            Assert.Contains("Trabajadores", textos);
            Assert.Contains(textos, x => x.StartsWith("Filtros: Departamento: Operaciones"));
            // 4 filas de cabecera de página (empresa, título, fecha, filtros) + nota + blanco + encabezado + datos.
            Assert.Equal(pantalla.Total, filas.Count(f => f.Descendants<Text>().Any(x => x.Text.StartsWith("Luis") || x.Text.StartsWith("Sara"))));
            Assert.NotNull(hoja.Descendants<AutoFilter>().SingleOrDefault());
        }

        var pdf = await Exp.ExportarAsync(TipoReporte.Trabajadores, FormatoExportacion.Pdf, filtro, default);
        Assert.Equal("application/pdf", pdf.TipoContenido);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf.Contenido, 0, 4));
        Assert.True(pdf.Contenido.Length > 1000);

        // Un reporte de permisos exportado tampoco incluye el motivo.
        var permisos = await Exp.ExportarAsync(TipoReporte.Permisos, FormatoExportacion.Excel, new ReporteFiltro(), default);
        using (var doc = SpreadsheetDocument.Open(new MemoryStream(permisos.Contenido), false))
        {
            var textos = doc.WorkbookPart!.WorksheetParts.SelectMany(p => p.Worksheet.Descendants<Text>()).Select(x => x.Text).ToList();
            Assert.DoesNotContain(textos, x => x.Contains("cardi", StringComparison.OrdinalIgnoreCase));
        }

        var auditorias = await _t.Db.Auditoria.AsNoTracking().Where(a => a.Accion == "Exportar").ToListAsync();
        Assert.Equal(3, auditorias.Count);
        Assert.DoesNotContain(auditorias, a => (a.DatosDespuesJson ?? "").Contains("Luis"));   // se audita la operación, no el contenido
    }

    // 6. Borradores -----------------------------------------------------------------------------
    private static GuardarBorradorRequest Borrador(string datos = "{\"titulo\":\"Vacaciones de verano\",\"dias\":3}") =>
        new() { Modulo = ModuloBorrador.Permisos, TipoFormulario = "SolicitudPermiso", DatosJson = datos };

    [Fact]
    public async Task Un_borrador_solo_lo_ve_su_creador_y_no_es_una_solicitud_oficial()
    {
        var s = await EscenarioAsync();
        Como(s, s.UsuarioLuis);
        var b = await Bor.CrearAsync(Borrador(), default);
        Assert.Equal("Vacaciones de verano", b.Resumen);
        Assert.Equal(b.FechaCreacionUtc, b.FechaActualizacionUtc);
        Assert.Single((await Bor.ListarAsync(new BorradorFiltro(), default)).Items);

        // No aparece como permiso ni en reportes, aprobaciones o notificaciones.
        Assert.Equal(0, (await Rep.ConsultarAsync(TipoReporte.Permisos, new ReporteFiltro(), default)).Total);
        Como(s, s.UsuarioSara);
        Assert.Empty((await _t.Svc<ISolicitudesPermisoService>().BandejaAsync(new SolicitudPermisoFiltro(), default)).Items);
        Assert.Empty((await _t.Svc<INotificacionesService>().ListarAsync(new NotificacionFiltro(), default)).Items);

        // Otros usuarios (incluida la administración) no lo ven ni lo tocan.
        foreach (var usuario in new[] { s.UsuarioOtro, s.E.AdminId })
        {
            Como(s, usuario);
            Assert.Empty((await Bor.ListarAsync(new BorradorFiltro(), default)).Items);
            await RechazaCon(ServiceErrorKind.NotFound, () => Bor.ObtenerAsync(b.BorradorId, default));
            await RechazaCon(ServiceErrorKind.NotFound, () => Bor.ActualizarAsync(b.BorradorId, Borrador(), default));
            await RechazaCon(ServiceErrorKind.NotFound, () => Bor.EliminarAsync(b.BorradorId, default));
        }

        Pasa(TimeSpan.FromMinutes(5));
        Como(s, s.UsuarioLuis);
        var actualizado = await Bor.ActualizarAsync(b.BorradorId, Borrador("{\"titulo\":\"Otro título\"}"), default);
        Assert.Equal("Otro título", actualizado.Resumen);
        Assert.True(actualizado.FechaActualizacionUtc > actualizado.FechaCreacionUtc);
        await Bor.EliminarAsync(b.BorradorId, default);
        Assert.Empty((await Bor.ListarAsync(new BorradorFiltro(), default)).Items);
    }

    private void Pasa(TimeSpan t) => _t.Clock.Advance(t);

    [Fact]
    public async Task Los_borradores_validan_modulo_permisos_contenido_y_vigencia()
    {
        var s = await EscenarioAsync();
        Como(s, s.UsuarioLuis);
        await RechazaCon(ServiceErrorKind.Validation, () => Bor.CrearAsync(new GuardarBorradorRequest { Modulo = "Nomina", TipoFormulario = "X", DatosJson = "{}" }, default));
        await RechazaCon(ServiceErrorKind.Validation, () => Bor.CrearAsync(Borrador("no es json"), default));
        await RechazaCon(ServiceErrorKind.Validation, () => Bor.CrearAsync(Borrador("[1,2]"), default));
        await RechazaCon(ServiceErrorKind.Validation, () => Bor.CrearAsync(Borrador("{\"datos\":{\"contrasena\":\"abc\"}}"), default));
        await RechazaCon(ServiceErrorKind.Validation, () => Bor.CrearAsync(new GuardarBorradorRequest { Modulo = ModuloBorrador.Permisos, TipoFormulario = "<script>", DatosJson = "{}" }, default));
        // Luis no puede crear proyectos: tampoco guarda borradores de ellos.
        await RechazaCon(ServiceErrorKind.Forbidden, () => Bor.CrearAsync(new GuardarBorradorRequest { Modulo = ModuloBorrador.Proyectos, TipoFormulario = "Proyecto", DatosJson = "{}" }, default));

        var b = await Bor.CrearAsync(Borrador(), default);
        await RechazaCon(ServiceErrorKind.Validation, () => Bor.ActualizarAsync(b.BorradorId, new GuardarBorradorRequest { Modulo = ModuloBorrador.Permisos, TipoFormulario = "Otro", DatosJson = "{}" }, default));

        Como(s, s.UsuarioSara);   // supervisor: sí puede capturar proyectos
        Assert.Equal("Plan", (await Bor.CrearAsync(new GuardarBorradorRequest { Modulo = ModuloBorrador.Proyectos, TipoFormulario = "Proyecto", DatosJson = "{\"nombre\":\"Plan\"}" }, default)).Resumen);

        Pasa(TimeSpan.FromDays(BorradoresService.DiasVigencia + 1));
        Como(s, s.UsuarioLuis);
        Assert.Empty((await Bor.ListarAsync(new BorradorFiltro(), default)).Items);   // vencido: deja de mostrarse
        await RechazaCon(ServiceErrorKind.NotFound, () => Bor.ObtenerAsync(b.BorradorId, default));
    }

    [Fact]
    public async Task Hay_un_limite_de_borradores_por_usuario()
    {
        var s = await EscenarioAsync();
        Como(s, s.UsuarioLuis);
        for (var i = 0; i < BorradoresService.MaxPorUsuario; i++) await Bor.CrearAsync(Borrador($"{{\"n\":{i}}}"), default);
        await RechazaCon(ServiceErrorKind.Conflict, () => Bor.CrearAsync(Borrador(), default));
        Como(s, s.UsuarioOtro);
        await Bor.CrearAsync(Borrador(), default);   // el límite es por usuario
    }

    // 7. Errores técnicos ----------------------------------------------------------------------
    [Fact]
    public async Task Los_errores_tecnicos_no_guardan_secretos_y_solo_los_consulta_quien_tiene_el_permiso()
    {
        var s = await EscenarioAsync();
        var correlacion = Guid.NewGuid();
        var token = new string('a', 40);
        ComoAdmin(s);
        try { throw new InvalidOperationException("password=Secreto123 token=abc"); }
        catch (Exception ex)
        {
            await Err.RegistrarAsync(ex, "POST", $"/api/auth/confirmar/{token}?clave=Secreto123", correlacion, s.E.EmpresaId, s.E.AdminId, default);
        }

        var lista = await Err.ListarAsync(new ErrorTecnicoFiltro(), default);
        var e = Assert.Single(lista.Items);
        Assert.Equal("System.InvalidOperationException", e.Codigo);
        Assert.Equal(correlacion, e.CorrelationId);
        Assert.Equal("Error", e.Nivel);
        Assert.Equal("/api/auth/confirmar/{…}", e.Ruta);
        var todo = string.Join(" ", e.Mensaje, e.Ruta, e.Codigo, e.DetalleTecnico);
        Assert.DoesNotContain("Secreto123", todo);
        Assert.DoesNotContain(token, todo);
        Assert.DoesNotContain("password", todo, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Bloque10Tests", e.DetalleTecnico);   // la pila de métodos sí se conserva
        Assert.Single((await Err.ListarAsync(new ErrorTecnicoFiltro { CorrelationId = correlacion }, default)).Items);
        Assert.Empty((await Err.ListarAsync(new ErrorTecnicoFiltro { CorrelationId = Guid.NewGuid() }, default)).Items);

        foreach (var usuario in new[] { s.UsuarioRita, s.UsuarioSara, s.UsuarioLuis })
        {
            Como(s, usuario);
            await RechazaCon(ServiceErrorKind.Forbidden, () => Err.ListarAsync(new ErrorTecnicoFiltro(), default));
        }

        var otra = await _t.CrearEmpresaAsync("Otra", "admin@o.com");
        _t.NewScope(); _t.Current.As(otra, otra.AdminId);
        Assert.Empty((await Err.ListarAsync(new ErrorTecnicoFiltro(), default)).Items);   // aislamiento entre empresas
    }

    // 8. Dashboard ------------------------------------------------------------------------------
    [Fact]
    public async Task El_dashboard_se_adapta_al_perfil_y_al_alcance_del_usuario()
    {
        var s = await EscenarioAsync();
        await ProyectoConTareaAsync(s, new DateOnly(2025, 12, 29));
        await SolicitarAsync(s, s.UsuarioLuis, "2026-01-12", "2026-01-13");

        ComoAdmin(s);
        var admin = await Dash.ObtenerAsync(default);
        Assert.Equal((NombresRol.Administrador, "empresa"), (admin.Perfil, admin.Ambito));
        Assert.Equal(3, admin.Trabajadores!.Activos);
        Assert.Equal(1, admin.Permisos!.PendientesPorRevisar);
        Assert.Equal(1, admin.Proyectos!.Activos);
        Assert.Equal(1, admin.Tareas!.Vencidas);
        Assert.NotNull(admin.UsuariosActivos);
        Assert.Contains(admin.Accesos, a => a.Ruta == "reportes");
        Assert.NotEmpty(admin.Actividad);

        Como(s, s.UsuarioRita);
        var rita = await Dash.ObtenerAsync(default);
        Assert.Equal((NombresRol.RecursosHumanos, "empresa"), (rita.Perfil, rita.Ambito));

        Como(s, s.UsuarioSara);
        var sara = await Dash.ObtenerAsync(default);
        Assert.Equal((NombresRol.Supervisor, "equipo"), (sara.Perfil, sara.Ambito));
        Assert.Equal(1, sara.Trabajadores!.Activos);                 // los trabajadores a su cargo (Luis)
        Assert.Equal(1, sara.Permisos!.PendientesPorRevisar);
        Assert.Null(sara.UsuariosActivos);

        Como(s, s.UsuarioLuis);
        var luis = await Dash.ObtenerAsync(default);
        Assert.Equal((NombresRol.Trabajador, "propio"), (luis.Perfil, luis.Ambito));
        Assert.Null(luis.Trabajadores);
        Assert.Null(luis.Permisos!.PendientesPorRevisar);
        Assert.Equal(1, luis.Permisos.MisPendientes);
        Assert.Equal(1, luis.Tareas!.Pendientes);
        Assert.Equal(1, luis.Tareas.Vencidas);
        Assert.True(luis.NotificacionesNoLeidas >= 1);                // la asignación de la tarea
        Assert.DoesNotContain(luis.Accesos, a => a.Ruta == "permisos/aprobaciones");
        Assert.Contains(luis.Accesos, a => a.Ruta == "mis-tareas");

        Como(s, s.UsuarioOtro);
        var otro = await Dash.ObtenerAsync(default);
        Assert.Equal(0, otro.Tareas!.Pendientes + otro.Tareas.Vencidas);
        Assert.Equal(0, otro.Permisos!.MisPendientes);

        var otra = await _t.CrearEmpresaAsync("Otra", "admin@o.com");
        _t.NewScope(); _t.Current.As(otra, otra.AdminId);
        var vacia = await Dash.ObtenerAsync(default);
        Assert.Equal(0, vacia.Trabajadores!.Activos);
        Assert.Equal(0, vacia.Tareas!.Vencidas);
        Assert.Empty(vacia.Actividad);
    }

    [Fact]
    public void Los_permisos_del_bloque_10_se_asignan_segun_el_rol()
    {
        Assert.Contains(Permisos.ReportesExportar, PermisosPorDefecto.PorRol[NombresRol.Administrador]);
        Assert.Contains(Permisos.ReportesExportar, PermisosPorDefecto.PorRol[NombresRol.RecursosHumanos]);
        Assert.DoesNotContain(Permisos.ReportesExportar, PermisosPorDefecto.PorRol[NombresRol.Supervisor]);
        Assert.DoesNotContain(Permisos.ReportesExportar, PermisosPorDefecto.PorRol[NombresRol.Trabajador]);
        Assert.All(NombresRol.Iniciales, r => Assert.Contains(Permisos.ReportesConsultar, PermisosPorDefecto.PorRol[r]));
        Assert.Contains(Permisos.ErroresConsultar, PermisosPorDefecto.PorRol[NombresRol.Administrador]);
        Assert.DoesNotContain(Permisos.ErroresConsultar, PermisosPorDefecto.PorRol[NombresRol.RecursosHumanos]);
    }
}
