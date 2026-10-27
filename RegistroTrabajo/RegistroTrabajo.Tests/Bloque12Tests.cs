using Microsoft.EntityFrameworkCore;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Tests;

public class Bloque12Tests : IDisposable
{
    private readonly TestEnv _t = new();
    public void Dispose() => _t.Dispose();

    private static async Task RechazaCon(ServiceErrorKind kind, Func<Task> accion) =>
        Assert.Equal(kind, (await Assert.ThrowsAnyAsync<ServiceException>(accion)).Kind);

    private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4 };
    private static readonly byte[] Jpeg = { 0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3 };

    private static ConfiguracionEmpresaDto Config(Action<ConfiguracionEmpresaDto>? cambia = null)
    {
        var c = new ConfiguracionEmpresaDto
        {
            ZonaHorariaId = "UTC", Cultura = "es-MX", FormatoFecha = "dd/MM/yyyy", FormatoHora = "HH:mm",
            CierreSesionInactividadMinutos = 30, DuracionSesionMaximaHoras = 12
        };
        cambia?.Invoke(c);
        return c;
    }

    private async Task<(EmpresaInfo A, EmpresaInfo B)> DosEmpresasAsync()
    {
        var a = await _t.CrearEmpresaAsync("Acme", "admin@a.com");
        var b = await _t.CrearEmpresaAsync("Beta", "admin@b.com");
        _t.NewScope(); _t.Current.As(a, a.AdminId);
        return (a, b);
    }

    // ---------- Configuración y validación ----------

    [Fact]
    public async Task Configuracion_nueva_se_guarda_y_se_audita()
    {
        var (a, _) = await DosEmpresasAsync();
        var svc = _t.Svc<IEmpresaService>();
        var r = await svc.ActualizarConfiguracionAsync(Config(c =>
        {
            c.NombreMostrado = " Acme MX "; c.TextoInstitucional = "Somos Acme"; c.PrimerDiaSemana = 0; c.Moneda = "mxn";
            c.FormatoFecha = "yyyy-MM-dd"; c.FormatoHora = "hh:mm tt";
        }), default);
        Assert.Equal("Acme MX", r.NombreMostrado);
        Assert.Equal("MXN", r.Moneda);
        Assert.Equal(0, r.PrimerDiaSemana);
        _t.NewScope();
        Assert.True(await _t.Db.Auditoria.AnyAsync(x => x.EmpresaId == a.EmpresaId && x.Modulo == "Empresa" && x.Accion == "Actualizar"));
    }

    [Theory]
    [InlineData("FormatoFecha", "d/M/yy")]
    [InlineData("FormatoHora", "HH:mm:ss.fff")]
    [InlineData("Moneda", "PESOS")]
    [InlineData("Zona", "No/Existe")]
    public async Task Valores_no_permitidos_se_rechazan(string campo, string valor)
    {
        await DosEmpresasAsync();
        var cfg = Config(c =>
        {
            if (campo == "FormatoFecha") c.FormatoFecha = valor;
            if (campo == "FormatoHora") c.FormatoHora = valor;
            if (campo == "Moneda") c.Moneda = valor;
            if (campo == "Zona") c.ZonaHorariaId = valor;
        });
        await RechazaCon(ServiceErrorKind.Validation, () => _t.Svc<IEmpresaService>().ActualizarConfiguracionAsync(cfg, default));
    }

    [Fact]
    public async Task Color_principal_muy_claro_se_rechaza_y_uno_oscuro_se_acepta()
    {
        await DosEmpresasAsync();
        var svc = _t.Svc<IEmpresaService>();
        await RechazaCon(ServiceErrorKind.Validation, () => svc.ActualizarAsync(new ActualizarEmpresaRequest { Nombre = "Acme", ColorPrimario = "#FFFF99" }, default));
        var ok = await svc.ActualizarAsync(new ActualizarEmpresaRequest { Nombre = "Acme", ColorPrimario = "#1F6FA8", ColorSecundario = "#FFFF99" }, default);
        Assert.Equal("#1F6FA8", ok.ColorPrimario);
    }

    [Fact]
    public async Task Actualizar_empresa_ignora_el_LogoUrl_enviado_por_el_cliente()
    {
        await DosEmpresasAsync();
        var r = await _t.Svc<IEmpresaService>().ActualizarAsync(new ActualizarEmpresaRequest { Nombre = "Acme", LogoUrl = "https://evil.example/x.png" }, default);
        Assert.Null(r.LogoUrl);
    }

    // ---------- Logotipo ----------

    [Fact]
    public async Task Logo_valido_se_guarda_reemplaza_y_elimina_sin_auditar_el_contenido()
    {
        var (a, _) = await DosEmpresasAsync();
        var logo = _t.Svc<ILogoEmpresaService>();
        await logo.GuardarAsync(Png, default);
        var l = await logo.ObtenerAsync(default);
        Assert.Equal("image/png", l!.TipoContenido);
        Assert.Equal("api/empresa/logo", (await _t.Svc<IEmpresaService>().ObtenerAsync(default)).LogoUrl);

        await logo.GuardarAsync(Jpeg, default);
        Assert.Equal("image/jpeg", (await logo.ObtenerAsync(default))!.TipoContenido);
        Assert.NotEqual(l.Hash, (await logo.ObtenerAsync(default))!.Hash);

        _t.NewScope();
        var aud = await _t.Db.Auditoria.Where(x => x.EmpresaId == a.EmpresaId && x.Entidad == "Logo").ToListAsync();
        Assert.Equal(2, aud.Count);
        Assert.All(aud, x => Assert.DoesNotContain("iVBOR", x.DatosDespuesJson ?? ""));

        await _t.Svc<ILogoEmpresaService>().EliminarAsync(default);
        Assert.Null(await _t.Svc<ILogoEmpresaService>().ObtenerAsync(default));
        Assert.Null((await _t.Svc<IEmpresaService>().ObtenerAsync(default)).LogoUrl);
    }

    [Fact]
    public async Task Logo_con_formato_falso_svg_vacio_o_demasiado_grande_se_rechaza()
    {
        await DosEmpresasAsync();
        var logo = _t.Svc<ILogoEmpresaService>();
        await RechazaCon(ServiceErrorKind.Validation, () => logo.GuardarAsync(System.Text.Encoding.UTF8.GetBytes("<svg onload='alert(1)'></svg>"), default));
        await RechazaCon(ServiceErrorKind.Validation, () => logo.GuardarAsync(System.Text.Encoding.UTF8.GetBytes("MZ-ejecutable.png"), default));
        await RechazaCon(ServiceErrorKind.Validation, () => logo.GuardarAsync(Array.Empty<byte>(), default));
        var grande = new byte[FormatosRegionales.MaxBytesLogo + 1]; Png.CopyTo(grande, 0);
        await RechazaCon(ServiceErrorKind.Validation, () => logo.GuardarAsync(grande, default));
    }

    [Fact]
    public async Task Logo_exige_permiso_para_cambiarse_pero_lo_ve_cualquier_usuario_de_la_empresa()
    {
        var (a, _) = await DosEmpresasAsync();
        await _t.Svc<ILogoEmpresaService>().GuardarAsync(Png, default);
        var luis = await _t.NuevoUsuarioAsync(a.EmpresaId, "luis@a.com", "Luis", "Mora", a.Roles[NombresRol.Trabajador]);
        _t.NewScope(); _t.Current.As(a, luis);
        await RechazaCon(ServiceErrorKind.Forbidden, () => _t.Svc<ILogoEmpresaService>().GuardarAsync(Jpeg, default));
        await RechazaCon(ServiceErrorKind.Forbidden, () => _t.Svc<ILogoEmpresaService>().EliminarAsync(default));
        Assert.NotNull(await _t.Svc<ILogoEmpresaService>().ObtenerAsync(default));
    }

    [Fact]
    public async Task Logos_y_apariencia_estan_aislados_por_empresa()
    {
        var (a, b) = await DosEmpresasAsync();
        await _t.Svc<ILogoEmpresaService>().GuardarAsync(Png, default);
        await _t.Svc<IEmpresaService>().ActualizarConfiguracionAsync(Config(c => c.NombreMostrado = "Solo Acme"), default);

        _t.NewScope(); _t.Current.As(b, b.AdminId);
        Assert.Null(await _t.Svc<ILogoEmpresaService>().ObtenerAsync(default));
        var ap = await _t.Svc<IAparienciaService>().ObtenerAsync(default);
        Assert.Equal("Beta", ap.Nombre);
        Assert.False(ap.TieneLogo);
    }

    // ---------- Apariencia ----------

    [Fact]
    public async Task Apariencia_la_puede_leer_cualquier_rol_y_no_contiene_datos_sensibles()
    {
        var (a, _) = await DosEmpresasAsync();
        await _t.Svc<IEmpresaService>().ActualizarAsync(new ActualizarEmpresaRequest { Nombre = "Acme SA", RFC = "AAA010101AAA", ColorPrimario = "#1F6FA8" }, default);
        await _t.Svc<ILogoEmpresaService>().GuardarAsync(Png, default);
        var luis = await _t.NuevoUsuarioAsync(a.EmpresaId, "luis@a.com", "Luis", "Mora", a.Roles[NombresRol.Trabajador]);
        _t.NewScope(); _t.Current.As(a, luis);
        var ap = await _t.Svc<IAparienciaService>().ObtenerAsync(default);
        Assert.Equal("Acme SA", ap.Nombre);
        Assert.Equal("#1F6FA8", ap.ColorPrimario);
        Assert.True(ap.TieneLogo);
        Assert.Equal(12, ap.VersionLogo!.Length);
        Assert.DoesNotContain(typeof(AparienciaEmpresaDto).GetProperties(), p => p.Name is "RFC" or "CorreoContacto" or "TelefonoContacto");
    }

    // ---------- Permisos efectivos ----------

    [Fact]
    public async Task Permisos_efectivos_solo_para_administrador_y_nunca_de_otra_empresa()
    {
        var (a, b) = await DosEmpresasAsync();
        var luis = await _t.NuevoUsuarioAsync(a.EmpresaId, "luis@a.com", "Luis", "Mora", a.Roles[NombresRol.Trabajador]);
        var rita = await _t.NuevoUsuarioAsync(a.EmpresaId, "rita@a.com", "Rita", "Gomez", a.Roles[NombresRol.RecursosHumanos]);

        var dto = await _t.Svc<IPermisosEfectivosService>().ObtenerAsync(luis, default);
        Assert.Contains(NombresRol.Trabajador, dto.Roles);
        Assert.Contains(dto.Permisos, p => p.Codigo == Permisos.ReportesConsultar);
        Assert.DoesNotContain(dto.Permisos, p => p.Codigo == Permisos.UsuariosCrear);

        await RechazaCon(ServiceErrorKind.NotFound, () => _t.Svc<IPermisosEfectivosService>().ObtenerAsync(b.AdminId, default));

        _t.NewScope(); _t.Current.As(a, rita);   // RR. HH. no lo tiene por defecto
        await RechazaCon(ServiceErrorKind.Forbidden, () => _t.Svc<IPermisosEfectivosService>().ObtenerAsync(luis, default));
        Assert.DoesNotContain(Permisos.EmpresaVerPermisosEfectivos, PermisosPorDefecto.PorRol[NombresRol.RecursosHumanos]);
    }

    // ---------- Estado del sistema ----------

    [Fact]
    public async Task Estado_del_sistema_solo_plataforma_y_sin_datos_de_conexion()
    {
        var emp = await _t.CrearEmpresaAsync("Acme", "admin@a.com");
        _t.Current.As(emp, emp.AdminId);
        await RechazaCon(ServiceErrorKind.Forbidden, () => _t.Svc<IEstadoSistemaService>().ObtenerAsync(default));

        var p = await _t.CrearPlataformaAsync();
        _t.NewScope(); _t.Current.As(p, p.AdminId);
        var e = await _t.Svc<IEstadoSistemaService>().ObtenerAsync(default);
        Assert.True(e.BaseDatosDisponible);
        Assert.False(string.IsNullOrWhiteSpace(e.Version));
        Assert.DoesNotContain(typeof(EstadoSistemaDto).GetProperties(), x => x.Name.Contains("Conexion", StringComparison.OrdinalIgnoreCase) || x.Name.Contains("Password", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Contraste_con_blanco_distingue_colores_claros_y_oscuros()
    {
        Assert.True(EmpresaService.ContrasteSuficienteConBlanco("#1F6FA8"));
        Assert.True(EmpresaService.ContrasteSuficienteConBlanco("#000000"));
        Assert.False(EmpresaService.ContrasteSuficienteConBlanco("#FFFFFF"));
        Assert.False(EmpresaService.ContrasteSuficienteConBlanco("#FFFF99"));
    }
}
