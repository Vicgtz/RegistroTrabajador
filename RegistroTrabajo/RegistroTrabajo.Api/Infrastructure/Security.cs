using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using RegistroTrabajo.Api.Settings;
using RegistroTrabajo.Data.Entities;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Api.Infrastructure;

public static class ClaimNames
{
    public const string UsuarioId = "sub";
    public const string EmpresaId = "empresa_id";
    public const string SesionId = "sid";
}

/// <summary>Identidad tomada exclusivamente de los claims del JWT ya validado (y de la sesión revalidada en BD).</summary>
public class HttpCurrentUser : ICurrentUser
{
    public const string CorrelationItem = "CorrelationId";
    private readonly IHttpContextAccessor _accessor;

    public HttpCurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    private HttpContext? Ctx => _accessor.HttpContext;
    public bool IsAuthenticated => Ctx?.User.Identity?.IsAuthenticated == true && UsuarioId is not null && EmpresaId is not null;
    public Guid? UsuarioId => Claim(ClaimNames.UsuarioId);
    public Guid? EmpresaId => Claim(ClaimNames.EmpresaId);
    public Guid? SesionId => Claim(ClaimNames.SesionId);
    public string? IpAddress => Ctx?.Connection.RemoteIpAddress?.ToString();
    public string? UserAgent => Ctx?.Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null;
    public Guid? CorrelationId => Ctx?.Items[CorrelationItem] as Guid?;

    private Guid? Claim(string type) =>
        Guid.TryParse(Ctx?.User.FindFirstValue(type), out var g) ? g : null;
}

public class JwtAccessTokenFactory : IAccessTokenFactory
{
    private readonly JwtSettings _jwt;
    private readonly TimeProvider _clock;

    public JwtAccessTokenFactory(RegistroTrabajoApiSettings settings, TimeProvider clock)
    {
        _jwt = settings.JwtSettings; _clock = clock;
    }

    public (string Token, DateTime ExpiraUtc) Crear(Usuario usuario, Guid sesionId, DateTime maxExpiraUtc)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var exp = now.AddMinutes(_jwt.TokenExpiryMinutes);
        if (exp > maxExpiraUtc) exp = maxExpiraUtc;

        var claims = new[]
        {
            new Claim(ClaimNames.UsuarioId, usuario.UsuarioId.ToString()),
            new Claim(ClaimNames.EmpresaId, usuario.EmpresaId.ToString()),
            new Claim(ClaimNames.SesionId, sesionId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        var creds = new SigningCredentials(Key(_jwt.SecretKey), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(_jwt.Issuer, _jwt.Audience, claims, now, exp, creds);
        return (new JwtSecurityTokenHandler().WriteToken(token), exp);
    }

    public static SymmetricSecurityKey Key(string secret) => new(Encoding.UTF8.GetBytes(secret));
}

// ---------------- Autorización por permiso del sistema ----------------
public class PermisoRequirement : IAuthorizationRequirement
{
    public string Codigo { get; }
    public PermisoRequirement(string codigo) => Codigo = codigo;
}

/// <summary>Exige un permiso del catálogo; se evalúa en el servidor contra la BD en cada solicitud.</summary>
public class RequierePermisoAttribute : AuthorizeAttribute, IAuthorizationRequirementData
{
    public string Codigo { get; }
    public RequierePermisoAttribute(string codigo) => Codigo = codigo;
    public IEnumerable<IAuthorizationRequirement> GetRequirements() { yield return new PermisoRequirement(Codigo); }
}

public class PermisoAuthorizationHandler : AuthorizationHandler<PermisoRequirement>
{
    private readonly IPermisoService _permisos;
    public PermisoAuthorizationHandler(IPermisoService permisos) => _permisos = permisos;

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermisoRequirement requirement)
    {
        var ct = (context.Resource as HttpContext)?.RequestAborted ?? CancellationToken.None;
        if (await _permisos.TienePermisoAsync(requirement.Codigo, ct)) context.Succeed(requirement);
    }
}
