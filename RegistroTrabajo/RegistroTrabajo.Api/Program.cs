using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RegistroTrabajo.Api.Infrastructure;
using RegistroTrabajo.Api.Settings;
using RegistroTrabajo.Data;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

var builder = WebApplication.CreateBuilder(args);

var settings = builder.Configuration
    .GetSection(RegistroTrabajoApiSettings.SectionName)
    .Get<RegistroTrabajoApiSettings>()
    ?? throw new InvalidOperationException($"Falta la sección {RegistroTrabajoApiSettings.SectionName} en appsettings.");

if (string.IsNullOrWhiteSpace(settings.JwtSettings.SecretKey) || settings.JwtSettings.SecretKey.Length < 32)
    throw new InvalidOperationException(
        $"Falta JwtSettings:SecretKey (mínimo 32 caracteres). Entorno actual: '{builder.Environment.EnvironmentName}' " +
        "(los user-secrets solo se cargan en 'Development'). Configúrela con user-secrets o variables de entorno; no la guarde en el repositorio.");

builder.Services.AddSingleton(settings);
builder.Services.AddDbContext<RegistroTrabajoDbContext>(o =>
    o.UseSqlServer(settings.DbSettings.BuildConnectionString()));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddSingleton<IAccessTokenFactory, JwtAccessTokenFactory>();
builder.Services.AddRegistroTrabajoServices(o =>
{
    o.MaxFailedAttempts = settings.Security.MaxFailedAttempts;
    o.LockoutMinutes = settings.Security.LockoutMinutes;
    o.ActivityTouchSeconds = settings.Security.ActivityTouchSeconds;
    o.RecoveryTokenMinutes = settings.Security.RecoveryTokenMinutes;
    o.RecoveryRequestsPerWindow = settings.Security.RecoveryRequestsPerWindow;
    o.RecoveryRequestWindowMinutes = settings.Security.RecoveryRequestWindowMinutes;
    o.ConfirmationTokenHours = settings.Security.ConfirmationTokenHours;
    o.RequireConfirmedEmail = settings.Security.RequireConfirmedEmail;
}, e =>
{
    e.Mode = settings.Email.Mode;
    e.Host = settings.Email.Host;
    e.Port = settings.Email.Port;
    e.UseSsl = settings.Email.UseSsl;
    e.User = settings.Email.User;
    e.Password = settings.Email.Password;
    e.From = settings.Email.From;
    e.FromName = settings.Email.FromName;
    e.DevDirectory = settings.Email.DevDirectory;
    e.ClientBaseUrl = string.IsNullOrWhiteSpace(settings.Email.ClientBaseUrl)
        ? settings.AllowedOrigins.FirstOrDefault() ?? e.ClientBaseUrl
        : settings.Email.ClientBaseUrl;
}, r =>
{
    r.Carpeta = settings.Respaldos.Carpeta;
    r.Comprimir = settings.Respaldos.Comprimir;
    r.MinutosOperacionVigente = settings.Respaldos.MinutosOperacionVigente;
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = settings.JwtSettings.Issuer,
            ValidateAudience = true, ValidAudience = settings.JwtSettings.Audience,
            ValidateIssuerSigningKey = true, IssuerSigningKey = JwtAccessTokenFactory.Key(settings.JwtSettings.SecretKey),
            ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = ClaimNames.UsuarioId
        };
        // Cada solicitud revalida en BD que la sesión no esté revocada/expirada y que la cuenta siga activa.
        o.Events = new JwtBearerEvents
        {
            OnTokenValidated = async ctx =>
            {
                var p = ctx.Principal!;
                var ok = Guid.TryParse(p.FindFirst(ClaimNames.UsuarioId)?.Value, out var uid)
                    & Guid.TryParse(p.FindFirst(ClaimNames.EmpresaId)?.Value, out var eid)
                    & Guid.TryParse(p.FindFirst(ClaimNames.SesionId)?.Value, out var sid);
                var validacion = ok
                    ? await ctx.HttpContext.RequestServices.GetRequiredService<ISesionService>()
                        .ValidarSesionDetalladaAsync(sid, uid, eid, ctx.HttpContext.RequestAborted)
                    : null;
                if (validacion is not { Valida: true })
                {
                    ctx.Fail("Sesión no válida.");
                    return;
                }
                // Lo usa ForzarCambioContrasenaMiddleware: con la bandera activa solo se permite cambiar la contraseña.
                ctx.HttpContext.Items[ForzarCambioContrasenaMiddleware.ItemKey] = validacion.DebeCambiarContrasena;
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddScoped<IAuthorizationHandler, PermisoAuthorizationHandler>();

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(settings.AllowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .WithExposedHeaders(CorrelationIdMiddleware.Header)));

builder.Services.AddControllers(o => ValidacionEnEspanol.ConfigurarEnlaceDeModelo(o.ModelBindingMessageProvider))
    .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = ValidacionEnEspanol.RespuestaInvalida);
builder.Services.AddOpenApi();

var app = builder.Build();

if (args.Contains("bootstrap-admin"))
    return await Bootstrap.RunAsync(app);
if (args.Contains("bootstrap-platform"))
    return await Bootstrap.RunPlatformAsync(app);
if (args.Contains("seed-dev-users"))
    return await Bootstrap.SeedDevUsersAsync(app);
if (args.Contains("reset-dev-password"))
    return await Bootstrap.ResetDevPasswordAsync(app);

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ErrorHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// En desarrollo el cliente usa http://localhost:5265; una redirección rompería el preflight de CORS (igual en pruebas con RedirigirAHttps=false).
if (!app.Environment.IsDevelopment() && settings.RedirigirAHttps) app.UseHttpsRedirection();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<ForzarCambioContrasenaMiddleware>();
app.UseAuthorization();
app.MapControllers();

app.Run();
return 0;

// Procedimiento de inicialización: dotnet run --project RegistroTrabajo.Api -- bootstrap-admin
// Datos por variables de entorno o user-secrets (nunca en el repositorio):
//   BOOTSTRAP_EMPRESA, BOOTSTRAP_ADMIN_EMAIL, BOOTSTRAP_ADMIN_PASSWORD, [BOOTSTRAP_ADMIN_NOMBRE], [BOOTSTRAP_ADMIN_APELLIDO]
static class Bootstrap
{
    // Solo desarrollo: dotnet run --project RegistroTrabajo.Api -- seed-dev-users
    // Crea (si no existen) la empresa demo y un usuario por rol. La contraseña común se lee de SEED_USERS_PASSWORD
    // (user-secrets o variable de entorno); no hay contraseñas por defecto en el código.
    // Con SEED_RESET_PASSWORD=true también restablece la contraseña de los usuarios demo que ya existen.
    public static async Task<int> SeedDevUsersAsync(WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            Console.Error.WriteLine("seed-dev-users solo está permitido en el entorno Development.");
            return 1;
        }

        var password = app.Configuration["SEED_USERS_PASSWORD"];
        if (string.IsNullOrWhiteSpace(password))
        {
            Console.Error.WriteLine("Defina SEED_USERS_PASSWORD (user-secrets o variable de entorno).");
            return 1;
        }

        var usuarios = new (string Rol, string Correo, string Nombre, string Apellido)[]
        {
            (NombresRol.Administrador, "admin@registrotrabajo.local", "Ana", "Administradora"),
            (NombresRol.RecursosHumanos, "rh@registrotrabajo.local", "Rosa", "Recursos"),
            (NombresRol.Supervisor, "supervisor@registrotrabajo.local", "Sergio", "Supervisor"),
            (NombresRol.Trabajador, "trabajador@registrotrabajo.local", "Tomás", "Trabajador")
        };

        using var scope = app.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IBootstrapService>();
        var db = scope.ServiceProvider.GetRequiredService<RegistroTrabajoDbContext>();
        try
        {
            if (!await db.Empresas.AnyAsync())
            {
                var a = usuarios[0];
                await svc.CrearEmpresaInicialAsync("Empresa Demo", a.Correo, a.Nombre, a.Apellido, password,
                    CancellationToken.None, debeCambiarContrasena: false);
                Console.WriteLine($"Empresa Demo creada con {a.Correo} ({a.Rol}).");
            }

            foreach (var u in usuarios)
            {
                var reset = app.Configuration["SEED_RESET_PASSWORD"] == "true";
                var hecho = await svc.CrearUsuarioConRolAsync(u.Rol, u.Correo, u.Nombre, u.Apellido, password, CancellationToken.None, reset);
                Console.WriteLine($"{(hecho ? (reset ? "Contraseña restablecida/creado" : "Creado") : "Ya existe")}  {u.Correo}  ({u.Rol})");
            }
            return 0;
        }
        catch (ServiceException ex)
        {
            Console.Error.WriteLine($"No se pudo crear los usuarios: {ex.Message}");
            return 1;
        }
    }

    // Solo desarrollo: dotnet run --project RegistroTrabajo.Api -- reset-dev-password
    // Restablece la contraseña de una cuenta existente (RESET_EMAIL y RESET_PASSWORD por variable de entorno o user-secrets),
    // limpia sus intentos fallidos y el bloqueo. No hay contraseñas por defecto en el código.
    public static async Task<int> ResetDevPasswordAsync(WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            Console.Error.WriteLine("reset-dev-password solo está permitido en el entorno Development.");
            return 1;
        }

        var correo = app.Configuration["RESET_EMAIL"];
        var password = app.Configuration["RESET_PASSWORD"];
        if (string.IsNullOrWhiteSpace(correo) || string.IsNullOrWhiteSpace(password))
        {
            Console.Error.WriteLine("Defina RESET_EMAIL y RESET_PASSWORD (variable de entorno o user-secrets).");
            return 1;
        }

        using var scope = app.Services.CreateScope();
        try
        {
            var hecho = await scope.ServiceProvider.GetRequiredService<IBootstrapService>().RestablecerContrasenaExistenteAsync(correo, password, CancellationToken.None);
            Console.WriteLine(hecho ? $"Contraseña restablecida para {correo}." : $"No existe una cuenta con el correo {correo}.");
            return hecho ? 0 : 1;
        }
        catch (ServiceException ex)
        {
            Console.Error.WriteLine($"No se pudo restablecer: {ex.Message}");
            return 1;
        }
    }

    // dotnet run --project RegistroTrabajo.Api -- bootstrap-platform
    // Crea la empresa de plataforma y su administrador (da de alta las demás empresas). Variables de entorno o user-secrets:
    //   BOOTSTRAP_PLATFORM_EMPRESA, BOOTSTRAP_PLATFORM_EMAIL, BOOTSTRAP_PLATFORM_PASSWORD, [BOOTSTRAP_PLATFORM_NOMBRE], [BOOTSTRAP_PLATFORM_APELLIDO]
    public static async Task<int> RunPlatformAsync(WebApplication app)
    {
        var cfg = app.Configuration;
        string? V(string k) => cfg[k];
        using var scope = app.Services.CreateScope();
        try
        {
            var id = await scope.ServiceProvider.GetRequiredService<IBootstrapService>().CrearEmpresaPlataformaAsync(
                V("BOOTSTRAP_PLATFORM_EMPRESA") ?? "Plataforma", V("BOOTSTRAP_PLATFORM_EMAIL") ?? "",
                V("BOOTSTRAP_PLATFORM_NOMBRE") ?? "Administrador", V("BOOTSTRAP_PLATFORM_APELLIDO") ?? "de plataforma",
                V("BOOTSTRAP_PLATFORM_PASSWORD") ?? "", CancellationToken.None);
            Console.WriteLine($"Empresa de plataforma creada: {id}. El administrador deberá cambiar su contraseña al iniciar sesión.");
            return 0;
        }
        catch (ServiceException ex)
        {
            Console.Error.WriteLine($"No se pudo crear la plataforma: {ex.Message}");
            return 1;
        }
    }

    public static async Task<int> RunAsync(WebApplication app)
    {
        var cfg = app.Configuration;
        string? V(string k) => cfg[k];
        using var scope = app.Services.CreateScope();
        try
        {
            var id = await scope.ServiceProvider.GetRequiredService<IBootstrapService>().CrearEmpresaInicialAsync(
                V("BOOTSTRAP_EMPRESA") ?? "", V("BOOTSTRAP_ADMIN_EMAIL") ?? "",
                V("BOOTSTRAP_ADMIN_NOMBRE") ?? "Administrador", V("BOOTSTRAP_ADMIN_APELLIDO") ?? "Inicial",
                V("BOOTSTRAP_ADMIN_PASSWORD") ?? "", CancellationToken.None);
            Console.WriteLine($"Empresa inicial creada: {id}. El administrador deberá cambiar su contraseña al iniciar sesión.");
            return 0;
        }
        catch (ServiceException ex)
        {
            Console.Error.WriteLine($"No se pudo inicializar: {ex.Message}");
            return 1;
        }
    }
}

