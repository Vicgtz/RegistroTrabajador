using Microsoft.EntityFrameworkCore;
using RegistroTrabajo.Data;
using RegistroTrabajo.Data.Entities;
using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public interface ITareasService
{
    Task<PagedResult<TareaResumenDto>> ListarPorProyectoAsync(Guid proyectoId, TareaFiltro filtro, CancellationToken ct);
    /// <summary>Tareas del trabajador autenticado, cada una con el proyecto al que pertenece.</summary>
    Task<PagedResult<TareaResumenDto>> MisTareasAsync(MisTareasFiltro filtro, CancellationToken ct);
    Task<TareaDetalleDto> ObtenerAsync(Guid tareaId, CancellationToken ct);
    Task<TareaDetalleDto> CrearAsync(Guid proyectoId, GuardarTareaRequest request, CancellationToken ct);
    Task<TareaDetalleDto> ActualizarAsync(Guid tareaId, GuardarTareaRequest request, CancellationToken ct);
    Task<TareaDetalleDto> AsignarAsync(Guid tareaId, AsignarTareaRequest request, CancellationToken ct);
    Task<TareaDetalleDto> RetirarAsignadoAsync(Guid tareaId, Guid trabajadorId, CancellationToken ct);
    Task<TareaDetalleDto> ActualizarAvanceAsync(Guid tareaId, ActualizarAvanceRequest request, CancellationToken ct);
    /// <summary>El responsable aprueba (completada) o devuelve (reabierta) una tarea en revisión.</summary>
    Task<TareaDetalleDto> RevisarAsync(Guid tareaId, RevisarTareaRequest request, CancellationToken ct);
    Task<TareaDetalleDto> ReabrirAsync(Guid tareaId, MotivoTareaRequest request, CancellationToken ct);
    /// <summary>Las tareas no se eliminan: se cancelan y conservan su historial.</summary>
    Task<TareaDetalleDto> CancelarAsync(Guid tareaId, MotivoTareaRequest request, CancellationToken ct);
    Task<IReadOnlyList<HistorialAvanceTareaDto>> HistorialAsync(Guid tareaId, CancellationToken ct);
}

public interface IComentariosTareaService
{
    Task<IReadOnlyList<ComentarioTareaDto>> ListarAsync(Guid tareaId, CancellationToken ct);
    /// <summary>Los comentarios publicados no se editan ni se eliminan: no existe operación para ello.</summary>
    Task<ComentarioTareaDto> PublicarAsync(Guid tareaId, PublicarComentarioRequest request, CancellationToken ct);
    Task<IReadOnlyList<UsuarioMencionableDto>> MencionablesAsync(Guid tareaId, CancellationToken ct);
}

public interface INotificacionesService
{
    Task<PagedResult<NotificacionDto>> ListarAsync(NotificacionFiltro filtro, CancellationToken ct);
    Task<int> ContarNoLeidasAsync(CancellationToken ct);
    Task MarcarLeidaAsync(Guid notificacionId, CancellationToken ct);
    Task MarcarTodasLeidasAsync(CancellationToken ct);
    Task ArchivarAsync(Guid notificacionId, CancellationToken ct);
}

public class TareasService : ITareasService
{
    private readonly RegistroTrabajoDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IPermisoService _permisos;
    private readonly IAuditoriaService _auditoria;
    private readonly TimeProvider _clock;

    public TareasService(RegistroTrabajoDbContext db, ICurrentUser current, IPermisoService permisos, IAuditoriaService auditoria, TimeProvider clock)
    {
        _db = db; _current = current; _permisos = permisos; _auditoria = auditoria; _clock = clock;
    }

    private DateTime Ahora => _clock.GetUtcNow().UtcDateTime;

    private sealed record Ctx(AccesoProyectos A, Tarea T, Proyecto P, bool Asignado, bool ProyectoCerrado);

    private async Task<Ctx> CargarAsync(Guid tareaId, bool tracking, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.ProyectosConsultar, ct);
        var a = await ProyectoHelper.AccesoAsync(_db, _current, _permisos, _clock, ct);
        var q = tracking ? _db.Tareas : _db.Tareas.AsNoTracking();
        var t = await q.FirstOrDefaultAsync(x => x.TareaId == tareaId && x.EmpresaId == a.EmpresaId, ct) ?? throw ServiceException.NotFound("Tarea no encontrada.");
        var p = await ProyectoHelper.CargarVisibleAsync(_db, a, t.ProyectoId, false, ct); // 404 si el proyecto no es visible
        var asignado = a.MiTrabajadorId is { } mi && await _db.AsignacionesTarea.AsNoTracking()
            .AnyAsync(x => x.EmpresaId == a.EmpresaId && x.TareaId == tareaId && x.TrabajadorId == mi && x.FechaRetiroUtc == null, ct);
        return new Ctx(a, t, p, asignado, await ProyectoHelper.EstaCerradoAsync(_db, p, ct));
    }

    private static void ExigirAbierto(Ctx c)
    {
        if (c.ProyectoCerrado) throw ServiceException.Conflict("El proyecto está cerrado; no se pueden modificar sus tareas.");
    }

    // ---------------- Consulta ----------------
    public async Task<PagedResult<TareaResumenDto>> ListarPorProyectoAsync(Guid proyectoId, TareaFiltro f, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.ProyectosConsultar, ct);
        var a = await ProyectoHelper.AccesoAsync(_db, _current, _permisos, _clock, ct);
        await ProyectoHelper.CargarVisibleAsync(_db, a, proyectoId, false, ct);
        var hoy = await ProyectoHelper.HoyAsync(_db, a.EmpresaId, _clock, ct);

        var q = _db.Tareas.AsNoTracking().Where(t => t.EmpresaId == a.EmpresaId && t.ProyectoId == proyectoId);
        if (!string.IsNullOrWhiteSpace(f.Buscar))
        {
            var b = f.Buscar.Trim().ToLower();
            q = q.Where(t => t.Titulo.ToLower().Contains(b));
        }
        if (f.Estado is { } e) q = q.Where(t => t.Estado == e);
        if (!f.IncluirCerradas) q = q.Where(t => t.Estado != EstadoTarea.Completada && t.Estado != EstadoTarea.Cancelada);
        if (f.TrabajadorId is { } w)
            q = q.Where(t => _db.AsignacionesTarea.Any(x => x.EmpresaId == t.EmpresaId && x.TareaId == t.TareaId && x.TrabajadorId == w && x.FechaRetiroUtc == null));
        if (f.SoloVencidas) q = q.Where(t => t.Estado != EstadoTarea.Completada && t.Estado != EstadoTarea.Cancelada && t.FechaVencimiento != null && t.FechaVencimiento < hoy);
        return await PaginarAsync(q, f, a.EmpresaId, hoy, ct);
    }

    public async Task<PagedResult<TareaResumenDto>> MisTareasAsync(MisTareasFiltro f, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.ProyectosConsultar, ct);
        var a = await ProyectoHelper.AccesoAsync(_db, _current, _permisos, _clock, ct);
        var hoy = await ProyectoHelper.HoyAsync(_db, a.EmpresaId, _clock, ct);
        if (a.MiTrabajadorId is not { } mi)
            return new PagedResult<TareaResumenDto> { Items = new List<TareaResumenDto>(), Page = 1, PageSize = Math.Clamp(f.PageSize, 1, 200), Total = 0 };

        var q = _db.Tareas.AsNoTracking().Where(t => t.EmpresaId == a.EmpresaId
            && _db.AsignacionesTarea.Any(x => x.EmpresaId == t.EmpresaId && x.TareaId == t.TareaId && x.TrabajadorId == mi && x.FechaRetiroUtc == null));
        if (f.Estado is { } e) q = q.Where(t => t.Estado == e);
        if (!f.IncluirCerradas && f.Estado is null) q = q.Where(t => t.Estado != EstadoTarea.Completada && t.Estado != EstadoTarea.Cancelada);
        if (f.SoloVencidas) q = q.Where(t => t.Estado != EstadoTarea.Completada && t.Estado != EstadoTarea.Cancelada && t.FechaVencimiento != null && t.FechaVencimiento < hoy);
        return await PaginarAsync(q, f, a.EmpresaId, hoy, ct);
    }

    private async Task<PagedResult<TareaResumenDto>> PaginarAsync(IQueryable<Tarea> q, PageQuery page, Guid empresaId, DateOnly hoy, CancellationToken ct)
    {
        var pagina = await q.OrderBy(t => (t.Estado == EstadoTarea.Completada || t.Estado == EstadoTarea.Cancelada) ? 1 : 0)
            .ThenBy(t => t.FechaVencimiento == null).ThenBy(t => t.FechaVencimiento).ThenByDescending(t => t.Prioridad).ThenBy(t => t.Titulo)
            .ToPagedAsync(page, ct);
        var items = await ResumenesAsync(empresaId, pagina.Items, hoy, ct);
        return new PagedResult<TareaResumenDto> { Items = items, Page = pagina.Page, PageSize = pagina.PageSize, Total = pagina.Total };
    }

    public async Task<TareaDetalleDto> ObtenerAsync(Guid tareaId, CancellationToken ct) => await DetalleAsync(await CargarAsync(tareaId, false, ct), ct);

    public async Task<IReadOnlyList<HistorialAvanceTareaDto>> HistorialAsync(Guid tareaId, CancellationToken ct)
    {
        var c = await CargarAsync(tareaId, false, ct);
        var filas = await _db.HistorialAvanceTarea.AsNoTracking().Where(h => h.EmpresaId == c.A.EmpresaId && h.TareaId == tareaId)
            .OrderByDescending(h => h.FechaUtc).ThenByDescending(h => h.HistorialAvanceTareaId).ToListAsync(ct);
        var nombres = await ProyectoHelper.NombresUsuariosAsync(_db, c.A.EmpresaId, filas.Select(h => h.ActualizadoPorUsuarioId), ct);
        return filas.Select(h => new HistorialAvanceTareaDto
        {
            Id = h.HistorialAvanceTareaId, PorcentajeAnterior = h.PorcentajeAnterior, PorcentajeNuevo = h.PorcentajeNuevo,
            EstadoAnterior = h.EstadoAnterior, EstadoNuevo = h.EstadoNuevo, ActualizadoPor = nombres.GetValueOrDefault(h.ActualizadoPorUsuarioId, "—"),
            FechaUtc = h.FechaUtc, Comentario = h.Comentario
        }).ToList();
    }

    // ---------------- Alta, edición y equipo ----------------
    public async Task<TareaDetalleDto> CrearAsync(Guid proyectoId, GuardarTareaRequest r, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.TareasGestionar, ct);
        var a = await ProyectoHelper.AccesoAsync(_db, _current, _permisos, _clock, ct);
        var p = await ProyectoHelper.CargarVisibleAsync(_db, a, proyectoId, false, ct);
        if (!a.Administra(p.ResponsableTrabajadorId)) throw ServiceException.Forbidden("No administra este proyecto.");
        if (await ProyectoHelper.EstaCerradoAsync(_db, p, ct)) throw ServiceException.Conflict("El proyecto está cerrado; no se pueden agregar tareas.");

        var asignados = (r.TrabajadorIds ?? new()).Distinct().ToList();
        await ValidarParticipantesAsync(p, asignados, ct);

        var now = Ahora;
        var t = new Tarea
        {
            TareaId = Guid.NewGuid(), EmpresaId = a.EmpresaId, ProyectoId = proyectoId, Titulo = Helpers.Limpiar(r.Titulo) ?? throw ServiceException.Validation("El título es obligatorio."),
            Descripcion = Helpers.Limpiar(r.Descripcion), FechaInicio = r.FechaInicio, FechaVencimiento = r.FechaVencimiento,
            Estado = EstadoTarea.Pendiente, PorcentajeAvance = 0, Prioridad = r.Prioridad, CreadaPorUsuarioId = a.UsuarioId, FechaCreacionUtc = now
        };
        _db.Tareas.Add(t);
        foreach (var w in asignados)
            _db.AsignacionesTarea.Add(new AsignacionTarea { AsignacionTareaId = Guid.NewGuid(), EmpresaId = a.EmpresaId, TareaId = t.TareaId, TrabajadorId = w, FechaAsignacionUtc = now, AsignadoPorUsuarioId = a.UsuarioId });
        _db.HistorialAvanceTarea.Add(new HistorialAvanceTarea
        {
            EmpresaId = a.EmpresaId, TareaId = t.TareaId, PorcentajeAnterior = null, PorcentajeNuevo = 0, EstadoAnterior = null, EstadoNuevo = EstadoTarea.Pendiente,
            ActualizadoPorUsuarioId = a.UsuarioId, FechaUtc = now, Comentario = "Creación de la tarea"
        });
        _auditoria.Registrar("Tareas", nameof(Tarea), t.TareaId.ToString(), "Crear", null, Resumen(t));
        await NotificarAsignadosAsync(a, t, p, asignados, now, ct);
        await _db.SaveSafeAsync(ct);
        return await DetalleAsync(await CargarAsync(t.TareaId, false, ct), ct);
    }

    public async Task<TareaDetalleDto> ActualizarAsync(Guid tareaId, GuardarTareaRequest r, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.TareasGestionar, ct);
        var c = await CargarAsync(tareaId, true, ct);
        ExigirGestion(c);
        if (EstadoTarea.EsCerrada(c.T.Estado)) throw ServiceException.Conflict("La tarea está cerrada; reábrala para editarla.");
        ProyectoHelper.AplicarRowVersion(_db, c.T, c.T.RowVersion, r.RowVersion, "la tarea");

        var antes = Resumen(c.T);
        c.T.Titulo = Helpers.Limpiar(r.Titulo) ?? throw ServiceException.Validation("El título es obligatorio.");
        c.T.Descripcion = Helpers.Limpiar(r.Descripcion);
        c.T.FechaInicio = r.FechaInicio; c.T.FechaVencimiento = r.FechaVencimiento; c.T.Prioridad = r.Prioridad;
        c.T.FechaActualizacionUtc = Ahora;
        _auditoria.Registrar("Tareas", nameof(Tarea), c.T.TareaId.ToString(), "Actualizar", antes, Resumen(c.T));
        await _db.SaveSafeAsync(ct);
        return await DetalleAsync(c, ct);
    }

    public async Task<TareaDetalleDto> AsignarAsync(Guid tareaId, AsignarTareaRequest r, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.TareasGestionar, ct);
        var c = await CargarAsync(tareaId, true, ct);
        ExigirGestion(c);
        if (EstadoTarea.EsCerrada(c.T.Estado)) throw ServiceException.Conflict("La tarea está cerrada; reábrala para cambiar sus responsables.");

        var ids = r.TrabajadorIds.Distinct().ToList();
        await ValidarParticipantesAsync(c.P, ids, ct);
        var actuales = await _db.AsignacionesTarea.AsNoTracking().Where(x => x.EmpresaId == c.A.EmpresaId && x.TareaId == tareaId && x.FechaRetiroUtc == null)
            .Select(x => x.TrabajadorId).ToListAsync(ct);
        var nuevos = ids.Except(actuales).ToList();
        if (nuevos.Count == 0) throw ServiceException.Conflict("Los trabajadores indicados ya están asignados a la tarea.");

        var now = Ahora;
        foreach (var w in nuevos)
            _db.AsignacionesTarea.Add(new AsignacionTarea { AsignacionTareaId = Guid.NewGuid(), EmpresaId = c.A.EmpresaId, TareaId = tareaId, TrabajadorId = w, FechaAsignacionUtc = now, AsignadoPorUsuarioId = c.A.UsuarioId });
        c.T.FechaActualizacionUtc = now;
        _auditoria.Registrar("Tareas", nameof(Tarea), tareaId.ToString(), "Asignar", null, new { Trabajadores = nuevos });
        await NotificarAsignadosAsync(c.A, c.T, c.P, nuevos, now, ct);
        await _db.SaveSafeAsync(ct);
        return await DetalleAsync(c, ct);
    }

    public async Task<TareaDetalleDto> RetirarAsignadoAsync(Guid tareaId, Guid trabajadorId, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.TareasGestionar, ct);
        var c = await CargarAsync(tareaId, true, ct);
        ExigirGestion(c);
        if (EstadoTarea.EsCerrada(c.T.Estado)) throw ServiceException.Conflict("La tarea está cerrada; reábrala para cambiar sus responsables.");
        var asig = await _db.AsignacionesTarea.FirstOrDefaultAsync(x => x.EmpresaId == c.A.EmpresaId && x.TareaId == tareaId && x.TrabajadorId == trabajadorId && x.FechaRetiroUtc == null, ct)
            ?? throw ServiceException.NotFound("El trabajador no está asignado a la tarea.");
        var now = Ahora;
        asig.FechaRetiroUtc = now < asig.FechaAsignacionUtc ? asig.FechaAsignacionUtc : now;
        c.T.FechaActualizacionUtc = now;
        _auditoria.Registrar("Tareas", nameof(Tarea), tareaId.ToString(), "RetirarAsignado", new { TrabajadorId = trabajadorId }, null);
        await _db.SaveSafeAsync(ct);
        return await DetalleAsync(c, ct);
    }

    // ---------------- Avance y revisión ----------------
    public async Task<TareaDetalleDto> ActualizarAvanceAsync(Guid tareaId, ActualizarAvanceRequest r, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.TareasActualizarAvance, ct);
        var c = await CargarAsync(tareaId, true, ct);
        ExigirAbierto(c);
        // Solo quien tiene la tarea asignada (o quien administra el proyecto) mueve su avance; nadie toca tareas ajenas sin autorización.
        if (!c.Asignado && !c.A.Administra(c.P.ResponsableTrabajadorId)) throw ServiceException.Forbidden("La tarea no le está asignada.");
        if (EstadoTarea.EsCerrada(c.T.Estado)) throw ServiceException.Conflict("La tarea está cerrada; pida que la reabran para seguir actualizándola.");
        ProyectoHelper.AplicarRowVersion(_db, c.T, c.T.RowVersion, r.RowVersion, "la tarea");

        var revisa = await PuedeRevisarAsync(c, ct);
        var pct = r.PorcentajeAvance;
        byte estado;
        if (r.Estado is { } e)
        {
            var permitidos = revisa
                ? new[] { EstadoTarea.Pendiente, EstadoTarea.EnProceso, EstadoTarea.EnRevision, EstadoTarea.Completada }
                : new[] { EstadoTarea.Pendiente, EstadoTarea.EnProceso, EstadoTarea.EnRevision };
            if (!permitidos.Contains(e)) throw ServiceException.Validation("Ese estado no se puede asignar desde la actualización de avance.");
            estado = e;
        }
        else
        {
            estado = pct == 0 ? EstadoTarea.Pendiente : pct < 100 ? EstadoTarea.EnProceso : revisa ? EstadoTarea.Completada : EstadoTarea.EnRevision;
        }
        if (estado is EstadoTarea.EnRevision or EstadoTarea.Completada && pct != 100)
            throw ServiceException.Validation("Una tarea en revisión o completada debe tener 100 % de avance.");
        if (estado == EstadoTarea.EnProceso && pct == 100)
            throw ServiceException.Validation("Con 100 % la tarea pasa a revisión; elija ese estado.");
        if (estado == EstadoTarea.Pendiente && pct != 0)
            throw ServiceException.Validation("Una tarea pendiente debe tener 0 % de avance.");
        if (pct == c.T.PorcentajeAvance && estado == c.T.Estado) throw ServiceException.Validation("No hay cambios que guardar.");

        var now = Ahora;
        RegistrarCambio(c, pct, estado, Helpers.Limpiar(r.Comentario), now);
        _auditoria.Registrar("Tareas", nameof(Tarea), tareaId.ToString(), "ActualizarAvance", new { Estado = c.T.Estado, c.T.PorcentajeAvance }, new { Estado = estado, PorcentajeAvance = pct });
        await ApplyAsync(c, pct, estado, now);

        if (estado == EstadoTarea.EnRevision)
        {
            var destino = await ProyectoHelper.UsuariosDeTrabajadoresAsync(_db, c.A.EmpresaId, new[] { c.P.ResponsableTrabajadorId }, ct);
            await NotificacionesHelper.NotificarAsync(_db, c.A.EmpresaId, destino.Values.Where(u => u != c.A.UsuarioId), TipoNotificacionTrabajo.TareaRevision,
                "Tarea en revisión", $"La tarea «{c.T.Titulo}» del proyecto «{c.P.Nombre}» espera su revisión.", nameof(Tarea), c.T.TareaId.ToString(), now, ct);
        }
        await _db.SaveSafeAsync(ct);
        return await DetalleAsync(c, ct);
    }

    public async Task<TareaDetalleDto> RevisarAsync(Guid tareaId, RevisarTareaRequest r, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.TareasRevisar, ct);
        var c = await CargarAsync(tareaId, true, ct);
        ExigirAbierto(c);
        if (!c.A.Administra(c.P.ResponsableTrabajadorId)) throw ServiceException.Forbidden("Solo quien administra el proyecto puede revisar sus tareas.");
        if (c.T.Estado != EstadoTarea.EnRevision) throw ServiceException.Conflict("La tarea no está en revisión.");
        var comentario = Helpers.Limpiar(r.Comentario);
        if (!r.Aprobar && comentario is null) throw ServiceException.Validation("Indique qué debe corregirse al devolver la tarea.");
        ProyectoHelper.AplicarRowVersion(_db, c.T, c.T.RowVersion, r.RowVersion, "la tarea");

        var now = Ahora;
        var estado = r.Aprobar ? EstadoTarea.Completada : EstadoTarea.Reabierta;
        RegistrarCambio(c, c.T.PorcentajeAvance, estado, comentario ?? "Revisión aprobada", now);
        _auditoria.Registrar("Tareas", nameof(Tarea), tareaId.ToString(), r.Aprobar ? "AprobarRevision" : "DevolverRevision", new { Estado = c.T.Estado }, new { Estado = estado });
        await ApplyAsync(c, c.T.PorcentajeAvance, estado, now);
        await NotificarAAsignadosAsync(c, TipoNotificacionTrabajo.TareaRevisada, "Tarea revisada",
            r.Aprobar ? $"La tarea «{c.T.Titulo}» fue aprobada." : $"La tarea «{c.T.Titulo}» fue devuelta con observaciones.", now, ct);
        await _db.SaveSafeAsync(ct);
        return await DetalleAsync(c, ct);
    }

    public async Task<TareaDetalleDto> ReabrirAsync(Guid tareaId, MotivoTareaRequest r, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.TareasRevisar, ct);
        var c = await CargarAsync(tareaId, true, ct);
        ExigirAbierto(c);
        if (!c.A.Administra(c.P.ResponsableTrabajadorId)) throw ServiceException.Forbidden("No administra este proyecto.");
        if (!EstadoTarea.EsCerrada(c.T.Estado)) throw ServiceException.Conflict("La tarea no está cerrada.");
        ProyectoHelper.AplicarRowVersion(_db, c.T, c.T.RowVersion, r.RowVersion, "la tarea");

        var now = Ahora;
        RegistrarCambio(c, c.T.PorcentajeAvance, EstadoTarea.Reabierta, r.Motivo.Trim(), now);
        _auditoria.Registrar("Tareas", nameof(Tarea), tareaId.ToString(), "Reabrir", new { Estado = c.T.Estado }, new { Estado = EstadoTarea.Reabierta });
        await ApplyAsync(c, c.T.PorcentajeAvance, EstadoTarea.Reabierta, now);
        await NotificarAAsignadosAsync(c, TipoNotificacionTrabajo.TareaRevisada, "Tarea reabierta", $"La tarea «{c.T.Titulo}» fue reabierta.", now, ct);
        await _db.SaveSafeAsync(ct);
        return await DetalleAsync(c, ct);
    }

    public async Task<TareaDetalleDto> CancelarAsync(Guid tareaId, MotivoTareaRequest r, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.TareasGestionar, ct);
        var c = await CargarAsync(tareaId, true, ct);
        ExigirAbierto(c);
        ExigirGestion(c);
        if (c.T.Estado == EstadoTarea.Cancelada) throw ServiceException.Conflict("La tarea ya está cancelada.");
        if (c.T.Estado == EstadoTarea.Completada) throw ServiceException.Conflict("Una tarea completada no se cancela; reábrala primero si es necesario.");
        ProyectoHelper.AplicarRowVersion(_db, c.T, c.T.RowVersion, r.RowVersion, "la tarea");

        var now = Ahora;
        RegistrarCambio(c, c.T.PorcentajeAvance, EstadoTarea.Cancelada, r.Motivo.Trim(), now);
        _auditoria.Registrar("Tareas", nameof(Tarea), tareaId.ToString(), "Cancelar", new { Estado = c.T.Estado }, new { Estado = EstadoTarea.Cancelada });
        await ApplyAsync(c, c.T.PorcentajeAvance, EstadoTarea.Cancelada, now);
        await NotificarAAsignadosAsync(c, TipoNotificacionTrabajo.TareaRevisada, "Tarea cancelada", $"La tarea «{c.T.Titulo}» fue cancelada.", now, ct);
        await _db.SaveSafeAsync(ct);
        return await DetalleAsync(c, ct);
    }

    // ---------------- Apoyo ----------------
    private void ExigirGestion(Ctx c)
    {
        ExigirAbierto(c);
        if (!c.A.Administra(c.P.ResponsableTrabajadorId)) throw ServiceException.Forbidden("No administra este proyecto.");
    }

    private async Task<bool> PuedeRevisarAsync(Ctx c, CancellationToken ct) =>
        c.A.Administra(c.P.ResponsableTrabajadorId) && await _permisos.TienePermisoAsync(Permisos.TareasRevisar, ct);

    private void RegistrarCambio(Ctx c, byte pct, byte estado, string? comentario, DateTime now) =>
        _db.HistorialAvanceTarea.Add(new HistorialAvanceTarea
        {
            EmpresaId = c.A.EmpresaId, TareaId = c.T.TareaId, PorcentajeAnterior = c.T.PorcentajeAvance, PorcentajeNuevo = pct,
            EstadoAnterior = c.T.Estado, EstadoNuevo = estado, ActualizadoPorUsuarioId = c.A.UsuarioId, FechaUtc = now, Comentario = comentario
        });

    private Task ApplyAsync(Ctx c, byte pct, byte estado, DateTime now)
    {
        c.T.PorcentajeAvance = pct;
        c.T.Estado = estado;
        c.T.FechaActualizacionUtc = now;
        c.T.FechaFinalizacionUtc = estado == EstadoTarea.Completada ? now : null;
        return Task.CompletedTask;
    }

    private async Task ValidarParticipantesAsync(Proyecto p, IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return;
        var participantes = await ProyectoHelper.ParticipantesAsync(_db, p, ct);
        if (ids.Any(i => !participantes.Contains(i)))
            throw ServiceException.Validation("Solo se puede asignar a trabajadores que participan en el proyecto; agréguelos primero al equipo.");
        await ProyectoHelper.ValidarTrabajadoresActivosAsync(_db, p.EmpresaId, ids, ct);
    }

    private async Task NotificarAsignadosAsync(AccesoProyectos a, Tarea t, Proyecto p, IEnumerable<Guid> trabajadores, DateTime now, CancellationToken ct)
    {
        var usuarios = await ProyectoHelper.UsuariosDeTrabajadoresAsync(_db, a.EmpresaId, trabajadores, ct);
        await NotificacionesHelper.NotificarAsync(_db, a.EmpresaId, usuarios.Values.Where(u => u != a.UsuarioId), TipoNotificacionTrabajo.TareaAsignada,
            "Nueva tarea asignada", $"Se le asignó la tarea «{t.Titulo}» del proyecto «{p.Nombre}».", nameof(Tarea), t.TareaId.ToString(), now, ct);
    }

    private async Task NotificarAAsignadosAsync(Ctx c, string tipo, string titulo, string mensaje, DateTime now, CancellationToken ct)
    {
        var asignados = await _db.AsignacionesTarea.AsNoTracking().Where(x => x.EmpresaId == c.A.EmpresaId && x.TareaId == c.T.TareaId && x.FechaRetiroUtc == null)
            .Select(x => x.TrabajadorId).ToListAsync(ct);
        var usuarios = await ProyectoHelper.UsuariosDeTrabajadoresAsync(_db, c.A.EmpresaId, asignados, ct);
        await NotificacionesHelper.NotificarAsync(_db, c.A.EmpresaId, usuarios.Values.Where(u => u != c.A.UsuarioId), tipo, titulo, mensaje, nameof(Tarea), c.T.TareaId.ToString(), now, ct);
    }

    private static object Resumen(Tarea t) => new { t.Titulo, t.FechaInicio, t.FechaVencimiento, t.Prioridad, t.Estado, t.PorcentajeAvance };

    private async Task<List<TareaResumenDto>> ResumenesAsync(Guid empresaId, IReadOnlyList<Tarea> tareas, DateOnly hoy, CancellationToken ct)
    {
        var ids = tareas.Select(t => t.TareaId).ToList();
        var proyectoIds = tareas.Select(t => t.ProyectoId).Distinct().ToList();
        var proyectos = await _db.Proyectos.AsNoTracking().Where(p => p.EmpresaId == empresaId && proyectoIds.Contains(p.ProyectoId)).ToDictionaryAsync(p => p.ProyectoId, p => p.Nombre, ct);
        var asig = await _db.AsignacionesTarea.AsNoTracking().Where(x => x.EmpresaId == empresaId && ids.Contains(x.TareaId) && x.FechaRetiroUtc == null)
            .Select(x => new { x.TareaId, x.TrabajadorId }).ToListAsync(ct);
        var nombres = await ProyectoHelper.NombresTrabajadoresAsync(_db, empresaId, asig.Select(x => x.TrabajadorId), ct);
        return tareas.Select(t => new TareaResumenDto
        {
            TareaId = t.TareaId, ProyectoId = t.ProyectoId, Proyecto = proyectos.GetValueOrDefault(t.ProyectoId, "—"), Titulo = t.Titulo, Estado = t.Estado,
            PorcentajeAvance = t.PorcentajeAvance, Prioridad = t.Prioridad, FechaInicio = t.FechaInicio, FechaVencimiento = t.FechaVencimiento,
            Vencida = !EstadoTarea.EsCerrada(t.Estado) && t.FechaVencimiento is { } f && f < hoy,
            Asignados = asig.Where(x => x.TareaId == t.TareaId).Select(x => nombres.GetValueOrDefault(x.TrabajadorId, "—")).OrderBy(n => n).ToList()
        }).ToList();
    }

    private async Task<TareaDetalleDto> DetalleAsync(Ctx c, CancellationToken ct)
    {
        // El contexto puede venir de antes de guardar: se relee la tarea para devolver su versión y fechas actuales.
        c = c with { T = await _db.Tareas.AsNoTracking().FirstAsync(x => x.TareaId == c.T.TareaId && x.EmpresaId == c.A.EmpresaId, ct) };
        var hoy = await ProyectoHelper.HoyAsync(_db, c.A.EmpresaId, _clock, ct);
        var r = (await ResumenesAsync(c.A.EmpresaId, new[] { c.T }, hoy, ct))[0];
        var asig = await _db.AsignacionesTarea.AsNoTracking().Where(x => x.EmpresaId == c.A.EmpresaId && x.TareaId == c.T.TareaId && x.FechaRetiroUtc == null)
            .OrderBy(x => x.FechaAsignacionUtc).ToListAsync(ct);
        var nombres = await ProyectoHelper.NombresTrabajadoresAsync(_db, c.A.EmpresaId, asig.Select(x => x.TrabajadorId), ct);
        var creador = await ProyectoHelper.NombresUsuariosAsync(_db, c.A.EmpresaId, new[] { c.T.CreadaPorUsuarioId }, ct);
        var asignado = c.A.MiTrabajadorId is { } mi && asig.Any(x => x.TrabajadorId == mi);
        var administra = c.A.Administra(c.P.ResponsableTrabajadorId);
        var abierta = !c.ProyectoCerrado;

        return new TareaDetalleDto
        {
            TareaId = r.TareaId, ProyectoId = r.ProyectoId, Proyecto = r.Proyecto, Titulo = r.Titulo, Estado = r.Estado, PorcentajeAvance = r.PorcentajeAvance,
            Prioridad = r.Prioridad, FechaInicio = r.FechaInicio, FechaVencimiento = r.FechaVencimiento, Vencida = r.Vencida, Asignados = r.Asignados,
            Descripcion = c.T.Descripcion, CreadaPor = creador.GetValueOrDefault(c.T.CreadaPorUsuarioId, "—"), FechaCreacionUtc = c.T.FechaCreacionUtc,
            FechaActualizacionUtc = c.T.FechaActualizacionUtc, FechaFinalizacionUtc = c.T.FechaFinalizacionUtc, RowVersion = ProyectoHelper.Version(c.T.RowVersion),
            Equipo = asig.Select(x => new AsignadoTareaDto { TrabajadorId = x.TrabajadorId, Nombre = nombres.GetValueOrDefault(x.TrabajadorId, "—"), FechaAsignacionUtc = x.FechaAsignacionUtc }).ToList(),
            PuedeEditar = abierta && administra && !EstadoTarea.EsCerrada(c.T.Estado) && await _permisos.TienePermisoAsync(Permisos.TareasGestionar, ct),
            PuedeActualizarAvance = abierta && (asignado || administra) && !EstadoTarea.EsCerrada(c.T.Estado) && await _permisos.TienePermisoAsync(Permisos.TareasActualizarAvance, ct),
            PuedeRevisar = abierta && administra && await _permisos.TienePermisoAsync(Permisos.TareasRevisar, ct),
            PuedeComentar = (asignado || administra) && await _permisos.TienePermisoAsync(Permisos.TareasComentar, ct)
        };
    }
}

public class ComentariosTareaService : IComentariosTareaService
{
    private readonly RegistroTrabajoDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IPermisoService _permisos;
    private readonly IAuditoriaService _auditoria;
    private readonly TimeProvider _clock;

    public ComentariosTareaService(RegistroTrabajoDbContext db, ICurrentUser current, IPermisoService permisos, IAuditoriaService auditoria, TimeProvider clock)
    {
        _db = db; _current = current; _permisos = permisos; _auditoria = auditoria; _clock = clock;
    }

    private async Task<(AccesoProyectos A, Tarea T, Proyecto P)> CargarAsync(Guid tareaId, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.ProyectosConsultar, ct);
        var a = await ProyectoHelper.AccesoAsync(_db, _current, _permisos, _clock, ct);
        var t = await _db.Tareas.AsNoTracking().FirstOrDefaultAsync(x => x.TareaId == tareaId && x.EmpresaId == a.EmpresaId, ct) ?? throw ServiceException.NotFound("Tarea no encontrada.");
        var p = await ProyectoHelper.CargarVisibleAsync(_db, a, t.ProyectoId, false, ct);
        return (a, t, p);
    }

    public async Task<IReadOnlyList<ComentarioTareaDto>> ListarAsync(Guid tareaId, CancellationToken ct)
    {
        var (a, _, _) = await CargarAsync(tareaId, ct);
        var filas = await _db.Comentarios.AsNoTracking().Where(x => x.EmpresaId == a.EmpresaId && x.TareaId == tareaId)
            .OrderBy(x => x.FechaCreacionUtc).ThenBy(x => x.ComentarioId).ToListAsync(ct);
        var ids = filas.Select(x => x.ComentarioId).ToList();
        var menciones = await _db.MencionesComentario.AsNoTracking().Where(m => m.EmpresaId == a.EmpresaId && ids.Contains(m.ComentarioId))
            .Select(m => new { m.ComentarioId, m.UsuarioMencionadoId }).ToListAsync(ct);
        var nombres = await ProyectoHelper.NombresUsuariosAsync(_db, a.EmpresaId, filas.Select(x => x.UsuarioId).Concat(menciones.Select(m => m.UsuarioMencionadoId)), ct);
        return filas.Select(x => new ComentarioTareaDto
        {
            ComentarioId = x.ComentarioId, UsuarioId = x.UsuarioId, Autor = nombres.GetValueOrDefault(x.UsuarioId, "—"), Contenido = x.Contenido,
            FechaCreacionUtc = x.FechaCreacionUtc, ComentarioPadreId = x.ComentarioPadreId,
            Menciones = menciones.Where(m => m.ComentarioId == x.ComentarioId).Select(m => nombres.GetValueOrDefault(m.UsuarioMencionadoId, "—")).ToList()
        }).ToList();
    }

    public async Task<IReadOnlyList<UsuarioMencionableDto>> MencionablesAsync(Guid tareaId, CancellationToken ct)
    {
        var (a, _, p) = await CargarAsync(tareaId, ct);
        var usuarios = await UsuariosDelProyectoAsync(a.EmpresaId, p, ct);
        var nombres = await ProyectoHelper.NombresUsuariosAsync(_db, a.EmpresaId, usuarios, ct);
        return usuarios.Select(u => new UsuarioMencionableDto { UsuarioId = u, Nombre = nombres.GetValueOrDefault(u, "—") }).OrderBy(u => u.Nombre).ToList();
    }

    public async Task<ComentarioTareaDto> PublicarAsync(Guid tareaId, PublicarComentarioRequest r, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.TareasComentar, ct);
        var (a, t, p) = await CargarAsync(tareaId, ct);
        var asignado = a.MiTrabajadorId is { } mi && await _db.AsignacionesTarea.AsNoTracking()
            .AnyAsync(x => x.EmpresaId == a.EmpresaId && x.TareaId == tareaId && x.TrabajadorId == mi && x.FechaRetiroUtc == null, ct);
        // Los asignados publican actualizaciones; quien administra el proyecto agrega instrucciones y observaciones.
        if (!asignado && !a.Administra(p.ResponsableTrabajadorId)) throw ServiceException.Forbidden("Solo los asignados o quien administra el proyecto pueden comentar.");

        var contenido = Helpers.Limpiar(r.Contenido) ?? throw ServiceException.Validation("El comentario no puede estar vacío.");
        if (r.ComentarioPadreId is { } padre && !await _db.Comentarios.AnyAsync(x => x.EmpresaId == a.EmpresaId && x.TareaId == tareaId && x.ComentarioId == padre, ct))
            throw ServiceException.Validation("El comentario al que responde no existe en esta tarea.");

        var elegibles = await UsuariosDelProyectoAsync(a.EmpresaId, p, ct);
        var menciones = (r.MencionesUsuarioIds ?? new()).Distinct().Where(u => u != a.UsuarioId).ToList();
        if (menciones.Any(u => !elegibles.Contains(u))) throw ServiceException.Validation("Solo se puede mencionar a quienes participan en el proyecto.");

        var now = _clock.GetUtcNow().UtcDateTime;
        var c = new Comentario
        {
            ComentarioId = Guid.NewGuid(), EmpresaId = a.EmpresaId, TareaId = tareaId, UsuarioId = a.UsuarioId, ComentarioPadreId = r.ComentarioPadreId,
            Contenido = contenido, FechaCreacionUtc = now
        };
        _db.Comentarios.Add(c);
        foreach (var u in menciones)
            _db.MencionesComentario.Add(new MencionComentario { EmpresaId = a.EmpresaId, ComentarioId = c.ComentarioId, UsuarioMencionadoId = u, FechaCreacionUtc = now });

        await NotificacionesHelper.NotificarAsync(_db, a.EmpresaId, menciones, TipoNotificacionTrabajo.TareaMencion, "Lo mencionaron en una tarea",
            $"Lo mencionaron en un comentario de la tarea «{t.Titulo}».", nameof(Tarea), tareaId.ToString(), now, ct);

        // Los asignados se enteran de los comentarios nuevos (salvo quien comenta y quienes ya recibieron la mención).
        var asignados = await _db.AsignacionesTarea.AsNoTracking().Where(x => x.EmpresaId == a.EmpresaId && x.TareaId == tareaId && x.FechaRetiroUtc == null)
            .Select(x => x.TrabajadorId).ToListAsync(ct);
        var usuariosAsignados = await ProyectoHelper.UsuariosDeTrabajadoresAsync(_db, a.EmpresaId, asignados, ct);
        await NotificacionesHelper.NotificarAsync(_db, a.EmpresaId, usuariosAsignados.Values.Where(u => u != a.UsuarioId && !menciones.Contains(u)),
            TipoNotificacionTrabajo.TareaComentario, "Nuevo comentario en una tarea", $"Hay un comentario nuevo en la tarea «{t.Titulo}».", nameof(Tarea), tareaId.ToString(), now, ct);

        _auditoria.Registrar("Tareas", nameof(Comentario), c.ComentarioId.ToString(), "Comentar", null, new { TareaId = tareaId, Menciones = menciones.Count });
        await _db.SaveSafeAsync(ct);

        var nombres = await ProyectoHelper.NombresUsuariosAsync(_db, a.EmpresaId, menciones.Append(a.UsuarioId), ct);
        return new ComentarioTareaDto
        {
            ComentarioId = c.ComentarioId, UsuarioId = c.UsuarioId, Autor = nombres.GetValueOrDefault(c.UsuarioId, "—"), Contenido = c.Contenido,
            FechaCreacionUtc = c.FechaCreacionUtc, ComentarioPadreId = c.ComentarioPadreId,
            Menciones = menciones.Select(u => nombres.GetValueOrDefault(u, "—")).ToList()
        };
    }

    /// <summary>Cuentas de usuario del equipo del proyecto (responsable y participantes vigentes) y de quien lo creó.</summary>
    private async Task<List<Guid>> UsuariosDelProyectoAsync(Guid empresaId, Proyecto p, CancellationToken ct)
    {
        var participantes = await ProyectoHelper.ParticipantesAsync(_db, p, ct);
        var usuarios = (await ProyectoHelper.UsuariosDeTrabajadoresAsync(_db, empresaId, participantes, ct)).Values.ToList();
        if (!usuarios.Contains(p.CreadoPorUsuarioId)) usuarios.Add(p.CreadoPorUsuarioId);
        return usuarios.Distinct().ToList();
    }
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

    public async Task<PagedResult<NotificacionDto>> ListarAsync(NotificacionFiltro f, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.NotificacionesConsultar, ct);
        var q = Propias(false);
        if (!f.IncluirArchivadas) q = q.Where(n => n.ArchivadaUtc == null);
        if (f.SoloNoLeidas) q = q.Where(n => n.LeidaUtc == null);
        var pagina = await q.OrderByDescending(n => n.CreadaUtc).ThenBy(n => n.NotificacionId).ToPagedAsync(f, ct);
        return new PagedResult<NotificacionDto>
        {
            Page = pagina.Page, PageSize = pagina.PageSize, Total = pagina.Total,
            Items = pagina.Items.Select(n => new NotificacionDto
            {
                NotificacionId = n.NotificacionId, Tipo = n.Tipo, Titulo = n.Titulo, Mensaje = n.Mensaje, EntidadTipo = n.EntidadTipo,
                EntidadId = n.EntidadId, CreadaUtc = n.CreadaUtc, Leida = n.LeidaUtc != null
            }).ToList()
        };
    }

    public async Task<int> ContarNoLeidasAsync(CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.NotificacionesConsultar, ct);
        return await Propias(false).CountAsync(n => n.LeidaUtc == null && n.ArchivadaUtc == null, ct);
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
}
