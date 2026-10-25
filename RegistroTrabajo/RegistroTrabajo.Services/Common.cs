using Microsoft.EntityFrameworkCore;
using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public enum ServiceErrorKind { Validation, Unauthorized, Forbidden, NotFound, Conflict }

public class ServiceException : Exception
{
    public ServiceErrorKind Kind { get; }

    public ServiceException(ServiceErrorKind kind, string message) : base(message) => Kind = kind;

    public static ServiceException Validation(string m) => new(ServiceErrorKind.Validation, m);
    public static ServiceException Unauthorized(string m) => new(ServiceErrorKind.Unauthorized, m);
    public static ServiceException Forbidden(string m) => new(ServiceErrorKind.Forbidden, m);
    public static ServiceException NotFound(string m) => new(ServiceErrorKind.NotFound, m);
    public static ServiceException Conflict(string m) => new(ServiceErrorKind.Conflict, m);
}

/// <summary>Identidad autenticada de la solicitud. Siempre derivada del servidor (claims validados), nunca del cuerpo del request.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid? UsuarioId { get; }
    Guid? EmpresaId { get; }
    Guid? SesionId { get; }
    string? IpAddress { get; }
    string? UserAgent { get; }
    Guid? CorrelationId { get; }
}

public static class CurrentUserExtensions
{
    public static Guid RequireEmpresaId(this ICurrentUser u) =>
        u.EmpresaId ?? throw ServiceException.Unauthorized("Autenticación requerida.");

    public static Guid RequireUsuarioId(this ICurrentUser u) =>
        u.UsuarioId ?? throw ServiceException.Unauthorized("Autenticación requerida.");
}

public class SecurityOptions
{
    public int MaxFailedAttempts { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
    /// <summary>Intervalo mínimo entre escrituras de UltimaActividadUtc.</summary>
    public int ActivityTouchSeconds { get; set; } = 60;
    /// <summary>Vigencia del enlace de recuperación de contraseña.</summary>
    public int RecoveryTokenMinutes { get; set; } = 30;
    /// <summary>Máximo de enlaces de recuperación que se generan por cuenta dentro de la ventana.</summary>
    public int RecoveryRequestsPerWindow { get; set; } = 3;
    /// <summary>Ventana (minutos) del límite de solicitudes de recuperación por cuenta.</summary>
    public int RecoveryRequestWindowMinutes { get; set; } = 15;
    /// <summary>Vigencia del enlace de confirmación de correo.</summary>
    public int ConfirmationTokenHours { get; set; } = 24;
    /// <summary>Si es verdadero, no se puede iniciar sesión sin haber confirmado el correo.</summary>
    public bool RequireConfirmedEmail { get; set; }
}

internal static class TokenHelper
{
    /// <summary>Token aleatorio de 32 bytes (URL-safe). Solo su hash se guarda en la base de datos.</summary>
    public static string Nuevo() =>
        Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    public static byte[] Hash(string token) =>
        System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
}

internal static class Helpers
{
    public static string NormalizarCorreo(string correo) => correo.Trim().ToUpperInvariant();

    public static string? Limpiar(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    public static async Task<PagedResult<T>> ToPagedAsync<T>(this IQueryable<T> q, PageQuery p, CancellationToken ct)
    {
        var page = Math.Max(1, p.Page);
        var size = Math.Clamp(p.PageSize, 1, 200);
        var total = await q.CountAsync(ct);
        var items = await q.Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new PagedResult<T> { Items = items, Page = page, PageSize = size, Total = total };
    }

    /// <summary>Guarda cambios traduciendo errores de BD a ServiceException sin filtrar detalles internos.</summary>
    public static async Task SaveSafeAsync(this DbContext db, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw ServiceException.Conflict("El registro fue modificado por otra operación. Intente de nuevo.");
        }
        catch (DbUpdateException)
        {
            throw ServiceException.Conflict("La operación viola una restricción de datos (duplicado o relación inválida).");
        }
    }
}
