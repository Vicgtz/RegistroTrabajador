using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RegistroTrabajo.Data;
using RegistroTrabajo.Data.Entities;
using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public record SesionValidada(bool Valida, bool DebeCambiarContrasena);

public interface ISesionService
{
    Task<PagedResult<SesionDto>> ListarActivasAsync(PageQuery query, CancellationToken ct);
    Task<PagedResult<SesionDto>> ListarMiasAsync(PageQuery query, CancellationToken ct);
    Task RevocarAsync(Guid sesionId, CancellationToken ct);
    /// <summary>Revoca una sesión del propio usuario (otro dispositivo o la actual).</summary>
    Task RevocarMiaAsync(Guid sesionId, CancellationToken ct);
    Task<int> RevocarTodasDeUsuarioAsync(Guid usuarioId, CancellationToken ct);
    /// <summary>Valida en cada solicitud que la sesión del JWT siga vigente y que cuenta y empresa estén activas.</summary>
    Task<bool> ValidarSesionAsync(Guid sesionId, Guid usuarioId, Guid empresaId, CancellationToken ct);
    Task<SesionValidada> ValidarSesionDetalladaAsync(Guid sesionId, Guid usuarioId, Guid empresaId, CancellationToken ct);
}

public class SesionService : ISesionService
{
    private readonly RegistroTrabajoDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IAuditoriaService _auditoria;
    private readonly TimeProvider _clock;
    private readonly SecurityOptions _options;

    public SesionService(RegistroTrabajoDbContext db, ICurrentUser current, IAuditoriaService auditoria,
        TimeProvider clock, IOptions<SecurityOptions> options)
    {
        _db = db; _current = current; _auditoria = auditoria; _clock = clock; _options = options.Value;
    }

    public async Task<PagedResult<SesionDto>> ListarActivasAsync(PageQuery query, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        return await Activas(_db.SesionesUsuario.AsNoTracking().Where(s => s.EmpresaId == empresaId)).ToPagedAsync(query, ct);
    }

    public async Task<PagedResult<SesionDto>> ListarMiasAsync(PageQuery query, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var usuarioId = _current.RequireUsuarioId();
        return await Activas(_db.SesionesUsuario.AsNoTracking().Where(s => s.EmpresaId == empresaId && s.UsuarioId == usuarioId)).ToPagedAsync(query, ct);
    }

    private IQueryable<SesionDto> Activas(IQueryable<SesionUsuario> q)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var actual = _current.SesionId;
        return q.Where(s => s.RevocadaUtc == null && s.ExpiraUtc > now)
            .OrderByDescending(s => s.UltimaActividadUtc)
            .Select(s => new SesionDto
            {
                SesionId = s.SesionId, UsuarioId = s.UsuarioId, UsuarioCorreo = s.Usuario.Correo, EsActual = s.SesionId == actual,
                Dispositivo = s.Dispositivo, UserAgent = s.UserAgent, DireccionIp = s.DireccionIp,
                FechaCreacionUtc = s.FechaCreacionUtc, UltimaActividadUtc = s.UltimaActividadUtc,
                ExpiraUtc = s.ExpiraUtc, RevocadaUtc = s.RevocadaUtc
            });
    }

    public async Task RevocarAsync(Guid sesionId, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var s = await _db.SesionesUsuario.FirstOrDefaultAsync(x => x.SesionId == sesionId && x.EmpresaId == empresaId, ct)
            ?? throw ServiceException.NotFound("Sesión no encontrada.");
        await RevocarYAuditarAsync(s, "Revocada por un administrador", "Revocar", ct);
    }

    public async Task RevocarMiaAsync(Guid sesionId, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var usuarioId = _current.RequireUsuarioId();
        var s = await _db.SesionesUsuario.FirstOrDefaultAsync(x => x.SesionId == sesionId && x.EmpresaId == empresaId && x.UsuarioId == usuarioId, ct)
            ?? throw ServiceException.NotFound("Sesión no encontrada.");
        await RevocarYAuditarAsync(s, "Cerrada por el propio usuario", "RevocarPropia", ct);
    }

    private async Task RevocarYAuditarAsync(SesionUsuario s, string motivo, string accion, CancellationToken ct)
    {
        if (s.RevocadaUtc is not null) return;
        Revocar(s, motivo, _clock.GetUtcNow().UtcDateTime);
        _auditoria.Registrar("Sesiones", nameof(SesionUsuario), s.SesionId.ToString(), accion, null, new { s.UsuarioId });
        await _db.SaveSafeAsync(ct);
    }

    public async Task<int> RevocarTodasDeUsuarioAsync(Guid usuarioId, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        if (!await _db.Usuarios.AnyAsync(u => u.UsuarioId == usuarioId && u.EmpresaId == empresaId, ct))
            throw ServiceException.NotFound("Usuario no encontrado.");
        var n = await RevocarActivasAsync(_db, empresaId, usuarioId, "Revocadas por un administrador",
            _clock.GetUtcNow().UtcDateTime, ct);
        _auditoria.Registrar("Sesiones", nameof(SesionUsuario), usuarioId.ToString(), "RevocarTodas", null, new { revocadas = n });
        await _db.SaveSafeAsync(ct);
        return n;
    }

    public async Task<bool> ValidarSesionAsync(Guid sesionId, Guid usuarioId, Guid empresaId, CancellationToken ct) =>
        (await ValidarSesionDetalladaAsync(sesionId, usuarioId, empresaId, ct)).Valida;

    public async Task<SesionValidada> ValidarSesionDetalladaAsync(Guid sesionId, Guid usuarioId, Guid empresaId, CancellationToken ct)
    {
        var invalida = new SesionValidada(false, false);
        var now = _clock.GetUtcNow().UtcDateTime;
        var s = await _db.SesionesUsuario.FirstOrDefaultAsync(x => x.SesionId == sesionId && x.UsuarioId == usuarioId && x.EmpresaId == empresaId, ct);
        if (s is null || s.RevocadaUtc is not null || s.ExpiraUtc <= now) return invalida;

        var cuenta = await _db.Usuarios.Where(u => u.UsuarioId == usuarioId && u.EmpresaId == empresaId
                && u.Estado == EstadoUsuario.Activo && u.Empresa.Estado == EstadoEmpresa.Activa)
            .Select(u => new { u.DebeCambiarContrasena }).FirstOrDefaultAsync(ct);
        if (cuenta is null) return invalida;

        var inactividad = await _db.ConfiguracionEmpresa.Where(c => c.EmpresaId == empresaId)
            .Select(c => (int?)c.CierreSesionInactividadMinutos).FirstOrDefaultAsync(ct);
        if (inactividad is { } min && now - s.UltimaActividadUtc > TimeSpan.FromMinutes(min))
        {
            Revocar(s, "Inactividad", now);
            await _db.SaveSafeAsync(ct);
            return invalida;
        }

        if (now - s.UltimaActividadUtc >= TimeSpan.FromSeconds(_options.ActivityTouchSeconds))
        {
            s.UltimaActividadUtc = now;
            await _db.SaveSafeAsync(ct);
        }
        return new SesionValidada(true, cuenta.DebeCambiarContrasena);
    }

    internal static void Revocar(SesionUsuario s, string motivo, DateTime now)
    {
        s.RevocadaUtc = now;
        s.MotivoRevocacion = motivo;
    }

    internal static async Task<int> RevocarActivasAsync(RegistroTrabajoDbContext db, Guid empresaId, Guid usuarioId,
        string motivo, DateTime now, CancellationToken ct, Guid? excepto = null)
    {
        var activas = await db.SesionesUsuario
            .Where(s => s.EmpresaId == empresaId && s.UsuarioId == usuarioId && s.RevocadaUtc == null && s.SesionId != excepto)
            .ToListAsync(ct);
        foreach (var s in activas) Revocar(s, motivo, now);
        return activas.Count;
    }
}
