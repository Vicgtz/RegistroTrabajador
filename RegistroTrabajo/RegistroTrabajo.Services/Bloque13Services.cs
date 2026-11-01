using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RegistroTrabajo.Data;
using RegistroTrabajo.Data.Entities;
using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

/// <summary>Administración de la clave de registro de la empresa (requiere empresa.configurar; la empresa sale del token).</summary>
public interface IClaveRegistroService
{
    Task<ClaveRegistroEstadoDto> EstadoAsync(CancellationToken ct);
    /// <summary>Genera una clave nueva (reemplaza la anterior) y la devuelve en claro por única vez.</summary>
    Task<ClaveRegistroGeneradaDto> GenerarAsync(CancellationToken ct);
    /// <summary>Desactiva el registro: ninguna clave vuelve a ser válida hasta generar una nueva.</summary>
    Task DesactivarAsync(CancellationToken ct);
}

/// <summary>Registro público de trabajadores con la clave de la empresa (anónimo; no usa ICurrentUser).</summary>
public interface IRegistroConClaveService
{
    Task RegistrarAsync(RegistroConClaveRequest request, CancellationToken ct);
}

internal static class ClaveRegistroHelper
{
    // Sin caracteres ambiguos (0/O, 1/I/L) para poder dictar o copiar la clave sin errores.
    private const string Alfabeto = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    /// <summary>16 caracteres (≈79 bits) en grupos de 4: XXXX-XXXX-XXXX-XXXX.</summary>
    public static string Nueva()
    {
        var chars = new char[19];
        for (int i = 0, j = 0; i < 16; i++)
        {
            if (i > 0 && i % 4 == 0) chars[j++] = '-';
            chars[j++] = Alfabeto[RandomNumberGenerator.GetInt32(Alfabeto.Length)];
        }
        return new string(chars);
    }

    /// <summary>Ignora guiones, espacios y mayúsculas/minúsculas.</summary>
    public static byte[] Hash(string clave) =>
        TokenHelper.Hash(new string(clave.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant());
}

public class ClaveRegistroService : IClaveRegistroService
{
    private readonly RegistroTrabajoDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IPermisoService _permisos;
    private readonly IAuditoriaService _auditoria;
    private readonly TimeProvider _clock;

    public ClaveRegistroService(RegistroTrabajoDbContext db, ICurrentUser current, IPermisoService permisos, IAuditoriaService auditoria, TimeProvider clock)
    {
        _db = db; _current = current; _permisos = permisos; _auditoria = auditoria; _clock = clock;
    }

    public async Task<ClaveRegistroEstadoDto> EstadoAsync(CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.EmpresaConfigurar, ct);
        var empresaId = _current.RequireEmpresaId();
        var c = await _db.ConfiguracionEmpresa.AsNoTracking().FirstOrDefaultAsync(x => x.EmpresaId == empresaId, ct)
            ?? throw ServiceException.NotFound("Configuración de la empresa no encontrada.");
        return new ClaveRegistroEstadoDto { Activa = c.ClaveRegistroHash is not null, GeneradaUtc = c.ClaveRegistroGeneradaUtc };
    }

    public async Task<ClaveRegistroGeneradaDto> GenerarAsync(CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.EmpresaConfigurar, ct);
        var empresaId = _current.RequireEmpresaId();
        var c = await _db.ConfiguracionEmpresa.FirstOrDefaultAsync(x => x.EmpresaId == empresaId, ct)
            ?? throw ServiceException.NotFound("Configuración de la empresa no encontrada.");

        var clave = ClaveRegistroHelper.Nueva();
        var now = _clock.GetUtcNow().UtcDateTime;
        var estabaActiva = c.ClaveRegistroHash is not null;
        c.ClaveRegistroHash = ClaveRegistroHelper.Hash(clave);
        c.ClaveRegistroGeneradaUtc = now;
        c.FechaActualizacionUtc = now;
        // Nunca se audita la clave ni su hash.
        _auditoria.Registrar("Empresa", nameof(ConfiguracionEmpresa), empresaId.ToString(), "GenerarClaveRegistro",
            new { activa = estabaActiva }, new { activa = true });
        await _db.SaveSafeAsync(ct);
        return new ClaveRegistroGeneradaDto { Clave = clave, GeneradaUtc = now };
    }

    public async Task DesactivarAsync(CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.EmpresaConfigurar, ct);
        var empresaId = _current.RequireEmpresaId();
        var c = await _db.ConfiguracionEmpresa.FirstOrDefaultAsync(x => x.EmpresaId == empresaId, ct)
            ?? throw ServiceException.NotFound("Configuración de la empresa no encontrada.");
        if (c.ClaveRegistroHash is null) return;

        c.ClaveRegistroHash = null;
        c.ClaveRegistroGeneradaUtc = null;
        c.FechaActualizacionUtc = _clock.GetUtcNow().UtcDateTime;
        _auditoria.Registrar("Empresa", nameof(ConfiguracionEmpresa), empresaId.ToString(), "DesactivarClaveRegistro",
            new { activa = true }, new { activa = false });
        await _db.SaveSafeAsync(ct);
    }
}

public class RegistroConClaveService : IRegistroConClaveService
{
    // Mensaje único: no revela si falló la clave, si el correo ya existe o si la empresa no admite registro.
    internal const string RegistroInvalido = "No se pudo completar el registro. Verifica la clave y tus datos.";

    private readonly RegistroTrabajoDbContext _db;
    private readonly IPasswordService _passwords;
    private readonly IAuditoriaService _auditoria;
    private readonly ICuentaService _cuentas;
    private readonly SecurityOptions _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<RegistroConClaveService> _logger;

    public RegistroConClaveService(RegistroTrabajoDbContext db, IPasswordService passwords, IAuditoriaService auditoria,
        ICuentaService cuentas, IOptions<SecurityOptions> options, TimeProvider clock, ILogger<RegistroConClaveService> logger)
    {
        _db = db; _passwords = passwords; _auditoria = auditoria; _cuentas = cuentas; _options = options.Value;
        _clock = clock; _logger = logger;
    }

    public async Task RegistrarAsync(RegistroConClaveRequest r, CancellationToken ct)
    {
        // La política de contraseña sí se informa: no depende de la clave ni revela nada de la empresa.
        _passwords.ValidarPolitica(r.Contrasena);

        var hash = ClaveRegistroHelper.Hash(r.Clave);
        var empresa = await _db.ConfiguracionEmpresa.AsNoTracking()
            .Where(c => c.ClaveRegistroHash == hash && c.Empresa.Estado == EstadoEmpresa.Activa && !c.Empresa.EsPlataforma)
            .Select(c => new { c.EmpresaId })
            .FirstOrDefaultAsync(ct);
        if (empresa is null)
        {
            _logger.LogWarning("Registro rechazado: clave de registro no válida.");
            throw ServiceException.Validation(RegistroInvalido);
        }

        var correo = r.Correo.Trim();
        var normalizado = Helpers.NormalizarCorreo(correo);
        if (correo.Length < 4 || await _db.Usuarios.AnyAsync(u => u.CorreoNormalizado == normalizado, ct))
            throw ServiceException.Validation(RegistroInvalido);

        // El rol es siempre el de menor privilegio; la clave nunca concede permisos elevados.
        var rol = await _db.Roles.AsNoTracking().FirstOrDefaultAsync(x => x.EmpresaId == empresa.EmpresaId
            && x.Nombre == NombresRol.Trabajador && x.EsSistema && x.Estado, ct);
        if (rol is null)
        {
            _logger.LogError("La empresa {EmpresaId} no tiene el rol de sistema '{Rol}' activo; no se pudo registrar la cuenta.", empresa.EmpresaId, NombresRol.Trabajador);
            throw ServiceException.Validation(RegistroInvalido);
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        var u = new Usuario
        {
            UsuarioId = Guid.NewGuid(), EmpresaId = empresa.EmpresaId,
            Correo = correo, CorreoNormalizado = normalizado, Nombre = r.Nombre.Trim(), Apellido = r.Apellido.Trim(),
            DebeCambiarContrasena = false, Estado = EstadoUsuario.Activo, FechaCreacionUtc = now, HashContrasena = string.Empty
        };
        u.HashContrasena = _passwords.Hash(u, r.Contrasena);
        _db.Usuarios.Add(u);
        _db.UsuarioRoles.Add(new UsuarioRol { EmpresaId = empresa.EmpresaId, UsuarioId = u.UsuarioId, RolId = rol.RolId, AsignadoUtc = now });
        _auditoria.Registrar("Usuarios", nameof(Usuario), u.UsuarioId.ToString(), "AutoRegistro", null,
            new { u.Correo, u.Nombre, u.Apellido, rol = rol.Nombre }, empresa.EmpresaId, u.UsuarioId);

        try { await _db.SaveSafeAsync(ct); }
        catch (ServiceException ex) when (ex.Kind == ServiceErrorKind.Conflict)
        {
            // Registro simultáneo con el mismo correo: misma respuesta genérica.
            throw ServiceException.Validation(RegistroInvalido);
        }

        // Sin correo salvo que la empresa exija confirmarlo para iniciar sesión.
        if (_options.RequireConfirmedEmail)
            await _cuentas.EnviarConfirmacionAsync(u, ct);
    }
}
