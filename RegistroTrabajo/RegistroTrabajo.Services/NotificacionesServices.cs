using Microsoft.EntityFrameworkCore;
using RegistroTrabajo.Data;
using RegistroTrabajo.Data.Entities;
using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public interface INotificacionesService
{
    /// <summary>Bandeja paginada del usuario conectado, filtrable por estado, categoría, fechas y texto. El historial son las archivadas.</summary>
    Task<PagedResult<NotificacionDto>> ListarAsync(NotificacionFiltro filtro, CancellationToken ct);
    Task<int> ContarNoLeidasAsync(CancellationToken ct);
    Task<ResumenNotificacionesDto> ResumenAsync(CancellationToken ct);
    Task MarcarLeidaAsync(Guid notificacionId, CancellationToken ct);
    Task MarcarTodasLeidasAsync(CancellationToken ct);
    /// <summary>Oculta la notificación de la bandeja principal; nunca toca el permiso, tarea o proyecto que la originó.</summary>
    Task ArchivarAsync(Guid notificacionId, CancellationToken ct);
    Task DesarchivarAsync(Guid notificacionId, CancellationToken ct);
    /// <summary>
    /// Ruta del recurso relacionado. Valida el tipo de entidad contra una lista cerrada y vuelve a comprobar el acceso actual del usuario:
    /// si ya no lo tiene responde 404 sin revelar datos del recurso.
    /// </summary>
    Task<DestinoNotificacionDto> ResolverDestinoAsync(Guid notificacionId, CancellationToken ct);
}

public interface IPreferenciasNotificacionService
{
    Task<IReadOnlyList<PreferenciaCategoriaDto>> ListarAsync(CancellationToken ct);
    /// <summary>Las categorías cuyos avisos son todos obligatorios no se pueden desactivar; en las mixtas solo se silencian los opcionales.</summary>
    Task<IReadOnlyList<PreferenciaCategoriaDto>> GuardarAsync(GuardarPreferenciasRequest request, CancellationToken ct);
}

internal static class PreferenciasHelper
{
    /// <summary>Claves (tipo o categoría) que el usuario marcó como prioritarias.</summary>
    public static async Task<HashSet<string>> PrioritariasAsync(RegistroTrabajoDbContext db, Guid empresaId, Guid usuarioId, CancellationToken ct) =>
        (await db.PreferenciasNotificacion.AsNoTracking()
            .Where(p => p.EmpresaId == empresaId && p.UsuarioId == usuarioId && p.Prioritaria)
            .Select(p => p.TipoNotificacion).ToListAsync(ct)).ToHashSet();

    public static bool EsPrioritaria(HashSet<string> claves, string tipo) =>
        claves.Contains(tipo) || claves.Contains(CatalogoNotificaciones.CategoriaDe(tipo));
}

public class NotificacionesService : INotificacionesService
{
    private readonly RegistroTrabajoDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IPermisoService _permisos;
    private readonly TimeProvider _clock;

    public NotificacionesService(RegistroTrabajoDbContext db, ICurrentUser current, IPermisoService permisos, TimeProvider clock)
    {
        _db = db; _current = current; _permisos = permisos; _clock = clock;
    }

    // Cada usuario solo ve y modifica su propia bandeja; una notificación ajena responde 404.
    private IQueryable<Notificacion> Propias(bool tracking)
    {
        var empresaId = _current.RequireEmpresaId();
        var usuarioId = _current.RequireUsuarioId();
        var q = tracking ? _db.Notificaciones : _db.Notificaciones.AsNoTracking();
        return q.Where(n => n.EmpresaId == empresaId && n.UsuarioDestinoId == usuarioId);
    }

    private async Task<TimeZoneInfo> ZonaAsync(Guid empresaId, CancellationToken ct) =>
        AsistenciaHelper.Zona(await _db.ConfiguracionEmpresa.AsNoTracking().Where(c => c.EmpresaId == empresaId).Select(c => c.ZonaHorariaId).FirstOrDefaultAsync(ct));

    private static DateTime InicioDelDiaUtc(DateOnly d, TimeZoneInfo zona)
    {
        var local = DateTime.SpecifyKind(d.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        if (zona.IsInvalidTime(local)) local = local.AddHours(1);
        return TimeZoneInfo.ConvertTimeToUtc(local, zona);
    }

    private static EstadoNotificacionFiltro EstadoEfectivo(NotificacionFiltro f) =>
        f.Estado != EstadoNotificacionFiltro.Bandeja ? f.Estado
        : f.SoloNoLeidas ? EstadoNotificacionFiltro.NoLeidas
        : f.IncluirArchivadas ? EstadoNotificacionFiltro.Todas
        : EstadoNotificacionFiltro.Bandeja;

    public async Task<PagedResult<NotificacionDto>> ListarAsync(NotificacionFiltro f, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.NotificacionesConsultar, ct);
        var empresaId = _current.RequireEmpresaId();
        var q = Propias(false);

        q = EstadoEfectivo(f) switch
        {
            EstadoNotificacionFiltro.NoLeidas => q.Where(n => n.LeidaUtc == null && n.ArchivadaUtc == null),
            EstadoNotificacionFiltro.Leidas => q.Where(n => n.LeidaUtc != null && n.ArchivadaUtc == null),
            EstadoNotificacionFiltro.Archivadas => q.Where(n => n.ArchivadaUtc != null),
            EstadoNotificacionFiltro.Todas => q,
            _ => q.Where(n => n.ArchivadaUtc == null)
        };

        if (!string.IsNullOrWhiteSpace(f.Categoria))
        {
            if (!CategoriaNotificacion.Todas.Contains(f.Categoria)) throw ServiceException.Validation("La categoría indicada no existe.");
            var tipos = CatalogoNotificaciones.TiposDe(f.Categoria).ToList();
            q = q.Where(n => tipos.Contains(n.Tipo));
        }

        if (f.Desde is not null || f.Hasta is not null)
        {
            if (f.Desde is { } d0 && f.Hasta is { } h0 && h0 < d0) throw ServiceException.Validation("El rango de fechas no es válido.");
            var zona = await ZonaAsync(empresaId, ct);
            if (f.Desde is { } d) { var ini = InicioDelDiaUtc(d, zona); q = q.Where(n => n.CreadaUtc >= ini); }
            if (f.Hasta is { } h) { var fin = InicioDelDiaUtc(h.AddDays(1), zona); q = q.Where(n => n.CreadaUtc < fin); }
        }

        if (!string.IsNullOrWhiteSpace(f.Buscar))
        {
            var b = f.Buscar.Trim().ToLower();
            q = q.Where(n => n.Titulo.ToLower().Contains(b) || n.Mensaje.ToLower().Contains(b));
        }

        var pagina = await q.OrderByDescending(n => n.CreadaUtc).ThenBy(n => n.NotificacionId).ToPagedAsync(f, ct);
        var prioritarias = await PreferenciasHelper.PrioritariasAsync(_db, empresaId, _current.RequireUsuarioId(), ct);
        return new PagedResult<NotificacionDto>
        {
            Page = pagina.Page, PageSize = pagina.PageSize, Total = pagina.Total,
            Items = pagina.Items.Select(n => Dto(n, prioritarias)).ToList()
        };
    }

    private static NotificacionDto Dto(Notificacion n, HashSet<string> prioritarias) => new()
    {
        NotificacionId = n.NotificacionId, Tipo = n.Tipo, Categoria = CatalogoNotificaciones.CategoriaDe(n.Tipo), Titulo = n.Titulo, Mensaje = n.Mensaje,
        EntidadTipo = n.EntidadTipo, EntidadId = n.EntidadId, CreadaUtc = n.CreadaUtc, Leida = n.LeidaUtc != null, Archivada = n.ArchivadaUtc != null,
        Prioritaria = PreferenciasHelper.EsPrioritaria(prioritarias, n.Tipo), Obligatoria = CatalogoNotificaciones.EsObligatoria(n.Tipo)
    };

    public async Task<int> ContarNoLeidasAsync(CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.NotificacionesConsultar, ct);
        return await Propias(false).CountAsync(n => n.LeidaUtc == null && n.ArchivadaUtc == null, ct);
    }

    public async Task<ResumenNotificacionesDto> ResumenAsync(CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.NotificacionesConsultar, ct);
        var porTipo = await Propias(false).Where(n => n.LeidaUtc == null && n.ArchivadaUtc == null)
            .GroupBy(n => n.Tipo).Select(g => new { Tipo = g.Key, N = g.Count() }).ToListAsync(ct);
        var prioritarias = await PreferenciasHelper.PrioritariasAsync(_db, _current.RequireEmpresaId(), _current.RequireUsuarioId(), ct);
        var res = new ResumenNotificacionesDto { NoLeidas = porTipo.Sum(x => x.N) };
        foreach (var c in CategoriaNotificacion.Todas) res.NoLeidasPorCategoria[c] = 0;
        foreach (var x in porTipo)
        {
            res.NoLeidasPorCategoria[CatalogoNotificaciones.CategoriaDe(x.Tipo)] += x.N;
            if (PreferenciasHelper.EsPrioritaria(prioritarias, x.Tipo)) res.Prioritarias += x.N;
        }
        return res;
    }

    public async Task MarcarLeidaAsync(Guid notificacionId, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.NotificacionesConsultar, ct);
        var n = await Propias(true).FirstOrDefaultAsync(x => x.NotificacionId == notificacionId, ct) ?? throw ServiceException.NotFound("Notificación no encontrada.");
        if (n.LeidaUtc is not null) return;
        n.LeidaUtc = _clock.GetUtcNow().UtcDateTime;
        await _db.SaveSafeAsync(ct);
    }

    public async Task MarcarTodasLeidasAsync(CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.NotificacionesConsultar, ct);
        var ahora = _clock.GetUtcNow().UtcDateTime;
        var pendientes = await Propias(true).Where(n => n.LeidaUtc == null && n.ArchivadaUtc == null).ToListAsync(ct);
        foreach (var n in pendientes) n.LeidaUtc = ahora;
        await _db.SaveSafeAsync(ct);
    }

    public async Task ArchivarAsync(Guid notificacionId, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.NotificacionesConsultar, ct);
        var n = await Propias(true).FirstOrDefaultAsync(x => x.NotificacionId == notificacionId, ct) ?? throw ServiceException.NotFound("Notificación no encontrada.");
        var ahora = _clock.GetUtcNow().UtcDateTime;
        n.ArchivadaUtc ??= ahora;
        n.LeidaUtc ??= ahora;
        await _db.SaveSafeAsync(ct);
    }

    public async Task DesarchivarAsync(Guid notificacionId, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.NotificacionesConsultar, ct);
        var n = await Propias(true).FirstOrDefaultAsync(x => x.NotificacionId == notificacionId, ct) ?? throw ServiceException.NotFound("Notificación no encontrada.");
        if (n.ArchivadaUtc is null) return;
        n.ArchivadaUtc = null;
        await _db.SaveSafeAsync(ct);
    }

    public async Task<DestinoNotificacionDto> ResolverDestinoAsync(Guid notificacionId, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.NotificacionesConsultar, ct);
        var n = await Propias(false).FirstOrDefaultAsync(x => x.NotificacionId == notificacionId, ct) ?? throw ServiceException.NotFound("Notificación no encontrada.");
        if (!Guid.TryParse(n.EntidadId, out var id)) throw ServiceException.Validation("Esta notificación no tiene un recurso asociado.");
        var sinAcceso = ServiceException.NotFound("Ya no tienes acceso a este recurso.");
        var empresaId = _current.RequireEmpresaId();

        switch (n.EntidadTipo)
        {
            case nameof(Tarea):
            {
                var a = await AccesoProyectosAsync(sinAcceso, ct);
                var proyectoId = await _db.Tareas.AsNoTracking().Where(t => t.EmpresaId == empresaId && t.TareaId == id).Select(t => (Guid?)t.ProyectoId).FirstOrDefaultAsync(ct);
                if (proyectoId is not { } pid || !await ProyectoHelper.Visibles(_db, a).AnyAsync(p => p.ProyectoId == pid, ct)) throw sinAcceso;
                return new DestinoNotificacionDto { Ruta = $"tareas/{id}" };
            }
            case nameof(Proyecto):
            {
                var a = await AccesoProyectosAsync(sinAcceso, ct);
                if (!await ProyectoHelper.Visibles(_db, a).AnyAsync(p => p.ProyectoId == id, ct)) throw sinAcceso;
                return new DestinoNotificacionDto { Ruta = $"proyectos/{id}" };
            }
            case nameof(SolicitudPermiso):
            {
                var trabajadorId = await _db.SolicitudesPermiso.AsNoTracking().Where(s => s.EmpresaId == empresaId && s.SolicitudPermisoId == id)
                    .Select(s => (Guid?)s.TrabajadorId).FirstOrDefaultAsync(ct);
                if (trabajadorId is not { } tid) throw sinAcceso;
                var mi = await TrabajadorHelper.IdDeUsuarioAsync(_db, empresaId, _current.RequireUsuarioId(), ct);
                if (mi == tid) return new DestinoNotificacionDto { Ruta = "permisos" };
                var alcance = await PermisosHelper.AlcanceAsync(_db, _current, _permisos, _clock, true, ct);
                if (alcance.Revisa(tid)) return new DestinoNotificacionDto { Ruta = "permisos/aprobaciones" };
                throw sinAcceso;
            }
            default:
                throw ServiceException.Validation("Esta notificación no tiene un recurso asociado.");
        }
    }

    private async Task<AccesoProyectos> AccesoProyectosAsync(ServiceException sinAcceso, CancellationToken ct)
    {
        if (!await _permisos.TienePermisoAsync(Permisos.ProyectosConsultar, ct)) throw sinAcceso;
        return await ProyectoHelper.AccesoAsync(_db, _current, _permisos, _clock, ct);
    }
}

public class PreferenciasNotificacionService : IPreferenciasNotificacionService
{
    private readonly RegistroTrabajoDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IPermisoService _permisos;
    private readonly IAuditoriaService _auditoria;

    public PreferenciasNotificacionService(RegistroTrabajoDbContext db, ICurrentUser current, IPermisoService permisos, IAuditoriaService auditoria)
    {
        _db = db; _current = current; _permisos = permisos; _auditoria = auditoria;
    }

    public async Task<IReadOnlyList<PreferenciaCategoriaDto>> ListarAsync(CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.NotificacionesConsultar, ct);
        var empresaId = _current.RequireEmpresaId();
        var usuarioId = _current.RequireUsuarioId();
        var filas = await _db.PreferenciasNotificacion.AsNoTracking().Where(p => p.EmpresaId == empresaId && p.UsuarioId == usuarioId).ToListAsync(ct);
        return CategoriaNotificacion.Todas.Select(c =>
        {
            var tipos = CatalogoNotificaciones.Tipos.Where(t => t.Categoria == c).ToList();
            var fila = filas.FirstOrDefault(p => p.TipoNotificacion == c);
            return new PreferenciaCategoriaDto
            {
                Categoria = c, Nombre = CategoriaNotificacion.Nombre(c), Descripcion = CategoriaNotificacion.Descripcion(c),
                Activa = fila?.Habilitada ?? true, Prioritaria = fila?.Prioritaria ?? false,
                TieneObligatorias = tipos.Any(t => t.Obligatoria), TodasObligatorias = tipos.All(t => t.Obligatoria),
                Obligatorias = tipos.Where(t => t.Obligatoria).Select(t => t.Descripcion).ToList(),
                Opcionales = tipos.Where(t => !t.Obligatoria).Select(t => t.Descripcion).ToList()
            };
        }).ToList();
    }

    public async Task<IReadOnlyList<PreferenciaCategoriaDto>> GuardarAsync(GuardarPreferenciasRequest r, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.NotificacionesConsultar, ct);
        var empresaId = _current.RequireEmpresaId();
        var usuarioId = _current.RequireUsuarioId();
        if (r.Categorias.Select(x => x.Categoria).Distinct().Count() != r.Categorias.Count) throw ServiceException.Validation("Hay categorías repetidas.");

        var actuales = await _db.PreferenciasNotificacion.Where(p => p.EmpresaId == empresaId && p.UsuarioId == usuarioId).ToListAsync(ct);
        var antes = actuales.Where(p => CategoriaNotificacion.Todas.Contains(p.TipoNotificacion)).Select(p => new { p.TipoNotificacion, p.Habilitada, p.Prioritaria }).ToList();
        foreach (var x in r.Categorias)
        {
            if (!CategoriaNotificacion.Todas.Contains(x.Categoria)) throw ServiceException.Validation("La categoría indicada no existe.");
            if (!x.Activa && CatalogoNotificaciones.TiposDe(x.Categoria).All(CatalogoNotificaciones.EsObligatoria))
                throw ServiceException.Validation($"Los avisos de «{CategoriaNotificacion.Nombre(x.Categoria)}» son obligatorios y no se pueden desactivar.");

            var fila = actuales.FirstOrDefault(p => p.TipoNotificacion == x.Categoria);
            if (fila is null)
            {
                if (x.Activa && !x.Prioritaria) continue; // es el valor por defecto: no hace falta fila
                _db.PreferenciasNotificacion.Add(new PreferenciaNotificacion
                {
                    EmpresaId = empresaId, UsuarioId = usuarioId, TipoNotificacion = x.Categoria, Habilitada = x.Activa, Prioritaria = x.Prioritaria
                });
            }
            else { fila.Habilitada = x.Activa; fila.Prioritaria = x.Prioritaria; }
        }
        _auditoria.Registrar("Notificaciones", nameof(PreferenciaNotificacion), usuarioId.ToString(), "GuardarPreferencias", antes,
            r.Categorias.Select(x => new { x.Categoria, x.Activa, x.Prioritaria }).ToList());
        await _db.SaveSafeAsync(ct);
        return await ListarAsync(ct);
    }
}
