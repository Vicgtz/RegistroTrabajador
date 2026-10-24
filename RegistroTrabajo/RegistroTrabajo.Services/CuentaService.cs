using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RegistroTrabajo.Data;
using RegistroTrabajo.Data.Entities;
using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

/// <summary>Recuperación de contraseña y confirmación de correo (enlaces de un solo uso; solo se guarda el hash del token).</summary>
public interface ICuentaService
{
    /// <summary>Siempre responde igual, exista o no la cuenta.</summary>
    Task SolicitarRecuperacionAsync(string correo, CancellationToken ct);
    Task RestablecerContrasenaAsync(string token, string contrasenaNueva, CancellationToken ct);
    Task ConfirmarCorreoAsync(string token, CancellationToken ct);
    /// <summary>Siempre responde igual, exista o no la cuenta.</summary>
    Task ReenviarConfirmacionAsync(string correo, CancellationToken ct);
    /// <summary>Genera y envía el enlace de confirmación para un usuario recién creado (los fallos de envío no interrumpen la operación).</summary>
    Task EnviarConfirmacionAsync(Usuario usuario, CancellationToken ct);
}

public class CuentaService : ICuentaService
{
    private const string EnlaceInvalido = "El enlace no es válido o ya expiró. Solicita uno nuevo.";

    private readonly RegistroTrabajoDbContext _db;
    private readonly IPasswordService _passwords;
    private readonly IAuditoriaService _auditoria;
    private readonly IEmailSender _email;
    private readonly EmailOptions _emailOptions;
    private readonly SecurityOptions _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<CuentaService> _logger;

    public CuentaService(RegistroTrabajoDbContext db, IPasswordService passwords, IAuditoriaService auditoria,
        IEmailSender email, IOptions<EmailOptions> emailOptions, IOptions<SecurityOptions> options,
        TimeProvider clock, ILogger<CuentaService> logger)
    {
        _db = db; _passwords = passwords; _auditoria = auditoria; _email = email;
        _emailOptions = emailOptions.Value; _options = options.Value; _clock = clock; _logger = logger;
    }

    // ---------------- Recuperación de contraseña ----------------
    public async Task SolicitarRecuperacionAsync(string correo, CancellationToken ct)
    {
        var u = await BuscarActivoAsync(correo, ct);
        if (u is null) return;

        var now = _clock.GetUtcNow().UtcDateTime;
        var previos = await _db.TokensRecuperacionContrasena
            .Where(t => t.UsuarioId == u.UsuarioId && t.UsadoUtc == null && t.RevocadoUtc == null).ToListAsync(ct);
        foreach (var p in previos) p.RevocadoUtc = now;

        var token = TokenHelper.Nuevo();
        _db.TokensRecuperacionContrasena.Add(new TokenRecuperacionContrasena
        {
            TokenRecuperacionId = Guid.NewGuid(), EmpresaId = u.EmpresaId, UsuarioId = u.UsuarioId,
            TokenHash = TokenHelper.Hash(token), CreadoUtc = now, ExpiraUtc = now.AddMinutes(_options.RecoveryTokenMinutes)
        });
        _auditoria.Registrar("Usuarios", nameof(Usuario), u.UsuarioId.ToString(), "SolicitarRecuperacion",
            empresaId: u.EmpresaId, usuarioId: u.UsuarioId);
        await _db.SaveSafeAsync(ct);

        var (asunto, cuerpo) = PlantillasCorreo.Recuperacion(_emailOptions, token, _options.RecoveryTokenMinutes);
        await EnviarSinFallarAsync(u.Correo, asunto, cuerpo, ct);
    }

    public async Task RestablecerContrasenaAsync(string token, string contrasenaNueva, CancellationToken ct)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var hash = TokenHelper.Hash(token);
        var t = await _db.TokensRecuperacionContrasena.Include(x => x.Usuario).ThenInclude(x => x.Empresa)
            .FirstOrDefaultAsync(x => x.TokenHash == hash, ct);

        if (t is null || t.UsadoUtc is not null || t.RevocadoUtc is not null || t.ExpiraUtc <= now
            || t.Usuario.Estado != EstadoUsuario.Activo || t.Usuario.Empresa.Estado != EstadoEmpresa.Activa)
            throw ServiceException.Validation(EnlaceInvalido);

        _passwords.ValidarPolitica(contrasenaNueva);

        var u = t.Usuario;
        u.HashContrasena = _passwords.Hash(u, contrasenaNueva);
        u.DebeCambiarContrasena = false;
        u.IntentosFallidos = 0;
        u.BloqueadoHastaUtc = null;
        u.FechaActualizacionUtc = now;
        t.UsadoUtc = now;
        foreach (var otro in await _db.TokensRecuperacionContrasena
                     .Where(x => x.UsuarioId == u.UsuarioId && x.UsadoUtc == null && x.RevocadoUtc == null && x.TokenRecuperacionId != t.TokenRecuperacionId)
                     .ToListAsync(ct))
            otro.RevocadoUtc = now;

        // Quien tenga la cuenta abierta debe volver a autenticarse con la contraseña nueva.
        await SesionService.RevocarActivasAsync(_db, u.EmpresaId, u.UsuarioId, "Restablecimiento de contraseña", now, ct);
        _auditoria.Registrar("Usuarios", nameof(Usuario), u.UsuarioId.ToString(), "RestablecerContrasenaConEnlace",
            empresaId: u.EmpresaId, usuarioId: u.UsuarioId);
        await _db.SaveSafeAsync(ct);
    }

    // ---------------- Confirmación de correo ----------------
    public async Task ConfirmarCorreoAsync(string token, CancellationToken ct)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var hash = TokenHelper.Hash(token);
        var t = await _db.TokensConfirmacionCorreo.Include(x => x.Usuario)
            .FirstOrDefaultAsync(x => x.TokenHash == hash, ct);

        if (t is null || t.UsadoUtc is not null || t.RevocadoUtc is not null || t.ExpiraUtc <= now)
            throw ServiceException.Validation(EnlaceInvalido);

        t.UsadoUtc = now;
        t.Usuario.CorreoConfirmadoUtc ??= now;
        t.Usuario.FechaActualizacionUtc = now;
        _auditoria.Registrar("Usuarios", nameof(Usuario), t.UsuarioId.ToString(), "ConfirmarCorreo",
            empresaId: t.EmpresaId, usuarioId: t.UsuarioId);
        await _db.SaveSafeAsync(ct);
    }

    public async Task ReenviarConfirmacionAsync(string correo, CancellationToken ct)
    {
        var u = await BuscarActivoAsync(correo, ct);
        if (u is null || u.CorreoConfirmadoUtc is not null) return;
        await EnviarConfirmacionAsync(u, ct);
    }

    public async Task EnviarConfirmacionAsync(Usuario usuario, CancellationToken ct)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var previos = await _db.TokensConfirmacionCorreo
            .Where(t => t.UsuarioId == usuario.UsuarioId && t.UsadoUtc == null && t.RevocadoUtc == null).ToListAsync(ct);
        foreach (var p in previos) p.RevocadoUtc = now;

        var token = TokenHelper.Nuevo();
        _db.TokensConfirmacionCorreo.Add(new TokenConfirmacionCorreo
        {
            TokenConfirmacionId = Guid.NewGuid(), EmpresaId = usuario.EmpresaId, UsuarioId = usuario.UsuarioId,
            TokenHash = TokenHelper.Hash(token), CreadoUtc = now, ExpiraUtc = now.AddHours(_options.ConfirmationTokenHours)
        });
        await _db.SaveSafeAsync(ct);

        var (asunto, cuerpo) = PlantillasCorreo.Confirmacion(_emailOptions, token, _options.ConfirmationTokenHours);
        await EnviarSinFallarAsync(usuario.Correo, asunto, cuerpo, ct);
    }

    private async Task<Usuario?> BuscarActivoAsync(string correo, CancellationToken ct)
    {
        var normalizado = Helpers.NormalizarCorreo(correo);
        return await _db.Usuarios.FirstOrDefaultAsync(u => u.CorreoNormalizado == normalizado
            && u.Estado == EstadoUsuario.Activo && u.Empresa.Estado == EstadoEmpresa.Activa, ct);
    }

    // Un fallo del servidor de correo no debe cambiar la respuesta pública ni revelar si la cuenta existe.
    private async Task EnviarSinFallarAsync(string para, string asunto, string cuerpo, CancellationToken ct)
    {
        try
        {
            await _email.EnviarAsync(para, asunto, cuerpo, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "No se pudo enviar el correo '{Asunto}'.", asunto);
        }
    }
}
