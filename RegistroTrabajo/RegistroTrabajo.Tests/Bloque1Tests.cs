using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RegistroTrabajo.Data.Entities;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Tests;

public class Bloque1Tests : IDisposable
{
    private readonly TestEnv _t = new();
    public void Dispose() => _t.Dispose();

    private static async Task<ServiceException> Rechaza(Func<Task> accion)
    {
        var ex = await Assert.ThrowsAnyAsync<ServiceException>(accion);
        return ex;
    }

    // 1. Empresas -------------------------------------------------------------
    [Fact]
    public async Task Bootstrap_crea_empresa_roles_permisos_y_admin_una_sola_vez()
    {
        var id = await _t.Svc<IBootstrapService>().CrearEmpresaInicialAsync("Acme", "Admin@Acme.com", "Ada", "Lovelace", TestEnv.Password, default);
        _t.NewScope();

        var empresa = await _t.Db.Empresas.SingleAsync();
        Assert.Equal(id, empresa.EmpresaId);
        Assert.Equal(NombresRol.Iniciales.OrderBy(x => x), (await _t.Db.Roles.Select(r => r.Nombre).ToListAsync()).OrderBy(x => x));
        var admin = await _t.Db.Usuarios.SingleAsync();
        Assert.Equal("ADMIN@ACME.COM", admin.CorreoNormalizado);
        Assert.True(admin.DebeCambiarContrasena);

        // El administrador recibe todos los permisos del catálogo.
        var permisos = await _t.Svc<IPermisoService>().ObtenerPermisosAsync(empresa.EmpresaId, admin.UsuarioId, default);
        Assert.Equal(Permisos.Todos.Length, permisos.Count);

        var ex = await Rechaza(() => _t.Svc<IBootstrapService>().CrearEmpresaInicialAsync("Otra", "x@y.com", "a", "b", TestEnv.Password, default));
        Assert.Equal(ServiceErrorKind.Conflict, ex.Kind);
    }

    [Fact]
    public async Task Empresa_se_consulta_y_actualiza_solo_la_propia()
    {
        var a = await _t.CrearEmpresaAsync("A", "admin@a.com");
        var b = await _t.CrearEmpresaAsync("B", "admin@b.com");
        _t.Current.As(a, a.AdminId);
        var upd = await _t.Svc<IEmpresaService>().ActualizarAsync(new ActualizarEmpresaRequest { Nombre = "A Renombrada", RFC = "abc010101xyz" }, default);
        Assert.Equal("ABC010101XYZ", upd.RFC);

        _t.Current.As(b, b.AdminId);
        Assert.Equal("B", (await _t.Svc<IEmpresaService>().ObtenerAsync(default)).Nombre);

        // RFC duplicado entre empresas -> conflicto.
        var ex = await Rechaza(() => _t.Svc<IEmpresaService>().ActualizarAsync(new ActualizarEmpresaRequest { Nombre = "B", RFC = "ABC010101XYZ" }, default));
        Assert.Equal(ServiceErrorKind.Conflict, ex.Kind);
    }

    // 2. Configuración independiente -----------------------------------------
    [Fact]
    public async Task Configuracion_es_independiente_por_empresa()
    {
        var a = await _t.CrearEmpresaAsync("A", "admin@a.com");
        var b = await _t.CrearEmpresaAsync("B", "admin@b.com");

        _t.Current.As(a, a.AdminId);
        var cfg = await _t.Svc<IEmpresaService>().ObtenerConfiguracionAsync(default);
        cfg.CierreSesionInactividadMinutos = 60;
        cfg.PermitirValidacionGps = true;
        await _t.Svc<IEmpresaService>().ActualizarConfiguracionAsync(cfg, default);

        _t.NewScope();
        _t.Current.As(b, b.AdminId);
        var cfgB = await _t.Svc<IEmpresaService>().ObtenerConfiguracionAsync(default);
        Assert.Equal(30, cfgB.CierreSesionInactividadMinutos);
        Assert.False(cfgB.PermitirValidacionGps);
    }

    [Theory]
    [InlineData("Zona/Inexistente", "es-MX", 30, 12)]
    [InlineData("UTC", "es-MX", 1, 12)]
    [InlineData("UTC", "es-MX", 30, 500)]
    public async Task Configuracion_invalida_se_rechaza(string zona, string cultura, short inactividad, short horas)
    {
        var a = await _t.CrearEmpresaAsync("A", "admin@a.com");
        _t.Current.As(a, a.AdminId);
        var ex = await Rechaza(() => _t.Svc<IEmpresaService>().ActualizarConfiguracionAsync(new ConfiguracionEmpresaDto
        {
            ZonaHorariaId = zona, Cultura = cultura, FormatoFecha = "dd/MM/yyyy", FormatoHora = "HH:mm",
            CierreSesionInactividadMinutos = inactividad, DuracionSesionMaximaHoras = horas
        }, default));
        Assert.Equal(ServiceErrorKind.Validation, ex.Kind);
    }

    // 3. Correo normalizado y único global -----------------------------------
    [Fact]
    public async Task Correo_se_normaliza_y_es_unico_globalmente()
    {
        var a = await _t.CrearEmpresaAsync("A", "admin@a.com");
        var b = await _t.CrearEmpresaAsync("B", "admin@b.com");

        _t.Current.As(a, a.AdminId);
        var creado = await _t.Svc<IUsuarioService>().CrearAsync(Nuevo("  Ana@Mail.com "), default);
        Assert.Equal("Ana@Mail.com", creado.Correo);
        Assert.Equal("ANA@MAIL.COM", (await _t.Db.Usuarios.FirstAsync(u => u.UsuarioId == creado.UsuarioId)).CorreoNormalizado);

        // Misma cuenta en OTRA empresa y con distinta capitalización -> conflicto global.
        _t.Current.As(b, b.AdminId);
        var ex = await Rechaza(() => _t.Svc<IUsuarioService>().CrearAsync(Nuevo("ANA@mail.COM"), default));
        Assert.Equal(ServiceErrorKind.Conflict, ex.Kind);
    }

    // 4. Contraseña protegida -------------------------------------------------
    [Fact]
    public async Task Usuario_se_crea_con_contrasena_hasheada_y_no_se_expone()
    {
        var a = await _t.CrearEmpresaAsync("A", "admin@a.com");
        _t.Current.As(a, a.AdminId);
        var dto = await _t.Svc<IUsuarioService>().CrearAsync(Nuevo("nuevo@a.com"), default);
        _t.NewScope();

        var u = await _t.Db.Usuarios.FirstAsync(x => x.UsuarioId == dto.UsuarioId);
        Assert.NotEqual(TestEnv.Password, u.HashContrasena);
        Assert.DoesNotContain(TestEnv.Password, u.HashContrasena);
        Assert.Equal(PasswordVerificationResult.Success, new PasswordHasher<Usuario>().VerifyHashedPassword(u, u.HashContrasena, TestEnv.Password));

        var json = System.Text.Json.JsonSerializer.Serialize(dto);
        Assert.DoesNotContain("hash", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("contrasena\":", json.Replace("DebeCambiarContrasena", ""), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("corta1A")]
    [InlineData("sinmayusculas123")]
    [InlineData("SINMINUSCULAS123")]
    [InlineData("SinNumerosAquiAbc")]
    public async Task Contrasena_debil_se_rechaza(string pwd)
    {
        var a = await _t.CrearEmpresaAsync("A", "admin@a.com");
        _t.Current.As(a, a.AdminId);
        var req = Nuevo("debil@a.com"); req.Contrasena = pwd;
        var ex = await Rechaza(() => _t.Svc<IUsuarioService>().CrearAsync(req, default));
        Assert.Equal(ServiceErrorKind.Validation, ex.Kind);
        Assert.False(await _t.Db.Usuarios.AnyAsync(u => u.CorreoNormalizado == "DEBIL@A.COM"));
    }

    // 5. Roles ----------------------------------------------------------------
    [Fact]
    public async Task Roles_se_asignan_consultan_y_no_se_duplican()
    {
        var a = await _t.CrearEmpresaAsync("A", "admin@a.com");
        var uid = await _t.NuevoUsuarioAsync(a.EmpresaId, "t@a.com", "T", "T");
        _t.Current.As(a, a.AdminId);

        await _t.Svc<IRolService>().AsignarRolAsync(uid, a.Roles[NombresRol.Trabajador], default);
        var roles = await _t.Svc<IRolService>().ObtenerRolesUsuarioAsync(uid, default);
        Assert.Equal(NombresRol.Trabajador, Assert.Single(roles).Nombre);

        var ex = await Rechaza(() => _t.Svc<IRolService>().AsignarRolAsync(uid, a.Roles[NombresRol.Trabajador], default));
        Assert.Equal(ServiceErrorKind.Conflict, ex.Kind);

        await _t.Svc<IRolService>().RetirarRolAsync(uid, a.Roles[NombresRol.Trabajador], default);
        Assert.Empty(await _t.Svc<IRolService>().ObtenerRolesUsuarioAsync(uid, default));
    }

    // 6 y 7. Permisos y rechazo de operaciones no autorizadas ---------------
    [Fact]
    public async Task Permisos_se_validan_por_rol()
    {
        var a = await _t.CrearEmpresaAsync("A", "admin@a.com");
        var trab = await _t.NuevoUsuarioAsync(a.EmpresaId, "t@a.com", "T", "T", a.Roles[NombresRol.Trabajador]);
        var sinRol = await _t.NuevoUsuarioAsync(a.EmpresaId, "s@a.com", "S", "S");

        _t.Current.As(a, a.AdminId);
        Assert.True(await _t.Svc<IPermisoService>().TienePermisoAsync(Permisos.UsuariosCrear, default));

        _t.Current.As(a, trab);
        Assert.True(await _t.Svc<IPermisoService>().TienePermisoAsync(Permisos.EmpresaConsultar, default));
        Assert.False(await _t.Svc<IPermisoService>().TienePermisoAsync(Permisos.UsuariosCrear, default));
        var ex = await Rechaza(() => _t.Svc<IPermisoService>().AsegurarPermisoAsync(Permisos.UsuariosCrear, default));
        Assert.Equal(ServiceErrorKind.Forbidden, ex.Kind);

        _t.Current.As(a, sinRol);
        Assert.False(await _t.Svc<IPermisoService>().TienePermisoAsync(Permisos.EmpresaConsultar, default));

        _t.Current.Anonymous();
        var ex2 = await Rechaza(() => _t.Svc<IPermisoService>().AsegurarPermisoAsync(Permisos.EmpresaConsultar, default));
        Assert.Equal(ServiceErrorKind.Unauthorized, ex2.Kind);
    }

    [Fact]
    public async Task Permisos_de_rol_se_configuran_y_surten_efecto()
    {
        var a = await _t.CrearEmpresaAsync("A", "admin@a.com");
        var sup = await _t.NuevoUsuarioAsync(a.EmpresaId, "sup@a.com", "S", "S", a.Roles[NombresRol.Supervisor]);

        _t.Current.As(a, a.AdminId);
        await _t.Svc<IRolService>().ConfigurarPermisosRolAsync(a.Roles[NombresRol.Supervisor],
            new[] { Permisos.EmpresaConsultar, Permisos.AuditoriaConsultar }, default);

        _t.NewScope();
        _t.Current.As(a, sup);
        Assert.True(await _t.Svc<IPermisoService>().TienePermisoAsync(Permisos.AuditoriaConsultar, default));
        Assert.False(await _t.Svc<IPermisoService>().TienePermisoAsync(Permisos.UsuariosConsultar, default));

        _t.Current.As(a, a.AdminId);
        var ex = await Rechaza(() => _t.Svc<IRolService>().ConfigurarPermisosRolAsync(a.Roles[NombresRol.Supervisor], new[] { "no.existe" }, default));
        Assert.Equal(ServiceErrorKind.Validation, ex.Kind);
    }

    [Fact]
    public async Task No_se_permite_elevar_privilegios()
    {
        var a = await _t.CrearEmpresaAsync("A", "admin@a.com");
        var rh = await _t.NuevoUsuarioAsync(a.EmpresaId, "rh@a.com", "R", "H", a.Roles[NombresRol.RecursosHumanos]);
        var trab = await _t.NuevoUsuarioAsync(a.EmpresaId, "t@a.com", "T", "T");

        _t.Current.As(a, rh);
        // RH no puede asignar Administrador (permisos que no posee) ni asignarse roles a sí mismo.
        Assert.Equal(ServiceErrorKind.Forbidden, (await Rechaza(() => _t.Svc<IRolService>().AsignarRolAsync(trab, a.Roles[NombresRol.Administrador], default))).Kind);
        Assert.Equal(ServiceErrorKind.Forbidden, (await Rechaza(() => _t.Svc<IRolService>().AsignarRolAsync(rh, a.Roles[NombresRol.Administrador], default))).Kind);
        // Tampoco crear un usuario ya con rol Administrador.
        var req = Nuevo("x@a.com"); req.RolIds = new[] { a.Roles[NombresRol.Administrador] };
        Assert.Equal(ServiceErrorKind.Forbidden, (await Rechaza(() => _t.Svc<IUsuarioService>().CrearAsync(req, default))).Kind);
        // Ni otorgar permisos que no tiene, ni tocar su propio rol.
        Assert.Equal(ServiceErrorKind.Forbidden, (await Rechaza(() => _t.Svc<IRolService>().ConfigurarPermisosRolAsync(a.Roles[NombresRol.Trabajador],
            new[] { Permisos.AuditoriaConsultar }, default))).Kind);
        Assert.Equal(ServiceErrorKind.Forbidden, (await Rechaza(() => _t.Svc<IRolService>().ConfigurarPermisosRolAsync(a.Roles[NombresRol.RecursosHumanos],
            Permisos.Todos, default))).Kind);

        Assert.False(await _t.Db.UsuarioRoles.AnyAsync(ur => ur.RolId == a.Roles[NombresRol.Administrador] && ur.UsuarioId != a.AdminId));
    }

    // 8. Aislamiento entre empresas ------------------------------------------
    [Fact]
    public async Task No_se_accede_a_datos_de_otra_empresa()
    {
        var a = await _t.CrearEmpresaAsync("A", "admin@a.com");
        var b = await _t.CrearEmpresaAsync("B", "admin@b.com");
        var uA = await _t.NuevoUsuarioAsync(a.EmpresaId, "u@a.com", "U", "A", a.Roles[NombresRol.Trabajador]);
        var uB = await _t.NuevoUsuarioAsync(b.EmpresaId, "x@b.com", "X", "B");
        await _t.LoginAsync("u@a.com");
        _t.NewScope();
        var sesionA = (await _t.Db.SesionesUsuario.SingleAsync()).SesionId;

        _t.Current.As(b, b.AdminId);
        Assert.Equal(ServiceErrorKind.NotFound, (await Rechaza(() => _t.Svc<IUsuarioService>().ObtenerAsync(uA, default))).Kind);
        Assert.Equal(ServiceErrorKind.NotFound, (await Rechaza(() => _t.Svc<IUsuarioService>().DesactivarAsync(uA, default))).Kind);
        Assert.Equal(ServiceErrorKind.NotFound, (await Rechaza(() => _t.Svc<IRolService>().AsignarRolAsync(uA, b.Roles[NombresRol.Trabajador], default))).Kind);
        Assert.Equal(ServiceErrorKind.NotFound, (await Rechaza(() => _t.Svc<IRolService>().AsignarRolAsync(uB, a.Roles[NombresRol.Trabajador], default))).Kind);
        Assert.Equal(ServiceErrorKind.NotFound, (await Rechaza(() => _t.Svc<IRolService>().ObtenerPermisosRolAsync(a.Roles[NombresRol.Trabajador], default))).Kind);
        Assert.Equal(ServiceErrorKind.NotFound, (await Rechaza(() => _t.Svc<ISesionService>().RevocarAsync(sesionA, default))).Kind);
        Assert.Equal(ServiceErrorKind.NotFound, (await Rechaza(() => _t.Svc<ISesionService>().RevocarTodasDeUsuarioAsync(uA, default))).Kind);

        Assert.DoesNotContain((await _t.Svc<IUsuarioService>().ListarAsync(new UsuarioFiltro(), default)).Items, x => x.UsuarioId == uA);
        Assert.Empty((await _t.Svc<ISesionService>().ListarActivasAsync(new PageQuery(), default)).Items);
        Assert.DoesNotContain((await _t.Svc<IAccesoService>().ListarAsync(new FechaFiltro(), default)).Items, x => x.UsuarioId == uA);

        // Y la sesión de A sigue intacta.
        Assert.True(await _t.Svc<ISesionService>().ValidarSesionAsync(sesionA, uA, a.EmpresaId, default));
        // Una sesión validada contra otra empresa no es válida.
        Assert.False(await _t.Svc<ISesionService>().ValidarSesionAsync(sesionA, uA, b.EmpresaId, default));
    }

    // 9. Historial de accesos y bloqueo --------------------------------------
    [Fact]
    public async Task Accesos_exitosos_y_fallidos_se_registran_sin_revelar_si_existe_la_cuenta()
    {
        var a = await _t.CrearEmpresaAsync("A", "admin@a.com");
        var okLogin = await _t.LoginAsync("ADMIN@a.com");
        Assert.False(string.IsNullOrEmpty(okLogin.RefreshToken));

        var malPwd = await Rechaza(() => _t.LoginAsync("admin@a.com", "Equivocada-123"));
        var noExiste = await Rechaza(() => _t.LoginAsync("fantasma@a.com"));
        Assert.Equal(ServiceErrorKind.Unauthorized, malPwd.Kind);
        Assert.Equal(malPwd.Message, noExiste.Message);   // mismo mensaje: no se revela existencia

        _t.NewScope();
        var h = await _t.Db.HistorialAcceso.OrderBy(x => x.HistorialAccesoId).ToListAsync();
        Assert.Equal(new[] { ResultadoAcceso.Exitoso, ResultadoAcceso.CredencialesInvalidas, ResultadoAcceso.CredencialesInvalidas }, h.Select(x => x.Resultado));
        Assert.Equal(a.AdminId, h[0].UsuarioId);
        Assert.Null(h[2].UsuarioId);
        Assert.Equal("fantasma@a.com", h[2].CorreoIntentado);
        Assert.All(h, x => { Assert.Equal("10.0.0.1", x.DireccionIp); Assert.Equal("xunit", x.UserAgent); });
        Assert.DoesNotContain(h, x => (x.Detalle ?? "").Contains(TestEnv.Password));
    }

    [Fact]
    public async Task Bloqueo_temporal_tras_intentos_fallidos_y_se_libera_con_el_tiempo()
    {
        var a = await _t.CrearEmpresaAsync("A", "admin@a.com");
        for (var i = 0; i < 3; i++) await Rechaza(() => _t.LoginAsync("admin@a.com", "Equivocada-123"));

        // Con bloqueo vigente ni la contraseña correcta entra, y el mensaje no cambia.
        var ex = await Rechaza(() => _t.LoginAsync("admin@a.com"));
        Assert.Equal(ServiceErrorKind.Unauthorized, ex.Kind);
        _t.NewScope();
        Assert.Contains(await _t.Db.HistorialAcceso.ToListAsync(), x => x.Resultado == ResultadoAcceso.CuentaBloqueada);

        _t.Clock.Advance(TimeSpan.FromMinutes(11));
        Assert.NotNull(await _t.LoginAsync("admin@a.com"));
        _t.NewScope();
        var u = await _t.Db.Usuarios.SingleAsync();
        Assert.Equal(0, u.IntentosFallidos);
        Assert.Null(u.BloqueadoHastaUtc);
    }

    // 10. Auditoría -----------------------------------------------------------
    [Fact]
    public async Task Operaciones_importantes_se_auditan_sin_datos_sensibles()
    {
        var a = await _t.CrearEmpresaAsync("A", "admin@a.com");
        _t.Current.As(a, a.AdminId);
        var dto = await _t.Svc<IUsuarioService>().CrearAsync(Nuevo("aud@a.com"), default);
        await _t.Svc<IUsuarioService>().ActualizarAsync(dto.UsuarioId, new ActualizarUsuarioRequest { Correo = "aud@a.com", Nombre = "Nuevo", Apellido = "Nombre" }, default);

        _t.NewScope();
        var filas = await _t.Db.Auditoria.OrderBy(x => x.AuditoriaId).ToListAsync();
        Assert.Equal(new[] { "Crear", "Actualizar" }, filas.Select(f => f.Accion));
        Assert.All(filas, f =>
        {
            Assert.Equal(a.EmpresaId, f.EmpresaId);
            Assert.Equal(a.AdminId, f.UsuarioId);
            Assert.Equal("10.0.0.1", f.DireccionIp);
            Assert.NotNull(f.CorrelationId);
            Assert.Equal(dto.UsuarioId.ToString(), f.EntidadId);
        });
        Assert.Null(filas[0].DatosAntesJson);
        Assert.Contains("Nuevo", filas[1].DatosDespuesJson);
        var todo = string.Join(' ', filas.Select(f => f.DatosAntesJson + f.DatosDespuesJson));
        Assert.DoesNotContain(TestEnv.Password, todo);
        Assert.DoesNotContain("AQAAAA", todo);   // prefijo de los hashes de Identity
    }

    [Fact]
    public void Auditoria_redacta_campos_sensibles_y_produce_json_valido()
    {
        var json = AuditoriaService.ToSafeJson(new { Nombre = "x", HashContrasena = "abc", anidado = new { RefreshToken = "t", Ok = 1 } });
        Assert.NotNull(json);
        using var doc = System.Text.Json.JsonDocument.Parse(json!);
        Assert.Equal("[REDACTADO]", doc.RootElement.GetProperty("HashContrasena").GetString());
        Assert.Equal("[REDACTADO]", doc.RootElement.GetProperty("anidado").GetProperty("RefreshToken").GetString());
        Assert.Equal("x", doc.RootElement.GetProperty("Nombre").GetString());
    }

    // 11. Sesiones ------------------------------------------------------------
    [Fact]
    public async Task Sesion_se_revoca_individualmente_y_en_conjunto()
    {
        var a = await _t.CrearEmpresaAsync("A", "admin@a.com");
        var uid = await _t.NuevoUsuarioAsync(a.EmpresaId, "u@a.com", "U", "U", a.Roles[NombresRol.Trabajador]);
        var t1 = await _t.LoginAsync("u@a.com");
        var t2 = await _t.LoginAsync("u@a.com");
        _t.NewScope();
        var sesiones = await _t.Db.SesionesUsuario.Where(s => s.UsuarioId == uid).ToListAsync();
        Assert.Equal(2, sesiones.Count);
        // Solo se guarda el hash del refresh token; nunca el token ni el JWT.
        Assert.All(sesiones, s => Assert.Equal(32, s.TokenRefreshHash.Length));
        Assert.DoesNotContain(sesiones, s => s.TokenRefreshHash.SequenceEqual(System.Text.Encoding.UTF8.GetBytes(t1.RefreshToken)));

        _t.Current.As(a, a.AdminId);
        await _t.Svc<ISesionService>().RevocarAsync(sesiones[0].SesionId, default);
        Assert.False(await _t.Svc<ISesionService>().ValidarSesionAsync(sesiones[0].SesionId, uid, a.EmpresaId, default));
        Assert.True(await _t.Svc<ISesionService>().ValidarSesionAsync(sesiones[1].SesionId, uid, a.EmpresaId, default));

        Assert.Equal(1, await _t.Svc<ISesionService>().RevocarTodasDeUsuarioAsync(uid, default));
        Assert.False(await _t.Svc<ISesionService>().ValidarSesionAsync(sesiones[1].SesionId, uid, a.EmpresaId, default));

        // El refresh de una sesión revocada falla.
        _t.Current.Anonymous();
        var ex = await Rechaza(() => _t.Svc<IAuthService>().RefreshAsync(new RefreshRequest { RefreshToken = t2.RefreshToken }, default));
        Assert.Equal(ServiceErrorKind.Unauthorized, ex.Kind);

        _t.NewScope();
        Assert.All(await _t.Db.SesionesUsuario.Where(s => s.UsuarioId == uid).ToListAsync(), s => Assert.NotNull(s.RevocadaUtc));
        Assert.Contains(await _t.Db.Auditoria.ToListAsync(), x => x.Accion == "RevocarTodas");
    }

    [Fact]
    public async Task Refresh_rota_el_token_y_el_anterior_deja_de_servir()
    {
        var a = await _t.CrearEmpresaAsync("A", "admin@a.com");
        var t1 = await _t.LoginAsync("admin@a.com");
        var t2 = await _t.Svc<IAuthService>().RefreshAsync(new RefreshRequest { RefreshToken = t1.RefreshToken }, default);
        Assert.NotEqual(t1.RefreshToken, t2.RefreshToken);
        var ex = await Rechaza(() => _t.Svc<IAuthService>().RefreshAsync(new RefreshRequest { RefreshToken = t1.RefreshToken }, default));
        Assert.Equal(ServiceErrorKind.Unauthorized, ex.Kind);
        await _t.Svc<IAuthService>().RefreshAsync(new RefreshRequest { RefreshToken = t2.RefreshToken }, default);
    }

    [Fact]
    public async Task Sesion_inactiva_o_expirada_deja_de_operar()
    {
        var a = await _t.CrearEmpresaAsync("A", "admin@a.com");
        await _t.LoginAsync("admin@a.com");
        _t.NewScope();
        var s = await _t.Db.SesionesUsuario.SingleAsync();

        _t.Clock.Advance(TimeSpan.FromMinutes(20));
        Assert.True(await _t.Svc<ISesionService>().ValidarSesionAsync(s.SesionId, a.AdminId, a.EmpresaId, default));
        _t.Clock.Advance(TimeSpan.FromMinutes(31));   // supera los 30 min de inactividad
        Assert.False(await _t.Svc<ISesionService>().ValidarSesionAsync(s.SesionId, a.AdminId, a.EmpresaId, default));
        _t.NewScope();
        Assert.Equal("Inactividad", (await _t.Db.SesionesUsuario.SingleAsync()).MotivoRevocacion);
    }

    [Fact]
    public async Task Usuario_desactivado_pierde_sesiones_permisos_y_acceso()
    {
        var a = await _t.CrearEmpresaAsync("A", "admin@a.com");
        var uid = await _t.NuevoUsuarioAsync(a.EmpresaId, "u@a.com", "U", "U", a.Roles[NombresRol.Trabajador]);
        await _t.LoginAsync("u@a.com");
        _t.NewScope();
        var sesion = (await _t.Db.SesionesUsuario.SingleAsync()).SesionId;

        _t.Current.As(a, a.AdminId);
        await _t.Svc<IUsuarioService>().DesactivarAsync(uid, default);

        Assert.False(await _t.Svc<ISesionService>().ValidarSesionAsync(sesion, uid, a.EmpresaId, default));
        Assert.Empty(await _t.Svc<IPermisoService>().ObtenerPermisosAsync(a.EmpresaId, uid, default));
        Assert.Equal(ServiceErrorKind.Unauthorized, (await Rechaza(() => _t.LoginAsync("u@a.com"))).Kind);
        _t.NewScope();
        Assert.Contains(await _t.Db.HistorialAcceso.ToListAsync(), x => x.Resultado == ResultadoAcceso.CuentaInactiva);
    }

    [Fact]
    public async Task No_se_puede_desactivar_a_uno_mismo_ni_al_ultimo_administrador()
    {
        var a = await _t.CrearEmpresaAsync("A", "admin@a.com");
        var rh = await _t.NuevoUsuarioAsync(a.EmpresaId, "rh@a.com", "R", "H", a.Roles[NombresRol.RecursosHumanos]);

        _t.Current.As(a, a.AdminId);
        Assert.Equal(ServiceErrorKind.Forbidden, (await Rechaza(() => _t.Svc<IUsuarioService>().DesactivarAsync(a.AdminId, default))).Kind);

        _t.Current.As(a, rh);
        Assert.Equal(ServiceErrorKind.Conflict, (await Rechaza(() => _t.Svc<IUsuarioService>().DesactivarAsync(a.AdminId, default))).Kind);
    }

    [Fact]
    public async Task Cambiar_contrasena_revoca_las_otras_sesiones()
    {
        var a = await _t.CrearEmpresaAsync("A", "admin@a.com");
        await _t.LoginAsync("admin@a.com");
        await _t.LoginAsync("admin@a.com");
        _t.NewScope();
        var sesiones = await _t.Db.SesionesUsuario.OrderBy(s => s.FechaCreacionUtc).ToListAsync();

        _t.Current.As(a, a.AdminId, sesiones[0].SesionId);
        await Rechaza(() => _t.Svc<IAuthService>().CambiarContrasenaAsync(new CambiarContrasenaRequest { ContrasenaActual = "mala", ContrasenaNueva = "Otra-Contrasena-9" }, default));
        await _t.Svc<IAuthService>().CambiarContrasenaAsync(new CambiarContrasenaRequest { ContrasenaActual = TestEnv.Password, ContrasenaNueva = "Otra-Contrasena-9" }, default);

        _t.NewScope();
        var despues = await _t.Db.SesionesUsuario.ToListAsync();
        Assert.Single(despues, s => s.RevocadaUtc is null);
        await _t.LoginAsync("admin@a.com", "Otra-Contrasena-9");
    }

    // Perfil para el cliente Blazor -------------------------------------------
    [Fact]
    public async Task Perfil_devuelve_empresa_roles_y_permisos_del_usuario_autenticado()
    {
        var a = await _t.CrearEmpresaAsync("Acme", "admin@a.com");
        var trab = await _t.NuevoUsuarioAsync(a.EmpresaId, "t@a.com", "Tere", "Trabajadora", a.Roles[NombresRol.Trabajador]);
        _t.Current.As(a, trab);

        var p = await _t.Svc<IAuthService>().ObtenerPerfilAsync(default);
        Assert.Equal("Acme", p.EmpresaNombre);
        Assert.Equal(new[] { NombresRol.Trabajador }, p.Roles);
        Assert.Equal(PermisosPorDefecto.PorRol[NombresRol.Trabajador].OrderBy(x => x, StringComparer.Ordinal), p.Permisos.OrderBy(x => x, StringComparer.Ordinal));
        var json = System.Text.Json.JsonSerializer.Serialize(p);
        Assert.DoesNotContain("hash", json, StringComparison.OrdinalIgnoreCase);

        _t.Current.Anonymous();
        Assert.Equal(ServiceErrorKind.Unauthorized, (await Rechaza(() => _t.Svc<IAuthService>().ObtenerPerfilAsync(default))).Kind);
    }

    // 12. Datos inválidos y errores de BD ------------------------------------
    [Fact]
    public async Task Errores_de_base_de_datos_se_traducen_a_conflicto_sin_filtrar_detalles()
    {
        var a = await _t.CrearEmpresaAsync("A", "admin@a.com");
        _t.NewScope();
        _t.Db.Roles.Add(new Rol { RolId = Guid.NewGuid(), EmpresaId = a.EmpresaId, Nombre = NombresRol.Supervisor, Estado = true });   // nombre duplicado en la empresa
        var ex = await Rechaza(() => _t.Db.SaveSafeAsync(default));
        Assert.Equal(ServiceErrorKind.Conflict, ex.Kind);
        Assert.DoesNotContain("UNIQUE", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sqlite", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Usuario_con_rol_o_empresa_inexistente_se_rechaza()
    {
        var a = await _t.CrearEmpresaAsync("A", "admin@a.com");
        _t.Current.As(a, a.AdminId);
        var req = Nuevo("zz@a.com"); req.RolIds = new[] { Guid.NewGuid() };
        Assert.Equal(ServiceErrorKind.Validation, (await Rechaza(() => _t.Svc<IUsuarioService>().CrearAsync(req, default))).Kind);

        _t.Current.Anonymous();
        Assert.Equal(ServiceErrorKind.Unauthorized, (await Rechaza(() => _t.Svc<IUsuarioService>().ListarAsync(new UsuarioFiltro(), default))).Kind);
    }

    [Fact]
    public async Task Respuestas_de_usuarios_y_sesiones_no_contienen_campos_internos()
    {
        var props = typeof(UsuarioDto).GetProperties().Concat(typeof(SesionDto).GetProperties()).Select(p => p.Name);
        Assert.DoesNotContain(props, n => n.Contains("Hash", StringComparison.OrdinalIgnoreCase)
            || n.Contains("Token", StringComparison.OrdinalIgnoreCase) || n.Contains("RowVersion"));
        await Task.CompletedTask;
    }

    private static CrearUsuarioRequest Nuevo(string correo) => new()
    {
        Correo = correo, Nombre = "Nombre", Apellido = "Apellido", Contrasena = TestEnv.Password
    };
}


