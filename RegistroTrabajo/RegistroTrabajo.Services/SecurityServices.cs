using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RegistroTrabajo.Data;
using RegistroTrabajo.Data.Entities;
using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

// ---------------- Contraseñas ----------------
public interface IPasswordService
{
    string Hash(Usuario usuario, string contrasena);
    PasswordVerificationResult Verify(Usuario usuario, string hash, string contrasena);
    void ValidarPolitica(string contrasena);
}

public class PasswordService : IPasswordService
{
    private readonly IPasswordHasher<Usuario> _hasher;
    public PasswordService(IPasswordHasher<Usuario> hasher) => _hasher = hasher;

    public string Hash(Usuario usuario, string contrasena) => _hasher.HashPassword(usuario, contrasena);

    public PasswordVerificationResult Verify(Usuario usuario, string hash, string contrasena) =>
        _hasher.VerifyHashedPassword(usuario, hash, contrasena);

    public void ValidarPolitica(string contrasena)
    {
        if (string.IsNullOrEmpty(contrasena) || contrasena.Length < 10 || contrasena.Length > 128
            || !contrasena.Any(char.IsUpper) || !contrasena.Any(char.IsLower) || !contrasena.Any(char.IsDigit))
            throw ServiceException.Validation(
                "La contraseña debe tener entre 10 y 128 caracteres, con mayúsculas, minúsculas y números.");
    }
}

// ---------------- Auditoría (solo inserción) ----------------
public interface IAuditoriaService
{
    /// <summary>Agrega el registro al contexto; se persiste junto con el SaveChanges de la operación auditada.</summary>
    void Registrar(string modulo, string entidad, string? entidadId, string accion,
        object? antes = null, object? despues = null, Guid? empresaId = null, Guid? usuarioId = null);

    Task<PagedResult<AuditoriaDto>> ListarAsync(AuditoriaFiltro filtro, CancellationToken ct);
}

public class AuditoriaService : IAuditoriaService
{
    private static readonly Regex Sensible = new(
        "contrasena|contraseña|password|hash|token|secret|clave|rowversion", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly RegistroTrabajoDbContext _db;
    private readonly ICurrentUser _current;
    private readonly TimeProvider _clock;

    public AuditoriaService(RegistroTrabajoDbContext db, ICurrentUser current, TimeProvider clock)
    {
        _db = db; _current = current; _clock = clock;
    }

    public void Registrar(string modulo, string entidad, string? entidadId, string accion,
        object? antes = null, object? despues = null, Guid? empresaId = null, Guid? usuarioId = null)
    {
        _db.Auditoria.Add(new Auditoria
        {
            EmpresaId = empresaId ?? _current.EmpresaId,
            UsuarioId = usuarioId ?? _current.UsuarioId,
            FechaUtc = _clock.GetUtcNow().UtcDateTime,
            Modulo = modulo,
            Entidad = entidad,
            EntidadId = entidadId,
            Accion = accion,
            DatosAntesJson = ToSafeJson(antes),
            DatosDespuesJson = ToSafeJson(despues),
            DireccionIp = _current.IpAddress,
            CorrelationId = _current.CorrelationId
        });
    }

    public async Task<PagedResult<AuditoriaDto>> ListarAsync(AuditoriaFiltro f, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var q = _db.Auditoria.AsNoTracking().Where(a => a.EmpresaId == empresaId);
        if (f.Desde is { } d) q = q.Where(a => a.FechaUtc >= d);
        if (f.Hasta is { } h) q = q.Where(a => a.FechaUtc <= h);
        if (!string.IsNullOrWhiteSpace(f.Entidad)) q = q.Where(a => a.Entidad == f.Entidad);
        if (!string.IsNullOrWhiteSpace(f.EntidadId)) q = q.Where(a => a.EntidadId == f.EntidadId);
        return await q.OrderByDescending(a => a.AuditoriaId).Select(a => new AuditoriaDto
        {
            AuditoriaId = a.AuditoriaId, UsuarioId = a.UsuarioId, FechaUtc = a.FechaUtc, Modulo = a.Modulo,
            Entidad = a.Entidad, EntidadId = a.EntidadId, Accion = a.Accion, DatosAntesJson = a.DatosAntesJson,
            DatosDespuesJson = a.DatosDespuesJson, DireccionIp = a.DireccionIp, CorrelationId = a.CorrelationId
        }).ToPagedAsync(f, ct);
    }

    internal static string? ToSafeJson(object? value)
    {
        if (value is null) return null;
        var node = JsonSerializer.SerializeToNode(value);
        Redactar(node);
        return node?.ToJsonString();
    }

    private static void Redactar(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Select(p => p.Key).ToList())
                {
                    if (Sensible.IsMatch(key)) obj[key] = "[REDACTADO]";
                    else Redactar(obj[key]);
                }
                break;
            case JsonArray arr:
                foreach (var item in arr) Redactar(item);
                break;
        }
    }
}

// ---------------- Historial de acceso ----------------
public interface IAccesoService
{
    /// <summary>Registra y persiste de inmediato (también cuando la operación principal falla).</summary>
    Task RegistrarAsync(byte resultado, string? correoIntentado, Guid? empresaId, Guid? usuarioId,
        string? detalle, CancellationToken ct);

    Task<PagedResult<HistorialAccesoDto>> ListarAsync(FechaFiltro filtro, CancellationToken ct);
}

public class AccesoService : IAccesoService
{
    private readonly RegistroTrabajoDbContext _db;
    private readonly ICurrentUser _current;
    private readonly TimeProvider _clock;

    public AccesoService(RegistroTrabajoDbContext db, ICurrentUser current, TimeProvider clock)
    {
        _db = db; _current = current; _clock = clock;
    }

    public async Task RegistrarAsync(byte resultado, string? correoIntentado, Guid? empresaId, Guid? usuarioId,
        string? detalle, CancellationToken ct)
    {
        _db.HistorialAcceso.Add(new HistorialAcceso
        {
            EmpresaId = empresaId,
            UsuarioId = usuarioId,
            CorreoIntentado = Recortar(correoIntentado, 254),
            FechaUtc = _clock.GetUtcNow().UtcDateTime,
            Resultado = resultado,
            DireccionIp = Recortar(_current.IpAddress, 45),
            UserAgent = Recortar(_current.UserAgent, 500),
            Detalle = Recortar(detalle, 300)
        });
        await _db.SaveSafeAsync(ct);
    }

    public async Task<PagedResult<HistorialAccesoDto>> ListarAsync(FechaFiltro f, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var q = _db.HistorialAcceso.AsNoTracking().Where(a => a.EmpresaId == empresaId);
        if (f.Desde is { } d) q = q.Where(a => a.FechaUtc >= d);
        if (f.Hasta is { } h) q = q.Where(a => a.FechaUtc <= h);
        return await q.OrderByDescending(a => a.HistorialAccesoId).Select(a => new HistorialAccesoDto
        {
            HistorialAccesoId = a.HistorialAccesoId, UsuarioId = a.UsuarioId, CorreoIntentado = a.CorreoIntentado,
            FechaUtc = a.FechaUtc, Resultado = a.Resultado, DireccionIp = a.DireccionIp,
            UserAgent = a.UserAgent, Detalle = a.Detalle
        }).ToPagedAsync(f, ct);
    }

    private static string? Recortar(string? s, int max) =>
        s is null ? null : (s.Length <= max ? s : s[..max]);
}

// ---------------- Permisos del sistema ----------------
public interface IPermisoService
{
    Task<IReadOnlySet<string>> ObtenerPermisosAsync(Guid empresaId, Guid usuarioId, CancellationToken ct);
    Task<bool> TienePermisoAsync(string codigo, CancellationToken ct);
    Task AsegurarPermisoAsync(string codigo, CancellationToken ct);
}

public class PermisoService : IPermisoService
{
    private readonly RegistroTrabajoDbContext _db;
    private readonly ICurrentUser _current;

    public PermisoService(RegistroTrabajoDbContext db, ICurrentUser current)
    {
        _db = db; _current = current;
    }

    public async Task<IReadOnlySet<string>> ObtenerPermisosAsync(Guid empresaId, Guid usuarioId, CancellationToken ct)
    {
        var cuenta = await _db.Usuarios.Where(u => u.UsuarioId == usuarioId && u.EmpresaId == empresaId
                && u.Estado == EstadoUsuario.Activo
                && u.Empresa.Estado == EstadoEmpresa.Activa)
            .Select(u => new { u.Empresa.EsPlataforma }).FirstOrDefaultAsync(ct);
        if (cuenta is null) return new HashSet<string>();

        var codigos = await _db.UsuarioRoles
            .Where(ur => ur.EmpresaId == empresaId && ur.UsuarioId == usuarioId && ur.Rol.Estado)
            .SelectMany(ur => ur.Rol.RolPermisos.Where(rp => rp.Permitido && rp.EmpresaId == empresaId)
                .Select(rp => rp.Permiso.Codigo))
            .Distinct()
            .ToListAsync(ct);

        // Defensa adicional: los permisos de plataforma solo cuentan para usuarios de la empresa de plataforma.
        if (!cuenta.EsPlataforma) codigos.RemoveAll(c => c.StartsWith("plataforma.", StringComparison.Ordinal));
        return codigos.ToHashSet(StringComparer.Ordinal);
    }

    public async Task<bool> TienePermisoAsync(string codigo, CancellationToken ct)
    {
        if (_current is not { IsAuthenticated: true, EmpresaId: { } e, UsuarioId: { } u }) return false;
        return (await ObtenerPermisosAsync(e, u, ct)).Contains(codigo);
    }

    public async Task AsegurarPermisoAsync(string codigo, CancellationToken ct)
    {
        if (!_current.IsAuthenticated) throw ServiceException.Unauthorized("Autenticación requerida.");
        if (!await TienePermisoAsync(codigo, ct)) throw ServiceException.Forbidden("No tiene permiso para esta operación.");
    }
}

