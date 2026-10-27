using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RegistroTrabajo.Data;
using RegistroTrabajo.Data.Entities;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Tests;

public class FakeClock : TimeProvider
{
    private DateTimeOffset _now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan t) => _now += t;
}

public class FakeCurrentUser : ICurrentUser
{
    public bool IsAuthenticated => UsuarioId is not null && EmpresaId is not null;
    public Guid? UsuarioId { get; set; }
    public Guid? EmpresaId { get; set; }
    public Guid? SesionId { get; set; }
    public string? IpAddress { get; set; } = "10.0.0.1";
    public string? UserAgent { get; set; } = "xunit";
    public Guid? CorrelationId { get; set; } = Guid.NewGuid();

    public void As(EmpresaInfo e, Guid usuarioId, Guid? sesionId = null)
    {
        EmpresaId = e.EmpresaId; UsuarioId = usuarioId; SesionId = sesionId;
    }
    public void Anonymous() { EmpresaId = null; UsuarioId = null; SesionId = null; }
}

class FakeTokenFactory : IAccessTokenFactory
{
    public (string Token, DateTime ExpiraUtc) Crear(Usuario usuario, Guid sesionId, DateTime maxExpiraUtc) =>
        ("jwt-" + sesionId, maxExpiraUtc);
}

public record CorreoEnviado(string Para, string Asunto, string Cuerpo)
{
    /// <summary>Token del enlace (?token=...) del cuerpo del mensaje.</summary>
    public string Token => Uri.UnescapeDataString(Cuerpo[(Cuerpo.IndexOf("token=", StringComparison.Ordinal) + 6)..].Split('\n', ' ', '\r')[0]);
}

public class FakeEmailSender : IEmailSender
{
    public List<CorreoEnviado> Enviados { get; } = new();
    public bool Falla { get; set; }

    public Task EnviarAsync(string para, string asunto, string cuerpo, CancellationToken ct)
    {
        if (Falla) throw new InvalidOperationException("SMTP caído");
        Enviados.Add(new CorreoEnviado(para, asunto, cuerpo));
        return Task.CompletedTask;
    }
}

public class FakeRespaldoEjecutor : IRespaldoEjecutor
{
    public List<string> Archivos { get; } = new();
    public bool FallaRespaldo { get; set; }
    public bool FallaVerificacion { get; set; }
    public string Hash { get; set; } = "ABC123";
    public long Tamano { get; set; } = 2048;

    public Task<ResultadoArchivoRespaldo> RespaldarAsync(bool diferencial, string archivo, CancellationToken ct)
    {
        if (FallaRespaldo) throw new IOException(@"No se pudo escribir C:\secreto\clave=123");
        Archivos.Add(archivo);
        return Task.FromResult(new ResultadoArchivoRespaldo(Tamano, Hash));
    }

    public Task<ResultadoArchivoRespaldo> VerificarAsync(string archivo, CancellationToken ct)
    {
        if (FallaVerificacion) throw new InvalidDataException("checksum");
        return Task.FromResult(new ResultadoArchivoRespaldo(Tamano, Hash));
    }
}

public record EmpresaInfo(Guid EmpresaId, Guid AdminId, string AdminCorreo, Dictionary<string, Guid> Roles);

public class TestEnv : IDisposable
{
    public const string Password = "Contrasena-Segura-1";

    private readonly SqliteConnection _conn;
    public ServiceProvider Root { get; }
    public IServiceScope Scope { get; private set; }
    public FakeClock Clock { get; } = new();
    public FakeCurrentUser Current { get; } = new();
    public FakeEmailSender Correo { get; } = new();
    public FakeRespaldoEjecutor Respaldo { get; } = new();
    public SecurityOptions Seguridad => Root.GetRequiredService<IOptions<SecurityOptions>>().Value;

    public RegistroTrabajoDbContext Db => Scope.ServiceProvider.GetRequiredService<RegistroTrabajoDbContext>();
    public T Svc<T>() where T : notnull => Scope.ServiceProvider.GetRequiredService<T>();

    public TestEnv(bool seedPermisos = true)
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddDbContext<RegistroTrabajoDbContext>(o => o.UseSqlite(_conn));
        sc.AddSingleton<ICurrentUser>(Current);
        sc.AddSingleton<TimeProvider>(Clock);
        sc.AddSingleton<IAccessTokenFactory, FakeTokenFactory>();
        sc.AddSingleton<IEmailSender>(Correo);
        sc.AddSingleton<IRespaldoEjecutor>(Respaldo);
        sc.AddRegistroTrabajoServices(o => { o.MaxFailedAttempts = 3; o.LockoutMinutes = 10; o.ActivityTouchSeconds = 0; });
        Root = sc.BuildServiceProvider();
        Scope = Root.CreateScope();
        Db.Database.EnsureCreated();
        if (seedPermisos) SeedPermisos();
    }

    /// <summary>Nuevo scope = nuevo DbContext (descarta el change tracker, como una solicitud nueva).</summary>
    public void NewScope()
    {
        Scope.Dispose();
        Scope = Root.CreateScope();
    }

    private void SeedPermisos()
    {
        var id = 1;
        foreach (var codigo in Permisos.Todos.Concat(PermisosPlataforma.Todos))
        {
            var partes = codigo.Split('.');
            Db.PermisosSistema.Add(new PermisoSistema { PermisoId = id++, Codigo = codigo, Modulo = partes[0], Accion = string.Join('.', partes.Skip(1)) });
        }
        Db.SaveChanges();
    }

    /// <summary>Crea empresa + configuración + 4 roles + administrador directamente (sin pasar por el bootstrap de una sola vez).</summary>
    public async Task<EmpresaInfo> CrearEmpresaAsync(string nombre, string adminCorreo)
    {
        var db = Db;
        var now = Clock.GetUtcNow().UtcDateTime;
        var catalogo = await db.PermisosSistema.ToListAsync();
        var empresa = new Empresa { EmpresaId = Guid.NewGuid(), Nombre = nombre, Estado = EstadoEmpresa.Activa, FechaCreacionUtc = now };
        db.Empresas.Add(empresa);
        db.ConfiguracionEmpresa.Add(new ConfiguracionEmpresa
        {
            EmpresaId = empresa.EmpresaId, ZonaHorariaId = "UTC", Cultura = "es-MX", FormatoFecha = "dd/MM/yyyy",
            FormatoHora = "HH:mm", CierreSesionInactividadMinutos = 30, DuracionSesionMaximaHoras = 12, FechaCreacionUtc = now
        });
        var roles = new Dictionary<string, Guid>();
        foreach (var n in NombresRol.Iniciales)
        {
            var rol = new Rol { RolId = Guid.NewGuid(), EmpresaId = empresa.EmpresaId, Nombre = n, EsSistema = true, Estado = true, FechaCreacionUtc = now };
            roles[n] = rol.RolId;
            db.Roles.Add(rol);
            foreach (var c in PermisosPorDefecto.PorRol[n])
                db.RolPermisos.Add(new RolPermiso { EmpresaId = empresa.EmpresaId, RolId = rol.RolId, PermisoId = catalogo.First(p => p.Codigo == c).PermisoId, Permitido = true, FechaActualizacionUtc = now });
        }
        var admin = await NuevoUsuarioAsync(empresa.EmpresaId, adminCorreo, "Admin", "Uno", roles[NombresRol.Administrador]);
        return new EmpresaInfo(empresa.EmpresaId, admin, adminCorreo, roles);
    }

    /// <summary>Crea la empresa de plataforma con su administrador (por el mismo camino que el comando bootstrap-platform).</summary>
    public async Task<EmpresaInfo> CrearPlataformaAsync(string adminCorreo = "plataforma@rt.com")
    {
        var id = await Svc<IBootstrapService>().CrearEmpresaPlataformaAsync("Plataforma", adminCorreo, "Pla", "Taforma", Password, default);
        NewScope();
        var admin = await Db.Usuarios.AsNoTracking().FirstAsync(u => u.EmpresaId == id);
        var roles = await Db.Roles.AsNoTracking().Where(r => r.EmpresaId == id).ToDictionaryAsync(r => r.Nombre, r => r.RolId);
        return new EmpresaInfo(id, admin.UsuarioId, adminCorreo, roles);
    }

    public async Task<Guid> NuevoUsuarioAsync(Guid empresaId, string correo, string nombre, string apellido, params Guid[] rolIds)
    {
        var db = Db;
        var u = new Usuario
        {
            UsuarioId = Guid.NewGuid(), EmpresaId = empresaId, Correo = correo, CorreoNormalizado = correo.Trim().ToUpperInvariant(),
            Nombre = nombre, Apellido = apellido, Estado = EstadoUsuario.Activo, FechaCreacionUtc = Clock.GetUtcNow().UtcDateTime,
            CorreoConfirmadoUtc = Clock.GetUtcNow().UtcDateTime, HashContrasena = ""
        };
        u.HashContrasena = new PasswordHasher<Usuario>().HashPassword(u, Password);
        db.Usuarios.Add(u);
        foreach (var r in rolIds)
            db.UsuarioRoles.Add(new UsuarioRol { EmpresaId = empresaId, UsuarioId = u.UsuarioId, RolId = r, AsignadoUtc = Clock.GetUtcNow().UtcDateTime });
        await db.SaveChangesAsync();
        return u.UsuarioId;
    }

    public async Task<TokenResponse> LoginAsync(string correo, string? contrasena = null)
    {
        Current.Anonymous();
        return await Svc<IAuthService>().LoginAsync(new LoginRequest { Correo = correo, Contrasena = contrasena ?? Password }, default);
    }

    public void Dispose()
    {
        Scope.Dispose();
        Root.Dispose();
        _conn.Dispose();
    }
}
