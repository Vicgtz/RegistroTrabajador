using Microsoft.EntityFrameworkCore;
using RegistroTrabajo.Data;
using RegistroTrabajo.Data.Entities;
using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public interface ITiposPermisoService
{
    Task<IReadOnlyList<TipoPermisoDto>> ListarAsync(bool soloActivos, CancellationToken ct);
    Task<TipoPermisoDto> CrearAsync(GuardarTipoPermisoRequest request, CancellationToken ct);
    Task<TipoPermisoDto> ActualizarAsync(Guid tipoId, GuardarTipoPermisoRequest request, CancellationToken ct);
    Task<TipoPermisoDto> CambiarEstadoAsync(Guid tipoId, bool activo, CancellationToken ct);
    Task<ReglaPermisoDto> CrearReglaAsync(Guid tipoId, GuardarReglaPermisoRequest request, CancellationToken ct);
    Task<ReglaPermisoDto> ActualizarReglaAsync(Guid tipoId, Guid reglaId, GuardarReglaPermisoRequest request, CancellationToken ct);
    /// <summary>Crea los tipos iniciales que falten (vacaciones, personales, citas médicas, incapacidad, asuntos familiares, por horas), sin reglas.</summary>
    Task<IReadOnlyList<TipoPermisoDto>> SembrarInicialesAsync(CancellationToken ct);
}

/// <summary>Catálogo de tipos de permiso por empresa y sus reglas (duración, límites, saldo anual, vigencia).</summary>
public class TiposPermisoService : ITiposPermisoService
{
    private static readonly (string Nombre, string Descripcion, byte Unidad, bool Aprobacion, bool Comprobante, bool Fraccionar)[] Iniciales =
    {
        ("Vacaciones", "Días de descanso anual", UnidadPermiso.Dias, true, false, false),
        ("Permiso personal", "Asuntos personales", UnidadPermiso.Dias, true, false, true),
        ("Cita médica", "Consulta o estudio médico", UnidadPermiso.Horas, true, false, false),
        ("Incapacidad", "Incapacidad médica", UnidadPermiso.Dias, true, true, false),
        ("Asuntos familiares", "Eventos o situaciones familiares", UnidadPermiso.Dias, true, false, true),
        ("Permiso por horas", "Ausencia de unas horas dentro de la jornada", UnidadPermiso.Horas, true, false, false)
    };

    private readonly RegistroTrabajoDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IAuditoriaService _auditoria;

    public TiposPermisoService(RegistroTrabajoDbContext db, ICurrentUser current, IAuditoriaService auditoria)
    {
        _db = db; _current = current; _auditoria = auditoria;
    }

    public async Task<IReadOnlyList<TipoPermisoDto>> ListarAsync(bool soloActivos, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var q = _db.TiposPermiso.AsNoTracking().Where(t => t.EmpresaId == empresaId);
        if (soloActivos) q = q.Where(t => t.Estado);
        var tipos = await q.OrderBy(t => t.Nombre).ToListAsync(ct);
        var reglas = await _db.ReglasPermiso.AsNoTracking().Where(r => r.EmpresaId == empresaId).ToListAsync(ct);
        return tipos.Select(t => ToDto(t, reglas.Where(r => r.TipoPermisoId == t.TipoPermisoId))).ToList();
    }

    public async Task<TipoPermisoDto> CrearAsync(GuardarTipoPermisoRequest r, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var nombre = Nombre(r);
        await AsegurarNombreLibreAsync(empresaId, nombre, null, ct);
        var t = new TipoPermiso { TipoPermisoId = Guid.NewGuid(), EmpresaId = empresaId, Estado = true };
        Aplicar(t, r, nombre);
        _db.TiposPermiso.Add(t);
        _auditoria.Registrar("Permisos", nameof(TipoPermiso), t.TipoPermisoId.ToString(), "CrearTipoPermiso", null, Resumen(t));
        await _db.SaveSafeAsync(ct);
        return ToDto(t, Array.Empty<ReglaPermiso>());
    }

    public async Task<TipoPermisoDto> ActualizarAsync(Guid tipoId, GuardarTipoPermisoRequest r, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var t = await CargarAsync(empresaId, tipoId, ct);
        var nombre = Nombre(r);
        await AsegurarNombreLibreAsync(empresaId, nombre, tipoId, ct);
        if (r.UnidadBase != t.UnidadBase && await _db.SolicitudesPermiso.AnyAsync(s => s.EmpresaId == empresaId && s.TipoPermisoId == tipoId, ct))
            throw ServiceException.Conflict("No se puede cambiar la unidad de un tipo que ya tiene solicitudes.");
        var antes = Resumen(t);
        Aplicar(t, r, nombre);
        _auditoria.Registrar("Permisos", nameof(TipoPermiso), tipoId.ToString(), "ActualizarTipoPermiso", antes, Resumen(t));
        await _db.SaveSafeAsync(ct);
        return await DtoAsync(t, ct);
    }

    public async Task<TipoPermisoDto> CambiarEstadoAsync(Guid tipoId, bool activo, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var t = await CargarAsync(empresaId, tipoId, ct);
        if (t.Estado != activo)
        {
            t.Estado = activo;
            _auditoria.Registrar("Permisos", nameof(TipoPermiso), tipoId.ToString(), activo ? "ActivarTipoPermiso" : "DesactivarTipoPermiso",
                new { Estado = !activo }, new { Estado = activo });
            await _db.SaveSafeAsync(ct);
        }
        return await DtoAsync(t, ct);
    }

    public async Task<ReglaPermisoDto> CrearReglaAsync(Guid tipoId, GuardarReglaPermisoRequest r, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        await CargarAsync(empresaId, tipoId, ct);
        await ValidarReglaAsync(empresaId, tipoId, null, r, ct);
        var regla = new ReglaPermiso { ReglaPermisoId = Guid.NewGuid(), EmpresaId = empresaId, TipoPermisoId = tipoId };
        Aplicar(regla, r);
        _db.ReglasPermiso.Add(regla);
        _auditoria.Registrar("Permisos", nameof(ReglaPermiso), regla.ReglaPermisoId.ToString(), "CrearReglaPermiso", null, new { tipoId, Regla = ToDto(regla) });
        await _db.SaveSafeAsync(ct);
        return ToDto(regla);
    }

    public async Task<ReglaPermisoDto> ActualizarReglaAsync(Guid tipoId, Guid reglaId, GuardarReglaPermisoRequest r, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var regla = await _db.ReglasPermiso.FirstOrDefaultAsync(x => x.ReglaPermisoId == reglaId && x.EmpresaId == empresaId && x.TipoPermisoId == tipoId, ct)
            ?? throw ServiceException.NotFound("Regla no encontrada.");
        await ValidarReglaAsync(empresaId, tipoId, reglaId, r, ct);
        var antes = ToDto(regla);
        Aplicar(regla, r);
        _auditoria.Registrar("Permisos", nameof(ReglaPermiso), reglaId.ToString(), "ActualizarReglaPermiso", antes, ToDto(regla));
        await _db.SaveSafeAsync(ct);
        return ToDto(regla);
    }

    public async Task<IReadOnlyList<TipoPermisoDto>> SembrarInicialesAsync(CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var existentes = await _db.TiposPermiso.Where(t => t.EmpresaId == empresaId).Select(t => t.Nombre).ToListAsync(ct);
        var creados = 0;
        foreach (var i in Iniciales)
        {
            if (existentes.Any(n => string.Equals(n, i.Nombre, StringComparison.OrdinalIgnoreCase))) continue;
            _db.TiposPermiso.Add(new TipoPermiso
            {
                TipoPermisoId = Guid.NewGuid(), EmpresaId = empresaId, Nombre = i.Nombre, Descripcion = i.Descripcion, UnidadBase = i.Unidad,
                RequiereAprobacion = i.Aprobacion, RequiereComprobante = i.Comprobante, PermiteFraccionar = i.Fraccionar, Estado = true
            });
            creados++;
        }
        if (creados > 0)
        {
            _auditoria.Registrar("Permisos", nameof(TipoPermiso), null, "SembrarTiposIniciales", null, new { creados });
            await _db.SaveSafeAsync(ct);
        }
        return await ListarAsync(false, ct);
    }

    // ---- apoyo ----
    private async Task<TipoPermiso> CargarAsync(Guid empresaId, Guid tipoId, CancellationToken ct) =>
        await _db.TiposPermiso.FirstOrDefaultAsync(t => t.TipoPermisoId == tipoId && t.EmpresaId == empresaId, ct)
        ?? throw ServiceException.NotFound("Tipo de permiso no encontrado.");

    private async Task<TipoPermisoDto> DtoAsync(TipoPermiso t, CancellationToken ct) =>
        ToDto(t, await _db.ReglasPermiso.AsNoTracking().Where(r => r.EmpresaId == t.EmpresaId && r.TipoPermisoId == t.TipoPermisoId).ToListAsync(ct));

    private static string Nombre(GuardarTipoPermisoRequest r)
    {
        var n = (r.Nombre ?? "").Trim();
        if (n.Length < 2) throw ServiceException.Validation("El nombre del tipo de permiso es obligatorio.");
        if (r.UnidadBase is not (UnidadPermiso.Dias or UnidadPermiso.Horas)) throw ServiceException.Validation("La unidad debe ser días u horas.");
        return n;
    }

    private async Task AsegurarNombreLibreAsync(Guid empresaId, string nombre, Guid? excluir, CancellationToken ct)
    {
        var n = nombre.ToUpper();
        if (await _db.TiposPermiso.AnyAsync(t => t.EmpresaId == empresaId && t.Nombre.ToUpper() == n && t.TipoPermisoId != excluir, ct))
            throw ServiceException.Conflict("Ya existe un tipo de permiso con ese nombre.");
    }

    private async Task ValidarReglaAsync(Guid empresaId, Guid tipoId, Guid? excluir, GuardarReglaPermisoRequest r, CancellationToken ct)
    {
        if (r.CantidadAnual is < 0) throw ServiceException.Validation("La cantidad anual no puede ser negativa.");
        if (r.LimitePorSolicitud is <= 0) throw ServiceException.Validation("El límite por solicitud debe ser mayor que cero.");
        if (r.AntiguedadMinimaMeses is < 0) throw ServiceException.Validation("La antigüedad mínima no puede ser negativa.");
        if (r.ActivaDesde is { } d && r.ActivaHasta is { } h && h < d) throw ServiceException.Validation("La vigencia final no puede ser anterior a la inicial.");
        if (!r.Estado) return;

        // Dos reglas activas no pueden regir el mismo día para el mismo tipo (la vigente sería ambigua).
        var otras = await _db.ReglasPermiso.AsNoTracking()
            .Where(x => x.EmpresaId == empresaId && x.TipoPermisoId == tipoId && x.Estado && x.ReglaPermisoId != excluir).ToListAsync(ct);
        foreach (var o in otras)
        {
            var empiezaAntes = r.ActivaDesde is null || o.ActivaHasta is null || r.ActivaDesde <= o.ActivaHasta;
            var terminaDespues = r.ActivaHasta is null || o.ActivaDesde is null || r.ActivaHasta >= o.ActivaDesde;
            if (empiezaAntes && terminaDespues) throw ServiceException.Conflict("Ya existe una regla activa que se traslapa con esa vigencia.");
        }
    }

    private static void Aplicar(TipoPermiso t, GuardarTipoPermisoRequest r, string nombre)
    {
        t.Nombre = nombre;
        t.Descripcion = Helpers.Limpiar(r.Descripcion);
        t.UnidadBase = r.UnidadBase;
        t.RequiereAprobacion = r.RequiereAprobacion;
        t.RequiereComprobante = r.RequiereComprobante;
        t.PermiteFraccionar = r.PermiteFraccionar;
    }

    private static void Aplicar(ReglaPermiso x, GuardarReglaPermisoRequest r)
    {
        x.AntiguedadMinimaMeses = r.AntiguedadMinimaMeses;
        x.CantidadAnual = r.CantidadAnual;
        x.LimitePorSolicitud = r.LimitePorSolicitud;
        x.PermiteSaldoNegativo = r.PermiteSaldoNegativo;
        x.ReiniciaAnualmente = r.ReiniciaAnualmente;
        x.ActivaDesde = r.ActivaDesde;
        x.ActivaHasta = r.ActivaHasta;
        x.Estado = r.Estado;
    }

    private static object Resumen(TipoPermiso t) => new { t.Nombre, t.UnidadBase, t.RequiereAprobacion, t.RequiereComprobante, t.PermiteFraccionar, t.Estado };

    private static ReglaPermisoDto ToDto(ReglaPermiso r) => new()
    {
        ReglaPermisoId = r.ReglaPermisoId, TipoPermisoId = r.TipoPermisoId, AntiguedadMinimaMeses = r.AntiguedadMinimaMeses,
        CantidadAnual = r.CantidadAnual, LimitePorSolicitud = r.LimitePorSolicitud, PermiteSaldoNegativo = r.PermiteSaldoNegativo,
        ReiniciaAnualmente = r.ReiniciaAnualmente, ActivaDesde = r.ActivaDesde, ActivaHasta = r.ActivaHasta, Estado = r.Estado
    };

    private static TipoPermisoDto ToDto(TipoPermiso t, IEnumerable<ReglaPermiso> reglas) => new()
    {
        TipoPermisoId = t.TipoPermisoId, Nombre = t.Nombre, Descripcion = t.Descripcion, UnidadBase = t.UnidadBase,
        RequiereAprobacion = t.RequiereAprobacion, RequiereComprobante = t.RequiereComprobante, PermiteFraccionar = t.PermiteFraccionar,
        Estado = t.Estado, Reglas = reglas.OrderByDescending(r => r.ActivaDesde).Select(ToDto).ToList()
    };
}
