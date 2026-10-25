using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RegistroTrabajo.Data.Entities;

namespace RegistroTrabajo.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRegistroTrabajoServices(this IServiceCollection services,
        Action<SecurityOptions>? configure = null, Action<EmailOptions>? configureEmail = null)
    {
        services.AddOptions<SecurityOptions>();
        if (configure is not null) services.Configure(configure);
        services.AddOptions<EmailOptions>();
        if (configureEmail is not null) services.Configure(configureEmail);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();
        services.TryAddSingleton<IEmailSender>(sp =>
        {
            var o = sp.GetRequiredService<IOptions<EmailOptions>>().Value;
            return o.Mode.Equals("Smtp", StringComparison.OrdinalIgnoreCase)
                ? new SmtpEmailSender(o)
                : new DevEmailSender(o, sp.GetRequiredService<ILogger<DevEmailSender>>());
        });

        services.AddScoped<IPasswordService, PasswordService>();
        services.AddScoped<IAuditoriaService, AuditoriaService>();
        services.AddScoped<IAccesoService, AccesoService>();
        services.AddScoped<IPermisoService, PermisoService>();
        services.AddScoped<IEmpresaService, EmpresaService>();
        services.AddScoped<IPlataformaService, PlataformaService>();
        services.AddScoped<RolService>();
        services.AddScoped<IRolService>(sp => sp.GetRequiredService<RolService>());
        services.AddScoped<ICuentaService, CuentaService>();
        services.AddScoped<IUsuarioService, UsuarioService>();
        services.AddScoped<ISesionService, SesionService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IBootstrapService, BootstrapService>();
        services.AddScoped<IDepartamentoService, DepartamentoService>();
        services.AddScoped<IPuestoService, PuestoService>();
        services.AddScoped<ITrabajadorService, TrabajadorService>();
        services.AddScoped<ISupervisionService, SupervisionService>();
        services.AddScoped<IContactoEmergenciaService, ContactoEmergenciaService>();
        services.AddScoped<ISolicitudCambioPerfilService, SolicitudCambioPerfilService>();
        return services;
    }
}
