using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RegistroTrabajo.Data;
using RegistroTrabajo.Data.Entities;
using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public interface IUsuarioService
{
    Task<PagedResult<UsuarioDto>> ListarAsync(UsuarioFiltro filtro, CancellationToken ct);
    Task<UsuarioDto> ObtenerAsync(Guid usuarioId, CancellationToken ct);
    Task<UsuarioDto> CrearAsync(CrearUsuarioRequest request, CancellationToken ct);
    /// <summary>Valida y agrega el usuario al contexto SIN guardar (para altas conjuntas, p. ej. trabajador + cuenta). Quien llama guarda y luego usa <see cref="NotificarAltaAsync"/>.</summary>
    Task<Usuario> PrepararAltaAsync(CrearUsuarioRequest request, CancellationToken ct);
    /// <summary>Genera el enlace de confirmación de correo de un usuario recién guardado (nunca falla el alta).</summary>
    Task NotificarAltaAsync(Usuario usuario, CancellationToken ct);
    Task<UsuarioDto> ActualizarAsync(Guid usuarioId, ActualizarUsuarioRequest request, CancellationToken ct);
    Task DesactivarAsync(Guid usuarioId, CancellationToken ct);
    Task ActivarAsync(Guid usuarioId, CancellationToken ct);
    /// <summary>Quita el bloqueo temporal por intentos fallidos.</summary>
    Task DesbloquearAsync(Guid usuarioId, CancellationToken ct);
    /// <summary>Asigna una contraseña nueva a otro usuario (deberá cambiarla al entrar) y cierra sus sesiones.</summary>
    Task RestablecerContrasenaAsync(Guid usuarioId, string contrasenaNueva, CancellationToken ct);
}

public class UsuarioService : IUsuarioService
{
    private readonly RegistroTrabajoDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IPasswordService _passwords;
    private readonly IAuditoriaService _auditoria;
    private readonly RolService _roles;
    private readonly ICuentaService _cuentas;
    private readonly TimeProvider _clock;
    private readonly ILogger<UsuarioService> _logger;

    public UsuarioService(RegistroTrabajoDbContext db, ICurrentUser current, IPasswordService passwords,
        IAuditoriaService auditoria, RolService roles, ICuentaService cuentas, TimeProvider clock, ILogger<UsuarioService> logger)
    {
        _db = db; _current = current; _passwords = passwords; _auditoria = auditoria; _roles = roles;
        _cuentas = cuentas; _clock = clock; _logger = logger;
    }

    private static IQueryable<UsuarioDto> Proyectar(IQueryable<Usuario> q) => q.Select(u => new UsuarioDto
    {
        UsuarioId = u.UsuarioId, Correo = u.Correo, Nombre = u.Nombre, Apellido = u.Apellido, Estado = u.Estado,
        DebeCambiarContrasena = u.DebeCambiarContrasena, TrabajadorId = u.TrabajadorId,
        UltimoAccesoUtc = u.UltimoAccesoUtc, CorreoConfirmadoUtc = u.CorreoConfirmadoUtc, BloqueadoHastaUtc = u.BloqueadoHastaUtc,
        FechaCreacionUtc = u.FechaCreacionUtc, FechaActualizacionUtc = u.FechaActualizacionUtc,
        Roles = u.UsuarioRoles.Select(ur => ur.Rol.Nombre).ToList()
    });

    public async Task<PagedResult<UsuarioDto>> ListarAsync(UsuarioFiltro f, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var q = _db.Usuarios.AsNoTracking().Where(u => u.EmpresaId == empresaId);
        if (f.Estado is { } estado) q = q.Where(u => u.Estado == estado);
        if (!string.IsNullOrWhiteSpace(f.Buscar))
        {
            var s = f.Buscar.Trim();
            q = q.Where(u => u.Nombre.Contains(s) || u.Apellido.Contains(s) || u.Correo.Contains(s));
        }
        return await Proyectar(q.OrderBy(u => u.Apellido).ThenBy(u => u.Nombre)).ToPagedAsync(f, ct);
    }

    public async Task<UsuarioDto> ObtenerAsync(Guid usuarioId, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        return await Proyectar(_db.Usuarios.AsNoTracking().Where(u => u.UsuarioId == usuarioId && u.EmpresaId == empresaId))
            .FirstOrDefaultAsync(ct)
            ?? throw ServiceException.NotFound("Usuario no encontrado.");
    }

    public async Task<UsuarioDto> CrearAsync(CrearUsuarioRequest r, CancellationToken ct)
    {
        var u = await PrepararAltaAsync(r, ct);
        await _db.SaveSafeAsync(ct);
        await EnviarConfirmacionSinFallarAsync(u, ct);
        return await ObtenerAsync(u.UsuarioId, ct);
    }

    public Task NotificarAltaAsync(Usuario usuario, CancellationToken ct) => EnviarConfirmacionSinFallarAsync(usuario, ct);

    public async Task<Usuario> PrepararAltaAsync(CrearUsuarioRequest r, CancellationToken ct)
    {
        var actorId = _current.RequireUsuarioId();
        var empresaId = _current.RequireEmpresaId();
        _passwords.ValidarPolitica(r.Contrasena);

        var correo = r.Correo.Trim();
        var normalizado = Helpers.NormalizarCorreo(correo);
        if (correo.Length < 4) throw ServiceException.Validation("Correo no válido.");
        if (await _db.Usuarios.AnyAsync(u => u.CorreoNormalizado == normalizado, ct))
            throw ServiceException.Conflict("El correo ya está registrado.");
        await ValidarTrabajadorAsync(r.TrabajadorId, empresaId, null, ct);

        var rolIds = (r.RolIds ?? Array.Empty<Guid>()).Distinct().ToList();
        var roles = await _db.Roles.AsNoTracking().Where(x => x.EmpresaId == empresaId && rolIds.Contains(x.RolId)).ToListAsync(ct);
        if (roles.Count != rolIds.Count) throw ServiceException.Validation("Uno o más roles no existen.");
        foreach (var rol in roles)
        {
            if (!rol.Estado) throw ServiceException.Validation("Uno de los roles está inactivo.");
            await _roles.AsegurarPuedeGestionarRolAsync(rol, actorId, empresaId, ct);
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        var u = new Usuario
        {
            UsuarioId = Guid.NewGuid(), EmpresaId = empresaId, TrabajadorId = r.TrabajadorId,
            Correo = correo, CorreoNormalizado = normalizado, Nombre = r.Nombre.Trim(), Apellido = r.Apellido.Trim(),
            DebeCambiarContrasena = r.DebeCambiarContrasena, Estado = EstadoUsuario.Activo,
            FechaCreacionUtc = now, HashContrasena = string.Empty
        };
        u.HashContrasena = _passwords.Hash(u, r.Contrasena);
        _db.Usuarios.Add(u);
        foreach (var rol in roles)
            _db.UsuarioRoles.Add(new UsuarioRol { EmpresaId = empresaId, UsuarioId = u.UsuarioId, RolId = rol.RolId, AsignadoUtc = now, AsignadoPorUsuarioId = actorId });

        _auditoria.Registrar("Usuarios", nameof(Usuario), u.UsuarioId.ToString(), "Crear", null,
            new { usuario = Snapshot(u), roles = roles.Select(x => x.Nombre) });
        return u;
    }

    public async Task<UsuarioDto> ActualizarAsync(Guid usuarioId, ActualizarUsuarioRequest r, CancellationToken ct)
    {
        var u = await CargarAsync(usuarioId, ct);
        var correo = r.Correo.Trim();
        var normalizado = Helpers.NormalizarCorreo(correo);
        if (correo.Length < 4) throw ServiceException.Validation("Correo no válido.");
        if (await _db.Usuarios.AnyAsync(x => x.CorreoNormalizado == normalizado && x.UsuarioId != usuarioId, ct))
            throw ServiceException.Conflict("El correo ya está registrado.");
        await ValidarTrabajadorAsync(r.TrabajadorId, u.EmpresaId, usuarioId, ct);

        var antes = Snapshot(u);
        var correoCambio = u.CorreoNormalizado != normalizado;
        u.Correo = correo;
        u.CorreoNormalizado = normalizado;
        u.Nombre = r.Nombre.Trim();
        u.Apellido = r.Apellido.Trim();
        u.DebeCambiarContrasena = r.DebeCambiarContrasena;
        u.TrabajadorId = r.TrabajadorId;
        u.FechaActualizacionUtc = _clock.GetUtcNow().UtcDateTime;
        if (correoCambio) u.CorreoConfirmadoUtc = null;   // un correo nuevo debe confirmarse otra vez

        _auditoria.Registrar("Usuarios", nameof(Usuario), usuarioId.ToString(), "Actualizar", antes, Snapshot(u));
        await _db.SaveSafeAsync(ct);
        if (correoCambio) await EnviarConfirmacionSinFallarAsync(u, ct);
        return await ObtenerAsync(usuarioId, ct);
    }

    public async Task DesactivarAsync(Guid usuarioId, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        if (usuarioId == _current.RequireUsuarioId())
            throw ServiceException.Forbidden("No puede desactivar su propia cuenta.");
        var u = await CargarAsync(usuarioId, ct);
        if (u.Estado == EstadoUsuario.Inactivo) return;

        var esAdmin = await _db.UsuarioRoles.AnyAsync(ur => ur.UsuarioId == usuarioId && ur.Rol.Nombre == NombresRol.Administrador && ur.Rol.EsSistema, ct);
        if (esAdmin)
        {
            var otros = await _db.UsuarioRoles.CountAsync(ur => ur.EmpresaId == empresaId && ur.Rol.Nombre == NombresRol.Administrador
                && ur.Rol.EsSistema && ur.UsuarioId != usuarioId && ur.Usuario.Estado == EstadoUsuario.Activo, ct);
            if (otros == 0) throw ServiceException.Conflict("No se puede desactivar al último administrador activo.");
        }

        var antes = Snapshot(u);
        var now = _clock.GetUtcNow().UtcDateTime;
        u.Estado = EstadoUsuario.Inactivo;
        u.FechaBajaUtc = now;
        u.FechaActualizacionUtc = now;
        await SesionService.RevocarActivasAsync(_db, empresaId, usuarioId, "Usuario desactivado", now, ct);

        _auditoria.Registrar("Usuarios", nameof(Usuario), usuarioId.ToString(), "Desactivar", antes, Snapshot(u));
        await _db.SaveSafeAsync(ct);
    }

    public async Task ActivarAsync(Guid usuarioId, CancellationToken ct)
    {
        var u = await CargarAsync(usuarioId, ct);
        if (u.Estado == EstadoUsuario.Activo) return;
        var antes = Snapshot(u);
        u.Estado = EstadoUsuario.Activo;
        u.FechaBajaUtc = null;
        u.IntentosFallidos = 0;
        u.BloqueadoHastaUtc = null;
        u.FechaActualizacionUtc = _clock.GetUtcNow().UtcDateTime;
        _auditoria.Registrar("Usuarios", nameof(Usuario), usuarioId.ToString(), "Activar", antes, Snapshot(u));
        await _db.SaveSafeAsync(ct);
    }

    public async Task DesbloquearAsync(Guid usuarioId, CancellationToken ct)
    {
        var u = await CargarAsync(usuarioId, ct);
        u.BloqueadoHastaUtc = null;
        u.IntentosFallidos = 0;
        u.FechaActualizacionUtc = _clock.GetUtcNow().UtcDateTime;
        _auditoria.Registrar("Usuarios", nameof(Usuario), usuarioId.ToString(), "Desbloquear");
        await _db.SaveSafeAsync(ct);
    }

    public async Task RestablecerContrasenaAsync(Guid usuarioId, string contrasenaNueva, CancellationToken ct)
    {
        var actorId = _current.RequireUsuarioId();
        var empresaId = _current.RequireEmpresaId();
        if (usuarioId == actorId)
            throw ServiceException.Forbidden("Para cambiar su propia contraseña use la opción de su cuenta.");
        var u = await CargarAsync(usuarioId, ct);
        _passwords.ValidarPolitica(contrasenaNueva);

        // Anti-elevación: no se puede tomar la cuenta de alguien con más permisos que uno mismo.
        var rolesDestino = await _db.UsuarioRoles.AsNoTracking().Where(ur => ur.EmpresaId == empresaId && ur.UsuarioId == usuarioId)
            .Select(ur => ur.Rol).ToListAsync(ct);
        foreach (var rol in rolesDestino)
            await _roles.AsegurarPuedeGestionarRolAsync(rol, actorId, empresaId, ct);

        var now = _clock.GetUtcNow().UtcDateTime;
        u.HashContrasena = _passwords.Hash(u, contrasenaNueva);
        u.DebeCambiarContrasena = true;
        u.IntentosFallidos = 0;
        u.BloqueadoHastaUtc = null;
        u.FechaActualizacionUtc = now;
        await SesionService.RevocarActivasAsync(_db, empresaId, usuarioId, "Contraseña restablecida por un administrador", now, ct);
        _auditoria.Registrar("Usuarios", nameof(Usuario), usuarioId.ToString(), "RestablecerContrasena");
        await _db.SaveSafeAsync(ct);
    }

    private async Task<Usuario> CargarAsync(Guid usuarioId, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        return await _db.Usuarios.FirstOrDefaultAsync(u => u.UsuarioId == usuarioId && u.EmpresaId == empresaId, ct)
            ?? throw ServiceException.NotFound("Usuario no encontrado.");
    }

    // El trabajador debe ser de la misma empresa y tener, a lo sumo, una cuenta de usuario.
    private async Task ValidarTrabajadorAsync(Guid? trabajadorId, Guid empresaId, Guid? usuarioActualId, CancellationToken ct)
    {
        if (trabajadorId is not { } id) return;
        if (!await _db.Trabajadores.AnyAsync(t => t.TrabajadorId == id && t.EmpresaId == empresaId, ct))
            throw ServiceException.Validation("El trabajador indicado no existe en la empresa.");
        if (await _db.Usuarios.AnyAsync(u => u.TrabajadorId == id && u.UsuarioId != usuarioActualId, ct))
            throw ServiceException.Conflict("El trabajador ya tiene una cuenta de usuario.");
    }

    private async Task EnviarConfirmacionSinFallarAsync(Usuario u, CancellationToken ct)
    {
        try
        {
            await _cuentas.EnviarConfirmacionAsync(u, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "No se pudo generar el enlace de confirmación de correo del usuario {UsuarioId}.", u.UsuarioId);
        }
    }

    // Sin contraseña/hash: solo campos auditables.
    private static object Snapshot(Usuario u) => new
    {
        u.Correo, u.Nombre, u.Apellido, u.Estado, u.TrabajadorId, u.DebeCambiarContrasena
    };
}
