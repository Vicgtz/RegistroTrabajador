using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RegistroTrabajo.Data;
using RegistroTrabajo.Data.Entities;
using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public interface IReporteService
{
    /// <summary>Reporte paginado dentro del alcance del usuario. Pantalla y exportación comparten el mismo código de consulta.</summary>
    Task<ReporteDto> ConsultarAsync(string tipo, ReporteFiltro filtro, CancellationToken ct);
}

public interface IExportacionService
{
    /// <summary>Exporta el reporte con los mismos filtros, alcance y exclusiones que la pantalla. Exige <c>reportes.exportar</c>.</summary>
    Task<ArchivoExportadoDto> ExportarAsync(string tipo, string formato, ReporteFiltro filtro, CancellationToken ct);
}

/// <summary>Resultado interno de un proveedor: celdas con su tipo real (texto, número, fecha) para pantalla, Excel y PDF.</summary>
internal sealed class ReporteResultado
{
    public List<ColumnaReporteDto> Columnas { get; init; } = new();
    public List<object?[]> Filas { get; init; } = new();
    public int Total { get; set; }
    public List<ResumenReporteDto> Resumen { get; } = new();
    public string? Nota { get; set; }
}

internal sealed record ReporteContexto(Guid EmpresaId, Guid UsuarioId, TimeZoneInfo Zona, DateOnly Hoy, CalendarioHabil Cal, DateOnly? Desde, DateOnly? Hasta);

public class ReporteService : IReporteService
{
    internal const int MaxPagina = 200;
    /// <summary>Tope de filas por consulta cuando se pide todo (exportaciones).</summary>
    internal const int MaxFilasExportacion = 10_000;
    private const int MaxFilasResumen = 50_000;
    private const int MaxDiasRango = 1830;

    private readonly RegistroTrabajoDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IPermisoService _permisos;
    private readonly TimeProvider _clock;

    public ReporteService(RegistroTrabajoDbContext db, ICurrentUser current, IPermisoService permisos, TimeProvider clock)
    {
        _db = db; _current = current; _permisos = permisos; _clock = clock;
    }

    public async Task<ReporteDto> ConsultarAsync(string tipo, ReporteFiltro f, CancellationToken ct)
    {
        var pageSize = Math.Clamp(f.PageSize, 1, MaxPagina);
        var page = Math.Max(1, f.Page);
        var (r, filtros, titulo, c) = await EjecutarAsync(tipo, f, (page - 1) * pageSize, pageSize, ct);
        return Dto(tipo, titulo, r, filtros, page, pageSize, c.Zona);
    }

    /// <summary>Todas las filas (hasta el tope) para exportar; <see cref="ConsultarAsync"/> y la exportación pasan por <see cref="EjecutarAsync"/>.</summary>
    internal async Task<(ReporteResultado Resultado, List<string> Filtros, string Titulo, ReporteContexto Contexto)> TodoAsync(string tipo, ReporteFiltro f, CancellationToken ct)
    {
        var res = await EjecutarAsync(tipo, f, 0, MaxFilasExportacion + 1, ct);
        if (res.Resultado.Total > MaxFilasExportacion)
            throw ServiceException.Validation($"El reporte tiene {res.Resultado.Total} filas y el máximo exportable es {MaxFilasExportacion}. Acote los filtros (fechas, departamento, trabajador…).");
        return res;
    }

    private async Task<(ReporteResultado Resultado, List<string> Filtros, string Titulo, ReporteContexto Contexto)> EjecutarAsync(string tipo, ReporteFiltro f, int skip, int take, CancellationToken ct)
    {
        if (!TipoReporte.Todos.Contains(tipo)) throw ServiceException.NotFound("Reporte no encontrado.");
        await _permisos.AsegurarPermisoAsync(Permisos.ReportesConsultar, ct);
        var empresaId = _current.RequireEmpresaId();
        var cfg = await _db.ConfiguracionEmpresa.AsNoTracking().Where(x => x.EmpresaId == empresaId).Select(x => x.ZonaHorariaId).FirstOrDefaultAsync(ct);
        var zona = AsistenciaHelper.Zona(cfg);
        var hoy = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(_clock.GetUtcNow().UtcDateTime, zona));
        var (desde, hasta) = Rango(f, hoy);
        var cal = await CalendarioHabil.CargarAsync(_db, empresaId, ct);
        var c = new ReporteContexto(empresaId, _current.RequireUsuarioId(), zona, hoy, cal, desde, hasta);

        var r = tipo switch
        {
            TipoReporte.Trabajadores => await TrabajadoresAsync(c, f, skip, take, ct),
            TipoReporte.Movimientos => await MovimientosAsync(c, f, skip, take, ct),
            TipoReporte.Permisos => await PermisosAsync(c, f, skip, take, ct),
            TipoReporte.Asistencia => await AsistenciaAsync(c, f, skip, take, ct),
            TipoReporte.Proyectos => await ProyectosAsync(c, f, skip, take, ct),
            TipoReporte.Tareas => await TareasAsync(c, f, skip, take, ct),
            _ => await CargaAsync(c, f, skip, take, ct)
        };
        return (r, await DescribirFiltrosAsync(c, f, ct), TipoReporte.Nombre(tipo), c);
    }

    // ---------------- Filtros y formato ----------------
    private static (DateOnly? Desde, DateOnly? Hasta) Rango(ReporteFiltro f, DateOnly hoy)
    {
        DateOnly? desde = f.Desde, hasta = f.Hasta;
        if (!string.IsNullOrWhiteSpace(f.Periodo))
        {
            var anio = f.Anio ?? hoy.Year;
            switch (f.Periodo.Trim().ToLowerInvariant())
            {
                case "anio":
                    desde = new DateOnly(anio, 1, 1); hasta = new DateOnly(anio, 12, 31); break;
                case "mes":
                {
                    var m = f.Numero ?? hoy.Month;
                    if (m is < 1 or > 12) throw ServiceException.Validation("El mes debe estar entre 1 y 12.");
                    desde = new DateOnly(anio, m, 1); hasta = desde.Value.AddMonths(1).AddDays(-1); break;
                }
                case "trimestre":
                {
                    var q = f.Numero ?? ((hoy.Month - 1) / 3 + 1);
                    if (q is < 1 or > 4) throw ServiceException.Validation("El trimestre debe estar entre 1 y 4.");
                    desde = new DateOnly(anio, (q - 1) * 3 + 1, 1); hasta = desde.Value.AddMonths(3).AddDays(-1); break;
                }
                default:
                    throw ServiceException.Validation("El periodo debe ser mes, trimestre o anio.");
            }
        }
        if (desde is { } d && hasta is { } h)
        {
            if (h < d) throw ServiceException.Validation("La fecha final no puede ser anterior a la inicial.");
            if (h.DayNumber - d.DayNumber > MaxDiasRango) throw ServiceException.Validation("El rango de fechas es demasiado amplio.");
        }
        return (desde, hasta);
    }

    private async Task<List<string>> DescribirFiltrosAsync(ReporteContexto c, ReporteFiltro f, CancellationToken ct)
    {
        var l = new List<string>();
        if (c.Desde is { } d) l.Add($"Desde: {d:dd/MM/yyyy}");
        if (c.Hasta is { } h) l.Add($"Hasta: {h:dd/MM/yyyy}");
        if (f.TrabajadorId is { } t) l.Add($"Trabajador: {(await ProyectoHelper.NombresTrabajadoresAsync(_db, c.EmpresaId, new[] { t }, ct)).GetValueOrDefault(t, "—")}");
        if (f.SupervisorId is { } s) l.Add($"Supervisor: {(await ProyectoHelper.NombresTrabajadoresAsync(_db, c.EmpresaId, new[] { s }, ct)).GetValueOrDefault(s, "—")}");
        if (f.DepartamentoId is { } dep) l.Add($"Departamento: {await _db.Departamentos.AsNoTracking().Where(x => x.EmpresaId == c.EmpresaId && x.DepartamentoId == dep).Select(x => x.Nombre).FirstOrDefaultAsync(ct) ?? "—"}");
        if (f.PuestoId is { } pu) l.Add($"Puesto: {await _db.Puestos.AsNoTracking().Where(x => x.EmpresaId == c.EmpresaId && x.PuestoId == pu).Select(x => x.Nombre).FirstOrDefaultAsync(ct) ?? "—"}");
        if (f.TipoPermisoId is { } tp) l.Add($"Tipo de permiso: {await _db.TiposPermiso.AsNoTracking().Where(x => x.EmpresaId == c.EmpresaId && x.TipoPermisoId == tp).Select(x => x.Nombre).FirstOrDefaultAsync(ct) ?? "—"}");
        if (f.ProyectoId is { } pr) l.Add($"Proyecto: {await _db.Proyectos.AsNoTracking().Where(x => x.EmpresaId == c.EmpresaId && x.ProyectoId == pr).Select(x => x.Nombre).FirstOrDefaultAsync(ct) ?? "—"}");
        if (f.Estado is { } e) l.Add($"Estado: {e}");
        if (!string.IsNullOrWhiteSpace(f.Buscar)) l.Add($"Búsqueda: {f.Buscar.Trim()}");
        if (f.SoloIncompletos) l.Add("Solo jornadas incompletas");
        if (f.SoloVencidas) l.Add("Solo vencidas");
        return l;
    }

    internal static string Texto(object? v) => v switch
    {
        null => "",
        DateOnly d => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
        DateTime dt => dt.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
        decimal m => m.ToString("0.##", CultureInfo.InvariantCulture),
        bool b => b ? "Sí" : "No",
        IFormattable x => x.ToString(null, CultureInfo.InvariantCulture),
        _ => v.ToString() ?? ""
    };

    private static ReporteDto Dto(string tipo, string titulo, ReporteResultado r, List<string> filtros, int page, int pageSize, TimeZoneInfo zona) => new()
    {
        Tipo = tipo, Titulo = titulo, Nota = r.Nota, GeneradoUtc = DateTime.UtcNow, FiltrosAplicados = filtros, Columnas = r.Columnas, Resumen = r.Resumen,
        Filas = r.Filas.Select(f => f.Select(Texto).Select(s => (string?)s).ToList()).ToList(), Page = page, PageSize = pageSize, Total = r.Total
    };

    private static ColumnaReporteDto Col(string clave, string titulo, string tipo = "texto", bool ordenable = false) =>
        new() { Clave = clave, Titulo = titulo, Tipo = tipo, Ordenable = ordenable };

    private DateTime? Local(DateTime? utc, TimeZoneInfo zona) =>
        utc is { } u ? TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(u, DateTimeKind.Utc), zona) : null;

    // ---------------- Alcances ----------------
    private sealed record AlcanceTrab(bool Todos, Guid? Mi, List<Guid> Ids);

    private async Task<List<Guid>> SupervisadosVigentesAsync(ReporteContexto c, Guid supervisor, CancellationToken ct) =>
        await _db.TrabajadorSupervisores.AsNoTracking()
            .Where(s => s.EmpresaId == c.EmpresaId && s.SupervisorTrabajadorId == supervisor && s.FechaInicio <= c.Hoy && (s.FechaFin == null || s.FechaFin >= c.Hoy))
            .Select(s => s.TrabajadorId).Distinct().ToListAsync(ct);

    /// <summary>Directorio completo para quien edita trabajadores (Administrador y RR. HH.); el supervisor ve a su equipo y cada trabajador, lo propio.</summary>
    private async Task<AlcanceTrab> AlcanceTrabajadoresAsync(ReporteContexto c, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.TrabajadoresConsultar, ct);
        var mi = await TrabajadorHelper.IdDeUsuarioAsync(_db, c.EmpresaId, c.UsuarioId, ct);
        if (await _permisos.TienePermisoAsync(Permisos.TrabajadoresEditar, ct)) return new AlcanceTrab(true, mi, new());
        var ids = mi is { } m ? await SupervisadosVigentesAsync(c, m, ct) : new List<Guid>();
        if (mi is { } y) ids.Add(y);
        if (ids.Count == 0) throw ServiceException.Forbidden("No tiene trabajadores a los que dar seguimiento.");
        return new AlcanceTrab(false, mi, ids);
    }

    private void ExigirEnAlcance(AlcanceTrab a, Guid trabajadorId)
    {
        if (!a.Todos && !a.Ids.Contains(trabajadorId)) throw ServiceException.NotFound("Trabajador no encontrado.");
    }

    private IQueryable<Guid> TrabajadoresDeSupervisor(ReporteContexto c, Guid supervisor) =>
        _db.TrabajadorSupervisores.AsNoTracking()
            .Where(s => s.EmpresaId == c.EmpresaId && s.SupervisorTrabajadorId == supervisor && s.FechaInicio <= c.Hoy && (s.FechaFin == null || s.FechaFin >= c.Hoy))
            .Select(s => s.TrabajadorId);

    // ---------------- Trabajadores ----------------
    private async Task<ReporteResultado> TrabajadoresAsync(ReporteContexto c, ReporteFiltro f, int skip, int take, CancellationToken ct)
    {
        var a = await AlcanceTrabajadoresAsync(c, ct);
        var q = _db.Trabajadores.AsNoTracking().Where(t => t.EmpresaId == c.EmpresaId);
        if (!a.Todos) { var ids = a.Ids; q = q.Where(t => ids.Contains(t.TrabajadorId)); }
        if (f.TrabajadorId is { } tid) { ExigirEnAlcance(a, tid); q = q.Where(t => t.TrabajadorId == tid); }
        if (f.DepartamentoId is { } dep) q = q.Where(t => t.DepartamentoId == dep);
        if (f.PuestoId is { } pu) q = q.Where(t => t.PuestoId == pu);
        if (f.Estado is { } e) q = q.Where(t => t.EstadoLaboral == e);
        if (f.SupervisorId is { } sup) { var supervisados = TrabajadoresDeSupervisor(c, sup); q = q.Where(t => supervisados.Contains(t.TrabajadorId)); }
        if (!string.IsNullOrWhiteSpace(f.Buscar))
        {
            var b = f.Buscar.Trim().ToLower();
            q = q.Where(t => (t.Nombres + " " + t.Apellidos).ToLower().Contains(b) || (t.NumeroEmpleado != null && t.NumeroEmpleado.ToLower().Contains(b)));
        }
        // Altas y bajas del periodo: quien ingresó o causó baja dentro del rango (la baja no lo elimina de los registros).
        if (c.Desde is { } d0) q = q.Where(t => t.FechaIngreso >= d0 || (t.FechaBaja != null && t.FechaBaja >= d0));
        if (c.Hasta is { } h0) q = q.Where(t => t.FechaIngreso <= h0 || (t.FechaBaja != null && t.FechaBaja <= h0));
        if (c.Desde is not null || c.Hasta is not null)
        {
            var d1 = c.Desde; var h1 = c.Hasta;
            q = q.Where(t => (t.FechaIngreso >= (d1 ?? DateOnly.MinValue) && t.FechaIngreso <= (h1 ?? DateOnly.MaxValue))
                || (t.FechaBaja != null && t.FechaBaja >= (d1 ?? DateOnly.MinValue) && t.FechaBaja <= (h1 ?? DateOnly.MaxValue)));
        }

        var total = await q.CountAsync(ct);
        var porEstado = await q.GroupBy(t => t.EstadoLaboral).Select(g => new { g.Key, N = g.Count() }).ToListAsync(ct);
        var ord = (f.OrdenarPor ?? "").ToLowerInvariant();
        var ordenada = (ord, f.Descendente) switch
        {
            ("ingreso", false) => q.OrderBy(t => t.FechaIngreso).ThenBy(t => t.TrabajadorId),
            ("ingreso", true) => q.OrderByDescending(t => t.FechaIngreso).ThenBy(t => t.TrabajadorId),
            ("estado", false) => q.OrderBy(t => t.EstadoLaboral).ThenBy(t => t.Apellidos),
            ("estado", true) => q.OrderByDescending(t => t.EstadoLaboral).ThenBy(t => t.Apellidos),
            (_, true) => q.OrderByDescending(t => t.Apellidos).ThenByDescending(t => t.Nombres).ThenBy(t => t.TrabajadorId),
            _ => q.OrderBy(t => t.Apellidos).ThenBy(t => t.Nombres).ThenBy(t => t.TrabajadorId)
        };
        var filas = await ordenada.Skip(skip).Take(take).Select(t => new
        {
            t.TrabajadorId, t.NumeroEmpleado, t.Nombres, t.Apellidos, Departamento = t.Departamento != null ? t.Departamento.Nombre : null,
            Puesto = t.Puesto != null ? t.Puesto.Nombre : null, t.EstadoLaboral, t.FechaIngreso, t.FechaBaja
        }).ToListAsync(ct);

        var idsPag = filas.Select(x => x.TrabajadorId).ToList();
        var sups = await _db.TrabajadorSupervisores.AsNoTracking()
            .Where(s => s.EmpresaId == c.EmpresaId && idsPag.Contains(s.TrabajadorId) && s.FechaInicio <= c.Hoy && (s.FechaFin == null || s.FechaFin >= c.Hoy))
            .Select(s => new { s.TrabajadorId, Nombre = s.Supervisor.Nombres + " " + s.Supervisor.Apellidos }).ToListAsync(ct);

        var r = new ReporteResultado
        {
            Total = total,
            Columnas =
            {
                Col("numero", "Núm. empleado"), Col("nombre", "Trabajador", ordenable: true), Col("departamento", "Departamento"), Col("puesto", "Puesto"),
                Col("estado", "Estado", ordenable: true), Col("ingreso", "Ingreso", "fecha", true), Col("baja", "Baja", "fecha"),
                Col("antiguedad", "Antigüedad"), Col("supervisor", "Supervisor")
            },
            Nota = "La antigüedad se calcula desde la fecha de ingreso hasta hoy (o hasta la baja). Los trabajadores dados de baja se conservan en los registros históricos."
        };
        foreach (var x in filas)
            r.Filas.Add(new object?[]
            {
                x.NumeroEmpleado, $"{x.Nombres} {x.Apellidos}", x.Departamento, x.Puesto, TextoEstadoLaboral(x.EstadoLaboral), x.FechaIngreso, x.FechaBaja,
                Antiguedad(x.FechaIngreso, x.FechaBaja ?? c.Hoy), string.Join(", ", sups.Where(s => s.TrabajadorId == x.TrabajadorId).Select(s => s.Nombre).OrderBy(n => n))
            });
        r.Resumen.Add(new() { Etiqueta = "Total", Valor = total.ToString() });
        r.Resumen.Add(new() { Etiqueta = "Activos", Valor = (porEstado.FirstOrDefault(x => x.Key == EstadoLaboral.Activo)?.N ?? 0).ToString() });
        r.Resumen.Add(new() { Etiqueta = "Inactivos", Valor = (porEstado.FirstOrDefault(x => x.Key == EstadoLaboral.Inactivo)?.N ?? 0).ToString() });
        r.Resumen.Add(new() { Etiqueta = "Dados de baja", Valor = (porEstado.FirstOrDefault(x => x.Key == EstadoLaboral.Baja)?.N ?? 0).ToString() });
        if (c.Desde is not null || c.Hasta is not null)
        {
            var d1 = c.Desde ?? DateOnly.MinValue; var h1 = c.Hasta ?? DateOnly.MaxValue;
            r.Resumen.Add(new() { Etiqueta = "Altas en el periodo", Valor = (await q.CountAsync(t => t.FechaIngreso >= d1 && t.FechaIngreso <= h1, ct)).ToString() });
            r.Resumen.Add(new() { Etiqueta = "Bajas en el periodo", Valor = (await q.CountAsync(t => t.FechaBaja != null && t.FechaBaja >= d1 && t.FechaBaja <= h1, ct)).ToString() });
        }
        return r;
    }

    internal static string TextoEstadoLaboral(byte e) => e switch { EstadoLaboral.Activo => "Activo", EstadoLaboral.Inactivo => "Inactivo", EstadoLaboral.Baja => "Baja", _ => "—" };

    internal static string Antiguedad(DateOnly ingreso, DateOnly corte)
    {
        if (corte < ingreso) return "0 meses";
        var meses = (corte.Year - ingreso.Year) * 12 + corte.Month - ingreso.Month - (corte.Day < ingreso.Day ? 1 : 0);
        var anios = meses / 12; var resto = meses % 12;
        return anios == 0 ? $"{resto} {(resto == 1 ? "mes" : "meses")}" : $"{anios} {(anios == 1 ? "año" : "años")} {resto} {(resto == 1 ? "mes" : "meses")}";
    }

    // ---------------- Cambios laborales ----------------
    // Solo cambios de organización; los cambios de datos personales ("Perfil") no se incluyen.
    private static readonly string[] TiposMovimiento =
    {
        TipoCambioLaboral.Ingreso, TipoCambioLaboral.Puesto, TipoCambioLaboral.Departamento, TipoCambioLaboral.Supervisor,
        TipoCambioLaboral.Estado, TipoCambioLaboral.Baja, TipoCambioLaboral.Reingreso
    };

    private async Task<ReporteResultado> MovimientosAsync(ReporteContexto c, ReporteFiltro f, int skip, int take, CancellationToken ct)
    {
        var a = await AlcanceTrabajadoresAsync(c, ct);
        var verSensibles = await _permisos.TienePermisoAsync(Permisos.TrabajadoresVerSensibles, ct);
        var q = _db.HistorialLaboral.AsNoTracking().Where(h => h.EmpresaId == c.EmpresaId && TiposMovimiento.Contains(h.TipoCambio));
        if (!a.Todos) { var ids = a.Ids; q = q.Where(h => ids.Contains(h.TrabajadorId)); }
        if (f.TrabajadorId is { } tid) { ExigirEnAlcance(a, tid); q = q.Where(h => h.TrabajadorId == tid); }
        if (f.DepartamentoId is { } dep) q = q.Where(h => h.Trabajador.DepartamentoId == dep);
        if (f.PuestoId is { } pu) q = q.Where(h => h.Trabajador.PuestoId == pu);
        if (f.SupervisorId is { } sup) { var supervisados = TrabajadoresDeSupervisor(c, sup); q = q.Where(h => supervisados.Contains(h.TrabajadorId)); }
        if (c.Desde is { } d) q = q.Where(h => h.FechaEfectiva >= d);
        if (c.Hasta is { } hh) q = q.Where(h => h.FechaEfectiva <= hh);
        if (!string.IsNullOrWhiteSpace(f.Buscar))
        {
            var b = f.Buscar.Trim().ToLower();
            q = q.Where(h => h.TipoCambio.ToLower().Contains(b) || (h.Trabajador.Nombres + " " + h.Trabajador.Apellidos).ToLower().Contains(b));
        }

        var total = await q.CountAsync(ct);
        var ordenada = f.Descendente ? q.OrderBy(h => h.FechaEfectiva).ThenBy(h => h.HistorialLaboralId) : q.OrderByDescending(h => h.FechaEfectiva).ThenByDescending(h => h.HistorialLaboralId);
        var filas = await ordenada.Skip(skip).Take(take).Select(h => new
        {
            h.FechaEfectiva, Nombre = h.Trabajador.Nombres + " " + h.Trabajador.Apellidos, h.TipoCambio, h.DatosAntesJson, h.DatosDespuesJson, h.Motivo
        }).ToListAsync(ct);

        var r = new ReporteResultado
        {
            Total = total,
            Columnas = { Col("fecha", "Fecha efectiva", "fecha", true), Col("trabajador", "Trabajador"), Col("tipo", "Cambio"), Col("antes", "Antes"), Col("despues", "Después"), Col("motivo", "Motivo") },
            Nota = "El motivo solo se muestra a quien puede ver datos sensibles de los trabajadores."
        };
        foreach (var x in filas)
            r.Filas.Add(new object?[] { x.FechaEfectiva, x.Nombre, x.TipoCambio, Aplanar(x.DatosAntesJson), Aplanar(x.DatosDespuesJson), verSensibles ? x.Motivo : null });
        r.Resumen.Add(new() { Etiqueta = "Cambios registrados", Valor = total.ToString() });
        return r;
    }

    /// <summary>Resume un JSON simple en "clave: valor"; si no es un objeto devuelve nada (no se vuelca contenido desconocido).</summary>
    private static string? Aplanar(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            var partes = doc.RootElement.EnumerateObject()
                .Where(p => p.Value.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
                .Select(p => $"{p.Name}: {p.Value}");
            var s = string.Join("; ", partes);
            return s.Length == 0 ? null : ProyectoHelper.Recortar(s, 200);
        }
        catch (JsonException) { return null; }
    }

    // ---------------- Permisos y ausencias ----------------
    private async Task<ReporteResultado> PermisosAsync(ReporteContexto c, ReporteFiltro f, int skip, int take, CancellationToken ct)
    {
        var alcance = await PermisosHelper.AlcanceAsync(_db, _current, _permisos, _clock, false, ct);
        var propios = alcance.MiTrabajadorId;
        var visibles = alcance.Supervisados.ToList();
        if (propios is { } m) visibles.Add(m);
        if (!alcance.Todos && visibles.Count == 0) throw ServiceException.Forbidden("No tiene solicitudes de permiso que consultar.");

        var q = _db.SolicitudesPermiso.AsNoTracking().Where(s => s.EmpresaId == c.EmpresaId);
        if (!alcance.Todos) q = q.Where(s => visibles.Contains(s.TrabajadorId));
        if (f.TrabajadorId is { } tid)
        {
            if (!alcance.Todos && !visibles.Contains(tid)) throw ServiceException.NotFound("Trabajador no encontrado.");
            q = q.Where(s => s.TrabajadorId == tid);
        }
        if (f.DepartamentoId is { } dep) q = q.Where(s => s.Trabajador.DepartamentoId == dep);
        if (f.SupervisorId is { } sup) { var supervisados = TrabajadoresDeSupervisor(c, sup); q = q.Where(s => supervisados.Contains(s.TrabajadorId)); }
        if (f.TipoPermisoId is { } tp) q = q.Where(s => s.TipoPermisoId == tp);
        if (f.Estado is { } e) q = q.Where(s => s.Estado == e);
        if (c.Desde is { } d) { var ini = d.ToDateTime(TimeOnly.MinValue); q = q.Where(s => s.FechaFin >= ini); }
        if (c.Hasta is { } h) { var fin = h.ToDateTime(new TimeOnly(23, 59, 59)); q = q.Where(s => s.FechaInicio <= fin); }
        if (!string.IsNullOrWhiteSpace(f.Buscar))
        {
            var b = f.Buscar.Trim().ToLower();
            q = q.Where(s => (s.Trabajador.Nombres + " " + s.Trabajador.Apellidos).ToLower().Contains(b) || s.TipoPermiso.Nombre.ToLower().Contains(b));
        }

        var total = await q.CountAsync(ct);
        var ord = (f.OrdenarPor ?? "").ToLowerInvariant();
        var ordenada = (ord, f.Descendente) switch
        {
            ("trabajador", false) => q.OrderBy(s => s.Trabajador.Apellidos).ThenBy(s => s.Trabajador.Nombres).ThenBy(s => s.SolicitudPermisoId),
            ("trabajador", true) => q.OrderByDescending(s => s.Trabajador.Apellidos).ThenBy(s => s.SolicitudPermisoId),
            ("estado", false) => q.OrderBy(s => s.Estado).ThenBy(s => s.FechaInicio),
            ("estado", true) => q.OrderByDescending(s => s.Estado).ThenBy(s => s.FechaInicio),
            (_, false) => q.OrderByDescending(s => s.FechaInicio).ThenBy(s => s.SolicitudPermisoId),
            _ => q.OrderBy(s => s.FechaInicio).ThenBy(s => s.SolicitudPermisoId)
        };
        // El motivo privado nunca se selecciona: ni en pantalla ni en exportaciones.
        var filas = await ordenada.Skip(skip).Take(take).Select(s => new
        {
            s.SolicitudPermisoId, Nombre = s.Trabajador.Nombres + " " + s.Trabajador.Apellidos,
            Departamento = s.Trabajador.Departamento != null ? s.Trabajador.Departamento.Nombre : null,
            Tipo = s.TipoPermiso.Nombre, s.FechaInicio, s.FechaFin, s.EsPorHoras, s.CantidadSolicitada, s.Estado, s.SolicitadaUtc
        }).ToListAsync(ct);

        var idsPag = filas.Select(x => x.SolicitudPermisoId).ToList();
        var decisiones = await _db.AprobacionesPermiso.AsNoTracking().Where(x => x.EmpresaId == c.EmpresaId && idsPag.Contains(x.SolicitudPermisoId))
            .Select(x => new { x.SolicitudPermisoId, x.RevisorUsuarioId, x.FechaDecisionUtc }).ToListAsync(ct);
        var nombres = await ProyectoHelper.NombresUsuariosAsync(_db, c.EmpresaId, decisiones.Select(x => x.RevisorUsuarioId), ct);

        var r = new ReporteResultado
        {
            Total = total,
            Columnas =
            {
                Col("trabajador", "Trabajador", ordenable: true), Col("departamento", "Departamento"), Col("tipo", "Tipo de permiso"), Col("inicio", "Inicio", "fechahora", true),
                Col("fin", "Fin", "fechahora"), Col("cantidad", "Cantidad", "numero"), Col("unidad", "Unidad"), Col("estado", "Estado", ordenable: true),
                Col("solicitada", "Solicitada", "fechahora"), Col("resuelto", "Resuelto por"), Col("resolucion", "Fecha de resolución", "fechahora")
            },
            Nota = "Los motivos y comprobantes de los permisos no forman parte de los reportes."
        };
        foreach (var x in filas)
        {
            var dec = decisiones.Where(d => d.SolicitudPermisoId == x.SolicitudPermisoId).OrderByDescending(d => d.FechaDecisionUtc).FirstOrDefault();
            r.Filas.Add(new object?[]
            {
                x.Nombre, x.Departamento, x.Tipo, x.FechaInicio, x.FechaFin, x.CantidadSolicitada, x.EsPorHoras ? "Horas" : "Días", TextoEstadoPermiso(x.Estado),
                Local(x.SolicitadaUtc, c.Zona), dec is null ? null : nombres.GetValueOrDefault(dec.RevisorUsuarioId, "—"), dec is null ? null : Local(dec.FechaDecisionUtc, c.Zona)
            });
        }

        // Los totales se suman en memoria (con tope) porque no todos los proveedores de BD suman decimales.
        var cant = await q.Select(s => new { s.Estado, s.EsPorHoras, s.CantidadSolicitada }).Take(MaxFilasResumen).ToListAsync(ct);
        decimal Suma(byte estado, bool horas) => cant.Where(x => x.Estado == estado && x.EsPorHoras == horas).Sum(x => x.CantidadSolicitada);
        r.Resumen.Add(new() { Etiqueta = "Solicitudes", Valor = total.ToString() });
        foreach (var (estado, nombre) in new[] { (EstadoSolicitudPermiso.Pendiente, "Pendientes"), (EstadoSolicitudPermiso.Aprobada, "Aprobadas"), (EstadoSolicitudPermiso.Rechazada, "Rechazadas"), (EstadoSolicitudPermiso.Cancelada, "Canceladas") })
            r.Resumen.Add(new() { Etiqueta = nombre, Valor = cant.Count(x => x.Estado == estado).ToString() });
        r.Resumen.Add(new() { Etiqueta = "Días autorizados", Valor = Suma(EstadoSolicitudPermiso.Aprobada, false).ToString("0.##", CultureInfo.InvariantCulture) });
        r.Resumen.Add(new() { Etiqueta = "Horas autorizadas", Valor = Suma(EstadoSolicitudPermiso.Aprobada, true).ToString("0.##", CultureInfo.InvariantCulture) });
        r.Resumen.Add(new() { Etiqueta = "Días pendientes de aprobar", Valor = Suma(EstadoSolicitudPermiso.Pendiente, false).ToString("0.##", CultureInfo.InvariantCulture) });
        r.Resumen.Add(new() { Etiqueta = "Horas pendientes de aprobar", Valor = Suma(EstadoSolicitudPermiso.Pendiente, true).ToString("0.##", CultureInfo.InvariantCulture) });
        return r;
    }

    internal static string TextoEstadoPermiso(byte e) => e switch
    {
        EstadoSolicitudPermiso.Pendiente => "Pendiente", EstadoSolicitudPermiso.Aprobada => "Aprobada", EstadoSolicitudPermiso.Rechazada => "Rechazada",
        EstadoSolicitudPermiso.Cancelada => "Cancelada", EstadoSolicitudPermiso.Concluida => "Concluida", _ => "—"
    };

    // ---------------- Asistencia ----------------
    private async Task<ReporteResultado> AsistenciaAsync(ReporteContexto c, ReporteFiltro f, int skip, int take, CancellationToken ct)
    {
        var alcance = await AsistenciaHelper.AlcanceAsync(_db, _current, _permisos, _clock, Permisos.AsistenciaConsultarTodos, Permisos.AsistenciaConsultar, ct);
        var visibles = alcance.Supervisados.ToList();
        if (alcance.MiTrabajadorId is { } m && await _permisos.TienePermisoAsync(Permisos.AsistenciaConsultarPropia, ct)) visibles.Add(m);
        if (!alcance.Todos && visibles.Count == 0) throw ServiceException.Forbidden("No tiene permiso para consultar la asistencia.");

        var q = _db.RegistrosAsistencia.AsNoTracking().Where(a => a.EmpresaId == c.EmpresaId);
        if (!alcance.Todos) q = q.Where(a => visibles.Contains(a.TrabajadorId));
        if (f.TrabajadorId is { } tid)
        {
            if (!alcance.Todos && !visibles.Contains(tid)) throw ServiceException.NotFound("Trabajador no encontrado.");
            q = q.Where(a => a.TrabajadorId == tid);
        }
        if (f.DepartamentoId is { } dep) q = q.Where(a => a.Trabajador.DepartamentoId == dep);
        if (f.PuestoId is { } pu) q = q.Where(a => a.Trabajador.PuestoId == pu);
        if (f.SupervisorId is { } sup) { var supervisados = TrabajadoresDeSupervisor(c, sup); q = q.Where(a => supervisados.Contains(a.TrabajadorId)); }
        if (f.Estado is { } e) q = q.Where(a => a.Estado == e);
        if (f.SoloIncompletos) q = q.Where(a => a.EntradaUtc != null && a.SalidaUtc == null && a.Estado != EstadoAsistencia.Anulada);
        if (c.Desde is { } d) q = q.Where(a => a.FechaLaboral >= d);
        if (c.Hasta is { } h) q = q.Where(a => a.FechaLaboral <= h);
        if (!string.IsNullOrWhiteSpace(f.Buscar))
        {
            var b = f.Buscar.Trim().ToLower();
            q = q.Where(a => (a.Trabajador.Nombres + " " + a.Trabajador.Apellidos).ToLower().Contains(b));
        }

        var total = await q.CountAsync(ct);
        var ord = (f.OrdenarPor ?? "").ToLowerInvariant();
        var ordenada = (ord, f.Descendente) switch
        {
            ("trabajador", false) => q.OrderBy(a => a.Trabajador.Apellidos).ThenBy(a => a.Trabajador.Nombres).ThenByDescending(a => a.FechaLaboral),
            ("trabajador", true) => q.OrderByDescending(a => a.Trabajador.Apellidos).ThenByDescending(a => a.FechaLaboral),
            (_, true) => q.OrderBy(a => a.FechaLaboral).ThenBy(a => a.EntradaUtc),
            _ => q.OrderByDescending(a => a.FechaLaboral).ThenBy(a => a.Trabajador.Apellidos).ThenBy(a => a.EntradaUtc)
        };
        var filas = await ordenada.Skip(skip).Take(take).Select(a => new
        {
            a.RegistroAsistenciaId, a.TrabajadorId, a.FechaLaboral, Nombre = a.Trabajador.Nombres + " " + a.Trabajador.Apellidos,
            Departamento = a.Trabajador.Departamento != null ? a.Trabajador.Departamento.Nombre : null, a.EntradaUtc, a.SalidaUtc, a.Estado
        }).ToListAsync(ct);

        var idsReg = filas.Select(x => x.RegistroAsistenciaId).ToList();
        var correcciones = await _db.CorreccionesAsistencia.AsNoTracking().Where(x => x.EmpresaId == c.EmpresaId && idsReg.Contains(x.RegistroAsistenciaId))
            .GroupBy(x => x.RegistroAsistenciaId).Select(g => new { Id = g.Key, N = g.Count() }).ToListAsync(ct);
        var idsTrab = filas.Select(x => x.TrabajadorId).Distinct().ToList();
        var horarios = await _db.AsignacionesHorario.AsNoTracking()
            .Where(x => x.EmpresaId == c.EmpresaId && idsTrab.Contains(x.TrabajadorId))
            .Select(x => new { x.TrabajadorId, x.FechaDesde, x.FechaHasta, x.MotivoExcepcion, Nombre = x.HorarioLaboral.Nombre }).ToListAsync(ct);

        var r = new ReporteResultado
        {
            Total = total,
            Columnas =
            {
                Col("fecha", "Fecha laboral", "fecha", true), Col("trabajador", "Trabajador", ordenable: true), Col("departamento", "Departamento"),
                Col("entrada", "Entrada", "fechahora"), Col("salida", "Salida", "fechahora"), Col("estado", "Estado"), Col("correcciones", "Correcciones", "numero"),
                Col("horario", "Horario asignado"), Col("excepcion", "Excepción")
            },
            Nota = "No se calculan nóminas, horas extra ni sanciones por retardos o faltas."
        };
        foreach (var x in filas)
        {
            // Si hay varias asignaciones vigentes ese día, manda la más reciente (la excepción individual tiene motivo).
            var asig = horarios.Where(a => a.TrabajadorId == x.TrabajadorId && a.FechaDesde <= x.FechaLaboral && (a.FechaHasta == null || a.FechaHasta >= x.FechaLaboral))
                .OrderByDescending(a => a.FechaDesde).FirstOrDefault();
            r.Filas.Add(new object?[]
            {
                x.FechaLaboral, x.Nombre, x.Departamento, Local(x.EntradaUtc, c.Zona), Local(x.SalidaUtc, c.Zona), TextoEstadoAsistencia(x.Estado, x.SalidaUtc),
                correcciones.FirstOrDefault(k => k.Id == x.RegistroAsistenciaId)?.N ?? 0, asig?.Nombre, asig?.MotivoExcepcion is null ? null : "Sí"
            });
        }

        var porEstado = await q.GroupBy(a => a.Estado).Select(g => new { g.Key, N = g.Count() }).ToListAsync(ct);
        r.Resumen.Add(new() { Etiqueta = "Registros", Valor = total.ToString() });
        r.Resumen.Add(new() { Etiqueta = "Completos", Valor = (porEstado.FirstOrDefault(x => x.Key == EstadoAsistencia.Completa)?.N ?? 0).ToString() });
        r.Resumen.Add(new() { Etiqueta = "Incompletos (entrada sin salida)", Valor = (await q.CountAsync(a => a.EntradaUtc != null && a.SalidaUtc == null && a.Estado != EstadoAsistencia.Anulada, ct)).ToString() });
        r.Resumen.Add(new() { Etiqueta = "Anulados", Valor = (porEstado.FirstOrDefault(x => x.Key == EstadoAsistencia.Anulada)?.N ?? 0).ToString() });
        r.Resumen.Add(new() { Etiqueta = "Con correcciones", Valor = (await q.CountAsync(a => _db.CorreccionesAsistencia.Any(k => k.RegistroAsistenciaId == a.RegistroAsistenciaId), ct)).ToString() });
        return r;
    }

    internal static string TextoEstadoAsistencia(byte e, DateTime? salida) => e switch
    {
        EstadoAsistencia.Abierta => "Incompleta (sin salida)",
        EstadoAsistencia.Completa => salida is null ? "Completa" : "Completa",
        EstadoAsistencia.Anulada => "Anulada",
        _ => "—"
    };

    // ---------------- Proyectos ----------------
    private async Task<ReporteResultado> ProyectosAsync(ReporteContexto c, ReporteFiltro f, int skip, int take, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.ProyectosConsultar, ct);
        var a = await ProyectoHelper.AccesoAsync(_db, _current, _permisos, _clock, ct);
        var q = ProyectoHelper.Visibles(_db, a);
        if (f.ProyectoId is { } pid) q = q.Where(p => p.ProyectoId == pid);
        if (f.TrabajadorId is { } tid)
            q = q.Where(p => p.ResponsableTrabajadorId == tid || _db.AsignacionesProyecto.Any(x => x.EmpresaId == p.EmpresaId && x.ProyectoId == p.ProyectoId && x.FechaSalida == null && x.TrabajadorId == tid));
        if (f.SupervisorId is { } sup) { var supervisados = TrabajadoresDeSupervisor(c, sup); q = q.Where(p => supervisados.Contains(p.ResponsableTrabajadorId)); }
        if (f.EstadoProyectoId is { } ep) q = q.Where(p => p.EstadoProyectoId == ep);
        if (c.Desde is { } d) q = q.Where(p => p.FechaFinPrevista == null || p.FechaFinPrevista >= d);
        if (c.Hasta is { } h) q = q.Where(p => p.FechaInicio == null || p.FechaInicio <= h);
        if (!string.IsNullOrWhiteSpace(f.Buscar)) { var b = f.Buscar.Trim().ToLower(); q = q.Where(p => p.Nombre.ToLower().Contains(b)); }

        var total = await q.CountAsync(ct);
        var ord = (f.OrdenarPor ?? "").ToLowerInvariant();
        var ordenada = (ord, f.Descendente) switch
        {
            ("fin", false) => q.OrderBy(p => p.FechaFinPrevista == null).ThenBy(p => p.FechaFinPrevista).ThenBy(p => p.Nombre),
            ("fin", true) => q.OrderByDescending(p => p.FechaFinPrevista).ThenBy(p => p.Nombre),
            (_, true) => q.OrderByDescending(p => p.Nombre),
            _ => q.OrderBy(p => p.Nombre)
        };
        var filas = await ordenada.Skip(skip).Take(take).Select(p => new
        {
            p.ProyectoId, p.Nombre, Estado = p.EstadoProyecto.Nombre, p.EstadoProyecto.EsFinal, p.ResponsableTrabajadorId, p.Prioridad, p.FechaInicio, p.FechaFinPrevista
        }).ToListAsync(ct);

        var ids = filas.Select(x => x.ProyectoId).ToList();
        var tareas = await _db.Tareas.AsNoTracking().Where(t => t.EmpresaId == c.EmpresaId && ids.Contains(t.ProyectoId))
            .Select(t => new { t.ProyectoId, t.Estado, t.PorcentajeAvance, t.FechaVencimiento }).ToListAsync(ct);
        var nombres = await ProyectoHelper.NombresTrabajadoresAsync(_db, c.EmpresaId, filas.Select(x => x.ResponsableTrabajadorId), ct);

        var r = new ReporteResultado
        {
            Total = total,
            Columnas =
            {
                Col("proyecto", "Proyecto", ordenable: true), Col("estado", "Estado"), Col("responsable", "Responsable"), Col("prioridad", "Prioridad"), Col("inicio", "Inicio", "fecha"),
                Col("fin", "Fin previsto", "fecha", true), Col("tareas", "Tareas", "numero"), Col("completadas", "Completadas", "numero"), Col("vencidas", "Vencidas", "numero"),
                Col("revision", "En revisión", "numero"), Col("avance", "Avance (%)", "numero"), Col("atencion", "Requiere atención")
            },
            Nota = "El avance es el promedio simple del porcentaje de las tareas no canceladas; no mide la productividad individual."
        };
        foreach (var x in filas)
        {
            var t = tareas.Where(k => k.ProyectoId == x.ProyectoId && k.Estado != EstadoTarea.Cancelada).ToList();
            var vencidas = t.Count(k => !EstadoTarea.EsCerrada(k.Estado) && k.FechaVencimiento is { } fv && c.Cal.EstaVencida(fv, c.Hoy));
            var revision = t.Count(k => k.Estado == EstadoTarea.EnRevision);
            var avance = t.Count == 0 ? 0 : (int)Math.Round(t.Average(k => (double)k.PorcentajeAvance));
            r.Filas.Add(new object?[]
            {
                x.Nombre, x.Estado, nombres.GetValueOrDefault(x.ResponsableTrabajadorId, "—"), FormatoPrioridad(x.Prioridad), x.FechaInicio, x.FechaFinPrevista,
                t.Count, t.Count(k => k.Estado == EstadoTarea.Completada), vencidas, revision, avance, !x.EsFinal && (vencidas > 0 || revision > 0)
            });
        }

        var estados = await q.GroupBy(p => new { p.EstadoProyecto.Nombre, p.EstadoProyecto.EsFinal }).Select(g => new { g.Key.Nombre, g.Key.EsFinal, N = g.Count() }).ToListAsync(ct);
        r.Resumen.Add(new() { Etiqueta = "Proyectos", Valor = total.ToString() });
        r.Resumen.Add(new() { Etiqueta = "Activos", Valor = estados.Where(e => !e.EsFinal && !EsPausa(e.Nombre)).Sum(e => e.N).ToString() });
        r.Resumen.Add(new() { Etiqueta = "Pausados", Valor = estados.Where(e => !e.EsFinal && EsPausa(e.Nombre)).Sum(e => e.N).ToString() });
        r.Resumen.Add(new() { Etiqueta = "Finalizados o cancelados", Valor = estados.Where(e => e.EsFinal).Sum(e => e.N).ToString() });
        return r;
    }

    /// <summary>Los estados de proyecto son configurables: se consideran "pausados" los que contienen "paus".</summary>
    internal static bool EsPausa(string nombreEstado) => nombreEstado.Contains("paus", StringComparison.OrdinalIgnoreCase);

    internal static string FormatoPrioridad(byte p) => p switch
    {
        PrioridadTrabajo.Baja => "Baja", PrioridadTrabajo.Media => "Media", PrioridadTrabajo.Alta => "Alta", PrioridadTrabajo.Urgente => "Urgente", _ => "—"
    };

    internal static string TextoEstadoTarea(byte e) => e switch
    {
        EstadoTarea.Pendiente => "Pendiente", EstadoTarea.EnProceso => "En proceso", EstadoTarea.EnRevision => "En revisión", EstadoTarea.Completada => "Completada",
        EstadoTarea.Reabierta => "Reabierta", EstadoTarea.Cancelada => "Cancelada", _ => "—"
    };

    // ---------------- Tareas ----------------
    private async Task<ReporteResultado> TareasAsync(ReporteContexto c, ReporteFiltro f, int skip, int take, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.ProyectosConsultar, ct);
        var a = await ProyectoHelper.AccesoAsync(_db, _current, _permisos, _clock, ct);
        var visibles = ProyectoHelper.Visibles(_db, a);
        var q = _db.Tareas.AsNoTracking().Where(t => t.EmpresaId == c.EmpresaId && visibles.Any(p => p.ProyectoId == t.ProyectoId));
        if (f.ProyectoId is { } pid) q = q.Where(t => t.ProyectoId == pid);
        if (f.TrabajadorId is { } tid)
            q = q.Where(t => _db.AsignacionesTarea.Any(x => x.EmpresaId == t.EmpresaId && x.TareaId == t.TareaId && x.TrabajadorId == tid && x.FechaRetiroUtc == null));
        if (f.Estado is { } e) q = q.Where(t => t.Estado == e);
        if (f.SoloVencidas)
        {
            var ultimo = c.Cal.UltimoHabilAnterior(c.Hoy);
            q = q.Where(t => t.Estado != EstadoTarea.Completada && t.Estado != EstadoTarea.Cancelada && t.FechaVencimiento != null && t.FechaVencimiento <= ultimo);
        }
        if (c.Desde is { } d) q = q.Where(t => t.FechaVencimiento != null && t.FechaVencimiento >= d);
        if (c.Hasta is { } h) q = q.Where(t => t.FechaVencimiento != null && t.FechaVencimiento <= h);
        if (!string.IsNullOrWhiteSpace(f.Buscar)) { var b = f.Buscar.Trim().ToLower(); q = q.Where(t => t.Titulo.ToLower().Contains(b)); }

        var total = await q.CountAsync(ct);
        var ord = (f.OrdenarPor ?? "").ToLowerInvariant();
        var ordenada = (ord, f.Descendente) switch
        {
            ("estado", false) => q.OrderBy(t => t.Estado).ThenBy(t => t.Titulo),
            ("estado", true) => q.OrderByDescending(t => t.Estado).ThenBy(t => t.Titulo),
            ("vencimiento", true) => q.OrderByDescending(t => t.FechaVencimiento).ThenBy(t => t.Titulo),
            _ => q.OrderBy(t => t.FechaVencimiento == null).ThenBy(t => t.FechaVencimiento).ThenBy(t => t.Titulo)
        };
        var pagina = await ordenada.Skip(skip).Take(take).ToListAsync(ct);
        var res = await TareaHelper.ResumenesAsync(_db, c.EmpresaId, pagina, c.Hoy, c.Cal, ct);

        var r = new ReporteResultado
        {
            Total = total,
            Columnas =
            {
                Col("tarea", "Tarea"), Col("proyecto", "Proyecto"), Col("asignados", "Asignados"), Col("estado", "Estado", ordenable: true), Col("avance", "Avance (%)", "numero"),
                Col("prioridad", "Prioridad"), Col("vencimiento", "Fecha límite", "fecha", true), Col("vencida", "Vencida"), Col("actualizacion", "Última actualización", "fechahora")
            },
            Nota = "Una tarea vencida conserva su estado real. El avance no debe interpretarse como medida de productividad individual."
        };
        for (var i = 0; i < pagina.Count; i++)
        {
            var t = pagina[i]; var x = res[i];
            r.Filas.Add(new object?[]
            {
                x.Titulo, x.Proyecto, string.Join(", ", x.Asignados), TextoEstadoTarea(x.Estado), (int)x.PorcentajeAvance, FormatoPrioridad(x.Prioridad),
                x.FechaVencimiento, x.Vencida, Local(t.FechaActualizacionUtc ?? t.FechaCreacionUtc, c.Zona)
            });
        }

        var porEstado = await q.GroupBy(t => t.Estado).Select(g => new { g.Key, N = g.Count() }).ToListAsync(ct);
        r.Resumen.Add(new() { Etiqueta = "Tareas", Valor = total.ToString() });
        foreach (var est in new byte[] { EstadoTarea.Pendiente, EstadoTarea.EnProceso, EstadoTarea.EnRevision, EstadoTarea.Reabierta, EstadoTarea.Completada, EstadoTarea.Cancelada })
            r.Resumen.Add(new() { Etiqueta = TextoEstadoTarea(est), Valor = (porEstado.FirstOrDefault(x => x.Key == est)?.N ?? 0).ToString() });
        var abiertas = await q.Where(t => t.Estado != EstadoTarea.Completada && t.Estado != EstadoTarea.Cancelada && t.FechaVencimiento != null)
            .Select(t => t.FechaVencimiento!.Value).Take(MaxFilasResumen).ToListAsync(ct);
        r.Resumen.Add(new() { Etiqueta = "Vencidas", Valor = abiertas.Count(fv => c.Cal.EstaVencida(fv, c.Hoy)).ToString() });
        return r;
    }

    // ---------------- Carga de trabajo ----------------
    private async Task<ReporteResultado> CargaAsync(ReporteContexto c, ReporteFiltro f, int skip, int take, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.ProyectosConsultar, ct);
        var a = await ProyectoHelper.AccesoAsync(_db, _current, _permisos, _clock, ct);
        var visibles = ProyectoHelper.Visibles(_db, a);
        var basico = from asig in _db.AsignacionesTarea.AsNoTracking()
                     join t in _db.Tareas.AsNoTracking() on new { asig.EmpresaId, asig.TareaId } equals new { t.EmpresaId, t.TareaId }
                     where asig.EmpresaId == c.EmpresaId && asig.FechaRetiroUtc == null && visibles.Any(p => p.ProyectoId == t.ProyectoId)
                     select new { asig.TrabajadorId, t.ProyectoId, t.Estado, t.FechaVencimiento };
        if (f.ProyectoId is { } pid) basico = basico.Where(x => x.ProyectoId == pid);
        if (f.TrabajadorId is { } tid) basico = basico.Where(x => x.TrabajadorId == tid);
        if (f.DepartamentoId is { } dep) basico = basico.Where(x => _db.Trabajadores.Any(w => w.EmpresaId == c.EmpresaId && w.TrabajadorId == x.TrabajadorId && w.DepartamentoId == dep));
        if (f.SupervisorId is { } sup) { var supervisados = TrabajadoresDeSupervisor(c, sup); basico = basico.Where(x => supervisados.Contains(x.TrabajadorId)); }

        var filas = await basico.Take(MaxFilasResumen).ToListAsync(ct);
        var nombres = await ProyectoHelper.NombresTrabajadoresAsync(_db, c.EmpresaId, filas.Select(x => x.TrabajadorId), ct);
        var grupos = filas.GroupBy(x => x.TrabajadorId).Select(g =>
        {
            var abiertas = g.Where(x => !EstadoTarea.EsCerrada(x.Estado)).ToList();
            return new
            {
                Id = g.Key, Nombre = nombres.GetValueOrDefault(g.Key, "—"),
                Abiertas = abiertas.Count,
                Pendientes = abiertas.Count(x => x.Estado == EstadoTarea.Pendiente),
                EnProceso = abiertas.Count(x => x.Estado == EstadoTarea.EnProceso),
                EnRevision = abiertas.Count(x => x.Estado == EstadoTarea.EnRevision),
                Reabiertas = abiertas.Count(x => x.Estado == EstadoTarea.Reabierta),
                Vencidas = abiertas.Count(x => x.FechaVencimiento is { } fv && c.Cal.EstaVencida(fv, c.Hoy)),
                Completadas = g.Count(x => x.Estado == EstadoTarea.Completada),
                Proyectos = g.Select(x => x.ProyectoId).Distinct().Count()
            };
        }).ToList();
        if (!string.IsNullOrWhiteSpace(f.Buscar)) grupos = grupos.Where(g => g.Nombre.Contains(f.Buscar.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

        var ord = (f.OrdenarPor ?? "").ToLowerInvariant();
        var ordenados = ord switch
        {
            "abiertas" => f.Descendente ? grupos.OrderBy(g => g.Abiertas) : grupos.OrderByDescending(g => g.Abiertas),
            "vencidas" => f.Descendente ? grupos.OrderBy(g => g.Vencidas) : grupos.OrderByDescending(g => g.Vencidas),
            _ => f.Descendente ? grupos.OrderByDescending(g => g.Nombre) : grupos.OrderBy(g => g.Nombre)
        };
        var lista = ordenados.ToList();

        var r = new ReporteResultado
        {
            Total = lista.Count,
            Columnas =
            {
                Col("trabajador", "Trabajador", ordenable: true), Col("proyectos", "Proyectos", "numero"), Col("abiertas", "Abiertas", "numero", true), Col("pendientes", "Pendientes", "numero"),
                Col("proceso", "En proceso", "numero"), Col("revision", "En revisión", "numero"), Col("reabiertas", "Reabiertas", "numero"),
                Col("vencidas", "Vencidas", "numero", true), Col("completadas", "Completadas", "numero")
            },
            Nota = "Distribución de tareas asignadas. No es una medida de productividad: no considera el tamaño ni la dificultad de cada tarea."
        };
        foreach (var g in lista.Skip(skip).Take(take))
            r.Filas.Add(new object?[] { g.Nombre, g.Proyectos, g.Abiertas, g.Pendientes, g.EnProceso, g.EnRevision, g.Reabiertas, g.Vencidas, g.Completadas });
        r.Resumen.Add(new() { Etiqueta = "Trabajadores con tareas", Valor = lista.Count.ToString() });
        r.Resumen.Add(new() { Etiqueta = "Tareas abiertas asignadas", Valor = lista.Sum(g => g.Abiertas).ToString() });
        r.Resumen.Add(new() { Etiqueta = "Vencidas", Valor = lista.Sum(g => g.Vencidas).ToString() });
        return r;
    }
}
