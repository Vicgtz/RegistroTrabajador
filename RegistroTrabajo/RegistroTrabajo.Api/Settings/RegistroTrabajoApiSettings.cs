using Microsoft.Data.SqlClient;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Api.Settings;

public class RegistroTrabajoApiSettings
{
    public const string SectionName = "RegistroTrabajoApiSettings";

    public DbSettings DbSettings { get; set; } = new();
    public JwtSettings JwtSettings { get; set; } = new();
    public SecurityOptions Security { get; set; } = new();
    public EmailOptions Email { get; set; } = new();
    public RespaldosOptions Respaldos { get; set; } = new();
    public string AllowedHosts { get; set; } = "*";
    /// <summary>Orígenes (clientes Blazor) autorizados por CORS.</summary>
    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
    /// <summary>Redirige HTTP a HTTPS (fuera de Development). Desactívelo solo en un servidor de pruebas que sirva únicamente HTTP.</summary>
    public bool RedirigirAHttps { get; set; } = true;
}

public class DbSettings
{
    public string ServerName { get; set; } = "localhost";
    public string DatabaseName { get; set; } = null!;
    public bool UseIntegratedSecurity { get; set; }
    public string? UserId { get; set; }
    public string? Password { get; set; }
    public bool MultipleActiveResultSets { get; set; }
    public bool Encrypt { get; set; }
    public bool TrustServerCertificate { get; set; }

    public string BuildConnectionString()
    {
        var b = new SqlConnectionStringBuilder
        {
            DataSource = ServerName,
            InitialCatalog = DatabaseName,
            IntegratedSecurity = UseIntegratedSecurity,
            MultipleActiveResultSets = MultipleActiveResultSets,
            Encrypt = Encrypt,
            TrustServerCertificate = TrustServerCertificate
        };
        if (!UseIntegratedSecurity)
        {
            b.UserID = UserId;
            b.Password = Password;
        }
        return b.ConnectionString;
    }
}

public class JwtSettings
{
    public string Issuer { get; set; } = null!;
    public string Audience { get; set; } = null!;
    public string SecretKey { get; set; } = null!;
    public int TokenExpiryMinutes { get; set; }
}


