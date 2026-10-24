using Microsoft.EntityFrameworkCore;
using RegistroTrabajo.Data;
using RegistroTrabajo.Data.Entities;
using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

/// <summary>
/// Administración de empresas. Es la única zona del sistema que opera sobre varias empresas, y solo la puede usar
/// un usuario de la empresa de plataforma (se comprueba además del permiso plataforma.*).
/// </summary>
public interface IPlataformaService
{
    Task<PagedResult<EmpresaPlataformaDto>> ListarEmpresasAsync(PageQuery query, string? buscar, CancellationToken ct);
    Task<EmpresaPlataformaDto> ObtenerEmpresaAsync(Guid empresaId, CancellationToken ct);
    Task<EmpresaPlataformaDto> CrearEmpresaAsync(CrearEmpresaPlataformaRequest request, CancellationToken ct);
    Task<EmpresaPlataformaDto> CambiarEstadoAsync(Guid empresaId, byte estado, CancellationToken ct);
}

public class PlataformaService : IPlataformaService
{
    private readonly RegistroTrabajoDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IPasswordService _passwords;
    private readonly IAuditoriaService _auditoria;
    private readonly TimeProvider _clock;

    public PlataformaService(RegistroTrabajoDbContext db, ICurrentUser current, IPasswordService passwords,
        IAuditoriaService auditoria, TimeProvider clock)
    {
        _db = db; _current = current; _passwords = passwords; _auditoria = auditoria; _clock = clock;
    }

    public async Task<PagedResult<EmpresaPlataformaDto>> ListarEmpresasAsync(PageQuery query, string? buscar, CancellationToken ct)
    {
        await AsegurarPlataformaAsync(ct);
        var q = _db.Empresas.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var s = buscar.Trim();
            q = q.Where(e => e.Nombre.Contains(s) || (e.NombreComercial != null && e.NombreComercial.Contains(s)) || (e.RFC != null && e.RFC.Contains(s)));
        }
        return await q.OrderBy(e => e.Nombre).Select(Proyeccion(_db)).ToPagedAsync(query, ct);
    }

    public async Task<EmpresaPlataformaDto> ObtenerEmpresaAsync(Guid empresaId, CancellationToken ct)
    {
        await AsegurarPlataformaAsync(ct);
        return await _db.Empresas.AsNoTracking().Where(e => e.EmpresaId == empresaId).Select(Proyeccion(_db)).FirstOrDefaultAsync(ct)
            ?? throw ServiceException.NotFound("Empresa no encontrada.");
    }

    public async Task<EmpresaPlataformaDto> CrearEmpresaAsync(CrearEmpresaPlataformaRequest r, CancellationToken ct)
    {
        await AsegurarPlataformaAsync(ct);
        _passwords.ValidarPolitica(r.AdminContrasena);

        var correo = r.AdminCorreo.Trim();
        var normalizado = Helpers.NormalizarCorreo(correo);
        if (correo.Length < 4) throw ServiceException.Validation("Correo no válido.");
        if (await _db.Usuarios.AnyAsync(u => u.CorreoNormalizado == normalizado, ct))
            throw ServiceException.Conflict("El correo del administrador ya está registrado.");

        var rfc = Helpers.Limpiar(r.RFC)?.ToUpperInvariant();
        if (rfc is not null && await _db.Empresas.AnyAsync(e => e.RFC == rfc, ct))
            throw ServiceException.Conflict("El RFC ya está registrado.");

        var catalogo = await _db.PermisosSistema.ToListAsync(ct);
        var (empresa, admin) = EmpresaFactory.Agregar(_db, _passwords, _clock.GetUtcNow().UtcDateTime, catalogo,
            new DatosEmpresa(r.Nombre, r.NombreComercial, rfc, r.CorreoContacto, false),
            new DatosAdmin(correo, r.AdminNombre, r.AdminApellido, r.AdminContrasena, true), false);

        _auditoria.Registrar("Plataforma", nameof(Empresa), empresa.EmpresaId.ToString(), "CrearEmpresa", null,
            new { empresa.Nombre, empresa.RFC, administrador = admin.Correo }, empresaId: empresa.EmpresaId);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await _db.SaveSafeAsync(ct);
        await tx.CommitAsync(ct);
        return await ObtenerEmpresaAsync(empresa.EmpresaId, ct);
    }

    public async Task<EmpresaPlataformaDto> CambiarEstadoAsync(Guid empresaId, byte estado, CancellationToken ct)
    {
        await AsegurarPlataformaAsync(ct);
        if (estado is < EstadoEmpresa.Activa or > EstadoEmpresa.Inactiva)
            throw ServiceException.Validation("Estado no válido.");

        var e = await _db.Empresas.FirstOrDefaultAsync(x => x.EmpresaId == empresaId, ct)
            ?? throw ServiceException.NotFound("Empresa no encontrada.");
        if (e.EsPlataforma && estado != EstadoEmpresa.Activa)
            throw ServiceException.Forbidden("La empresa de plataforma no se puede suspender ni desactivar.");
        if (e.Estado == estado) return await ObtenerEmpresaAsync(empresaId, ct);

        var now = _clock.GetUtcNow().UtcDateTime;
        var anterior = e.Estado;
        e.Estado = estado;
        e.FechaActualizacionUtc = now;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        if (estado != EstadoEmpresa.Activa)
        {
            // Una empresa suspendida o inactiva pierde de inmediato todas sus sesiones.
            var activas = await _db.SesionesUsuario.Where(s => s.EmpresaId == empresaId && s.RevocadaUtc == null).ToListAsync(ct);
            foreach (var s in activas) SesionService.Revocar(s, "Empresa suspendida o desactivada", now);
        }
        _auditoria.Registrar("Plataforma", nameof(Empresa), empresaId.ToString(), "CambiarEstadoEmpresa",
            new { Estado = anterior }, new { Estado = estado }, empresaId: empresaId);
        await _db.SaveSafeAsync(ct);
        await tx.CommitAsync(ct);
        return await ObtenerEmpresaAsync(empresaId, ct);
    }

    private async Task AsegurarPlataformaAsync(CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        if (!await _db.Empresas.AnyAsync(e => e.EmpresaId == empresaId && e.EsPlataforma && e.Estado == EstadoEmpresa.Activa, ct))
            throw ServiceException.Forbidden("No tiene permiso para esta operación.");
    }

    private static System.Linq.Expressions.Expression<Func<Empresa, EmpresaPlataformaDto>> Proyeccion(RegistroTrabajoDbContext db) =>
        e => new EmpresaPlataformaDto
        {
            EmpresaId = e.EmpresaId, Nombre = e.Nombre, NombreComercial = e.NombreComercial, RFC = e.RFC,
            CorreoContacto = e.CorreoContacto, Estado = e.Estado, EsPlataforma = e.EsPlataforma,
            FechaCreacionUtc = e.FechaCreacionUtc, UsuariosTotal = db.Usuarios.Count(u => u.EmpresaId == e.EmpresaId)
        };
}
