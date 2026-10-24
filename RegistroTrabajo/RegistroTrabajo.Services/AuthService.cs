using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RegistroTrabajo.Data;
using RegistroTrabajo.Data.Entities;
using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

/// <summary>Emite el JWT de acceso (implementado en la API, que posee la configuración de firma).</summary>
public interface IAccessTokenFactory
{
    (string Token, DateTime ExpiraUtc) Crear(Usuario usuario, Guid sesionId, DateTime maxExpiraUtc);
}

public interface IAuthService
{
    Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken ct);
    Task<TokenResponse> RefreshAsync(RefreshRequest request, CancellationToken ct);
    Task LogoutAsync(CancellationToken ct);
    Task CambiarContrasenaAsync(CambiarContrasenaRequest request, CancellationToken ct);
    Task<PerfilDto> ObtenerPerfilAsync(CancellationToken ct);
}

public class AuthService : IAuthService
{
    // Mensaje único: no revela si la cuenta existe, está bloqueada o inactiva.
    private const string CredencialesInvalidas = "Credenciales inválidas.";

    private readonly RegistroTrabajoDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IPasswordService _passwords;
    private readonly IAccessTokenFactory _tokens;
    private readonly IAccesoService _accesos;
    private readonly IAuditoriaService _auditoria;
    private readonly TimeProvider _clock;
    private readonly SecurityOptions _options;
    private readonly IPermisoService _permisos;

    public AuthService(RegistroTrabajoDbContext db, ICurrentUser current, IPasswordService passwords,
        IAccessTokenFactory tokens, IAccesoService accesos, IAuditoriaService auditoria, TimeProvider clock,
        IOptions<SecurityOptions> options, IPermisoService permisos)
    {
        _db = db; _current = current; _passwords = passwords; _tokens = tokens; _accesos = accesos;
        _auditoria = auditoria; _clock = clock; _options = options.Value; _permisos = permisos;
    }

    public async Task<TokenResponse> LoginAsync(LoginRequest r, CancellationToken ct)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var normalizado = Helpers.NormalizarCorreo(r.Correo);
        var u = await _db.Usuarios.Include(x => x.Empresa).FirstOrDefaultAsync(x => x.CorreoNormalizado == normalizado, ct);

        if (u is null)
        {
            // Igualar el tiempo de respuesta con el de una cuenta existente.
            _passwords.Verify(new Usuario(), DummyHash.Value, r.Contrasena);
            await _accesos.RegistrarAsync(ResultadoAcceso.CredencialesInvalidas, r.Correo, null, null, "Cuenta no encontrada", ct);
            throw ServiceException.Unauthorized(CredencialesInvalidas);
        }

        if (u.BloqueadoHastaUtc is { } hasta && hasta > now)
        {
            await _accesos.RegistrarAsync(ResultadoAcceso.CuentaBloqueada, r.Correo, u.EmpresaId, u.UsuarioId, "Bloqueo temporal vigente", ct);
            throw ServiceException.Unauthorized(CredencialesInvalidas);
        }

        if (u.Estado != EstadoUsuario.Activo || u.Empresa.Estado != EstadoEmpresa.Activa)
        {
            await _accesos.RegistrarAsync(ResultadoAcceso.CuentaInactiva, r.Correo, u.EmpresaId, u.UsuarioId, "Cuenta o empresa inactiva", ct);
            throw ServiceException.Unauthorized(CredencialesInvalidas);
        }

        var verif = _passwords.Verify(u, u.HashContrasena, r.Contrasena);
        if (verif == PasswordVerificationResult.Failed)
        {
            u.IntentosFallidos++;
            string detalle = "Contraseña incorrecta";
            if (u.IntentosFallidos >= _options.MaxFailedAttempts)
            {
                u.BloqueadoHastaUtc = now.AddMinutes(_options.LockoutMinutes);
                u.IntentosFallidos = 0;
                detalle = "Contraseña incorrecta; cuenta bloqueada temporalmente";
            }
            await _accesos.RegistrarAsync(ResultadoAcceso.CredencialesInvalidas, r.Correo, u.EmpresaId, u.UsuarioId, detalle, ct);
            throw ServiceException.Unauthorized(CredencialesInvalidas);
        }

        if (_options.RequireConfirmedEmail && u.CorreoConfirmadoUtc is null)
        {
            // Solo quien conoce la contraseña llega aquí: no se revela nada a terceros.
            await _accesos.RegistrarAsync(ResultadoAcceso.CuentaInactiva, r.Correo, u.EmpresaId, u.UsuarioId, "Correo sin confirmar", ct);
            throw ServiceException.Forbidden("Debes confirmar tu correo electrónico antes de iniciar sesión.");
        }

        if (verif == PasswordVerificationResult.SuccessRehashNeeded)
            u.HashContrasena = _passwords.Hash(u, r.Contrasena);
        u.IntentosFallidos = 0;
        u.BloqueadoHastaUtc = null;
        u.UltimoAccesoUtc = now;

        var cfg = await _db.ConfiguracionEmpresa.AsNoTracking().FirstAsync(c => c.EmpresaId == u.EmpresaId, ct);
        var sesion = new SesionUsuario
        {
            SesionId = Guid.NewGuid(), EmpresaId = u.EmpresaId, UsuarioId = u.UsuarioId,
            Dispositivo = Helpers.Limpiar(r.Dispositivo), UserAgent = Recortar(_current.UserAgent, 500),
            DireccionIp = Recortar(_current.IpAddress, 45),
            FechaCreacionUtc = now, UltimaActividadUtc = now, ExpiraUtc = now.AddHours(cfg.DuracionSesionMaximaHoras)
        };
        var refresh = TokenHelper.Nuevo();
        sesion.TokenRefreshHash = TokenHelper.Hash(refresh);
        _db.SesionesUsuario.Add(sesion);

        await _accesos.RegistrarAsync(ResultadoAcceso.Exitoso, r.Correo, u.EmpresaId, u.UsuarioId, null, ct);
        return Emitir(u, sesion, refresh);
    }

    public async Task<TokenResponse> RefreshAsync(RefreshRequest r, CancellationToken ct)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var hash = TokenHelper.Hash(r.RefreshToken);
        var s = await _db.SesionesUsuario.Include(x => x.Usuario).ThenInclude(x => x.Empresa)
            .FirstOrDefaultAsync(x => x.TokenRefreshHash == hash, ct);

        if (s is null || s.RevocadaUtc is not null || s.ExpiraUtc <= now
            || s.Usuario.Estado != EstadoUsuario.Activo || s.Usuario.Empresa.Estado != EstadoEmpresa.Activa)
        {
            await _accesos.RegistrarAsync(ResultadoAcceso.RefreshInvalido, s?.Usuario.Correo, s?.EmpresaId, s?.UsuarioId,
                "Refresh token no válido", ct);
            throw ServiceException.Unauthorized("Sesión no válida.");
        }

        var inactividad = await _db.ConfiguracionEmpresa.Where(c => c.EmpresaId == s.EmpresaId)
            .Select(c => (int)c.CierreSesionInactividadMinutos).FirstAsync(ct);
        if (now - s.UltimaActividadUtc > TimeSpan.FromMinutes(inactividad))
        {
            SesionService.Revocar(s, "Inactividad", now);
            await _accesos.RegistrarAsync(ResultadoAcceso.RefreshInvalido, s.Usuario.Correo, s.EmpresaId, s.UsuarioId, "Sesión inactiva", ct);
            throw ServiceException.Unauthorized("Sesión no válida.");
        }

        var nuevo = TokenHelper.Nuevo();   // rotación: el token anterior deja de ser válido
        s.TokenRefreshHash = TokenHelper.Hash(nuevo);
        s.UltimaActividadUtc = now;
        await _db.SaveSafeAsync(ct);
        return Emitir(s.Usuario, s, nuevo);
    }

    public async Task LogoutAsync(CancellationToken ct)
    {
        var sesionId = _current.SesionId ?? throw ServiceException.Unauthorized("Autenticación requerida.");
        var empresaId = _current.RequireEmpresaId();
        var s = await _db.SesionesUsuario.FirstOrDefaultAsync(x => x.SesionId == sesionId && x.EmpresaId == empresaId, ct);
        if (s is { RevocadaUtc: null })
        {
            SesionService.Revocar(s, "Cierre de sesión", _clock.GetUtcNow().UtcDateTime);
            await _db.SaveSafeAsync(ct);
        }
    }

    public async Task CambiarContrasenaAsync(CambiarContrasenaRequest r, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var usuarioId = _current.RequireUsuarioId();
        var u = await _db.Usuarios.FirstOrDefaultAsync(x => x.UsuarioId == usuarioId && x.EmpresaId == empresaId, ct)
            ?? throw ServiceException.Unauthorized("Autenticación requerida.");

        if (_passwords.Verify(u, u.HashContrasena, r.ContrasenaActual) == PasswordVerificationResult.Failed)
            throw ServiceException.Validation("La contraseña actual no es correcta.");
        _passwords.ValidarPolitica(r.ContrasenaNueva);
        if (r.ContrasenaNueva == r.ContrasenaActual)
            throw ServiceException.Validation("La nueva contraseña debe ser distinta de la actual.");

        var now = _clock.GetUtcNow().UtcDateTime;
        u.HashContrasena = _passwords.Hash(u, r.ContrasenaNueva);
        u.DebeCambiarContrasena = false;
        u.FechaActualizacionUtc = now;
        await SesionService.RevocarActivasAsync(_db, empresaId, usuarioId, "Cambio de contraseña", now, ct, excepto: _current.SesionId);
        _auditoria.Registrar("Usuarios", nameof(Usuario), usuarioId.ToString(), "CambiarContrasena");
        await _db.SaveSafeAsync(ct);
    }

    public async Task<PerfilDto> ObtenerPerfilAsync(CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var usuarioId = _current.RequireUsuarioId();
        var u = await _db.Usuarios.AsNoTracking().Include(x => x.Empresa)
            .FirstOrDefaultAsync(x => x.UsuarioId == usuarioId && x.EmpresaId == empresaId, ct)
            ?? throw ServiceException.Unauthorized("Autenticación requerida.");
        var roles = await _db.UsuarioRoles.AsNoTracking()
            .Where(ur => ur.EmpresaId == empresaId && ur.UsuarioId == usuarioId && ur.Rol.Estado)
            .Select(ur => ur.Rol.Nombre).OrderBy(n => n).ToListAsync(ct);
        var permisos = await _permisos.ObtenerPermisosAsync(empresaId, usuarioId, ct);
        return new PerfilDto
        {
            UsuarioId = u.UsuarioId, EmpresaId = u.EmpresaId, EmpresaNombre = u.Empresa.Nombre, Correo = u.Correo,
            Nombre = u.Nombre, Apellido = u.Apellido, DebeCambiarContrasena = u.DebeCambiarContrasena,
            CorreoConfirmado = u.CorreoConfirmadoUtc != null, EsPlataforma = u.Empresa.EsPlataforma,
            Roles = roles, Permisos = permisos.OrderBy(p => p, StringComparer.Ordinal).ToList()
        };
    }

    private TokenResponse Emitir(Usuario u, SesionUsuario s, string refresh)
    {
        var (token, exp) = _tokens.Crear(u, s.SesionId, s.ExpiraUtc);
        return new TokenResponse
        {
            AccessToken = token, AccessTokenExpiraUtc = exp, RefreshToken = refresh,
            RefreshTokenExpiraUtc = s.ExpiraUtc, DebeCambiarContrasena = u.DebeCambiarContrasena
        };
    }

    private static string? Recortar(string? s, int max) => s is null ? null : (s.Length <= max ? s : s[..max]);

    // Hash válido de una contraseña aleatoria, solo para igualar tiempos cuando el correo no existe.
    private static class DummyHash
    {
        public static readonly string Value =
            new PasswordHasher<Usuario>().HashPassword(new Usuario(), Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)));
    }
}
