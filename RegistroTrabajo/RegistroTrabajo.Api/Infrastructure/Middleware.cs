using Microsoft.AspNetCore.Mvc;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Api.Infrastructure;

public class CorrelationIdMiddleware
{
    public const string Header = "X-Correlation-Id";
    private readonly RequestDelegate _next;
    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public Task InvokeAsync(HttpContext ctx)
    {
        var id = Guid.TryParse(ctx.Request.Headers[Header].FirstOrDefault(), out var g) ? g : Guid.NewGuid();
        ctx.Items[HttpCurrentUser.CorrelationItem] = id;
        ctx.Response.Headers[Header] = id.ToString();
        return _next(ctx);
    }
}

/// <summary>Traduce ServiceException a ProblemDetails; cualquier otra excepción devuelve 500 genérico sin detalles.</summary>
public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next; _logger = logger;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (OperationCanceledException) when (ctx.RequestAborted.IsCancellationRequested)
        {
            ctx.Response.StatusCode = 499;
        }
        catch (ServiceException ex)
        {
            var status = ex.Kind switch
            {
                ServiceErrorKind.Validation => StatusCodes.Status400BadRequest,
                ServiceErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
                ServiceErrorKind.Forbidden => StatusCodes.Status403Forbidden,
                ServiceErrorKind.NotFound => StatusCodes.Status404NotFound,
                _ => StatusCodes.Status409Conflict
            };
            await Write(ctx, status, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error no controlado. CorrelationId={CorrelationId}", ctx.Items[HttpCurrentUser.CorrelationItem]);
            await RegistrarErrorTecnicoAsync(ctx, ex);
            await Write(ctx, StatusCodes.Status500InternalServerError, "Ocurrió un error interno.");
        }
    }

    /// <summary>
    /// Guarda el error técnico con su correlación (Bloque 10). Usa un alcance propio, porque el contexto de la solicitud pudo quedar en
    /// mal estado, y nunca propaga fallos: registrar el error no debe cambiar la respuesta genérica al usuario.
    /// </summary>
    private async Task RegistrarErrorTecnicoAsync(HttpContext ctx, Exception ex)
    {
        try
        {
            Guid? empresaId = null, usuarioId = null;
            try
            {
                var actual = ctx.RequestServices.GetService<ICurrentUser>();
                empresaId = actual?.EmpresaId; usuarioId = actual?.UsuarioId;
            }
            catch (Exception) { /* sin identidad: se registra sin empresa ni usuario */ }

            using var alcance = ctx.RequestServices.GetRequiredService<IServiceScopeFactory>().CreateScope();
            await alcance.ServiceProvider.GetRequiredService<IErrorTecnicoService>().RegistrarAsync(
                ex, ctx.Request.Method, ctx.Request.Path.Value ?? "", ctx.Items[HttpCurrentUser.CorrelationItem] as Guid?, empresaId, usuarioId, CancellationToken.None);
        }
        catch (Exception inner)
        {
            _logger.LogWarning(inner, "No se pudo registrar el error técnico en la base de datos.");
        }
    }

    private static Task Write(HttpContext ctx, int status, string detail)
    {
        if (ctx.Response.HasStarted) return Task.CompletedTask;
        ctx.Response.StatusCode = status;
        return ctx.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = ReasonPhrase(status),
            Detail = detail,
            Extensions = { ["correlationId"] = ctx.Items[HttpCurrentUser.CorrelationItem]?.ToString() }
        });
    }

    private static string ReasonPhrase(int status) => status switch
    {
        400 => "Solicitud no válida", 401 => "No autenticado", 403 => "Acceso denegado",
        404 => "No encontrado", 409 => "Conflicto", _ => "Error interno"
    };
}


/// <summary>
/// Si la cuenta tiene DebeCambiarContrasena activo, la API solo permite consultar el perfil, cambiar la contraseña y cerrar sesión.
/// (Antes solo lo forzaba la interfaz.)
/// </summary>
public class ForzarCambioContrasenaMiddleware
{
    public const string ItemKey = "DebeCambiarContrasena";
    private static readonly string[] Permitidas = { "/api/auth/me", "/api/auth/cambiar-contrasena", "/api/auth/logout" };
    private readonly RequestDelegate _next;
    public ForzarCambioContrasenaMiddleware(RequestDelegate next) => _next = next;

    public Task InvokeAsync(HttpContext ctx)
    {
        var debeCambiar = ctx.Items[ItemKey] as bool? == true;
        if (!debeCambiar || Permitidas.Any(p => ctx.Request.Path.Equals(p, StringComparison.OrdinalIgnoreCase)))
            return _next(ctx);

        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        return ctx.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Acceso denegado",
            Detail = "Debes cambiar tu contraseña antes de continuar.",
            Extensions = { ["codigo"] = "cambio_contrasena_requerido", ["correlationId"] = ctx.Items[HttpCurrentUser.CorrelationItem]?.ToString() }
        });
    }
}