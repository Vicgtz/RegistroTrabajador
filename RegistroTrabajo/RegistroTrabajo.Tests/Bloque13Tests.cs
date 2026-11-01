using Microsoft.EntityFrameworkCore;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Tests;

/// <summary>Bloque 13: registro de cuentas con clave de registro por empresa.</summary>
public class Bloque13Tests : IDisposable
{
    private readonly TestEnv _t = new();
    public void Dispose() => _t.Dispose();

    private static RegistroConClaveRequest Datos(string clave, string correo = "nuevo@rt.com", string? contrasena = null) => new()
    {
        Clave = clave, Correo = correo, Nombre = "Nora", Apellido = "Nueva", Contrasena = contrasena ?? TestEnv.Password
    };

    private async Task<(EmpresaInfo Empresa, string Clave)> EmpresaConClaveAsync(string nombre = "Acme", string admin = "admin@acme.com")
    {
        var e = await _t.CrearEmpresaAsync(nombre, admin);
        _t.Current.As(e, e.AdminId);
        var g = await _t.Svc<IClaveRegistroService>().GenerarAsync(default);
        _t.Current.Anonymous();
        _t.NewScope();
        return (e, g.Clave);
    }

    [Fact]
    public async Task Registro_con_clave_valida_crea_un_trabajador_que_puede_entrar()
    {
        var (e, clave) = await EmpresaConClaveAsync();

        await _t.Svc<IRegistroConClaveService>().RegistrarAsync(Datos(clave), default);
        _t.NewScope();

        var u = await _t.Db.Usuarios.Include(x => x.UsuarioRoles).ThenInclude(r => r.Rol).SingleAsync(x => x.CorreoNormalizado == "NUEVO@RT.COM");
        Assert.Equal(e.EmpresaId, u.EmpresaId);
        Assert.False(u.DebeCambiarContrasena);
        Assert.Equal(EstadoUsuario.Activo, u.Estado);
        Assert.Equal(new[] { NombresRol.Trabajador }, u.UsuarioRoles.Select(r => r.Rol.Nombre));
        Assert.Contains(await _t.Db.Auditoria.ToListAsync(), a => a.Accion == "AutoRegistro" && a.EmpresaId == e.EmpresaId);
        Assert.Empty(_t.Correo.Enviados);                    // sin correo mientras la empresa no exija confirmarlo

        var token = await _t.LoginAsync("nuevo@rt.com");
        Assert.False(string.IsNullOrEmpty(token.AccessToken));
    }

    [Fact]
    public async Task La_clave_ignora_guiones_espacios_y_mayusculas()
    {
        var (e, clave) = await EmpresaConClaveAsync();
        var escrita = " " + clave.Replace("-", " ").ToLowerInvariant() + " ";

        await _t.Svc<IRegistroConClaveService>().RegistrarAsync(Datos(escrita), default);
        _t.NewScope();
        Assert.Equal(e.EmpresaId, (await _t.Db.Usuarios.SingleAsync(x => x.CorreoNormalizado == "NUEVO@RT.COM")).EmpresaId);
    }

    [Fact]
    public async Task Cada_clave_registra_en_su_propia_empresa()
    {
        var (a, claveA) = await EmpresaConClaveAsync("Acme", "admin@acme.com");
        var (b, claveB) = await EmpresaConClaveAsync("Beta", "admin@beta.com");

        await _t.Svc<IRegistroConClaveService>().RegistrarAsync(Datos(claveA, "a@rt.com"), default);
        await _t.Svc<IRegistroConClaveService>().RegistrarAsync(Datos(claveB, "b@rt.com"), default);
        _t.NewScope();

        Assert.Equal(a.EmpresaId, (await _t.Db.Usuarios.SingleAsync(x => x.CorreoNormalizado == "A@RT.COM")).EmpresaId);
        Assert.Equal(b.EmpresaId, (await _t.Db.Usuarios.SingleAsync(x => x.CorreoNormalizado == "B@RT.COM")).EmpresaId);
    }

    [Fact]
    public async Task Clave_incorrecta_o_correo_repetido_dan_el_mismo_error_generico()
    {
        var (_, clave) = await EmpresaConClaveAsync();
        var svc = _t.Svc<IRegistroConClaveService>();

        var malaClave = await Assert.ThrowsAsync<ServiceException>(() => svc.RegistrarAsync(Datos("AAAA-BBBB-CCCC-DDDD"), default));
        var repetido = await Assert.ThrowsAsync<ServiceException>(() => svc.RegistrarAsync(Datos(clave, "admin@acme.com"), default));

        Assert.Equal(RegistroConClaveService.RegistroInvalido, malaClave.Message);
        Assert.Equal(malaClave.Message, repetido.Message);
        Assert.Equal(ServiceErrorKind.Validation, malaClave.Kind);
        Assert.Equal(1, await _t.Db.Usuarios.CountAsync(x => x.CorreoNormalizado == "ADMIN@ACME.COM"));
    }

    [Fact]
    public async Task Contrasena_debil_se_rechaza()
    {
        var (_, clave) = await EmpresaConClaveAsync();
        await Assert.ThrowsAsync<ServiceException>(() => _t.Svc<IRegistroConClaveService>().RegistrarAsync(Datos(clave, contrasena: "corta"), default));
        Assert.False(await _t.Db.Usuarios.AnyAsync(x => x.CorreoNormalizado == "NUEVO@RT.COM"));
    }

    [Fact]
    public async Task Sin_clave_activa_o_con_clave_reemplazada_el_registro_falla()
    {
        var (e, vieja) = await EmpresaConClaveAsync();

        // Una clave nueva invalida la anterior.
        _t.Current.As(e, e.AdminId);
        var nueva = (await _t.Svc<IClaveRegistroService>().GenerarAsync(default)).Clave;
        _t.Current.Anonymous();
        Assert.NotEqual(vieja, nueva);
        await Assert.ThrowsAsync<ServiceException>(() => _t.Svc<IRegistroConClaveService>().RegistrarAsync(Datos(vieja), default));

        // Desactivar el registro invalida también la vigente.
        _t.Current.As(e, e.AdminId);
        await _t.Svc<IClaveRegistroService>().DesactivarAsync(default);
        Assert.False((await _t.Svc<IClaveRegistroService>().EstadoAsync(default)).Activa);
        _t.Current.Anonymous();
        await Assert.ThrowsAsync<ServiceException>(() => _t.Svc<IRegistroConClaveService>().RegistrarAsync(Datos(nueva), default));
    }

    [Fact]
    public async Task Empresa_suspendida_no_admite_registro()
    {
        var (e, clave) = await EmpresaConClaveAsync();
        var empresa = await _t.Db.Empresas.SingleAsync(x => x.EmpresaId == e.EmpresaId);
        empresa.Estado = EstadoEmpresa.Suspendida;
        await _t.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<ServiceException>(() => _t.Svc<IRegistroConClaveService>().RegistrarAsync(Datos(clave), default));
    }

    [Fact]
    public async Task Solo_quien_tiene_empresa_configurar_administra_la_clave()
    {
        var e = await _t.CrearEmpresaAsync("Acme", "admin@acme.com");
        var trabajador = await _t.NuevoUsuarioAsync(e.EmpresaId, "t@acme.com", "Tomas", "Trabajador", e.Roles[NombresRol.Trabajador]);
        _t.Current.As(e, trabajador);
        var svc = _t.Svc<IClaveRegistroService>();

        await Assert.ThrowsAsync<ServiceException>(() => svc.GenerarAsync(default));
        await Assert.ThrowsAsync<ServiceException>(() => svc.EstadoAsync(default));
        await Assert.ThrowsAsync<ServiceException>(() => svc.DesactivarAsync(default));
    }

    [Fact]
    public async Task La_clave_no_se_guarda_en_claro_ni_se_audita()
    {
        var (e, clave) = await EmpresaConClaveAsync();

        var cfg = await _t.Db.ConfiguracionEmpresa.AsNoTracking().SingleAsync(c => c.EmpresaId == e.EmpresaId);
        Assert.NotNull(cfg.ClaveRegistroHash);
        Assert.Equal(32, cfg.ClaveRegistroHash!.Length);
        var limpia = clave.Replace("-", "");
        var auditoria = await _t.Db.Auditoria.AsNoTracking().ToListAsync();
        Assert.Contains(auditoria, a => a.Accion == "GenerarClaveRegistro");
        Assert.DoesNotContain(auditoria, a => (a.DatosDespuesJson ?? "").Contains(limpia, StringComparison.OrdinalIgnoreCase)
                                              || (a.DatosAntesJson ?? "").Contains(limpia, StringComparison.OrdinalIgnoreCase));
    }
}
