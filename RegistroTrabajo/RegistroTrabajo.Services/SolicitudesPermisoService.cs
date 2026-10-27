using Microsoft.EntityFrameworkCore;
using RegistroTrabajo.Data;
using RegistroTrabajo.Data.Entities;
using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public interface ISolicitudesPermisoService
{
    Task<SolicitudPermisoDto> CrearAsync(CrearSolicitudPermisoRequest request, CancellationToken ct);
    /// <summary>Edita una solicitud propia que sigue pendiente (vuelve a validar reglas y a reservar saldo).</summary>
    Task<SolicitudPermisoDto> ActualizarAsync(Guid solicitudId, ActualizarSolicitudPermisoRequest request, CancellationToken ct);
    /// <summary>Cancela una solicitud propia pendiente y libera su saldo reservado.</summary>
    Task<SolicitudPermisoDto> CancelarAsync(Guid solicitudId, CancellationToken ct);
    Task<PagedResult<SolicitudPermisoDto>> MisSolicitudesAsync(SolicitudPermisoFiltro filtro, CancellationToken ct);
    /// <summary>Historial según el alcance del usuario: toda la empresa o solo los trabajadores que supervisa.</summary>
    Task<PagedResult<SolicitudPermisoDto>> ListarAsync(SolicitudPermisoFiltro filtro, CancellationToken ct);
    /// <summary>Bandeja de aprobaciones: solicitudes (pendientes por defecto) que el usuario puede resolver. Nunca incluye las propias.</summary>
    Task<PagedResult<SolicitudPermisoDto>> BandejaAsync(SolicitudPermisoFiltro filtro, CancellationToken ct);
    Task<SolicitudPermisoDto> ObtenerAsync(Guid solicitudId, CancellationToken ct);
    /// <summary>Ausencias aprobadas en un rango, sin motivo ni tipo (privacidad).</summary>
    Task<IReadOnlyList<AusenciaCalendarioDto>> CalendarioAsync(DateOnly desde, DateOnly hasta, CancellationToken ct);
}

/// <summary>
/// Solicitudes de permiso del trabajador. El saldo se reserva al crear una solicitud pendiente, se consume al aprobarla y se libera
/// si se rechaza o cancela (ver <see cref="AprobacionesPermisoService"/>). Una clave de idempotencia evita duplicados por reintentos.
/// </summary>
public class SolicitudesPermisoService : ISolicitudesPermisoService
{
    private const int MaxDiasCalendario = 93;

    private readonly RegistroTrabajoDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IPermisoService _permisos;
    private readonly IAuditoriaService _auditoria;
    private readonly ICalculoDuracionService _calculo;
    private readonly TimeProvider _clock;

    public SolicitudesPermisoService(RegistroTrabajoDbContext db, ICurrentUser current, IPermisoService permisos, IAuditoriaService auditoria,
        ICalculoDuracionService calculo, TimeProvider clock)
    {
        _db = db; _current = current; _permisos = permisos; _auditoria = auditoria; _calculo = calculo; _clock = clock;
    }

    public async Task<SolicitudPermisoDto> CrearAsync(CrearSolicitudPermisoRequest r, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var usuarioId = _current.RequireUsuarioId();
        var now = _clock.GetUtcNow().UtcDateTime;
        var t = await MiTrabajadorAsync(empresaId, ct);

        var clave = Helpers.Limpiar(r.ClaveIdempotencia);
        if (clave is not null)
        {
            // Reintento: se devuelve la solicitud ya creada sin volver a validar ni a tocar el saldo.
            var previa = await _db.SolicitudesPermiso.AsNoTracking()
                .Where(s => s.EmpresaId == empresaId && s.TrabajadorId == t.TrabajadorId && s.ClaveIdempotencia == clave)
                .Select(s => (Guid?)s.SolicitudPermisoId).FirstOrDefaultAsync(ct);
            if (previa is { } id) return await DtoAsync(empresaId, id, t.TrabajadorId, ct);
        }

        var v = await ValidarAsync(empresaId, t, r.TipoPermisoId, r.FechaInicio, r.FechaFin, r.EsPorHoras, null, ct);
        var s = new SolicitudPermiso
        {
            SolicitudPermisoId = Guid.NewGuid(), EmpresaId = empresaId, TrabajadorId = t.TrabajadorId, TipoPermisoId = v.Tipo.TipoPermisoId,
            FechaInicio = v.Calculo.Inicio, FechaFin = v.Calculo.Fin, EsPorHoras = r.EsPorHoras, CantidadSolicitada = v.Calculo.Cantidad,
            MotivoPrivado = Helpers.Limpiar(r.Motivo), Estado = EstadoSolicitudPermiso.Pendiente, SolicitadaUtc = now, ClaveIdempotencia = clave
        };
        _db.SolicitudesPermiso.Add(s);

        var saldo = await ReservarSaldoAsync(empresaId, s, v.Regla, usuarioId, now, ct);
        if (!v.Tipo.RequiereAprobacion)
        {
            s.Estado = EstadoSolicitudPermiso.Aprobada;
            _db.AprobacionesPermiso.Add(new AprobacionPermiso
            {
                EmpresaId = empresaId, SolicitudPermisoId = s.SolicitudPermisoId, RevisorUsuarioId = usuarioId, Nivel = 1,
                Decision = DecisionPermiso.Aprobada, Comentario = "Aprobación automática: el tipo de permiso no requiere aprobación.", FechaDecisionUtc = now
            });
            if (saldo is not null) PermisosHelper.Consumir(_db, saldo, s.SolicitudPermisoId, s.CantidadSolicitada, false, usuarioId, now, "Consumo por permiso aprobado automáticamente");
        }
        else if (saldo is not null)
        {
            PermisosHelper.Reservar(saldo, s.CantidadSolicitada, now);
        }

        // La auditoría no incluye el motivo: es información privada.
        _auditoria.Registrar("Permisos", nameof(SolicitudPermiso), s.SolicitudPermisoId.ToString(), "SolicitarPermiso", null,
            new { s.TipoPermisoId, s.FechaInicio, s.FechaFin, s.EsPorHoras, Cantidad = s.CantidadSolicitada, s.Estado });
        if (s.Estado == EstadoSolicitudPermiso.Pendiente)
        {
            var revisores = await PermisosHelper.UsuariosSupervisoresAsync(_db, empresaId, t.TrabajadorId, _clock, ct);
            await PermisosHelper.NotificarAsync(_db, empresaId, revisores, "permiso.solicitud_pendiente", "Solicitud de permiso pendiente",
                $"{t.Nombres} {t.Apellidos} registró una solicitud de permiso que requiere su revisión.", s.SolicitudPermisoId, now, ct, $"sp:{s.SolicitudPermisoId}");
        }
        await _db.SaveSafeAsync(ct);
        return await DtoAsync(empresaId, s.SolicitudPermisoId, t.TrabajadorId, ct);
    }

    public async Task<SolicitudPermisoDto> ActualizarAsync(Guid solicitudId, ActualizarSolicitudPermisoRequest r, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var usuarioId = _current.RequireUsuarioId();
        var now = _clock.GetUtcNow().UtcDateTime;
        var t = await MiTrabajadorAsync(empresaId, ct);
        var s = await CargarPropiaAsync(empresaId, t.TrabajadorId, solicitudId, ct);
        if (s.Estado != EstadoSolicitudPermiso.Pendiente) throw ServiceException.Conflict("Solo se pueden editar solicitudes pendientes.");
        AplicarRowVersion(s, r.RowVersion);

        var antes = new { s.TipoPermisoId, s.FechaInicio, s.FechaFin, s.EsPorHoras, Cantidad = s.CantidadSolicitada };
        var anterior = await PermisosHelper.SaldoDeSolicitudAsync(_db, s, ct);
        if (anterior is not null) PermisosHelper.LiberarReserva(anterior, s.CantidadSolicitada, now);

        var v = await ValidarAsync(empresaId, t, r.TipoPermisoId, r.FechaInicio, r.FechaFin, r.EsPorHoras, s.SolicitudPermisoId, ct);
        s.TipoPermisoId = v.Tipo.TipoPermisoId;
        s.FechaInicio = v.Calculo.Inicio;
        s.FechaFin = v.Calculo.Fin;
        s.EsPorHoras = r.EsPorHoras;
        s.CantidadSolicitada = v.Calculo.Cantidad;
        s.MotivoPrivado = Helpers.Limpiar(r.Motivo);
        s.ModificadaUtc = now;
        var saldo = await ReservarSaldoAsync(empresaId, s, v.Regla, usuarioId, now, ct);
        if (saldo is not null) PermisosHelper.Reservar(saldo, s.CantidadSolicitada, now);

        _auditoria.Registrar("Permisos", nameof(SolicitudPermiso), solicitudId.ToString(), "EditarSolicitudPermiso", antes,
            new { s.TipoPermisoId, s.FechaInicio, s.FechaFin, s.EsPorHoras, Cantidad = s.CantidadSolicitada });
        await _db.SaveSafeAsync(ct);
        return await DtoAsync(empresaId, solicitudId, t.TrabajadorId, ct);
    }

    public async Task<SolicitudPermisoDto> CancelarAsync(Guid solicitudId, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var now = _clock.GetUtcNow().UtcDateTime;
        var t = await MiTrabajadorAsync(empresaId, ct);
        var s = await CargarPropiaAsync(empresaId, t.TrabajadorId, solicitudId, ct);
        if (s.Estado == EstadoSolicitudPermiso.Aprobada)
            throw ServiceException.Conflict("La solicitud ya fue aprobada; solicite su cancelación para que un responsable la revise.");
        if (s.Estado != EstadoSolicitudPermiso.Pendiente) throw ServiceException.Conflict("La solicitud ya fue resuelta.");

        var saldo = await PermisosHelper.SaldoDeSolicitudAsync(_db, s, ct);
        if (saldo is not null) PermisosHelper.LiberarReserva(saldo, s.CantidadSolicitada, now);
        s.Estado = EstadoSolicitudPermiso.Cancelada;
        s.ModificadaUtc = now;
        _auditoria.Registrar("Permisos", nameof(SolicitudPermiso), solicitudId.ToString(), "CancelarSolicitudPermiso",
            new { Estado = EstadoSolicitudPermiso.Pendiente }, new { s.Estado });
        await _db.SaveSafeAsync(ct);
        return await DtoAsync(empresaId, solicitudId, t.TrabajadorId, ct);
    }

    public async Task<PagedResult<SolicitudPermisoDto>> MisSolicitudesAsync(SolicitudPermisoFiltro f, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var t = await MiTrabajadorAsync(empresaId, ct);
        var q = Filtrar(_db.SolicitudesPermiso.AsNoTracking().Where(s => s.EmpresaId == empresaId && s.TrabajadorId == t.TrabajadorId), f);
        return await PaginarAsync(q, f, t.TrabajadorId, ct);
    }

    public async Task<PagedResult<SolicitudPermisoDto>> ListarAsync(SolicitudPermisoFiltro f, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var alcance = await PermisosHelper.AlcanceAsync(_db, _current, _permisos, _clock, false, ct);
        await AsegurarPuedeVerOtrosAsync(alcance, ct);
        var q = _db.SolicitudesPermiso.AsNoTracking().Where(s => s.EmpresaId == empresaId);
        if (!alcance.Todos)
        {
            var ids = alcance.Supervisados;
            q = q.Where(s => ids.Contains(s.TrabajadorId));
        }
        return await PaginarAsync(Filtrar(q, f), f, alcance.MiTrabajadorId, ct);
    }

    public async Task<PagedResult<SolicitudPermisoDto>> BandejaAsync(SolicitudPermisoFiltro f, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var alcance = await PermisosHelper.AlcanceAsync(_db, _current, _permisos, _clock, true, ct);
        if (!alcance.Todos && !await _permisos.TienePermisoAsync(Permisos.PermisosAprobar, ct))
            throw ServiceException.Forbidden("No tiene permiso para revisar solicitudes de permiso.");
        var q = _db.SolicitudesPermiso.AsNoTracking().Where(s => s.EmpresaId == empresaId);
        if (alcance.MiTrabajadorId is { } mi) q = q.Where(s => s.TrabajadorId != mi);
        if (!alcance.Todos)
        {
            var ids = alcance.Supervisados;
            q = q.Where(s => ids.Contains(s.TrabajadorId));
        }
        if (f.Estado is null) f.Estado = EstadoSolicitudPermiso.Pendiente;
        return await PaginarAsync(Filtrar(q, f), f, alcance.MiTrabajadorId, ct);
    }

    public async Task<SolicitudPermisoDto> ObtenerAsync(Guid solicitudId, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var alcance = await PermisosHelper.AlcanceAsync(_db, _current, _permisos, _clock, false, ct);
        var trabajadorId = await _db.SolicitudesPermiso.AsNoTracking()
            .Where(s => s.SolicitudPermisoId == solicitudId && s.EmpresaId == empresaId)
            .Select(s => (Guid?)s.TrabajadorId).FirstOrDefaultAsync(ct);
        // Las solicitudes fuera del alcance del usuario responden igual que las inexistentes.
        if (trabajadorId is not { } tid || !(tid == alcance.MiTrabajadorId || alcance.Incluye(tid)))
            throw ServiceException.NotFound("Solicitud no encontrada.");
        return await DtoAsync(empresaId, solicitudId, alcance.MiTrabajadorId, ct);
    }

    public async Task<IReadOnlyList<AusenciaCalendarioDto>> CalendarioAsync(DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        if (hasta < desde) throw ServiceException.Validation("El rango de fechas no es válido.");
        if (hasta.DayNumber - desde.DayNumber > MaxDiasCalendario) throw ServiceException.Validation($"El rango no puede superar {MaxDiasCalendario} días.");
        var alcance = await PermisosHelper.AlcanceAsync(_db, _current, _permisos, _clock, false, ct);
        await AsegurarPuedeVerOtrosAsync(alcance, ct);

        var ini = desde.ToDateTime(TimeOnly.MinValue);
        var fin = hasta.ToDateTime(new TimeOnly(23, 59, 59));
        var q = _db.SolicitudesPermiso.AsNoTracking()
            .Where(s => s.EmpresaId == empresaId && s.Estado == EstadoSolicitudPermiso.Aprobada && s.FechaInicio <= fin && s.FechaFin >= ini);
        if (!alcance.Todos)
        {
            var ids = alcance.Supervisados;
            q = q.Where(s => ids.Contains(s.TrabajadorId));
        }
        // Solo nombre y fechas: ni el tipo ni el motivo salen al calendario.
        return await q.OrderBy(s => s.FechaInicio).Select(s => new AusenciaCalendarioDto
        {
            TrabajadorId = s.TrabajadorId, TrabajadorNombre = s.Trabajador.Nombres + " " + s.Trabajador.Apellidos,
            FechaInicio = s.FechaInicio, FechaFin = s.FechaFin
        }).ToListAsync(ct);
    }

    // ---- validación y saldo ----
    private sealed record Validada(TipoPermiso Tipo, ReglaPermiso? Regla, CalculoResultado Calculo);

    private async Task<Validada> ValidarAsync(Guid empresaId, Trabajador t, Guid tipoId, DateTime inicio, DateTime fin, bool porHoras, Guid? excluirId, CancellationToken ct)
    {
        if (t.EstadoLaboral != EstadoLaboral.Activo) throw ServiceException.Validation("Solo los trabajadores activos pueden solicitar permisos.");
        var tipo = await _db.TiposPermiso.AsNoTracking().FirstOrDefaultAsync(x => x.TipoPermisoId == tipoId && x.EmpresaId == empresaId, ct)
            ?? throw ServiceException.Validation("El tipo de permiso indicado no existe.");
        if (!tipo.Estado) throw ServiceException.Validation("El tipo de permiso no está disponible.");

        var calculo = await _calculo.CalcularAsync(empresaId, t.TrabajadorId, tipo, inicio, fin, porHoras, ct);
        var fecha = DateOnly.FromDateTime(calculo.Inicio);
        if (fecha < TrabajadorHelper.Hoy(_clock)) throw ServiceException.Validation("No se pueden solicitar permisos con fecha pasada.");
        if (calculo.Inicio.Year != calculo.Fin.Year) throw ServiceException.Validation("La solicitud no puede abarcar dos años; divídala por año.");

        var regla = await PermisosHelper.ReglaVigenteAsync(_db, empresaId, tipoId, fecha, ct);
        if (regla is null)
        {
            if (await _db.ReglasPermiso.AnyAsync(x => x.EmpresaId == empresaId && x.TipoPermisoId == tipoId && x.Estado, ct))
                throw ServiceException.Validation("El tipo de permiso no está disponible en las fechas solicitadas.");
        }
        else
        {
            if (regla.AntiguedadMinimaMeses is { } min)
            {
                var meses = (fecha.Year - t.FechaIngreso.Year) * 12 + fecha.Month - t.FechaIngreso.Month - (fecha.Day < t.FechaIngreso.Day ? 1 : 0);
                if (meses < min) throw ServiceException.Validation($"Este permiso requiere una antigüedad mínima de {min} meses.");
            }
            if (regla.LimitePorSolicitud is { } limite && calculo.Cantidad > limite)
                throw ServiceException.Validation($"La solicitud ({calculo.Cantidad:0.##}) excede el límite por solicitud ({limite:0.##}).");
        }

        var traslape = await _db.SolicitudesPermiso.AnyAsync(x => x.EmpresaId == empresaId && x.TrabajadorId == t.TrabajadorId
            && x.SolicitudPermisoId != excluirId
            && (x.Estado == EstadoSolicitudPermiso.Pendiente || x.Estado == EstadoSolicitudPermiso.Aprobada)
            && x.FechaInicio < calculo.Fin && x.FechaFin > calculo.Inicio, ct);
        if (traslape) throw ServiceException.Conflict("Ya tiene un permiso pendiente o aprobado que se traslapa con esas fechas.");
        return new Validada(tipo, regla, calculo);
    }

    /// <summary>Devuelve el saldo del año si el tipo lo controla (regla con cantidad anual o saldo ya existente) tras comprobar que alcanza.</summary>
    private async Task<SaldoPermiso?> ReservarSaldoAsync(Guid empresaId, SolicitudPermiso s, ReglaPermiso? regla, Guid usuarioId, DateTime now, CancellationToken ct)
    {
        var anio = (short)s.FechaInicio.Year;
        var controla = regla?.CantidadAnual is not null
            || await _db.SaldosPermiso.AnyAsync(x => x.EmpresaId == empresaId && x.TrabajadorId == s.TrabajadorId && x.TipoPermisoId == s.TipoPermisoId && x.PeriodoAnio == anio, ct);
        if (!controla) return null;

        var saldo = await PermisosHelper.ObtenerOCrearSaldoAsync(_db, empresaId, s.TrabajadorId, s.TipoPermisoId, anio, regla, usuarioId, now, ct);
        var disponible = PermisosHelper.Disponible(saldo);
        if (s.CantidadSolicitada > disponible && !(regla?.PermiteSaldoNegativo ?? false))
            throw ServiceException.Validation($"Saldo insuficiente: disponible {disponible:0.##}, solicitado {s.CantidadSolicitada:0.##}.");
        return saldo;
    }

    // ---- apoyo ----
    private async Task<Trabajador> MiTrabajadorAsync(Guid empresaId, CancellationToken ct)
    {
        var id = await TrabajadorHelper.IdDeUsuarioAsync(_db, empresaId, _current.RequireUsuarioId(), ct)
            ?? throw ServiceException.NotFound("Su cuenta no está vinculada a un expediente de trabajador.");
        return await TrabajadorHelper.CargarAsync(_db, empresaId, id, false, ct);
    }

    /// <summary>Las solicitudes ajenas responden 404: un trabajador solo gestiona las suyas.</summary>
    private async Task<SolicitudPermiso> CargarPropiaAsync(Guid empresaId, Guid trabajadorId, Guid solicitudId, CancellationToken ct) =>
        await _db.SolicitudesPermiso.FirstOrDefaultAsync(x => x.SolicitudPermisoId == solicitudId && x.EmpresaId == empresaId && x.TrabajadorId == trabajadorId, ct)
        ?? throw ServiceException.NotFound("Solicitud no encontrada.");

    private void AplicarRowVersion(SolicitudPermiso s, string rowVersion)
    {
        byte[] enviada;
        try { enviada = Convert.FromBase64String(rowVersion); }
        catch (FormatException) { throw ServiceException.Validation("La versión de la solicitud no es válida."); }
        if (s.RowVersion is { } actual && !actual.AsSpan().SequenceEqual(enviada))
            throw ServiceException.Conflict("La solicitud fue modificada por otra operación. Recargue e intente de nuevo.");
        _db.Entry(s).Property(x => x.RowVersion).OriginalValue = enviada;
    }

    private async Task AsegurarPuedeVerOtrosAsync(PermisosHelper.Alcance alcance, CancellationToken ct)
    {
        if (!alcance.Todos && !await _permisos.TienePermisoAsync(Permisos.PermisosAprobar, ct))
            throw ServiceException.Forbidden("No tiene permiso para consultar solicitudes de otros trabajadores.");
    }

    private static IQueryable<SolicitudPermiso> Filtrar(IQueryable<SolicitudPermiso> q, SolicitudPermisoFiltro f)
    {
        if (f.TrabajadorId is { } t) q = q.Where(s => s.TrabajadorId == t);
        if (f.TipoPermisoId is { } tp) q = q.Where(s => s.TipoPermisoId == tp);
        if (f.Estado is { } e) q = q.Where(s => s.Estado == e);
        if (f.Desde is { } d) { var ini = d.ToDateTime(TimeOnly.MinValue); q = q.Where(s => s.FechaFin >= ini); }
        if (f.Hasta is { } h) { var fin = h.ToDateTime(new TimeOnly(23, 59, 59)); q = q.Where(s => s.FechaInicio <= fin); }
        return q;
    }

    private sealed record Fila(SolicitudPermiso S, string Nombre, string Tipo, byte Unidad, Guid? Revisor, DateTime? FechaDecision, string? Comentario, bool CancelacionPendiente);

    private static IQueryable<Fila> Proyectar(IQueryable<SolicitudPermiso> q) => q.Select(s => new Fila(
        s, s.Trabajador.Nombres + " " + s.Trabajador.Apellidos, s.TipoPermiso.Nombre, s.TipoPermiso.UnidadBase,
        s.AprobacionesPermiso.OrderByDescending(a => a.AprobacionPermisoId).Select(a => (Guid?)a.RevisorUsuarioId).FirstOrDefault(),
        s.AprobacionesPermiso.OrderByDescending(a => a.AprobacionPermisoId).Select(a => (DateTime?)a.FechaDecisionUtc).FirstOrDefault(),
        s.AprobacionesPermiso.OrderByDescending(a => a.AprobacionPermisoId).Select(a => a.Comentario).FirstOrDefault(),
        s.SolicitudesCancelacionPermiso.Any(c => c.Estado == EstadoCancelacionPermiso.Pendiente)));

    private async Task<PagedResult<SolicitudPermisoDto>> PaginarAsync(IQueryable<SolicitudPermiso> q, PageQuery page, Guid? miTrabajadorId, CancellationToken ct)
    {
        var verMotivo = await _permisos.TienePermisoAsync(Permisos.PermisosVerMotivoPrivado, ct);
        var pagina = await Proyectar(q.OrderByDescending(s => s.FechaInicio).ThenByDescending(s => s.SolicitadaUtc)).ToPagedAsync(page, ct);
        return new PagedResult<SolicitudPermisoDto>
        {
            Items = pagina.Items.Select(x => ToDto(x, verMotivo || x.S.TrabajadorId == miTrabajadorId)).ToList(),
            Page = pagina.Page, PageSize = pagina.PageSize, Total = pagina.Total
        };
    }

    private async Task<SolicitudPermisoDto> DtoAsync(Guid empresaId, Guid solicitudId, Guid? miTrabajadorId, CancellationToken ct)
    {
        var verMotivo = await _permisos.TienePermisoAsync(Permisos.PermisosVerMotivoPrivado, ct);
        var fila = await Proyectar(_db.SolicitudesPermiso.AsNoTracking().Where(s => s.SolicitudPermisoId == solicitudId && s.EmpresaId == empresaId)).FirstAsync(ct);
        return ToDto(fila, verMotivo || fila.S.TrabajadorId == miTrabajadorId);
    }

    private static SolicitudPermisoDto ToDto(Fila f, bool verMotivo) => new()
    {
        SolicitudPermisoId = f.S.SolicitudPermisoId, TrabajadorId = f.S.TrabajadorId, TrabajadorNombre = f.Nombre,
        TipoPermisoId = f.S.TipoPermisoId, TipoPermiso = f.Tipo, Unidad = f.Unidad, FechaInicio = f.S.FechaInicio, FechaFin = f.S.FechaFin,
        EsPorHoras = f.S.EsPorHoras, Cantidad = f.S.CantidadSolicitada, Motivo = verMotivo ? f.S.MotivoPrivado : null, Estado = f.S.Estado,
        SolicitadaUtc = f.S.SolicitadaUtc, ModificadaUtc = f.S.ModificadaUtc, RevisorUsuarioId = f.Revisor, FechaDecisionUtc = f.FechaDecision,
        ComentarioDecision = f.Comentario, CancelacionPendiente = f.CancelacionPendiente,
        RowVersion = Convert.ToBase64String(f.S.RowVersion ?? Array.Empty<byte>())
    };
}
