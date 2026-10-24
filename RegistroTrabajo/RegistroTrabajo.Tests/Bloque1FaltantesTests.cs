using Microsoft.EntityFrameworkCore;
using RegistroTrabajo.Data.Entities;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Tests;

/// <summary>Cierre del Bloque 1: plataforma, roles propios, desbloqueo/restablecimiento, recuperación y confirmación de correo, mis sesiones.</summary>
public class Bloque1FaltantesTests : IDisposable
{
    private readonly TestEnv _t = new();
    public void Dispose() => _t.Dispose();

    private static async Task<ServiceException> Rechaza(Func<Task> accion) =>
        await Assert.ThrowsAnyAsync<ServiceException>(accion);

    private static CrearEmpresaPlataformaRequest NuevaEmpresa(string nombre, string correo, string? rfc = null) => new()
    {
        Nombre = nombre, AdminCorreo = correo, AdminNombre = "Ana", AdminApellido = "Admin",
        AdminContrasena = TestEnv.Password, RFC = rfc
    };

    // ---------------- Plataforma ----------------
    [Fact]
    public async Task Plataforma_da_de_alta_una_empresa_con_sus_roles_y_su_administrador()
    {
        var p = await _t.CrearPlataformaAsync();
        _t.Current.As(p, p.AdminId);
        var dto = await _t.Svc<IPlataformaService>().CrearEmpresaAsync(NuevaEmpresa("Nueva SA", "admin@nueva.com", "abc010101xyz"), default);

        Assert.Equal("Nueva SA", dto.Nombre);
        Assert.Equal("ABC010101XYZ", dto.RFC);
        Assert.Equal(1, dto.UsuariosTotal);

        _t.NewScope();
        var roles = await _t.Db.Roles.Where(r => r.EmpresaId == dto.EmpresaId).Select(r => r.Nombre).ToListAsync();
        Assert.Equal(NombresRol.Iniciales.OrderBy(x => x), roles.OrderBy(x => x));
        Assert.DoesNotContain(NombresRol.AdministradorPlataforma, roles);

        var admin = await _t.Db.Usuarios.SingleAsync(u => u.EmpresaId == dto.EmpresaId);
        Assert.True(admin.DebeCambiarContrasena);
        Assert.NotNull(admin.CorreoConfirmadoUtc);
        var permisos = await _t.Svc<IPermisoService>().ObtenerPermisosAsync(dto.EmpresaId, admin.UsuarioId, default);
        Assert.Equal(Permisos.Todos.Length, permisos.Count);                    // todos los de empresa...
        Assert.DoesNotContain(permisos, c => c.StartsWith("plataforma."));      // ...ninguno de plataforma

        // El administrador nuevo puede iniciar sesión.
        Assert.NotNull(await _t.LoginAsync("admin@nueva.com"));

        // Auditoría de la alta en la empresa creada.
        Assert.Contains(await _t.Db.Auditoria.ToListAsync(), a => a.Accion == "CrearEmpresa" && a.EmpresaId == dto.EmpresaId && a.UsuarioId == p.AdminId);
    }

    [Fact]
    public async Task Plataforma_rechaza_correo_o_rfc_duplicados_y_contrasena_debil()
    {
        var p = await _t.CrearPlataformaAsync();
        await _t.CrearEmpresaAsync("Existente", "dup@x.com");
        _t.Current.As(p, p.AdminId);
        var svc = _t.Svc<IPlataformaService>();

        await svc.CrearEmpresaAsync(NuevaEmpresa("Uno", "uno@x.com", "AAA010101AAA"), default);
        Assert.Equal(ServiceErrorKind.Conflict, (await Rechaza(() => svc.CrearEmpresaAsync(NuevaEmpresa("Dos", "DUP@x.com"), default))).Kind);
        Assert.Equal(ServiceErrorKind.Conflict, (await Rechaza(() => svc.CrearEmpresaAsync(NuevaEmpresa("Tres", "tres@x.com", "aaa010101aaa"), default))).Kind);
        var debil = NuevaEmpresa("Cuatro", "cuatro@x.com"); debil.AdminContrasena = "corta";
        Assert.Equal(ServiceErrorKind.Validation, (await Rechaza(() => svc.CrearEmpresaAsync(debil, default))).Kind);
        Assert.False(await _t.Db.Empresas.AnyAsync(e => e.Nombre == "Dos" || e.Nombre == "Tres" || e.Nombre == "Cuatro"));
    }

    [Fact]
    public async Task Solo_la_empresa_de_plataforma_puede_administrar_empresas()
    {
        var a = await _t.CrearEmpresaAsync("A", "admin@a.com");
        var b = await _t.CrearEmpresaAsync("B", "admin@b.com");
        _t.Current.As(a, a.AdminId);
        var svc = _t.Svc<IPlataformaService>();

        Assert.Equal(ServiceErrorKind.Forbidden, (await Rechaza(() => svc.ListarEmpresasAsync(new PageQuery(), null, default))).Kind);
        Assert.Equal(ServiceErrorKind.Forbidden, (await Rechaza(() => svc.ObtenerEmpresaAsync(b.EmpresaId, default))).Kind);
        Assert.Equal(ServiceErrorKind.Forbidden, (await Rechaza(() => svc.CrearEmpresaAsync(NuevaEmpresa("X", "x@x.com"), default))).Kind);
        Assert.Equal(ServiceErrorKind.Forbidden, (await Rechaza(() => svc.CambiarEstadoAsync(b.EmpresaId, EstadoEmpresa.Suspendida, default))).Kind);
    }

    [Fact]
    public async Task Los_permisos_de_plataforma_nunca_funcionan_en_una_empresa_normal()
    {
        var a = await _t.CrearEmpresaAsync("A", "admin@a.com");
        // Aunque alguien insertara el permiso directamente en un rol de la empresa, no se concede.
        var permiso = await _t.Db.PermisosSistema.FirstAsync(p => p.Codigo == Permisos.PlataformaEmpresasCrear);
        _t.Db.RolPermisos.Add(new RolPermiso { EmpresaId = a.EmpresaId, RolId = a.Roles[NombresRol.Administrador], PermisoId = permiso.PermisoId, Permitido = true, FechaActualizacionUtc = DateTime.UtcNow });
        await _t.Db.SaveChangesAsync();
        _t.NewScope();

        _t.Current.As(a, a.AdminId);
        Assert.False(await _t.Svc<IPermisoService>().TienePermisoAsync(Permisos.PlataformaEmpresasCrear, default));
        // Y no se puede asignar por el servicio ni verlo en el catálogo.
        var rh = a.Roles[NombresRol.RecursosHumanos];
        Assert.Equal(ServiceErrorKind.Forbidden, (await Rechaza(() => _t.Svc<IRolService>().ConfigurarPermisosRolAsync(rh, new[] { Permisos.PlataformaEmpresasCrear }, default))).Kind);
        Assert.DoesNotContain((await _t.Svc<IRolService>().ListarPermisosAsync(default)), x => x.Codigo.StartsWith("plataforma."));
    }

    [Fact]
    public async Task Suspender_una_empresa_revoca_sus_sesiones_y_bloquea_el_acceso_hasta_reactivarla()
    {
        var p = await _t.CrearPlataformaAsync();
        var a = await _t.CrearEmpresaAsync("A", "admin@a.com");
        var uid = await _t.NuevoUsuarioAsync(a.EmpresaId, "u@a.com", "U", "A", a.Roles[NombresRol.Trabajador]);
        await _t.LoginAsync("u@a.com");
        _t.NewScope();
        var sesion = (await _t.Db.SesionesUsuario.SingleAsync()).SesionId;

        _t.Current.As(p, p.AdminId);
        var dto = await _t.Svc<IPlataformaService>().CambiarEstadoAsync(a.EmpresaId, EstadoEmpresa.Suspendida, default);
        Assert.Equal(EstadoEmpresa.Suspendida, dto.Estado);

        Assert.False(await _t.Svc<ISesionService>().ValidarSesionAsync(sesion, uid, a.EmpresaId, default));
        Assert.Empty(await _t.Svc<IPermisoService>().ObtenerPermisosAsync(a.EmpresaId, uid, default));
        Assert.Equal(ServiceErrorKind.Unauthorized, (await Rechaza(() => _t.LoginAsync("u@a.com"))).Kind);

        _t.Current.As(p, p.AdminId);
        await _t.Svc<IPlataformaService>().CambiarEstadoAsync(a.EmpresaId, EstadoEmpresa.Activa, default);
        Assert.NotNull(await _t.LoginAsync("u@a.com"));

        _t.NewScope();
        Assert.Contains(await _t.Db.Auditoria.ToListAsync(), x => x.Accion == "CambiarEstadoEmpresa" && x.EmpresaId == a.EmpresaId);
    }

    [Fact]
    public async Task La_empresa_de_plataforma_no_se_puede_desactivar_y_el_estado_se_valida()
    {
        var p = await _t.CrearPlataformaAsync();
        _t.Current.As(p, p.AdminId);
        var svc = _t.Svc<IPlataformaService>();
        Assert.Equal(ServiceErrorKind.Forbidden, (await Rechaza(() => svc.CambiarEstadoAsync(p.EmpresaId, EstadoEmpresa.Inactiva, default))).Kind);
        Assert.Equal(ServiceErrorKind.Validation, (await Rechaza(() => svc.CambiarEstadoAsync(p.EmpresaId, 9, default))).Kind);
        Assert.Equal(ServiceErrorKind.NotFound, (await Rechaza(() => svc.CambiarEstadoAsync(Guid.NewGuid(), EstadoEmpresa.Inactiva, default))).Kind);
    }

    [Fact]
    public async Task Solo_puede_existir_una_empresa_de_plataforma()
    {
        await _t.CrearPlataformaAsync();
        var ex = await Rechaza(() => _t.Svc<IBootstrapService>().CrearEmpresaPlataformaAsync("Otra", "otra@x.com", "a", "b", TestEnv.Password, default));
        Assert.Equal(ServiceErrorKind.Conflict, ex.Kind);
    }

    // ---------------- Roles propios ----------------
    [Fact]
    public async Task Roles_propios_se_crean_editan_y_desactivan_sin_tocar_los_de_sistema()
    {
        var a = await _t.CrearEmpresaAsync("A", "admin@a.com");
        var trab = await _t.NuevoUsuarioAsync(a.EmpresaId, "t@a.com", "T", "T");
        _t.Current.As(a, a.AdminId);
        var svc = _t.Svc<IRolService>();

        var rol = await svc.CrearRolAsync(new CrearRolRequest { Nombre = "  Auditor  ", Descripcion = "Solo lectura" }, default);
        Assert.Equal("Auditor", rol.Nombre);
        Assert.False(rol.EsSistema);
        Assert.True(rol.Estado);

        Assert.Equal(ServiceErrorKind.Validation, (await Rechaza(() => svc.CrearRolAsync(new CrearRolRequest { Nombre = "supervisor" }, default))).Kind);   // reservado
        Assert.Equal(ServiceErrorKind.Validation, (await Rechaza(() => svc.CrearRolAsync(new CrearRolRequest { Nombre = "Administrador de plataforma" }, default))).Kind);
        Assert.Equal(ServiceErrorKind.Conflict, (await Rechaza(() => svc.CrearRolAsync(new CrearRolRequest { Nombre = "auditor" }, default))).Kind);

        var editado = await svc.ActualizarRolAsync(rol.RolId, new ActualizarRolRequest { Nombre = "Auditor externo" }, default);
        Assert.Equal("Auditor externo", editado.Nombre);

        // Se le dan permisos y se asigna: el usuario los recibe.
        await svc.ConfigurarPermisosRolAsync(rol.RolId, new[] { Permisos.AuditoriaConsultar }, default);
        await svc.AsignarRolAsync(trab, rol.RolId, default);
        _t.Current.As(a, trab);
        Assert.True(await _t.Svc<IPermisoService>().TienePermisoAsync(Permisos.AuditoriaConsultar, default));

        // Desactivado: ya no concede permisos ni se puede asignar a otros.
        _t.Current.As(a, a.AdminId);
        await svc.CambiarEstadoRolAsync(rol.RolId, false, default);
        _t.Current.As(a, trab);
        Assert.False(await _t.Svc<IPermisoService>().TienePermisoAsync(Permisos.AuditoriaConsultar, default));
        _t.Current.As(a, a.AdminId);
        var otro = await _t.NuevoUsuarioAsync(a.EmpresaId, "o@a.com", "O", "O");
        Assert.Equal(ServiceErrorKind.Validation, (await Rechaza(() => svc.AsignarRolAsync(otro, rol.RolId, default))).Kind);

        // Roles de sistema: intocables.
        var sup = a.Roles[NombresRol.Supervisor];
        Assert.Equal(ServiceErrorKind.Forbidden, (await Rechaza(() => svc.ActualizarRolAsync(sup, new ActualizarRolRequest { Nombre = "Otro" }, default))).Kind);
        Assert.Equal(ServiceErrorKind.Forbidden, (await Rechaza(() => svc.CambiarEstadoRolAsync(sup, false, default))).Kind);

        _t.NewScope();
        Assert.Contains(await _t.Db.Auditoria.ToListAsync(), x => x.Accion == "CrearRol");
    }

    [Fact]
    public async Task Los_roles_de_otra_empresa_no_se_pueden_editar()
    {
        var a = await _t.CrearEmpresaAsync("A", "admin@a.com");
        var b = await _t.CrearEmpresaAsync("B", "admin@b.com");
        _t.Current.As(b, b.AdminId);
        var rolB = await _t.Svc<IRolService>().CrearRolAsync(new CrearRolRequest { Nombre = "Interno" }, default);

        _t.Current.As(a, a.AdminId);
        Assert.Equal(ServiceErrorKind.NotFound, (await Rechaza(() => _t.Svc<IRolService>().ActualizarRolAsync(rolB.RolId, new ActualizarRolRequest { Nombre = "Hack" }, default))).Kind);
        Assert.Equal(ServiceErrorKind.NotFound, (await Rechaza(() => _t.Svc<IRolService>().CambiarEstadoRolAsync(rolB.RolId, false, default))).Kind);
    }

    // ---------------- Desbloqueo y restablecimiento ----------------
    [Fact]
    public async Task Un_administrador_desbloquea_una_cuenta_bloqueada()
    {
        var a = await _t.CrearEmpresaAsync("A", "admin@a.com");
        var uid = await _t.NuevoUsuarioAsync(a.EmpresaId, "u@a.com", "U", "A", a.Roles[NombresRol.Trabajador]);
        for (var i = 0; i < 3; i++) await Rechaza(() => _t.LoginAsync("u@a.com", "Equivocada-123"));
        await Rechaza(() => _t.LoginAsync("u@a.com"));   // bloqueada incluso con la contraseña correcta

        _t.Current.As(a, a.AdminId);
        await _t.Svc<IUsuarioService>().DesbloquearAsync(uid, default);
        Assert.NotNull(await _t.LoginAsync("u@a.com"));

        _t.NewScope();
        Assert.Contains(await _t.Db.Auditoria.ToListAsync(), x => x.Accion == "Desbloquear" && x.EntidadId == uid.ToString());
    }

    [Fact]
    public async Task Restablecer_la_contrasena_de_otro_usuario_cierra_sus_sesiones_y_exige_cambiarla()
    {
        var a = await _t.CrearEmpresaAsync("A", "admin@a.com");
        var uid = await _t.NuevoUsuarioAsync(a.EmpresaId, "u@a.com", "U", "A", a.Roles[NombresRol.Trabajador]);
        await _t.LoginAsync("u@a.com");
        _t.NewScope();
        var sesion = (await _t.Db.SesionesUsuario.SingleAsync()).SesionId;

        _t.Current.As(a, a.AdminId);
        await _t.Svc<IUsuarioService>().RestablecerContrasenaAsync(uid, "Nueva-Contrasena-7", default);

        Assert.False(await _t.Svc<ISesionService>().ValidarSesionAsync(sesion, uid, a.EmpresaId, default));
        Assert.Equal(ServiceErrorKind.Unauthorized, (await Rechaza(() => _t.LoginAsync("u@a.com"))).Kind);   // la anterior ya no sirve
        var token = await _t.LoginAsync("u@a.com", "Nueva-Contrasena-7");
        Assert.True(token.DebeCambiarContrasena);

        _t.NewScope();
        var auditoria = string.Join(' ', (await _t.Db.Auditoria.ToListAsync()).Select(x => x.DatosAntesJson + x.DatosDespuesJson));
        Assert.DoesNotContain("Nueva-Contrasena-7", auditoria);
        Assert.Contains(await _t.Db.Auditoria.ToListAsync(), x => x.Accion == "RestablecerContrasena");
    }

    [Fact]
    public async Task Restablecer_contrasena_respeta_la_anti_elevacion_y_los_datos_invalidos()
    {
        var a = await _t.CrearEmpresaAsync("A", "admin@a.com");
        var rh = await _t.NuevoUsuarioAsync(a.EmpresaId, "rh@a.com", "R", "H", a.Roles[NombresRol.RecursosHumanos]);
        var trab = await _t.NuevoUsuarioAsync(a.EmpresaId, "t@a.com", "T", "T", a.Roles[NombresRol.Trabajador]);
        var permiso = await _t.Db.PermisosSistema.FirstAsync(p => p.Codigo == Permisos.UsuariosRestablecerContrasena);
        _t.Db.RolPermisos.Add(new RolPermiso { EmpresaId = a.EmpresaId, RolId = a.Roles[NombresRol.RecursosHumanos], PermisoId = permiso.PermisoId, Permitido = true, FechaActualizacionUtc = DateTime.UtcNow });
        await _t.Db.SaveChangesAsync();
        _t.NewScope();

        _t.Current.As(a, rh);
        var svc = _t.Svc<IUsuarioService>();
        Assert.Equal(ServiceErrorKind.Forbidden, (await Rechaza(() => svc.RestablecerContrasenaAsync(a.AdminId, "Nueva-Contrasena-7", default))).Kind);   // no puede tomar la cuenta del administrador
        Assert.Equal(ServiceErrorKind.Forbidden, (await Rechaza(() => svc.RestablecerContrasenaAsync(rh, "Nueva-Contrasena-7", default))).Kind);            // ni la propia por esta vía
        Assert.Equal(ServiceErrorKind.Validation, (await Rechaza(() => svc.RestablecerContrasenaAsync(trab, "corta", default))).Kind);
        await svc.RestablecerContrasenaAsync(trab, "Nueva-Contrasena-7", default);                                                                           // a un trabajador, sí

        _t.Current.As(a, a.AdminId);
        Assert.Equal(ServiceErrorKind.NotFound, (await Rechaza(() => svc.RestablecerContrasenaAsync(Guid.NewGuid(), "Nueva-Contrasena-7", default))).Kind);
    }

    // ---------------- Recuperación de contraseña ----------------
    [Fact]
    public async Task Recuperacion_no_revela_si_la_cuenta_existe_ni_falla_si_el_correo_no_sale()
    {
        await _t.CrearEmpresaAsync("A", "admin@a.com");
        var cuentas = _t.Svc<ICuentaService>();

        await cuentas.SolicitarRecuperacionAsync("fantasma@a.com", default);       // sin excepción, sin correo, sin token
        Assert.Empty(_t.Correo.Enviados);
        Assert.False(await _t.Db.TokensRecuperacionContrasena.AnyAsync());

        _t.Correo.Falla = true;
        await cuentas.SolicitarRecuperacionAsync("admin@a.com", default);          // el servidor de correo falla: la respuesta es la misma
        Assert.True(await _t.Db.TokensRecuperacionContrasena.AnyAsync());
    }

    [Fact]
    public async Task Recuperacion_flujo_completo_token_de_un_solo_uso_y_solo_se_guarda_el_hash()
    {
        var a = await _t.CrearEmpresaAsync("A", "admin@a.com");
        await _t.LoginAsync("admin@a.com");

        await _t.Svc<ICuentaService>().SolicitarRecuperacionAsync("ADMIN@a.com", default);
        var correo = Assert.Single(_t.Correo.Enviados);
        Assert.Equal("admin@a.com", correo.Para);
        var token = correo.Token;
        Assert.False(string.IsNullOrWhiteSpace(token));

        _t.NewScope();
        var fila = await _t.Db.TokensRecuperacionContrasena.SingleAsync();
        Assert.Equal(32, fila.TokenHash.Length);
        Assert.NotEqual(token, Convert.ToBase64String(fila.TokenHash));

        // Contraseña débil: se rechaza sin gastar el token.
        var cuentas = _t.Svc<ICuentaService>();
        Assert.Equal(ServiceErrorKind.Validation, (await Rechaza(() => cuentas.RestablecerContrasenaAsync(token, "corta", default))).Kind);

        await cuentas.RestablecerContrasenaAsync(token, "Recuperada-Segura-5", default);

        // Un solo uso, sesiones cerradas y la contraseña anterior ya no sirve.
        Assert.Equal(ServiceErrorKind.Validation, (await Rechaza(() => cuentas.RestablecerContrasenaAsync(token, "Otra-Segura-Mas-6", default))).Kind);
        _t.NewScope();
        Assert.All(await _t.Db.SesionesUsuario.ToListAsync(), s => Assert.NotNull(s.RevocadaUtc));
        Assert.Equal(ServiceErrorKind.Unauthorized, (await Rechaza(() => _t.LoginAsync("admin@a.com"))).Kind);
        Assert.NotNull(await _t.LoginAsync("admin@a.com", "Recuperada-Segura-5"));
    }

    [Fact]
    public async Task Recuperacion_el_enlace_caduca_y_uno_nuevo_invalida_el_anterior()
    {
        await _t.CrearEmpresaAsync("A", "admin@a.com");
        var cuentas = _t.Svc<ICuentaService>();

        await cuentas.SolicitarRecuperacionAsync("admin@a.com", default);
        var viejo = _t.Correo.Enviados[0].Token;
        await cuentas.SolicitarRecuperacionAsync("admin@a.com", default);
        var nuevo = _t.Correo.Enviados[1].Token;
        Assert.NotEqual(viejo, nuevo);
        Assert.Equal(ServiceErrorKind.Validation, (await Rechaza(() => cuentas.RestablecerContrasenaAsync(viejo, "Recuperada-Segura-5", default))).Kind);

        _t.Clock.Advance(TimeSpan.FromMinutes(31));   // vigencia: 30 minutos
        Assert.Equal(ServiceErrorKind.Validation, (await Rechaza(() => cuentas.RestablecerContrasenaAsync(nuevo, "Recuperada-Segura-5", default))).Kind);
        Assert.Equal(ServiceErrorKind.Validation, (await Rechaza(() => cuentas.RestablecerContrasenaAsync("token-inventado", "Recuperada-Segura-5", default))).Kind);
    }

    [Fact]
    public async Task Recuperacion_no_aplica_a_cuentas_desactivadas()
    {
        var a = await _t.CrearEmpresaAsync("A", "admin@a.com");
        var uid = await _t.NuevoUsuarioAsync(a.EmpresaId, "u@a.com", "U", "A", a.Roles[NombresRol.Trabajador]);
        _t.Current.As(a, a.AdminId);
        await _t.Svc<IUsuarioService>().DesactivarAsync(uid, default);
        await _t.Svc<ICuentaService>().SolicitarRecuperacionAsync("u@a.com", default);
        Assert.Empty(_t.Correo.Enviados);
    }

    // ---------------- Confirmación de correo ----------------
    [Fact]
    public async Task Un_usuario_nuevo_recibe_un_enlace_de_confirmacion_y_lo_confirma_una_sola_vez()
    {
        var a = await _t.CrearEmpresaAsync("A", "admin@a.com");
        _t.Current.As(a, a.AdminId);
        var dto = await _t.Svc<IUsuarioService>().CrearAsync(new CrearUsuarioRequest
        { Correo = "nuevo@a.com", Nombre = "N", Apellido = "U", Contrasena = TestEnv.Password }, default);
        Assert.Null(dto.CorreoConfirmadoUtc);

        var correo = Assert.Single(_t.Correo.Enviados);
        Assert.Equal("nuevo@a.com", correo.Para);
        Assert.Contains("confirmar-correo?token=", correo.Cuerpo);

        _t.Current.Anonymous();
        await _t.Svc<ICuentaService>().ConfirmarCorreoAsync(correo.Token, default);
        _t.NewScope();
        Assert.NotNull((await _t.Db.Usuarios.SingleAsync(u => u.UsuarioId == dto.UsuarioId)).CorreoConfirmadoUtc);

        Assert.Equal(ServiceErrorKind.Validation, (await Rechaza(() => _t.Svc<ICuentaService>().ConfirmarCorreoAsync(correo.Token, default))).Kind);
        Assert.Equal(ServiceErrorKind.Validation, (await Rechaza(() => _t.Svc<ICuentaService>().ConfirmarCorreoAsync("inventado", default))).Kind);
    }

    [Fact]
    public async Task Crear_usuario_no_falla_si_el_correo_de_confirmacion_no_puede_enviarse()
    {
        var a = await _t.CrearEmpresaAsync("A", "admin@a.com");
        _t.Correo.Falla = true;
        _t.Current.As(a, a.AdminId);
        var dto = await _t.Svc<IUsuarioService>().CrearAsync(new CrearUsuarioRequest
        { Correo = "nuevo@a.com", Nombre = "N", Apellido = "U", Contrasena = TestEnv.Password }, default);
        Assert.NotEqual(Guid.Empty, dto.UsuarioId);
    }

    [Fact]
    public async Task Cambiar_el_correo_exige_confirmarlo_de_nuevo()
    {
        var a = await _t.CrearEmpresaAsync("A", "admin@a.com");
        var uid = await _t.NuevoUsuarioAsync(a.EmpresaId, "u@a.com", "U", "A", a.Roles[NombresRol.Trabajador]);
        _t.Current.As(a, a.AdminId);
        var dto = await _t.Svc<IUsuarioService>().ActualizarAsync(uid, new ActualizarUsuarioRequest { Correo = "u2@a.com", Nombre = "U", Apellido = "A" }, default);
        Assert.Null(dto.CorreoConfirmadoUtc);
        Assert.Equal("u2@a.com", Assert.Single(_t.Correo.Enviados).Para);

        // Si solo cambian el nombre, no se pide confirmar nada.
        var token = await Enlace(uid);
        await _t.Svc<ICuentaService>().ConfirmarCorreoAsync(token, default);
        _t.Correo.Enviados.Clear();
        var dto2 = await _t.Svc<IUsuarioService>().ActualizarAsync(uid, new ActualizarUsuarioRequest { Correo = "u2@a.com", Nombre = "Otro", Apellido = "A" }, default);
        Assert.NotNull(dto2.CorreoConfirmadoUtc);
        Assert.Empty(_t.Correo.Enviados);
    }

    private async Task<string> Enlace(Guid usuarioId)
    {
        await _t.Svc<ICuentaService>().ReenviarConfirmacionAsync("u2@a.com", default);
        return _t.Correo.Enviados.Last().Token;
    }

    [Fact]
    public async Task Con_confirmacion_obligatoria_no_se_inicia_sesion_hasta_confirmar()
    {
        _t.Seguridad.RequireConfirmedEmail = true;
        var a = await _t.CrearEmpresaAsync("A", "admin@a.com");
        _t.Current.As(a, a.AdminId);
        await _t.Svc<IUsuarioService>().CrearAsync(new CrearUsuarioRequest
        { Correo = "nuevo@a.com", Nombre = "N", Apellido = "U", Contrasena = TestEnv.Password }, default);
        var token = _t.Correo.Enviados.Single().Token;

        // Contraseña incorrecta: mensaje genérico; correcta pero sin confirmar: se explica el motivo.
        Assert.Equal(ServiceErrorKind.Unauthorized, (await Rechaza(() => _t.LoginAsync("nuevo@a.com", "Equivocada-123"))).Kind);
        Assert.Equal(ServiceErrorKind.Forbidden, (await Rechaza(() => _t.LoginAsync("nuevo@a.com"))).Kind);

        _t.Current.Anonymous();
        await _t.Svc<ICuentaService>().ConfirmarCorreoAsync(token, default);
        Assert.NotNull(await _t.LoginAsync("nuevo@a.com"));
    }

    [Fact]
    public async Task Reenviar_confirmacion_no_revela_la_cuenta_y_solo_aplica_a_pendientes()
    {
        var a = await _t.CrearEmpresaAsync("A", "admin@a.com");   // creada ya confirmada
        await _t.Svc<ICuentaService>().ReenviarConfirmacionAsync("admin@a.com", default);
        await _t.Svc<ICuentaService>().ReenviarConfirmacionAsync("fantasma@a.com", default);
        Assert.Empty(_t.Correo.Enviados);
    }

    // ---------------- Mis sesiones y bandera de cambio de contraseña ----------------
    [Fact]
    public async Task Cada_usuario_ve_y_cierra_solo_sus_propias_sesiones()
    {
        var a = await _t.CrearEmpresaAsync("A", "admin@a.com");
        var u1 = await _t.NuevoUsuarioAsync(a.EmpresaId, "u1@a.com", "U", "1", a.Roles[NombresRol.Trabajador]);
        var u2 = await _t.NuevoUsuarioAsync(a.EmpresaId, "u2@a.com", "U", "2", a.Roles[NombresRol.Trabajador]);
        await _t.LoginAsync("u1@a.com");
        await _t.LoginAsync("u1@a.com");
        await _t.LoginAsync("u2@a.com");
        _t.NewScope();
        var sesiones = await _t.Db.SesionesUsuario.ToListAsync();
        var actual = sesiones.First(s => s.UsuarioId == u1).SesionId;
        var ajena = sesiones.First(s => s.UsuarioId == u2).SesionId;
        var otraMia = sesiones.Last(s => s.UsuarioId == u1).SesionId;

        _t.Current.As(a, u1, actual);
        var svc = _t.Svc<ISesionService>();
        var mias = await svc.ListarMiasAsync(new PageQuery(), default);
        Assert.Equal(2, mias.Total);
        Assert.All(mias.Items, s => Assert.Equal("u1@a.com", s.UsuarioCorreo));
        Assert.Single(mias.Items, s => s.EsActual);

        Assert.Equal(ServiceErrorKind.NotFound, (await Rechaza(() => svc.RevocarMiaAsync(ajena, default))).Kind);
        await svc.RevocarMiaAsync(otraMia, default);
        Assert.Equal(1, (await svc.ListarMiasAsync(new PageQuery(), default)).Total);
        Assert.True(await svc.ValidarSesionAsync(ajena, u2, a.EmpresaId, default));   // la ajena sigue intacta
    }

    [Fact]
    public async Task La_validacion_de_sesion_informa_si_debe_cambiar_la_contrasena()
    {
        var a = await _t.CrearEmpresaAsync("A", "admin@a.com");
        var uid = await _t.NuevoUsuarioAsync(a.EmpresaId, "u@a.com", "U", "A", a.Roles[NombresRol.Trabajador]);
        await _t.LoginAsync("u@a.com");
        _t.NewScope();
        var sesion = (await _t.Db.SesionesUsuario.SingleAsync()).SesionId;

        Assert.Equal(new SesionValidada(true, false), await _t.Svc<ISesionService>().ValidarSesionDetalladaAsync(sesion, uid, a.EmpresaId, default));
        (await _t.Db.Usuarios.FirstAsync(u => u.UsuarioId == uid)).DebeCambiarContrasena = true;
        await _t.Db.SaveChangesAsync();
        Assert.Equal(new SesionValidada(true, true), await _t.Svc<ISesionService>().ValidarSesionDetalladaAsync(sesion, uid, a.EmpresaId, default));
        Assert.Equal(new SesionValidada(false, false), await _t.Svc<ISesionService>().ValidarSesionDetalladaAsync(sesion, uid, Guid.NewGuid(), default));
    }

    // ---------------- Vínculo con trabajadores ----------------
    private static Trabajador NuevoTrabajador(Guid empresaId) => new()
    {
        TrabajadorId = Guid.NewGuid(), EmpresaId = empresaId, Nombres = "Nombre", Apellidos = "Apellido",
        FechaIngreso = new DateOnly(2026, 1, 1), EstadoLaboral = 1, FechaCreacionUtc = DateTime.UtcNow
    };

    [Fact]
    public async Task El_usuario_solo_se_vincula_a_un_trabajador_de_su_empresa_y_sin_repetirlo()
    {
        var a = await _t.CrearEmpresaAsync("A", "admin@a.com");
        var b = await _t.CrearEmpresaAsync("B", "admin@b.com");
        var tA = NuevoTrabajador(a.EmpresaId);
        var tB = NuevoTrabajador(b.EmpresaId);
        _t.Db.AddRange(tA, tB);
        await _t.Db.SaveChangesAsync();
        _t.NewScope();

        _t.Current.As(a, a.AdminId);
        var svc = _t.Svc<IUsuarioService>();
        CrearUsuarioRequest Req(string correo, Guid? trabajador) => new()
        { Correo = correo, Nombre = "N", Apellido = "U", Contrasena = TestEnv.Password, TrabajadorId = trabajador };

        Assert.Equal(ServiceErrorKind.Validation, (await Rechaza(() => svc.CrearAsync(Req("x@a.com", tB.TrabajadorId), default))).Kind);   // de otra empresa
        Assert.Equal(ServiceErrorKind.Validation, (await Rechaza(() => svc.CrearAsync(Req("x@a.com", Guid.NewGuid()), default))).Kind);    // inexistente
        var ok = await svc.CrearAsync(Req("ok@a.com", tA.TrabajadorId), default);
        Assert.Equal(tA.TrabajadorId, ok.TrabajadorId);
        Assert.Equal(ServiceErrorKind.Conflict, (await Rechaza(() => svc.CrearAsync(Req("otro@a.com", tA.TrabajadorId), default))).Kind);    // ya tiene cuenta

        // Editar al mismo usuario conservando su trabajador no es conflicto.
        var igual = await svc.ActualizarAsync(ok.UsuarioId, new ActualizarUsuarioRequest { Correo = "ok@a.com", Nombre = "Nuevo", Apellido = "U", TrabajadorId = tA.TrabajadorId }, default);
        Assert.Equal("Nuevo", igual.Nombre);
    }
}
