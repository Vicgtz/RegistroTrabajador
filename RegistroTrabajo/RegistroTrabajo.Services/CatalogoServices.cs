using Microsoft.EntityFrameworkCore;
using RegistroTrabajo.Data;
using RegistroTrabajo.Data.Entities;
using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public interface IDepartamentoService
{
    Task<IReadOnlyList<CatalogoItemDto>> ListarAsync(bool soloActivos, CancellationToken ct);
    Task<CatalogoItemDto> CrearAsync(GuardarCatalogoRequest request, CancellationToken ct);
    Task<CatalogoItemDto> ActualizarAsync(Guid id, GuardarCatalogoRequest request, CancellationToken ct);
    Task<CatalogoItemDto> CambiarEstadoAsync(Guid id, bool estado, CancellationToken ct);
}

public interface IPuestoService
{
    Task<IReadOnlyList<CatalogoItemDto>> ListarAsync(bool soloActivos, CancellationToken ct);
    Task<CatalogoItemDto> CrearAsync(GuardarCatalogoRequest request, CancellationToken ct);
    Task<CatalogoItemDto> ActualizarAsync(Guid id, GuardarCatalogoRequest request, CancellationToken ct);
    Task<CatalogoItemDto> CambiarEstadoAsync(Guid id, bool estado, CancellationToken ct);
}

/// <summary>Catálogo de departamentos por empresa. Nunca se borran: se desactivan y conservan sus referencias históricas.</summary>
public class DepartamentoService : IDepartamentoService
{
    private readonly RegistroTrabajoDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IAuditoriaService _auditoria;

    public DepartamentoService(RegistroTrabajoDbContext db, ICurrentUser current, IAuditoriaService auditoria)
    {
        _db = db; _current = current; _auditoria = auditoria;
    }

    public async Task<IReadOnlyList<CatalogoItemDto>> ListarAsync(bool soloActivos, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        return await _db.Departamentos.AsNoTracking()
            .Where(d => d.EmpresaId == empresaId && (!soloActivos || d.Estado))
            .OrderBy(d => d.Nombre)
            .Select(d => new CatalogoItemDto
            {
                Id = d.DepartamentoId, Nombre = d.Nombre, Descripcion = d.Descripcion, Estado = d.Estado,
                Trabajadores = d.Trabajadores.Count(t => t.EstadoLaboral != EstadoLaboral.Baja)
            }).ToListAsync(ct);
    }

    public async Task<CatalogoItemDto> CrearAsync(GuardarCatalogoRequest r, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var nombre = CatalogoHelper.ValidarNombre(r.Nombre, "departamento");
        if (await _db.Departamentos.AnyAsync(d => d.EmpresaId == empresaId && d.Nombre.ToLower() == nombre.ToLower(), ct))
            throw ServiceException.Conflict("Ya existe un departamento con ese nombre.");

        var d = new Departamento
        {
            DepartamentoId = Guid.NewGuid(), EmpresaId = empresaId, Nombre = nombre,
            Descripcion = Helpers.Limpiar(r.Descripcion), Estado = true
        };
        _db.Departamentos.Add(d);
        _auditoria.Registrar("Departamentos", nameof(Departamento), d.DepartamentoId.ToString(), "Crear", null, new { d.Nombre, d.Descripcion });
        await _db.SaveSafeAsync(ct);
        return ToDto(d);
    }

    public async Task<CatalogoItemDto> ActualizarAsync(Guid id, GuardarCatalogoRequest r, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var d = await CargarAsync(id, ct);
        var nombre = CatalogoHelper.ValidarNombre(r.Nombre, "departamento");
        if (await _db.Departamentos.AnyAsync(x => x.EmpresaId == empresaId && x.DepartamentoId != id && x.Nombre.ToLower() == nombre.ToLower(), ct))
            throw ServiceException.Conflict("Ya existe un departamento con ese nombre.");

        var antes = new { d.Nombre, d.Descripcion };
        d.Nombre = nombre;
        d.Descripcion = Helpers.Limpiar(r.Descripcion);
        _auditoria.Registrar("Departamentos", nameof(Departamento), id.ToString(), "Actualizar", antes, new { d.Nombre, d.Descripcion });
        await _db.SaveSafeAsync(ct);
        return ToDto(d);
    }

    public async Task<CatalogoItemDto> CambiarEstadoAsync(Guid id, bool estado, CancellationToken ct)
    {
        var d = await CargarAsync(id, ct);
        if (d.Estado == estado) return ToDto(d);
        d.Estado = estado;
        _auditoria.Registrar("Departamentos", nameof(Departamento), id.ToString(), estado ? "Activar" : "Desactivar",
            new { Estado = !estado }, new { Estado = estado });
        await _db.SaveSafeAsync(ct);
        return ToDto(d);
    }

    private async Task<Departamento> CargarAsync(Guid id, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        return await _db.Departamentos.FirstOrDefaultAsync(d => d.DepartamentoId == id && d.EmpresaId == empresaId, ct)
            ?? throw ServiceException.NotFound("Departamento no encontrado.");
    }

    private static CatalogoItemDto ToDto(Departamento d) => new()
    {
        Id = d.DepartamentoId, Nombre = d.Nombre, Descripcion = d.Descripcion, Estado = d.Estado
    };
}

/// <summary>Catálogo de puestos por empresa (misma política que los departamentos).</summary>
public class PuestoService : IPuestoService
{
    private readonly RegistroTrabajoDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IAuditoriaService _auditoria;

    public PuestoService(RegistroTrabajoDbContext db, ICurrentUser current, IAuditoriaService auditoria)
    {
        _db = db; _current = current; _auditoria = auditoria;
    }

    public async Task<IReadOnlyList<CatalogoItemDto>> ListarAsync(bool soloActivos, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        return await _db.Puestos.AsNoTracking()
            .Where(p => p.EmpresaId == empresaId && (!soloActivos || p.Estado))
            .OrderBy(p => p.Nombre)
            .Select(p => new CatalogoItemDto
            {
                Id = p.PuestoId, Nombre = p.Nombre, Descripcion = p.Descripcion, Estado = p.Estado,
                Trabajadores = p.Trabajadores.Count(t => t.EstadoLaboral != EstadoLaboral.Baja)
            }).ToListAsync(ct);
    }

    public async Task<CatalogoItemDto> CrearAsync(GuardarCatalogoRequest r, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var nombre = CatalogoHelper.ValidarNombre(r.Nombre, "puesto");
        if (await _db.Puestos.AnyAsync(p => p.EmpresaId == empresaId && p.Nombre.ToLower() == nombre.ToLower(), ct))
            throw ServiceException.Conflict("Ya existe un puesto con ese nombre.");

        var p = new Puesto
        {
            PuestoId = Guid.NewGuid(), EmpresaId = empresaId, Nombre = nombre,
            Descripcion = Helpers.Limpiar(r.Descripcion), Estado = true
        };
        _db.Puestos.Add(p);
        _auditoria.Registrar("Puestos", nameof(Puesto), p.PuestoId.ToString(), "Crear", null, new { p.Nombre, p.Descripcion });
        await _db.SaveSafeAsync(ct);
        return ToDto(p);
    }

    public async Task<CatalogoItemDto> ActualizarAsync(Guid id, GuardarCatalogoRequest r, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var p = await CargarAsync(id, ct);
        var nombre = CatalogoHelper.ValidarNombre(r.Nombre, "puesto");
        if (await _db.Puestos.AnyAsync(x => x.EmpresaId == empresaId && x.PuestoId != id && x.Nombre.ToLower() == nombre.ToLower(), ct))
            throw ServiceException.Conflict("Ya existe un puesto con ese nombre.");

        var antes = new { p.Nombre, p.Descripcion };
        p.Nombre = nombre;
        p.Descripcion = Helpers.Limpiar(r.Descripcion);
        _auditoria.Registrar("Puestos", nameof(Puesto), id.ToString(), "Actualizar", antes, new { p.Nombre, p.Descripcion });
        await _db.SaveSafeAsync(ct);
        return ToDto(p);
    }

    public async Task<CatalogoItemDto> CambiarEstadoAsync(Guid id, bool estado, CancellationToken ct)
    {
        var p = await CargarAsync(id, ct);
        if (p.Estado == estado) return ToDto(p);
        p.Estado = estado;
        _auditoria.Registrar("Puestos", nameof(Puesto), id.ToString(), estado ? "Activar" : "Desactivar",
            new { Estado = !estado }, new { Estado = estado });
        await _db.SaveSafeAsync(ct);
        return ToDto(p);
    }

    private async Task<Puesto> CargarAsync(Guid id, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        return await _db.Puestos.FirstOrDefaultAsync(p => p.PuestoId == id && p.EmpresaId == empresaId, ct)
            ?? throw ServiceException.NotFound("Puesto no encontrado.");
    }

    private static CatalogoItemDto ToDto(Puesto p) => new()
    {
        Id = p.PuestoId, Nombre = p.Nombre, Descripcion = p.Descripcion, Estado = p.Estado
    };
}

internal static class CatalogoHelper
{
    public static string ValidarNombre(string nombre, string que)
    {
        var n = (nombre ?? "").Trim();
        if (n.Length < 2) throw ServiceException.Validation($"El nombre del {que} debe tener al menos 2 caracteres.");
        return n;
    }
}
