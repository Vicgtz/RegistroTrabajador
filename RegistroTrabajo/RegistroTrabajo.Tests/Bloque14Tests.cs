using System.IO.Compression;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Tests;

public class Bloque14Tests : IDisposable
{
    private readonly TestEnv _t = new();
    public void Dispose() => _t.Dispose();

    private static async Task RechazaCon(ServiceErrorKind kind, Func<Task> accion) =>
        Assert.Equal(kind, (await Assert.ThrowsAnyAsync<ServiceException>(accion)).Kind);

    private IExpedienteProfesionalService Exp => _t.Svc<IExpedienteProfesionalService>();
    private ICurriculumService Cv => _t.Svc<ICurriculumService>();

    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj\n<<>>\nendobj\n");

    private static byte[] Docx()
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            var e = zip.CreateEntry("word/document.xml");
            using var w = new StreamWriter(e.Open());
            w.Write("<w:document/>");
        }
        return ms.ToArray();
    }

    /// <summary>Empresa con admin (todos los permisos) y dos trabajadores con cuenta de rol Trabajador. El contexto queda como admin.</summary>
    private async Task<(EmpresaInfo E, Guid Ana, Guid AnaUsuario, Guid Beto, Guid BetoUsuario)> EscenarioAsync(string nombre = "Acme", string correo = "admin@a.com")
    {
        var e = await _t.CrearEmpresaAsync(nombre, correo);
        _t.Current.As(e, e.AdminId);
        var ua = await _t.NuevoUsuarioAsync(e.EmpresaId, "ana@" + correo, "Ana", "Ruiz", e.Roles[NombresRol.Trabajador]);
        var ub = await _t.NuevoUsuarioAsync(e.EmpresaId, "beto@" + correo, "Beto", "Soto", e.Roles[NombresRol.Trabajador]);
        var ana = await _t.Svc<ITrabajadorService>().CrearAsync(new CrearTrabajadorRequest { Nombres = "Ana", Apellidos = "Ruiz", FechaIngreso = new DateOnly(2025, 1, 1), UsuarioId = ua }, default);
        var beto = await _t.Svc<ITrabajadorService>().CrearAsync(new CrearTrabajadorRequest { Nombres = "Beto", Apellidos = "Soto", FechaIngreso = new DateOnly(2025, 1, 1), UsuarioId = ub }, default);
        return (e, ana.TrabajadorId, ua, beto.TrabajadorId, ub);
    }

    private static GuardarHabilidadRequest Hab(string nombre, byte nivel = NivelHabilidad.Intermedio) => new() { Nombre = nombre, Nivel = nivel };

    // ---------------- Habilidades ----------------
    [Fact]
    public async Task Rh_agrega_habilidades_aprobadas_y_el_catalogo_no_duplica_por_mayusculas_o_espacios()
    {
        var (_, ana, _, beto, _) = await EscenarioAsync();
        var a = await Exp.GuardarHabilidadAsync(ana, Hab("C#", NivelHabilidad.Avanzado), default);
        var b = await Exp.GuardarHabilidadAsync(beto, Hab("  c#  "), default);
        Assert.Equal(EstadoExpediente.Aprobado, a.Estado);
        Assert.Equal(a.HabilidadId, b.HabilidadId);
        Assert.Single(await _t.Db.Habilidades.ToListAsync());

        // Repetir la habilidad del mismo trabajador la actualiza, no la duplica.
        var c = await Exp.GuardarHabilidadAsync(ana, Hab("C#", NivelHabilidad.Basico), default);
        Assert.Equal(a.TrabajadorHabilidadId, c.TrabajadorHabilidadId);
        Assert.Equal(NivelHabilidad.Basico, c.Nivel);
        Assert.Single((await Exp.ObtenerAsync(ana, default)).Habilidades);
    }

    [Fact]
    public async Task Trabajador_propone_su_habilidad_queda_pendiente_y_no_aparece_en_busquedas_hasta_aprobarse()
    {
        var (e, ana, ua, _, _) = await EscenarioAsync();
        _t.Current.As(e, ua);
        var p = await Exp.GuardarHabilidadAsync(ana, Hab("Python", NivelHabilidad.Avanzado), default);
        Assert.Equal(EstadoExpediente.Pendiente, p.Estado);
        var catalogoId = p.HabilidadId;

        _t.NewScope(); _t.Current.As(e, e.AdminId);
        Assert.Empty((await Exp.BuscarAsync(new PerfilFiltro { HabilidadIds = new() { catalogoId } }, default)).Items);
        var pendientes = await Exp.PendientesAsync(default);
        var pend = Assert.Single(pendientes);
        Assert.Equal("Habilidad", pend.Clase);

        await Exp.ResolverHabilidadAsync(pend.Id, new ResolverExpedienteRequest { Aprobar = true }, default);
        var r = await Exp.BuscarAsync(new PerfilFiltro { HabilidadIds = new() { catalogoId } }, default);
        Assert.Equal(ana, Assert.Single(r.Items).TrabajadorId);
        Assert.Empty(await Exp.PendientesAsync(default));
        await RechazaCon(ServiceErrorKind.Conflict, () => Exp.ResolverHabilidadAsync(pend.Id, new ResolverExpedienteRequest { Aprobar = false, MotivoRechazo = "x" }, default));
    }

    [Fact]
    public async Task Rechazar_exige_motivo_y_el_trabajador_lo_ve()
    {
        var (e, ana, ua, _, _) = await EscenarioAsync();
        _t.Current.As(e, ua);
        await Exp.CrearEstudioAsync(ana, new GuardarEstudioRequest { Tipo = TipoEstudio.Maestria, Titulo = "MBA" }, default);

        _t.NewScope(); _t.Current.As(e, e.AdminId);
        var pend = Assert.Single(await Exp.PendientesAsync(default));
        await RechazaCon(ServiceErrorKind.Validation, () => Exp.ResolverEstudioAsync(pend.Id, new ResolverExpedienteRequest { Aprobar = false }, default));
        await Exp.ResolverEstudioAsync(pend.Id, new ResolverExpedienteRequest { Aprobar = false, MotivoRechazo = "Sin comprobante" }, default);

        _t.NewScope(); _t.Current.As(e, ua);
        var estudio = Assert.Single((await Exp.ObtenerAsync(ana, default)).Estudios);
        Assert.Equal(EstadoExpediente.Rechazado, estudio.Estado);
        Assert.Equal("Sin comprobante", estudio.MotivoRechazo);
    }

    [Fact]
    public async Task Trabajador_no_modifica_a_otro_ni_aprueba_ni_busca()
    {
        var (e, ana, ua, beto, _) = await EscenarioAsync();
        _t.Current.As(e, ua);
        await RechazaCon(ServiceErrorKind.Forbidden, () => Exp.GuardarHabilidadAsync(beto, Hab("SQL"), default));
        await RechazaCon(ServiceErrorKind.Forbidden, () => Exp.CrearEstudioAsync(beto, new GuardarEstudioRequest { Titulo = "Lic." }, default));
        await RechazaCon(ServiceErrorKind.Forbidden, () => Exp.PendientesAsync(default));
        await RechazaCon(ServiceErrorKind.Forbidden, () => Exp.BuscarAsync(new PerfilFiltro(), default));
        await RechazaCon(ServiceErrorKind.Forbidden, () => Exp.ObtenerAsync(beto, default));
        Assert.Equal(ana, (await Exp.MiExpedienteAsync(default)).TrabajadorId);
    }

    [Fact]
    public async Task Quien_solo_consulta_ve_unicamente_lo_aprobado()
    {
        var (e, ana, ua, _, _) = await EscenarioAsync();
        await Exp.GuardarHabilidadAsync(ana, Hab("SQL"), default);                 // aprobada por RH
        _t.NewScope(); _t.Current.As(e, ua);
        await Exp.GuardarHabilidadAsync(ana, Hab("Go"), default);                  // pendiente (propuesta)

        var sup = await _t.NuevoUsuarioAsync(e.EmpresaId, "sup@x.com", "Sup", "Uno", e.Roles[NombresRol.Supervisor]);
        _t.NewScope(); _t.Current.As(e, sup);
        var vista = await Exp.ObtenerAsync(ana, default);
        Assert.Equal("SQL", Assert.Single(vista.Habilidades).Nombre);
        Assert.False(vista.PuedeEditar);
        await RechazaCon(ServiceErrorKind.Forbidden, () => Exp.GuardarHabilidadAsync(ana, Hab("Rust"), default));
    }

    // ---------------- Estudios ----------------
    [Fact]
    public async Task Estudios_se_validan_y_se_ordenan_con_los_en_curso_primero()
    {
        var (_, ana, _, _, _) = await EscenarioAsync();
        await RechazaCon(ServiceErrorKind.Validation, () => Exp.CrearEstudioAsync(ana, new GuardarEstudioRequest
        { Titulo = "X", FechaInicio = new DateOnly(2020, 1, 1), FechaFin = new DateOnly(2019, 1, 1) }, default));
        await RechazaCon(ServiceErrorKind.Validation, () => Exp.CrearEstudioAsync(ana, new GuardarEstudioRequest
        { Titulo = "Maestría", EnCurso = true, FechaFin = new DateOnly(2020, 1, 1) }, default));
        await RechazaCon(ServiceErrorKind.Validation, () => Exp.CrearEstudioAsync(ana, new GuardarEstudioRequest { Titulo = "Algo", Tipo = 9 }, default));

        await Exp.CrearEstudioAsync(ana, new GuardarEstudioRequest { Tipo = TipoEstudio.Licenciatura, Titulo = "Ing. Sistemas", Institucion = "UNAM", FechaInicio = new DateOnly(2015, 1, 1), FechaFin = new DateOnly(2019, 6, 1) }, default);
        await Exp.CrearEstudioAsync(ana, new GuardarEstudioRequest { Tipo = TipoEstudio.Maestria, Titulo = "Ciencia de Datos", EnCurso = true }, default);
        var lista = (await Exp.ObtenerAsync(ana, default)).Estudios;
        Assert.Equal(2, lista.Count);
        Assert.Equal("Ciencia de Datos", lista[0].Titulo);
    }

    [Fact]
    public async Task Editar_un_estudio_propio_aprobado_lo_devuelve_a_pendiente()
    {
        var (e, ana, ua, _, _) = await EscenarioAsync();
        var est = await Exp.CrearEstudioAsync(ana, new GuardarEstudioRequest { Titulo = "Licenciatura" }, default);
        Assert.Equal(EstadoExpediente.Aprobado, est.Estado);

        _t.NewScope(); _t.Current.As(e, ua);
        var editado = await Exp.ActualizarEstudioAsync(ana, est.EstudioId, new GuardarEstudioRequest { Titulo = "Licenciatura en Física" }, default);
        Assert.Equal(EstadoExpediente.Pendiente, editado.Estado);
        await Exp.EliminarEstudioAsync(ana, est.EstudioId, default);
        Assert.Empty((await Exp.ObtenerAsync(ana, default)).Estudios);
    }

    // ---------------- Búsqueda ----------------
    [Fact]
    public async Task Busqueda_por_habilidades_todas_o_alguna_nivel_minimo_y_estudio()
    {
        var (_, ana, _, beto, _) = await EscenarioAsync();
        var cs = await Exp.GuardarHabilidadAsync(ana, Hab("C#", NivelHabilidad.Avanzado), default);
        var py = await Exp.GuardarHabilidadAsync(ana, Hab("Python", NivelHabilidad.Basico), default);
        await Exp.GuardarHabilidadAsync(beto, Hab("Python", NivelHabilidad.Avanzado), default);
        await Exp.CrearEstudioAsync(ana, new GuardarEstudioRequest { Tipo = TipoEstudio.Maestria, Titulo = "Ciencia de Datos", Institucion = "ITAM" }, default);

        var todas = await Exp.BuscarAsync(new PerfilFiltro { HabilidadIds = new() { cs.HabilidadId, py.HabilidadId }, TodasLasHabilidades = true }, default);
        Assert.Equal(ana, Assert.Single(todas.Items).TrabajadorId);

        var alguna = await Exp.BuscarAsync(new PerfilFiltro { HabilidadIds = new() { cs.HabilidadId, py.HabilidadId }, TodasLasHabilidades = false }, default);
        Assert.Equal(2, alguna.Total);

        var avanzado = await Exp.BuscarAsync(new PerfilFiltro { HabilidadIds = new() { py.HabilidadId }, NivelMinimo = NivelHabilidad.Avanzado }, default);
        Assert.Equal(beto, Assert.Single(avanzado.Items).TrabajadorId);

        var maestria = await Exp.BuscarAsync(new PerfilFiltro { TipoEstudio = TipoEstudio.Maestria }, default);
        var item = Assert.Single(maestria.Items);
        Assert.Equal(ana, item.TrabajadorId);
        Assert.Contains("C# (Avanzado)", item.Habilidades);
        Assert.Contains("Maestría: Ciencia de Datos", item.Estudios);

        Assert.Single((await Exp.BuscarAsync(new PerfilFiltro { Estudio = "itam" }, default)).Items);
        Assert.Empty((await Exp.BuscarAsync(new PerfilFiltro { Estudio = "mit" }, default)).Items);
    }

    [Fact]
    public async Task Busqueda_excluye_dados_de_baja_por_defecto_y_limita_habilidades()
    {
        var (_, ana, _, _, _) = await EscenarioAsync();
        var cs = await Exp.GuardarHabilidadAsync(ana, Hab("C#"), default);
        await _t.Svc<ITrabajadorService>().DarDeBajaAsync(ana, new BajaTrabajadorRequest { FechaBaja = new DateOnly(2026, 1, 1), Motivo = "Renuncia" }, default);
        Assert.Empty((await Exp.BuscarAsync(new PerfilFiltro { HabilidadIds = new() { cs.HabilidadId } }, default)).Items);
        Assert.Single((await Exp.BuscarAsync(new PerfilFiltro { HabilidadIds = new() { cs.HabilidadId }, SoloActivos = false }, default)).Items);

        var demasiadas = Enumerable.Range(0, LimitesCurriculum.MaxHabilidadesPorBusqueda + 1).Select(_ => Guid.NewGuid()).ToList();
        await RechazaCon(ServiceErrorKind.Validation, () => Exp.BuscarAsync(new PerfilFiltro { HabilidadIds = demasiadas }, default));
    }

    // ---------------- Multiempresa ----------------
    [Fact]
    public async Task Otra_empresa_no_ve_ni_modifica_el_expediente_ni_comparte_catalogo()
    {
        var (_, ana, _, _, _) = await EscenarioAsync("Acme", "admin@a.com");
        await Exp.GuardarHabilidadAsync(ana, Hab("C#"), default);
        var habilidadA = (await _t.Db.Habilidades.AsNoTracking().SingleAsync()).HabilidadId;

        var b = await _t.CrearEmpresaAsync("Beta", "admin@b.com");
        _t.NewScope(); _t.Current.As(b, b.AdminId);
        await RechazaCon(ServiceErrorKind.NotFound, () => Exp.ObtenerAsync(ana, default));
        await RechazaCon(ServiceErrorKind.NotFound, () => Exp.GuardarHabilidadAsync(ana, Hab("SQL"), default));
        await RechazaCon(ServiceErrorKind.NotFound, () => Cv.GuardarAsync(ana, "cv.pdf", Pdf, default));
        Assert.Empty(await Exp.ListarHabilidadesAsync(null, false, default));
        await RechazaCon(ServiceErrorKind.NotFound, () => Exp.ActualizarHabilidadCatalogoAsync(habilidadA, new ActualizarHabilidadRequest { Nombre = "Otra" }, default));
        Assert.Empty((await Exp.BuscarAsync(new PerfilFiltro { HabilidadIds = new() { habilidadA }, TodasLasHabilidades = false }, default)).Items);
    }

    [Fact]
    public async Task Catalogo_se_renombra_sin_duplicar_y_una_desactivada_no_la_propone_el_trabajador()
    {
        var (e, ana, ua, _, _) = await EscenarioAsync();
        var cs = await Exp.GuardarHabilidadAsync(ana, Hab("C#"), default);
        var py = await Exp.GuardarHabilidadAsync(ana, Hab("Python"), default);
        await RechazaCon(ServiceErrorKind.Conflict, () => Exp.ActualizarHabilidadCatalogoAsync(py.HabilidadId, new ActualizarHabilidadRequest { Nombre = "c#" }, default));
        await Exp.ActualizarHabilidadCatalogoAsync(cs.HabilidadId, new ActualizarHabilidadRequest { Nombre = "CSharp", Activa = false }, default);
        Assert.DoesNotContain(await Exp.ListarHabilidadesAsync(null, false, default), h => h.HabilidadId == cs.HabilidadId);
        Assert.Contains(await Exp.ListarHabilidadesAsync(null, true, default), h => h.HabilidadId == cs.HabilidadId);

        _t.NewScope(); _t.Current.As(e, ua);
        await RechazaCon(ServiceErrorKind.Validation, () => Exp.GuardarHabilidadAsync(ana, Hab("csharp"), default));
        await RechazaCon(ServiceErrorKind.Forbidden, () => Exp.ActualizarHabilidadCatalogoAsync(py.HabilidadId, new ActualizarHabilidadRequest { Nombre = "Py" }, default));
    }

    // ---------------- Currículum ----------------
    [Fact]
    public async Task Curriculum_acepta_pdf_y_docx_reales_y_rechaza_el_resto()
    {
        var (_, ana, _, _, _) = await EscenarioAsync();
        var info = await Cv.GuardarAsync(ana, @"C:\fakepath\..\Mi CV.final.pdf", Pdf, default);
        Assert.Equal("application/pdf", info.TipoContenido);
        Assert.Equal("Mi CV.final.pdf", info.NombreArchivo);

        await RechazaCon(ServiceErrorKind.Validation, () => Cv.GuardarAsync(ana, "cv.pdf", Encoding.UTF8.GetBytes("<script>alert(1)</script>"), default));
        await RechazaCon(ServiceErrorKind.Validation, () => Cv.GuardarAsync(ana, "cv.docx", new byte[] { (byte)'P', (byte)'K', 3, 4, 0, 0 }, default));   // zip no válido
        await RechazaCon(ServiceErrorKind.Validation, () => Cv.GuardarAsync(ana, "cv.pdf", Array.Empty<byte>(), default));
        var grande = new byte[LimitesCurriculum.MaxBytes + 1];
        Array.Copy(Pdf, grande, Pdf.Length);
        await RechazaCon(ServiceErrorKind.Validation, () => Cv.GuardarAsync(ana, "cv.pdf", grande, default));

        // Reemplazo: sigue habiendo uno solo; el nombre toma la extensión del tipo real aunque el cliente mienta.
        var nuevo = await Cv.GuardarAsync(ana, "perfil.pdf", Docx(), default);
        Assert.Equal("perfil.docx", nuevo.NombreArchivo);
        Assert.Single(await _t.Db.CurriculosTrabajador.ToListAsync());
        var descarga = await Cv.ObtenerAsync(ana, default);
        Assert.Equal(CurriculumService.TipoDocx, descarga!.TipoContenido);

        await Cv.EliminarAsync(ana, default);
        Assert.Null(await Cv.ObtenerAsync(ana, default));
        Assert.True(await _t.Db.Auditoria.AnyAsync(x => x.Modulo == "Expediente" && x.Accion == "SubirCurriculum"));
    }

    [Fact]
    public async Task Curriculum_lo_sube_el_propio_trabajador_pero_no_lo_descarga_otro_trabajador()
    {
        var (e, ana, ua, _, ub) = await EscenarioAsync();
        _t.Current.As(e, ua);
        await Cv.GuardarAsync(ana, "ana.pdf", Pdf, default);
        Assert.NotNull(await Cv.ObtenerAsync(ana, default));

        _t.NewScope(); _t.Current.As(e, ub);
        await RechazaCon(ServiceErrorKind.Forbidden, () => Cv.ObtenerAsync(ana, default));
        await RechazaCon(ServiceErrorKind.Forbidden, () => Cv.GuardarAsync(ana, "x.pdf", Pdf, default));
        await RechazaCon(ServiceErrorKind.Forbidden, () => Cv.EliminarAsync(ana, default));
    }

    // ---------------- Permisos por defecto ----------------
    [Fact]
    public void Permisos_por_defecto_del_expediente_profesional()
    {
        Assert.All(Permisos.Bloque14, p => Assert.Contains(p, PermisosPorDefecto.PorRol[NombresRol.Administrador]));
        Assert.All(Permisos.Bloque14, p => Assert.Contains(p, PermisosPorDefecto.PorRol[NombresRol.RecursosHumanos]));
        Assert.DoesNotContain(Permisos.ExpedienteEditar, PermisosPorDefecto.PorRol[NombresRol.Supervisor]);
        Assert.DoesNotContain(Permisos.ExpedienteAprobar, PermisosPorDefecto.PorRol[NombresRol.Supervisor]);
        Assert.Contains(Permisos.ExpedienteBuscar, PermisosPorDefecto.PorRol[NombresRol.Supervisor]);
        Assert.All(Permisos.Bloque14, p => Assert.DoesNotContain(p, PermisosPorDefecto.PorRol[NombresRol.Trabajador]));
    }
}
