using Microsoft.EntityFrameworkCore;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Tests;

/// <summary>Al dar de alta un trabajador se crea también su usuario (una sola operación: o se guardan ambos o ninguno).</summary>
public class AltaTrabajadorConCuentaTests : IDisposable
{
    private readonly TestEnv _t = new();
    public void Dispose() => _t.Dispose();

    private ITrabajadorService Trab => _t.Svc<ITrabajadorService>();

    private static async Task<ServiceException> Rechaza(Func<Task> accion) =>
        await Assert.ThrowsAnyAsync<ServiceException>(accion);

    private static CrearTrabajadorRequest Nuevo(string nombres, string apellidos, string correo, Guid? rolId = null) => new()
    {
        Nombres = nombres, Apellidos = apellidos, FechaIngreso = new DateOnly(2025, 6, 1),
        Cuenta = new CuentaTrabajadorRequest { Correo = correo, Contrasena = "Clave-Segura-123", RolId = rolId }
    };

    private async Task<EmpresaInfo> EmpresaAsync(string nombre, string correoAdmin)
    {
        var e = await _t.CrearEmpresaAsync(nombre, correoAdmin);
        _t.Current.As(e, e.AdminId);
        return e;
    }

    [Fact]
    public async Task Crear_trabajador_crea_su_usuario_con_rol_Trabajador_y_ambos_quedan_vinculados()
    {
        await EmpresaAsync("A", "admin@a.com");

        var t = await Trab.CrearAsync(Nuevo("Ana", "Ruiz", "ana@a.com"), default);

        Assert.NotNull(t.UsuarioId);
        var usuario = await _t.Db.Usuarios.AsNoTracking().Include(u => u.UsuarioRoles).ThenInclude(ur => ur.Rol)
            .SingleAsync(u => u.UsuarioId == t.UsuarioId);
        Assert.Equal("ana@a.com", usuario.Correo);
        Assert.Equal("Ana", usuario.Nombre);
        Assert.Equal("Ruiz", usuario.Apellido);
        Assert.Equal(t.TrabajadorId, usuario.TrabajadorId);
        Assert.True(usuario.DebeCambiarContrasena);
        Assert.Equal(NombresRol.Trabajador, Assert.Single(usuario.UsuarioRoles).Rol.Nombre);
    }

    [Fact]
    public async Task Si_el_correo_ya_existe_no_se_crea_ni_el_trabajador_ni_el_usuario()
    {
        await EmpresaAsync("A", "admin@a.com");

        var ex = await Rechaza(() => Trab.CrearAsync(Nuevo("Ana", "Ruiz", "admin@a.com"), default));

        Assert.Equal(ServiceErrorKind.Conflict, ex.Kind);
        Assert.Equal(0, await _t.Db.Trabajadores.CountAsync());
        Assert.Equal(1, await _t.Db.Usuarios.CountAsync());   // solo el administrador
    }

    [Fact]
    public async Task Una_contrasena_debil_no_deja_un_trabajador_sin_cuenta()
    {
        await EmpresaAsync("A", "admin@a.com");
        var r = Nuevo("Ana", "Ruiz", "ana@a.com");
        r.Cuenta!.Contrasena = "corta";

        var ex = await Rechaza(() => Trab.CrearAsync(r, default));

        Assert.Equal(ServiceErrorKind.Validation, ex.Kind);
        Assert.Equal(0, await _t.Db.Trabajadores.CountAsync());
    }

    [Fact]
    public async Task No_se_acepta_una_cuenta_nueva_y_un_usuario_existente_a_la_vez()
    {
        var e = await EmpresaAsync("A", "admin@a.com");
        var r = Nuevo("Ana", "Ruiz", "ana@a.com");
        r.UsuarioId = e.AdminId;

        var ex = await Rechaza(() => Trab.CrearAsync(r, default));

        Assert.Equal(ServiceErrorKind.Validation, ex.Kind);
        Assert.Equal(0, await _t.Db.Trabajadores.CountAsync());
    }

    [Fact]
    public async Task El_rol_de_la_cuenta_debe_ser_de_la_empresa()
    {
        await EmpresaAsync("A", "admin@a.com");

        var ex = await Rechaza(() => Trab.CrearAsync(Nuevo("Ana", "Ruiz", "ana@a.com", Guid.NewGuid()), default));

        Assert.Equal(ServiceErrorKind.Validation, ex.Kind);
        Assert.Equal(0, await _t.Db.Trabajadores.CountAsync());
        Assert.Equal(1, await _t.Db.Usuarios.CountAsync());
    }
}
