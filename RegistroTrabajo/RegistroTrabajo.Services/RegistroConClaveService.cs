using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RegistroTrabajo.Data;
using RegistroTrabajo.Data.Entities;
using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

/// <summary>Registro público de trabajadores con la clave de la empresa (anónimo; no usa ICurrentUser).</summary>
public interface IRegistroConClaveService
{
    Task RegistrarAsync(RegistroConClaveRequest request, CancellationToken ct);
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
