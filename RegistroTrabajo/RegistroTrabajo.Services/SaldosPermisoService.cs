using Microsoft.EntityFrameworkCore;
using RegistroTrabajo.Data;
using RegistroTrabajo.Data.Entities;
using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public interface ISaldosPermisoService
{
    /// <summary>Saldos de un trabajador (por defecto el propio) para un año (por defecto el actual).</summary>
    Task<IReadOnlyList<SaldoPermisoDto>> ListarAsync(SaldoFiltro filtro, CancellationToken ct);
    /// <summary>Libro de movimientos de un saldo: asignaciones, consumos, devoluciones y ajustes.</summary>
    Task<IReadOnlyList<MovimientoSaldoDto>> MovimientosAsync(Guid saldoId, CancellationToken ct);
    /// <summary>Ajuste administrativo (positivo o negativo) con motivo obligatorio; queda en el libro y en la auditoría.</summary>
    Task<SaldoPermisoDto> AjustarAsync(AjustarSaldoRequest request, CancellationToken ct);
    /// <summary>Crea los saldos del año que falten para los trabajadores activos según las reglas vigentes. Idempotente.</summary>
    Task<int> GenerarAnualAsync(short anio, CancellationToken ct);
}

/// <summary>
/// Saldos por trabajador, tipo y año. Cada cambio es una fila del libro (MovimientosSaldoPermiso); el saldo es el acumulado
/// con RowVersion para detectar modificaciones concurrentes. Ver <see cref="TipoMovimientoSaldo"/> para el significado de cada movimiento.
/// </summary>
public class SaldosPermisoService : ISaldosPermisoService
{
    private readonly RegistroTrabajoDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IPermisoService _permisos;
    private readonly IAuditoriaService _auditoria;
    private readonly TimeProvider _clock;

    public SaldosPermisoService(RegistroTrabajoDbContext db, ICurrentUser current, IPermisoService permisos, IAuditoriaService auditoria, TimeProvider clock)
    {
        _db = db; _current = current; _permisos = permisos; _auditoria = auditoria; _clock = clock;
    }

    public async Task<IReadOnlyList<SaldoPermisoDto>> ListarAsync(SaldoFiltro f, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var mi = await TrabajadorHelper.IdDeUsuarioAsync(_db, empresaId, _current.RequireUsuarioId(), ct);
        var trabajadorId = f.TrabajadorId ?? mi ?? throw ServiceException.NotFound("Su cuenta no está vinculada a un expediente de trabajador.");
        await AsegurarPuedeVerAsync(trabajadorId, mi, ct);
        var anio = f.Anio ?? (short)_clock.GetUtcNow().Year;
        return await _db.SaldosPermiso.AsNoTracking()
            .Where(s => s.EmpresaId == empresaId && s.TrabajadorId == trabajadorId && s.PeriodoAnio == anio)
            .OrderBy(s => s.TipoPermiso.Nombre)
            .Select(Proyeccion).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MovimientoSaldoDto>> MovimientosAsync(Guid saldoId, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var mi = await TrabajadorHelper.IdDeUsuarioAsync(_db, empresaId, _current.RequireUsuarioId(), ct);
        var trabajadorId = await _db.SaldosPermiso.AsNoTracking().Where(s => s.SaldoPermisoId == saldoId && s.EmpresaId == empresaId)
            .Select(s => (Guid?)s.TrabajadorId).FirstOrDefaultAsync(ct)
            ?? throw ServiceException.NotFound("Saldo no encontrado.");
        try { await AsegurarPuedeVerAsync(trabajadorId, mi, ct); }
        catch (ServiceException e) when (e.Kind == ServiceErrorKind.Forbidden) { throw ServiceException.NotFound("Saldo no encontrado."); }
        return await _db.MovimientosSaldoPermiso.AsNoTracking().Where(m => m.EmpresaId == empresaId && m.SaldoPermisoId == saldoId)
            .OrderBy(m => m.MovimientoSaldoPermisoId)
            .Select(m => new MovimientoSaldoDto
            {
                MovimientoSaldoPermisoId = m.MovimientoSaldoPermisoId, SaldoPermisoId = m.SaldoPermisoId, TipoMovimiento = m.TipoMovimiento,
                Cantidad = m.Cantidad, Descripcion = m.Descripcion, SolicitudPermisoId = m.SolicitudPermisoId,
                RegistradoPorUsuarioId = m.RegistradoPorUsuarioId, FechaUtc = m.FechaUtc
            }).ToListAsync(ct);
    }

    public async Task<SaldoPermisoDto> AjustarAsync(AjustarSaldoRequest r, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var actorId = _current.RequireUsuarioId();
        var now = _clock.GetUtcNow().UtcDateTime;
        var motivo = (r.Motivo ?? "").Trim();
        if (motivo.Length < 3) throw ServiceException.Validation("Indique el motivo del ajuste.");
        if (r.Cantidad == 0) throw ServiceException.Validation("La cantidad del ajuste no puede ser cero.");
        if (decimal.Round(r.Cantidad, 2) != r.Cantidad) throw ServiceException.Validation("La cantidad admite como máximo dos decimales.");

        var t = await TrabajadorHelper.CargarAsync(_db, empresaId, r.TrabajadorId, false, ct);
        if (t.EstadoLaboral == EstadoLaboral.Baja) throw ServiceException.Validation("El trabajador está dado de baja.");
        var tipo = await _db.TiposPermiso.AsNoTracking().FirstOrDefaultAsync(x => x.TipoPermisoId == r.TipoPermisoId && x.EmpresaId == empresaId, ct)
            ?? throw ServiceException.Validation("El tipo de permiso indicado no existe.");

        var regla = await PermisosHelper.ReglaVigenteAsync(_db, empresaId, tipo.TipoPermisoId, FechaReferencia(r.PeriodoAnio), ct);
        var existia = await _db.SaldosPermiso.AnyAsync(s => s.EmpresaId == empresaId && s.TrabajadorId == t.TrabajadorId
            && s.TipoPermisoId == tipo.TipoPermisoId && s.PeriodoAnio == r.PeriodoAnio, ct);
        var saldo = await PermisosHelper.ObtenerOCrearSaldoAsync(_db, empresaId, t.TrabajadorId, tipo.TipoPermisoId, r.PeriodoAnio, regla, actorId, now, ct);
        var antes = new { saldo.CantidadAsignada, existia };

        var nuevaAsignada = saldo.CantidadAsignada + r.Cantidad;
        if (nuevaAsignada < 0) throw ServiceException.Validation("El ajuste dejaría el saldo asignado en negativo.");
        if (r.Cantidad < 0 && nuevaAsignada < saldo.CantidadUtilizada + saldo.CantidadReservada && !(regla?.PermiteSaldoNegativo ?? false))
            throw ServiceException.Validation("El ajuste dejaría el saldo por debajo de lo ya utilizado o reservado.");

        saldo.CantidadAsignada = nuevaAsignada;
        saldo.FechaActualizacionUtc = now;
        _db.MovimientosSaldoPermiso.Add(r.Cantidad > 0
            ? PermisosHelper.Movimiento(saldo, null, TipoMovimientoSaldo.Asignacion, r.Cantidad, $"Ajuste: {motivo}", actorId, now)
            : PermisosHelper.Movimiento(saldo, null, TipoMovimientoSaldo.Ajuste, -r.Cantidad, $"Ajuste: {motivo}", actorId, now));
        _auditoria.Registrar("Permisos", nameof(SaldoPermiso), saldo.SaldoPermisoId.ToString(), "AjustarSaldoPermiso", antes,
            new { saldo.CantidadAsignada, r.Cantidad, motivo, r.TrabajadorId, r.TipoPermisoId, r.PeriodoAnio });
        await _db.SaveSafeAsync(ct);

        return await _db.SaldosPermiso.AsNoTracking().Where(s => s.SaldoPermisoId == saldo.SaldoPermisoId).Select(Proyeccion).FirstAsync(ct);
    }

    public async Task<int> GenerarAnualAsync(short anio, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var actorId = _current.RequireUsuarioId();
        var now = _clock.GetUtcNow().UtcDateTime;
        if (anio < 2000 || anio > 2200) throw ServiceException.Validation("El año no es válido.");
        var fin = new DateOnly(anio, 12, 31);
        var referencia = FechaReferencia(anio);

        var tipos = await _db.TiposPermiso.AsNoTracking().Where(t => t.EmpresaId == empresaId && t.Estado).ToListAsync(ct);
        var trabajadores = await _db.Trabajadores.AsNoTracking()
            .Where(t => t.EmpresaId == empresaId && t.EstadoLaboral == EstadoLaboral.Activo && t.FechaIngreso <= fin)
            .Select(t => t.TrabajadorId).ToListAsync(ct);
        var existentes = (await _db.SaldosPermiso.AsNoTracking().Where(s => s.EmpresaId == empresaId && s.PeriodoAnio == anio)
            .Select(s => new { s.TrabajadorId, s.TipoPermisoId }).ToListAsync(ct)).Select(s => (s.TrabajadorId, s.TipoPermisoId)).ToHashSet();

        var creados = 0;
        foreach (var tipo in tipos)
        {
            var regla = await PermisosHelper.ReglaVigenteAsync(_db, empresaId, tipo.TipoPermisoId, referencia, ct);
            if (regla?.CantidadAnual is not > 0) continue; // sin cantidad anual el tipo no controla saldo
            foreach (var trabajadorId in trabajadores)
            {
                if (existentes.Contains((trabajadorId, tipo.TipoPermisoId))) continue;
                await PermisosHelper.ObtenerOCrearSaldoAsync(_db, empresaId, trabajadorId, tipo.TipoPermisoId, anio, regla, actorId, now, ct);
                creados++;
            }
        }
        if (creados > 0)
        {
            _auditoria.Registrar("Permisos", nameof(SaldoPermiso), null, "GenerarSaldosAnuales", null, new { anio, creados });
            await _db.SaveSafeAsync(ct);
        }
        return creados;
    }

    // ---- apoyo ----
    /// <summary>El propio trabajador, o saldos.consultar dentro del alcance (toda la empresa o sus supervisados).</summary>
    private async Task AsegurarPuedeVerAsync(Guid trabajadorId, Guid? mi, CancellationToken ct)
    {
        if (trabajadorId == mi) return;
        if (!await _permisos.TienePermisoAsync(Permisos.SaldosConsultar, ct))
            throw ServiceException.Forbidden("No tiene permiso para consultar saldos de otros trabajadores.");
        var alcance = await PermisosHelper.AlcanceAsync(_db, _current, _permisos, _clock, false, ct);
        if (!alcance.Incluye(trabajadorId)) throw ServiceException.Forbidden("No tiene autorización para consultar los saldos de este trabajador.");
    }

    /// <summary>Fecha con la que se elige la regla de un año: hoy si es el año en curso; si no, su primer día.</summary>
    private DateOnly FechaReferencia(short anio)
    {
        var hoy = TrabajadorHelper.Hoy(_clock);
        return hoy.Year == anio ? hoy : new DateOnly(anio, 1, 1);
    }

    // Expresión (no método) para que EF la traduzca a SQL junto con los datos del trabajador y del tipo.
    private static readonly System.Linq.Expressions.Expression<Func<SaldoPermiso, SaldoPermisoDto>> Proyeccion = s => new SaldoPermisoDto
    {
        SaldoPermisoId = s.SaldoPermisoId, TrabajadorId = s.TrabajadorId, TrabajadorNombre = s.Trabajador.Nombres + " " + s.Trabajador.Apellidos,
        TipoPermisoId = s.TipoPermisoId, TipoPermiso = s.TipoPermiso.Nombre, Unidad = s.TipoPermiso.UnidadBase, PeriodoAnio = s.PeriodoAnio,
        Asignada = s.CantidadAsignada, Utilizada = s.CantidadUtilizada, Reservada = s.CantidadReservada,
        Disponible = s.CantidadAsignada - s.CantidadUtilizada - s.CantidadReservada
    };
}
