using System.Reflection;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using RegistroTrabajo.Data;
using RegistroTrabajo.Data.Entities;
using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public interface IPermisosEfectivosService
{
    Task<PermisosEfectivosDto> ObtenerAsync(Guid usuarioId, CancellationToken ct);
}

public class PermisosEfectivosService : IPermisosEfectivosService
{
    private readonly RegistroTrabajoDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IPermisoService _permisos;

    public PermisosEfectivosService(RegistroTrabajoDbContext db, ICurrentUser current, IPermisoService permisos)
    {
        _db = db; _current = current; _permisos = permisos;
    }

    public async Task<PermisosEfectivosDto> ObtenerAsync(Guid usuarioId, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.UsuariosConsultar, ct);
        await _permisos.AsegurarPermisoAsync(Permisos.EmpresaVerPermisosEfectivos, ct);
        var empresaId = _current.RequireEmpresaId();
        // Un usuario de otra empresa responde 404, igual que si no existiera.
        var u = await _db.Usuarios.AsNoTracking().FirstOrDefaultAsync(x => x.UsuarioId == usuarioId && x.EmpresaId == empresaId, ct)
            ?? throw ServiceException.NotFound("Usuario no encontrado.");
        var roles = await _db.UsuarioRoles.AsNoTracking().Where(x => x.EmpresaId == empresaId && x.UsuarioId == usuarioId)
            .Join(_db.Roles, ur => ur.RolId, r => r.RolId, (ur, r) => r.Nombre).OrderBy(n => n).ToListAsync(ct);
        var codigos = await _permisos.ObtenerPermisosAsync(empresaId, usuarioId, ct);
        var catalogo = await _db.PermisosSistema.AsNoTracking().Where(p => codigos.Contains(p.Codigo)).ToListAsync(ct);
        return new PermisosEfectivosDto
        {
            UsuarioId = u.UsuarioId, Usuario = $"{u.Nombre} {u.Apellido}", Estado = u.Estado, Roles = roles,
            Permisos = catalogo.OrderBy(p => p.Modulo).ThenBy(p => p.Codigo)
                .Select(p => new PermisoEfectivoDto { Codigo = p.Codigo, Modulo = p.Modulo, Descripcion = p.Descripcion }).ToList()
        };
    }
}
