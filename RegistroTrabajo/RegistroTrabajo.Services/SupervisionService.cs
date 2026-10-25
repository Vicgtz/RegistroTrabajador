using Microsoft.EntityFrameworkCore;
using RegistroTrabajo.Data;
using RegistroTrabajo.Data.Entities;
using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public interface ISupervisionService
{
    /// <summary>Supervisores del trabajador, vigentes y pasados (más recientes primero).</summary>
    Task<IReadOnlyList<SupervisorAsignadoDto>> ListarAsync(Guid trabajadorId, CancellationToken ct);
    Task<SupervisorAsignadoDto> AsignarAsync(Guid trabajadorId, AsignarSupervisorRequest request, CancellationToken ct);
    Task<SupervisorAsignadoDto> FinalizarAsync(Guid trabajadorId, long supervisionId, FinalizarSupervisionRequest request, CancellationToken ct);
    /// <summary>Trabajadores que tienen vigente al trabajador indicado como supervisor.</summary>
    Task<IReadOnlyList<TrabajadorResumenDto>> SupervisadosAsync(Guid supervisorTrabajadorId, CancellationToken ct);
}

/// <summary>
/// Relaciones de supervisión con vigencia. Un trabajador puede tener varios supervisores y un supervisor trabajadores
/// de distintos departamentos; los cambios nunca se borran (se cierran con FechaFin) y quedan en HistorialLaboral.
/// </summary>
public class SupervisionService : ISupervisionService
{
    private readonly RegistroTrabajoDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IAuditoriaService _auditoria;
    private readonly TimeProvider _clock;

    public SupervisionService(RegistroTrabajoDbContext db, ICurrentUser current, IAuditoriaService auditoria, TimeProvider clock)
    {
        _db = db; _current = current; _auditoria = auditoria; _clock = clock;
    }

    public async Task<IReadOnlyList<SupervisorAsignadoDto>> ListarAsync(Guid trabajadorId, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        await TrabajadorHelper.CargarAsync(_db, empresaId, trabajadorId, false, ct);
        return await _db.TrabajadorSupervisores.AsNoTracking()
            .Where(s => s.EmpresaId == empresaId && s.TrabajadorId == trabajadorId)
            .OrderByDescending(s => s.FechaFin == null).ThenByDescending(s => s.FechaInicio)
            .Select(s => new SupervisorAsignadoDto
            {
                TrabajadorSupervisorId = s.TrabajadorSupervisorId, TrabajadorId = s.TrabajadorId,
                SupervisorTrabajadorId = s.SupervisorTrabajadorId,
                SupervisorNombre = s.Supervisor.Nombres + " " + s.Supervisor.Apellidos,
                DepartamentoId = s.DepartamentoId, FechaInicio = s.FechaInicio, FechaFin = s.FechaFin, EsPrincipal = s.EsPrincipal
            }).ToListAsync(ct);
    }

    public async Task<SupervisorAsignadoDto> AsignarAsync(Guid trabajadorId, AsignarSupervisorRequest r, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var actorId = _current.RequireUsuarioId();
        var now = _clock.GetUtcNow().UtcDateTime;
        var hoy = TrabajadorHelper.Hoy(_clock);
        var inicio = r.FechaInicio ?? hoy;

        var t = await TrabajadorHelper.CargarAsync(_db, empresaId, trabajadorId, false, ct);
        if (t.EstadoLaboral == EstadoLaboral.Baja) throw ServiceException.Validation("El trabajador está dado de baja.");
        if (r.SupervisorTrabajadorId == trabajadorId) throw ServiceException.Validation("Un trabajador no puede supervisarse a sí mismo.");
        var sup = await _db.Trabajadores.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TrabajadorId == r.SupervisorTrabajadorId && x.EmpresaId == empresaId, ct)
            ?? throw ServiceException.Validation("El supervisor indicado no existe en la empresa.");
        if (sup.EstadoLaboral != EstadoLaboral.Activo) throw ServiceException.Validation("El supervisor debe estar activo.");
        if (inicio < t.FechaIngreso) throw ServiceException.Validation("La supervisión no puede comenzar antes del ingreso del trabajador.");
        if (r.DepartamentoId is { } d && !await _db.Departamentos.AnyAsync(x => x.DepartamentoId == d && x.EmpresaId == empresaId, ct))
            throw ServiceException.Validation("El departamento indicado no existe.");

        if (await _db.TrabajadorSupervisores.AnyAsync(s => s.EmpresaId == empresaId && s.TrabajadorId == trabajadorId
            && s.SupervisorTrabajadorId == r.SupervisorTrabajadorId && (s.FechaFin == null || s.FechaFin >= inicio), ct))
            throw ServiceException.Conflict("Ese supervisor ya está asignado al trabajador.");
        // Evita ciclos directos (A supervisa a B y B supervisa a A).
        if (await _db.TrabajadorSupervisores.AnyAsync(s => s.EmpresaId == empresaId && s.TrabajadorId == r.SupervisorTrabajadorId
            && s.SupervisorTrabajadorId == trabajadorId && (s.FechaFin == null || s.FechaFin >= hoy), ct))
            throw ServiceException.Conflict("El supervisor indicado ya es supervisado por este trabajador.");

        if (r.EsPrincipal)
        {
            var principales = await _db.TrabajadorSupervisores.Where(s => s.EmpresaId == empresaId && s.TrabajadorId == trabajadorId
                && s.EsPrincipal && (s.FechaFin == null || s.FechaFin >= hoy)).ToListAsync(ct);
            foreach (var p in principales) p.EsPrincipal = false;
        }

        var fila = new TrabajadorSupervisor
        {
            EmpresaId = empresaId, TrabajadorId = trabajadorId, SupervisorTrabajadorId = r.SupervisorTrabajadorId,
            DepartamentoId = r.DepartamentoId, FechaInicio = inicio, EsPrincipal = r.EsPrincipal
        };
        _db.TrabajadorSupervisores.Add(fila);
        var nombre = $"{sup.Nombres} {sup.Apellidos}";
        _db.HistorialLaboral.Add(TrabajadorHelper.Historial(t, TipoCambioLaboral.Supervisor, null,
            new { accion = "Asignado", supervisorTrabajadorId = sup.TrabajadorId, supervisor = nombre, r.EsPrincipal }, inicio, null, actorId, now));
        _auditoria.Registrar("Supervision", nameof(TrabajadorSupervisor), trabajadorId.ToString(), "AsignarSupervisor", null,
            new { supervisorTrabajadorId = sup.TrabajadorId, fechaInicio = inicio, r.EsPrincipal });
        await _db.SaveSafeAsync(ct);
        return ToDto(fila, nombre);
    }

    public async Task<SupervisorAsignadoDto> FinalizarAsync(Guid trabajadorId, long supervisionId, FinalizarSupervisionRequest r, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var actorId = _current.RequireUsuarioId();
        var now = _clock.GetUtcNow().UtcDateTime;
        var fin = r.FechaFin ?? TrabajadorHelper.Hoy(_clock);

        var t = await TrabajadorHelper.CargarAsync(_db, empresaId, trabajadorId, false, ct);
        var fila = await _db.TrabajadorSupervisores.FirstOrDefaultAsync(s => s.TrabajadorSupervisorId == supervisionId
            && s.EmpresaId == empresaId && s.TrabajadorId == trabajadorId, ct)
            ?? throw ServiceException.NotFound("Relación de supervisión no encontrada.");
        if (fila.FechaFin is { } ya && ya <= fin) throw ServiceException.Conflict("La supervisión ya está finalizada.");
        if (fin < fila.FechaInicio) throw ServiceException.Validation("La fecha de fin no puede ser anterior al inicio.");

        var sup = await _db.Trabajadores.AsNoTracking().Where(x => x.TrabajadorId == fila.SupervisorTrabajadorId && x.EmpresaId == empresaId)
            .Select(x => x.Nombres + " " + x.Apellidos).FirstAsync(ct);
        fila.FechaFin = fin;
        _db.HistorialLaboral.Add(TrabajadorHelper.Historial(t, TipoCambioLaboral.Supervisor,
            new { accion = "Asignado", supervisorTrabajadorId = fila.SupervisorTrabajadorId, supervisor = sup },
            new { accion = "Finalizado", supervisorTrabajadorId = fila.SupervisorTrabajadorId, supervisor = sup }, fin, null, actorId, now));
        _auditoria.Registrar("Supervision", nameof(TrabajadorSupervisor), trabajadorId.ToString(), "FinalizarSupervision",
            new { supervisionId, fechaFin = (DateOnly?)null }, new { supervisionId, fechaFin = fin });
        await _db.SaveSafeAsync(ct);
        return ToDto(fila, sup);
    }

    public async Task<IReadOnlyList<TrabajadorResumenDto>> SupervisadosAsync(Guid supervisorTrabajadorId, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        await TrabajadorHelper.CargarAsync(_db, empresaId, supervisorTrabajadorId, false, ct);
        var hoy = TrabajadorHelper.Hoy(_clock);
        return await _db.TrabajadorSupervisores.AsNoTracking()
            .Where(s => s.EmpresaId == empresaId && s.SupervisorTrabajadorId == supervisorTrabajadorId
                && s.FechaInicio <= hoy && (s.FechaFin == null || s.FechaFin >= hoy))
            .Select(s => s.Supervisado).Distinct().OrderBy(t => t.Apellidos).ThenBy(t => t.Nombres)
            .Select(t => new TrabajadorResumenDto
            {
                TrabajadorId = t.TrabajadorId, NumeroEmpleado = t.NumeroEmpleado, Nombres = t.Nombres, Apellidos = t.Apellidos,
                DepartamentoId = t.DepartamentoId, Departamento = t.Departamento != null ? t.Departamento.Nombre : null,
                PuestoId = t.PuestoId, Puesto = t.Puesto != null ? t.Puesto.Nombre : null, EstadoLaboral = t.EstadoLaboral
            }).ToListAsync(ct);
    }

    private static SupervisorAsignadoDto ToDto(TrabajadorSupervisor s, string nombre) => new()
    {
        TrabajadorSupervisorId = s.TrabajadorSupervisorId, TrabajadorId = s.TrabajadorId, SupervisorTrabajadorId = s.SupervisorTrabajadorId,
        SupervisorNombre = nombre, DepartamentoId = s.DepartamentoId, FechaInicio = s.FechaInicio, FechaFin = s.FechaFin, EsPrincipal = s.EsPrincipal
    };
}
