using Microsoft.EntityFrameworkCore;
using RegistroTrabajo.Data;
using RegistroTrabajo.Data.Entities;
using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public interface IRolService
{
    Task<IReadOnlyList<RolDto>> ListarRolesAsync(CancellationToken ct);
    Task<IReadOnlyList<PermisoDto>> ListarPermisosAsync(CancellationToken ct);
    Task<RolPermisosDto> ObtenerPermisosRolAsync(Guid rolId, CancellationToken ct);
    Task<RolPermisosDto> ConfigurarPermisosRolAsync(Guid rolId, IReadOnlyList<string> codigos, CancellationToken ct);
    Task<RolDto> CrearRolAsync(CrearRolRequest request, CancellationToken ct);
    Task<RolDto> ActualizarRolAsync(Guid rolId, ActualizarRolRequest request, CancellationToken ct);
    Task<RolDto> CambiarEstadoRolAsync(Guid rolId, bool estado, CancellationToken ct);
    Task<IReadOnlyList<RolDto>> ObtenerRolesUsuarioAsync(Guid usuarioId, CancellationToken ct);
    Task AsignarRolAsync(Guid usuarioId, Guid rolId, CancellationToken ct);
    Task RetirarRolAsync(Guid usuarioId, Guid rolId, CancellationToken ct);
}

public class RolService : IRolService
{
    private const string PrefijoPlataforma = "plataforma.";

    private readonly RegistroTrabajoDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IPermisoService _permisos;
    private readonly IAuditoriaService _auditoria;
    private readonly TimeProvider _clock;

    public RolService(RegistroTrabajoDbContext db, ICurrentUser current, IPermisoService permisos,
        IAuditoriaService auditoria, TimeProvider clock)
    {
        _db = db; _current = current; _permisos = permisos; _auditoria = auditoria; _clock = clock;
    }

    public async Task<IReadOnlyList<RolDto>> ListarRolesAsync(CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        return await _db.Roles.AsNoTracking().Where(r => r.EmpresaId == empresaId).OrderBy(r => r.Nombre)
            .Select(r => new RolDto { RolId = r.RolId, Nombre = r.Nombre, Descripcion = r.Descripcion, EsSistema = r.EsSistema, Estado = r.Estado })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PermisoDto>> ListarPermisosAsync(CancellationToken ct)
    {
        var esPlataforma = await EsPlataformaAsync(ct);
        return await _db.PermisosSistema.AsNoTracking()
            .Where(p => esPlataforma || !p.Codigo.StartsWith(PrefijoPlataforma))
            .OrderBy(p => p.Modulo).ThenBy(p => p.Accion)
            .Select(p => new PermisoDto { PermisoId = p.PermisoId, Codigo = p.Codigo, Modulo = p.Modulo, Accion = p.Accion, Descripcion = p.Descripcion })
            .ToListAsync(ct);
    }

    public async Task<RolPermisosDto> ObtenerPermisosRolAsync(Guid rolId, CancellationToken ct)
    {
        var rol = await ObtenerRolAsync(rolId, false, ct);
        return new RolPermisosDto { RolId = rol.RolId, Permisos = await CodigosDeRolAsync(rol.EmpresaId, rol.RolId, ct) };
    }

    public async Task<RolPermisosDto> ConfigurarPermisosRolAsync(Guid rolId, IReadOnlyList<string> codigos, CancellationToken ct)
    {
        var actorId = _current.RequireUsuarioId();
        var empresaId = _current.RequireEmpresaId();
        var rol = await ObtenerRolAsync(rolId, true, ct);

        if (EsRolProtegido(rol))
            throw ServiceException.Forbidden("Los permisos de los roles Administrador y Administrador de plataforma no se pueden modificar.");
        if (await _db.UsuarioRoles.AnyAsync(ur => ur.EmpresaId == empresaId && ur.UsuarioId == actorId && ur.RolId == rolId, ct))
            throw ServiceException.Forbidden("No puede modificar los permisos de un rol que usted tiene asignado.");

        var solicitados = codigos.Select(c => c.Trim()).Where(c => c.Length > 0).ToHashSet(StringComparer.Ordinal);
        var catalogo = await _db.PermisosSistema.ToListAsync(ct);
        var desconocidos = solicitados.Except(catalogo.Select(p => p.Codigo)).ToList();
        if (desconocidos.Count > 0) throw ServiceException.Validation("Uno o más permisos no existen en el catálogo.");
        if (solicitados.Any(c => c.StartsWith(PrefijoPlataforma, StringComparison.Ordinal)) && !await EsPlataformaAsync(ct))
            throw ServiceException.Forbidden("Los permisos de plataforma no se pueden asignar en esta empresa.");

        var actuales = await _db.RolPermisos.Where(rp => rp.EmpresaId == empresaId && rp.RolId == rolId).ToListAsync(ct);
        var antes = actuales.Where(rp => rp.Permitido).Select(rp => catalogo.First(p => p.PermisoId == rp.PermisoId).Codigo).OrderBy(x => x).ToList();

        var propios = await _permisos.ObtenerPermisosAsync(empresaId, actorId, ct);
        var nuevos = solicitados.Except(antes).ToList();
        if (nuevos.Any(c => !propios.Contains(c)))
            throw ServiceException.Forbidden("No puede otorgar permisos que usted no posee.");

        var now = _clock.GetUtcNow().UtcDateTime;
        foreach (var p in catalogo)
        {
            var permitido = solicitados.Contains(p.Codigo);
            var fila = actuales.FirstOrDefault(rp => rp.PermisoId == p.PermisoId);
            if (fila is null)
            {
                if (!permitido) continue;
                _db.RolPermisos.Add(new RolPermiso
                {
                    EmpresaId = empresaId, RolId = rolId, PermisoId = p.PermisoId, Permitido = true,
                    FechaActualizacionUtc = now, ActualizadoPorUsuarioId = actorId
                });
            }
            else if (fila.Permitido != permitido)
            {
                fila.Permitido = permitido;
                fila.FechaActualizacionUtc = now;
                fila.ActualizadoPorUsuarioId = actorId;
            }
        }

        var despues = solicitados.OrderBy(x => x).ToList();
        _auditoria.Registrar("Roles", nameof(RolPermiso), rolId.ToString(), "ConfigurarPermisos",
            new { permisos = antes }, new { permisos = despues });
        await _db.SaveSafeAsync(ct);
        return new RolPermisosDto { RolId = rolId, Permisos = despues };
    }

    // ---------------- Roles propios de la empresa ----------------
    public async Task<RolDto> CrearRolAsync(CrearRolRequest r, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var nombre = ValidarNombre(r.Nombre);
        if (await _db.Roles.AnyAsync(x => x.EmpresaId == empresaId && x.Nombre.ToLower() == nombre.ToLower(), ct))
            throw ServiceException.Conflict("Ya existe un rol con ese nombre.");

        var rol = new Rol
        {
            RolId = Guid.NewGuid(), EmpresaId = empresaId, Nombre = nombre, Descripcion = Helpers.Limpiar(r.Descripcion),
            EsSistema = false, Estado = true, FechaCreacionUtc = _clock.GetUtcNow().UtcDateTime
        };
        _db.Roles.Add(rol);
        _auditoria.Registrar("Roles", nameof(Rol), rol.RolId.ToString(), "CrearRol", null, new { rol.Nombre, rol.Descripcion });
        await _db.SaveSafeAsync(ct);
        return ToDto(rol);
    }

    public async Task<RolDto> ActualizarRolAsync(Guid rolId, ActualizarRolRequest r, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var rol = await ObtenerRolAsync(rolId, true, ct);
        if (rol.EsSistema) throw ServiceException.Forbidden("Los roles de sistema no se pueden editar.");

        var nombre = ValidarNombre(r.Nombre);
        if (await _db.Roles.AnyAsync(x => x.EmpresaId == empresaId && x.Nombre.ToLower() == nombre.ToLower() && x.RolId != rolId, ct))
            throw ServiceException.Conflict("Ya existe un rol con ese nombre.");

        var antes = new { rol.Nombre, rol.Descripcion };
        rol.Nombre = nombre;
        rol.Descripcion = Helpers.Limpiar(r.Descripcion);
        _auditoria.Registrar("Roles", nameof(Rol), rolId.ToString(), "ActualizarRol", antes, new { rol.Nombre, rol.Descripcion });
        await _db.SaveSafeAsync(ct);
        return ToDto(rol);
    }

    public async Task<RolDto> CambiarEstadoRolAsync(Guid rolId, bool estado, CancellationToken ct)
    {
        var rol = await ObtenerRolAsync(rolId, true, ct);
        if (rol.EsSistema) throw ServiceException.Forbidden("Los roles de sistema no se pueden desactivar.");
        if (rol.Estado == estado) return ToDto(rol);

        rol.Estado = estado;
        _auditoria.Registrar("Roles", nameof(Rol), rolId.ToString(), estado ? "ActivarRol" : "DesactivarRol",
            new { Estado = !estado }, new { Estado = estado });
        await _db.SaveSafeAsync(ct);
        return ToDto(rol);
    }

    // ---------------- Asignación a usuarios ----------------
    public async Task<IReadOnlyList<RolDto>> ObtenerRolesUsuarioAsync(Guid usuarioId, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        await AsegurarUsuarioAsync(usuarioId, empresaId, ct);
        return await _db.UsuarioRoles.AsNoTracking().Where(ur => ur.EmpresaId == empresaId && ur.UsuarioId == usuarioId)
            .Select(ur => new RolDto { RolId = ur.Rol.RolId, Nombre = ur.Rol.Nombre, Descripcion = ur.Rol.Descripcion, EsSistema = ur.Rol.EsSistema, Estado = ur.Rol.Estado })
            .ToListAsync(ct);
    }

    public async Task AsignarRolAsync(Guid usuarioId, Guid rolId, CancellationToken ct)
    {
        var actorId = _current.RequireUsuarioId();
        var empresaId = _current.RequireEmpresaId();
        if (usuarioId == actorId) throw ServiceException.Forbidden("No puede modificar sus propios roles.");

        await AsegurarUsuarioAsync(usuarioId, empresaId, ct);
        var rol = await ObtenerRolAsync(rolId, false, ct);
        if (!rol.Estado) throw ServiceException.Validation("El rol está inactivo.");
        await AsegurarPuedeGestionarRolAsync(rol, actorId, empresaId, ct);

        if (await _db.UsuarioRoles.AnyAsync(ur => ur.UsuarioId == usuarioId && ur.RolId == rolId, ct))
            throw ServiceException.Conflict("El usuario ya tiene ese rol.");

        _db.UsuarioRoles.Add(new UsuarioRol
        {
            EmpresaId = empresaId, UsuarioId = usuarioId, RolId = rolId,
            AsignadoUtc = _clock.GetUtcNow().UtcDateTime, AsignadoPorUsuarioId = actorId
        });
        _auditoria.Registrar("Roles", nameof(UsuarioRol), usuarioId.ToString(), "AsignarRol", null, new { rolId, rol = rol.Nombre });
        await _db.SaveSafeAsync(ct);
    }

    public async Task RetirarRolAsync(Guid usuarioId, Guid rolId, CancellationToken ct)
    {
        var actorId = _current.RequireUsuarioId();
        var empresaId = _current.RequireEmpresaId();
        if (usuarioId == actorId) throw ServiceException.Forbidden("No puede modificar sus propios roles.");

        var rol = await ObtenerRolAsync(rolId, false, ct);
        await AsegurarPuedeGestionarRolAsync(rol, actorId, empresaId, ct);

        var fila = await _db.UsuarioRoles.FirstOrDefaultAsync(ur => ur.EmpresaId == empresaId && ur.UsuarioId == usuarioId && ur.RolId == rolId, ct)
            ?? throw ServiceException.NotFound("El usuario no tiene ese rol.");

        if (rol.Nombre == NombresRol.Administrador && rol.EsSistema)
        {
            var otrosAdmins = await _db.UsuarioRoles.CountAsync(ur => ur.EmpresaId == empresaId && ur.RolId == rolId
                && ur.UsuarioId != usuarioId && ur.Usuario.Estado == EstadoUsuario.Activo, ct);
            if (otrosAdmins == 0) throw ServiceException.Conflict("No se puede retirar el rol al último administrador activo.");
        }

        _db.UsuarioRoles.Remove(fila);
        _auditoria.Registrar("Roles", nameof(UsuarioRol), usuarioId.ToString(), "RetirarRol", new { rolId, rol = rol.Nombre }, null);
        await _db.SaveSafeAsync(ct);
    }

    /// <summary>Anti-elevación: solo se puede gestionar un rol cuyos permisos se poseen.</summary>
    internal async Task AsegurarPuedeGestionarRolAsync(Rol rol, Guid actorId, Guid empresaId, CancellationToken ct)
    {
        var propios = await _permisos.ObtenerPermisosAsync(empresaId, actorId, ct);
        var delRol = await CodigosDeRolAsync(empresaId, rol.RolId, ct);
        if (delRol.Any(c => !propios.Contains(c)))
            throw ServiceException.Forbidden("No puede gestionar un rol con permisos que usted no posee.");
    }

    private static bool EsRolProtegido(Rol rol) =>
        rol.EsSistema && (rol.Nombre == NombresRol.Administrador || rol.Nombre == NombresRol.AdministradorPlataforma);

    private static string ValidarNombre(string nombre)
    {
        var n = nombre.Trim();
        if (n.Length < 2) throw ServiceException.Validation("El nombre del rol debe tener al menos 2 caracteres.");
        if (NombresRol.Iniciales.Append(NombresRol.AdministradorPlataforma).Any(r => string.Equals(r, n, StringComparison.OrdinalIgnoreCase)))
            throw ServiceException.Validation("Ese nombre está reservado para un rol de sistema.");
        return n;
    }

    private static RolDto ToDto(Rol r) => new()
    {
        RolId = r.RolId, Nombre = r.Nombre, Descripcion = r.Descripcion, EsSistema = r.EsSistema, Estado = r.Estado
    };

    private Task<bool> EsPlataformaAsync(CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        return _db.Empresas.AnyAsync(e => e.EmpresaId == empresaId && e.EsPlataforma, ct);
    }

    private async Task<Rol> ObtenerRolAsync(Guid rolId, bool tracking, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var q = tracking ? _db.Roles : _db.Roles.AsNoTracking();
        return await q.FirstOrDefaultAsync(r => r.RolId == rolId && r.EmpresaId == empresaId, ct)
            ?? throw ServiceException.NotFound("Rol no encontrado.");
    }

    private async Task AsegurarUsuarioAsync(Guid usuarioId, Guid empresaId, CancellationToken ct)
    {
        if (!await _db.Usuarios.AnyAsync(u => u.UsuarioId == usuarioId && u.EmpresaId == empresaId, ct))
            throw ServiceException.NotFound("Usuario no encontrado.");
    }

    private async Task<List<string>> CodigosDeRolAsync(Guid empresaId, Guid rolId, CancellationToken ct) =>
        await _db.RolPermisos.AsNoTracking().Where(rp => rp.EmpresaId == empresaId && rp.RolId == rolId && rp.Permitido)
            .Select(rp => rp.Permiso.Codigo).OrderBy(c => c).ToListAsync(ct);
}
