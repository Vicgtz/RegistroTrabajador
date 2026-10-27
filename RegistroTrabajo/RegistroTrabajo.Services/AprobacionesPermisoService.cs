using Microsoft.EntityFrameworkCore;
using RegistroTrabajo.Data;
using RegistroTrabajo.Data.Entities;
using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public interface IAprobacionesPermisoService
{
    Task<SolicitudPermisoDto> ResolverAsync(Guid solicitudId, ResolverPermisoRequest request, CancellationToken ct);
    /// <summary>El trabajador pide cancelar un permiso ya aprobado; un responsable debe autorizarlo.</summary>
    Task<SolicitudCancelacionDto> SolicitarCancelacionAsync(Guid solicitudId, SolicitarCancelacionRequest request, CancellationToken ct);
    Task<PagedResult<SolicitudCancelacionDto>> ListarCancelacionesAsync(CancelacionPermisoFiltro filtro, CancellationToken ct);
    Task<SolicitudCancelacionDto> ResolverCancelacionAsync(Guid cancelacionId, ResolverPermisoRequest request, CancellationToken ct);
}

/// <summary>
/// Decisiones sobre solicitudes de permiso y sobre cancelaciones de permisos aprobados. Reglas: se revisa solo dentro del alcance
/// (supervisor vigente o permisos.aprobar_todos), nunca lo propio, y cada decisión queda en AprobacionesPermiso/auditoría.
/// El saldo se mueve en el mismo SaveChanges que la decisión, con RowVersion y el índice único de movimientos como defensa ante reintentos.
/// </summary>
public class AprobacionesPermisoService : IAprobacionesPermisoService
{
    private readonly RegistroTrabajoDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IPermisoService _permisos;
    private readonly IAuditoriaService _auditoria;
    private readonly ISolicitudesPermisoService _solicitudes;
    private readonly TimeProvider _clock;

    public AprobacionesPermisoService(RegistroTrabajoDbContext db, ICurrentUser current, IPermisoService permisos, IAuditoriaService auditoria,
        ISolicitudesPermisoService solicitudes, TimeProvider clock)
    {
        _db = db; _current = current; _permisos = permisos; _auditoria = auditoria; _solicitudes = solicitudes; _clock = clock;
    }

    public async Task<SolicitudPermisoDto> ResolverAsync(Guid solicitudId, ResolverPermisoRequest r, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var actorId = _current.RequireUsuarioId();
        var now = _clock.GetUtcNow().UtcDateTime;
        var s = await _db.SolicitudesPermiso.FirstOrDefaultAsync(x => x.SolicitudPermisoId == solicitudId && x.EmpresaId == empresaId, ct)
            ?? throw ServiceException.NotFound("Solicitud no encontrada.");
        if (s.Estado != EstadoSolicitudPermiso.Pendiente) throw ServiceException.Conflict("La solicitud ya fue resuelta.");
        await AsegurarPuedeRevisarAsync(s.TrabajadorId, ct);
        var comentario = Helpers.Limpiar(r.Comentario);
        if (!r.Aprobar && comentario is null) throw ServiceException.Validation("Indique el motivo del rechazo.");

        var saldo = await PermisosHelper.SaldoDeSolicitudAsync(_db, s, ct);
        if (r.Aprobar)
        {
            if (saldo is not null) PermisosHelper.Consumir(_db, saldo, s.SolicitudPermisoId, s.CantidadSolicitada, true, actorId, now, "Consumo por permiso aprobado");
        }
        else if (saldo is not null)
        {
            PermisosHelper.LiberarReserva(saldo, s.CantidadSolicitada, now);
        }

        s.Estado = r.Aprobar ? EstadoSolicitudPermiso.Aprobada : EstadoSolicitudPermiso.Rechazada;
        s.ModificadaUtc = now;
        _db.AprobacionesPermiso.Add(new AprobacionPermiso
        {
            EmpresaId = empresaId, SolicitudPermisoId = s.SolicitudPermisoId, RevisorUsuarioId = actorId, Nivel = 1,
            Decision = r.Aprobar ? DecisionPermiso.Aprobada : DecisionPermiso.Rechazada, Comentario = comentario, FechaDecisionUtc = now
        });
        _auditoria.Registrar("Permisos", nameof(SolicitudPermiso), solicitudId.ToString(), r.Aprobar ? "AprobarPermiso" : "RechazarPermiso",
            new { Estado = EstadoSolicitudPermiso.Pendiente }, new { s.Estado, Cantidad = s.CantidadSolicitada });
        await NotificarTrabajadorAsync(empresaId, s, "permiso.resuelta", "Solicitud de permiso resuelta",
            r.Aprobar ? "Su solicitud de permiso fue aprobada." : "Su solicitud de permiso fue rechazada.", now, ct, $"sr:{solicitudId}:{s.Estado}");
        await _db.SaveSafeAsync(ct);
        return await _solicitudes.ObtenerAsync(solicitudId, ct);
    }

    public async Task<SolicitudCancelacionDto> SolicitarCancelacionAsync(Guid solicitudId, SolicitarCancelacionRequest r, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var actorId = _current.RequireUsuarioId();
        var now = _clock.GetUtcNow().UtcDateTime;
        var mi = await TrabajadorHelper.IdDeUsuarioAsync(_db, empresaId, actorId, ct)
            ?? throw ServiceException.NotFound("Su cuenta no está vinculada a un expediente de trabajador.");
        var s = await _db.SolicitudesPermiso.AsNoTracking().FirstOrDefaultAsync(x => x.SolicitudPermisoId == solicitudId && x.EmpresaId == empresaId && x.TrabajadorId == mi, ct)
            ?? throw ServiceException.NotFound("Solicitud no encontrada.");
        if (s.Estado != EstadoSolicitudPermiso.Aprobada) throw ServiceException.Conflict("Solo se puede solicitar la cancelación de un permiso aprobado.");
        if (s.FechaFin < now) throw ServiceException.Conflict("El permiso ya concluyó; no se puede cancelar.");
        if (await _db.SolicitudesCancelacionPermiso.AnyAsync(c => c.EmpresaId == empresaId && c.SolicitudPermisoId == solicitudId
            && c.Estado == EstadoCancelacionPermiso.Pendiente, ct))
            throw ServiceException.Conflict("Ya hay una solicitud de cancelación pendiente para este permiso.");

        var c = new SolicitudCancelacionPermiso
        {
            SolicitudCancelacionId = Guid.NewGuid(), EmpresaId = empresaId, SolicitudPermisoId = solicitudId, SolicitadaPorUsuarioId = actorId,
            Motivo = Helpers.Limpiar(r.Motivo), Estado = EstadoCancelacionPermiso.Pendiente, SolicitadaUtc = now
        };
        _db.SolicitudesCancelacionPermiso.Add(c);
        _auditoria.Registrar("Permisos", nameof(SolicitudCancelacionPermiso), c.SolicitudCancelacionId.ToString(), "SolicitarCancelacionPermiso", null, new { solicitudId });

        var nombre = await _db.Trabajadores.AsNoTracking().Where(t => t.TrabajadorId == mi && t.EmpresaId == empresaId).Select(t => t.Nombres + " " + t.Apellidos).FirstAsync(ct);
        var revisores = await PermisosHelper.UsuariosSupervisoresAsync(_db, empresaId, mi, _clock, ct);
        await PermisosHelper.NotificarAsync(_db, empresaId, revisores, "permiso.cancelacion_pendiente", "Cancelación de permiso pendiente",
            $"{nombre} solicitó cancelar un permiso aprobado.", solicitudId, now, ct, $"cp:{c.SolicitudCancelacionId}");
        await _db.SaveSafeAsync(ct);
        return (await CancelacionesAsync(empresaId, _db.SolicitudesCancelacionPermiso.AsNoTracking().Where(x => x.SolicitudCancelacionId == c.SolicitudCancelacionId), 1, 1, ct)).Items[0];
    }

    public async Task<PagedResult<SolicitudCancelacionDto>> ListarCancelacionesAsync(CancelacionPermisoFiltro f, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var alcance = await PermisosHelper.AlcanceAsync(_db, _current, _permisos, _clock, true, ct);
        if (!alcance.Todos && !await _permisos.TienePermisoAsync(Permisos.PermisosAprobar, ct))
            throw ServiceException.Forbidden("No tiene permiso para revisar cancelaciones de permisos.");
        var q = _db.SolicitudesCancelacionPermiso.AsNoTracking().Where(c => c.EmpresaId == empresaId);
        if (alcance.MiTrabajadorId is { } mi) q = q.Where(c => c.SolicitudPermiso.TrabajadorId != mi);
        if (!alcance.Todos)
        {
            var ids = alcance.Supervisados;
            q = q.Where(c => ids.Contains(c.SolicitudPermiso.TrabajadorId));
        }
        q = q.Where(c => c.Estado == (f.Estado ?? EstadoCancelacionPermiso.Pendiente));
        return await CancelacionesAsync(empresaId, q, f.Page, f.PageSize, ct);
    }

    public async Task<SolicitudCancelacionDto> ResolverCancelacionAsync(Guid cancelacionId, ResolverPermisoRequest r, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var actorId = _current.RequireUsuarioId();
        var now = _clock.GetUtcNow().UtcDateTime;
        var c = await _db.SolicitudesCancelacionPermiso.FirstOrDefaultAsync(x => x.SolicitudCancelacionId == cancelacionId && x.EmpresaId == empresaId, ct)
            ?? throw ServiceException.NotFound("Solicitud de cancelación no encontrada.");
        if (c.Estado != EstadoCancelacionPermiso.Pendiente) throw ServiceException.Conflict("La solicitud de cancelación ya fue resuelta.");
        var s = await _db.SolicitudesPermiso.FirstAsync(x => x.SolicitudPermisoId == c.SolicitudPermisoId && x.EmpresaId == empresaId, ct);
        await AsegurarPuedeRevisarAsync(s.TrabajadorId, ct);
        var comentario = Helpers.Limpiar(r.Comentario);
        if (!r.Aprobar && comentario is null) throw ServiceException.Validation("Indique el motivo del rechazo.");

        if (r.Aprobar)
        {
            if (s.Estado != EstadoSolicitudPermiso.Aprobada) throw ServiceException.Conflict("El permiso ya no está aprobado.");
            s.Estado = EstadoSolicitudPermiso.Cancelada;
            s.ModificadaUtc = now;
            var saldo = await PermisosHelper.SaldoDeSolicitudAsync(_db, s, ct);
            // Solo se devuelve lo que realmente se consumió, y una sola vez (el índice único lo garantiza ante reintentos).
            if (saldo is not null
                && await _db.MovimientosSaldoPermiso.AnyAsync(m => m.SolicitudPermisoId == s.SolicitudPermisoId && m.TipoMovimiento == TipoMovimientoSaldo.Consumo, ct)
                && !await _db.MovimientosSaldoPermiso.AnyAsync(m => m.SolicitudPermisoId == s.SolicitudPermisoId && m.TipoMovimiento == TipoMovimientoSaldo.Devolucion, ct))
                PermisosHelper.Devolver(_db, saldo, s.SolicitudPermisoId, s.CantidadSolicitada, actorId, now, "Devolución por cancelación de permiso aprobado");
        }
        c.Estado = r.Aprobar ? EstadoCancelacionPermiso.Aprobada : EstadoCancelacionPermiso.Rechazada;
        c.RevisadaPorUsuarioId = actorId;
        c.RevisadaUtc = now;
        c.ComentarioRevision = comentario;
        _auditoria.Registrar("Permisos", nameof(SolicitudCancelacionPermiso), cancelacionId.ToString(), r.Aprobar ? "AprobarCancelacionPermiso" : "RechazarCancelacionPermiso",
            new { Estado = EstadoCancelacionPermiso.Pendiente }, new { c.Estado, solicitudId = s.SolicitudPermisoId });
        await NotificarTrabajadorAsync(empresaId, s, "permiso.cancelacion_resuelta", "Cancelación de permiso resuelta",
            r.Aprobar ? "Su solicitud de cancelación fue aprobada." : "Su solicitud de cancelación fue rechazada.", now, ct, $"cr:{cancelacionId}");
        await _db.SaveSafeAsync(ct);
        return (await CancelacionesAsync(empresaId, _db.SolicitudesCancelacionPermiso.AsNoTracking().Where(x => x.SolicitudCancelacionId == cancelacionId), 1, 1, ct)).Items[0];
    }

    // ---- apoyo ----
    /// <summary>Supervisor vigente del trabajador o permisos.aprobar_todos; nunca el propio trabajador.</summary>
    private async Task AsegurarPuedeRevisarAsync(Guid trabajadorId, CancellationToken ct)
    {
        var alcance = await PermisosHelper.AlcanceAsync(_db, _current, _permisos, _clock, true, ct);
        if (alcance.MiTrabajadorId == trabajadorId) throw ServiceException.Forbidden("No puede resolver su propia solicitud.");
        if (!alcance.Revisa(trabajadorId)) throw ServiceException.Forbidden("No tiene autorización para resolver solicitudes de este trabajador.");
    }

    private async Task NotificarTrabajadorAsync(Guid empresaId, SolicitudPermiso s, string tipo, string titulo, string mensaje, DateTime now, CancellationToken ct, string? clave = null)
    {
        var usuario = await _db.Trabajadores.AsNoTracking().Where(t => t.TrabajadorId == s.TrabajadorId && t.EmpresaId == empresaId)
            .Select(t => t.UsuarioId).FirstOrDefaultAsync(ct);
        if (usuario is { } u) await PermisosHelper.NotificarAsync(_db, empresaId, new[] { u }, tipo, titulo, mensaje, s.SolicitudPermisoId, now, ct, clave);
    }

    private static async Task<PagedResult<SolicitudCancelacionDto>> CancelacionesAsync(Guid empresaId, IQueryable<SolicitudCancelacionPermiso> q, int page, int pageSize, CancellationToken ct)
    {
        var pq = new PageQuery { Page = page, PageSize = pageSize };
        var pagina = await q.OrderBy(c => c.SolicitadaUtc).Select(c => new SolicitudCancelacionDto
        {
            SolicitudCancelacionId = c.SolicitudCancelacionId, SolicitudPermisoId = c.SolicitudPermisoId,
            TrabajadorId = c.SolicitudPermiso.TrabajadorId,
            TrabajadorNombre = c.SolicitudPermiso.Trabajador.Nombres + " " + c.SolicitudPermiso.Trabajador.Apellidos,
            TipoPermiso = c.SolicitudPermiso.TipoPermiso.Nombre, FechaInicio = c.SolicitudPermiso.FechaInicio, FechaFin = c.SolicitudPermiso.FechaFin,
            Cantidad = c.SolicitudPermiso.CantidadSolicitada, Motivo = c.Motivo, Estado = c.Estado, SolicitadaUtc = c.SolicitadaUtc,
            RevisadaPorUsuarioId = c.RevisadaPorUsuarioId, RevisadaUtc = c.RevisadaUtc, ComentarioRevision = c.ComentarioRevision
        }).ToPagedAsync(pq, ct);
        return pagina;
    }
}
