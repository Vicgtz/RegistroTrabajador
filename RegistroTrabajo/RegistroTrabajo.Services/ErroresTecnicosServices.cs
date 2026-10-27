using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RegistroTrabajo.Data;
using RegistroTrabajo.Data.Entities;
using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public interface IErrorTecnicoService
{
    /// <summary>
    /// Guarda el error técnico para diagnóstico. No incluye el mensaje de la excepción, contraseñas, tokens, cabeceras ni cuerpos:
    /// solo tipo, ruta sin parámetros, correlación y la pila de métodos. Es independiente de la auditoría de negocio.
    /// </summary>
    Task RegistrarAsync(Exception ex, string metodo, string ruta, Guid? correlationId, Guid? empresaId, Guid? usuarioId, CancellationToken ct);
    Task<PagedResult<ErrorTecnicoDto>> ListarAsync(ErrorTecnicoFiltro filtro, CancellationToken ct);
}

public class ErrorTecnicoService : IErrorTecnicoService
{
    private const int MaxFramesPila = 12;
    private static readonly Regex Guid32 = new(@"^[0-9a-fA-F]{8}-([0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}$", RegexOptions.Compiled);

    private readonly RegistroTrabajoDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IPermisoService _permisos;
    private readonly TimeProvider _clock;

    public ErrorTecnicoService(RegistroTrabajoDbContext db, ICurrentUser current, IPermisoService permisos, TimeProvider clock)
    {
        _db = db; _current = current; _permisos = permisos; _clock = clock;
    }

    public async Task RegistrarAsync(Exception ex, string metodo, string ruta, Guid? correlationId, Guid? empresaId, Guid? usuarioId, CancellationToken ct)
    {
        var rutaSegura = RutaSegura(ruta);
        var tipo = ex.GetType().FullName ?? ex.GetType().Name;
        _db.ErroresTecnicos.Add(new ErrorTecnico
        {
            EmpresaId = empresaId, UsuarioId = usuarioId, FechaUtc = _clock.GetUtcNow().UtcDateTime, Nivel = "Error",
            Codigo = Recortar(tipo, 100),
            Mensaje = Recortar($"Error no controlado en {Limpio(metodo)} {rutaSegura}", 1000),
            CorrelationId = correlationId, Ruta = Recortar(rutaSegura, 500), DetalleTecnico = Pila(ex)
        });
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Quita parámetros de consulta y enmascara segmentos que parecen tokens (largos y no son GUID).</summary>
    internal static string RutaSegura(string? ruta)
    {
        var path = (ruta ?? "").Split('?', '#')[0];
        var segmentos = path.Split('/', StringSplitOptions.None)
            .Select(s => s.Length >= 24 && !Guid32.IsMatch(s) ? "{…}" : s);
        return string.Join('/', segmentos);
    }

    /// <summary>Cadena de tipos de excepción y métodos de la pila; sin mensajes ni valores de parámetros.</summary>
    private static string Pila(Exception ex)
    {
        var sb = new StringBuilder();
        for (var e = ex; e is not null; e = e.InnerException)
        {
            sb.AppendLine(e.GetType().FullName);
            foreach (var f in new StackTrace(e, false).GetFrames().Take(MaxFramesPila))
            {
                var m = f.GetMethod();
                if (m is null) continue;
                sb.Append("  en ").Append(m.DeclaringType?.FullName).Append('.').AppendLine(m.Name);
            }
            if (sb.Length > 8000) break;
        }
        return Recortar(sb.ToString(), 8000);
    }

    private static string Limpio(string s) => new(s.Where(ch => ch >= ' ').ToArray());
    private static string Recortar(string s, int max) => s.Length <= max ? s : s[..max];

    public async Task<PagedResult<ErrorTecnicoDto>> ListarAsync(ErrorTecnicoFiltro f, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.ErroresConsultar, ct);
        var empresaId = _current.RequireEmpresaId();
        var esPlataforma = await _db.Empresas.AsNoTracking().Where(e => e.EmpresaId == empresaId).Select(e => e.EsPlataforma).FirstOrDefaultAsync(ct);
        // Cada empresa ve solo sus errores; los que no pudieron asociarse a una empresa los ve la empresa de plataforma.
        var q = _db.ErroresTecnicos.AsNoTracking().Where(e => e.EmpresaId == empresaId || (esPlataforma && e.EmpresaId == null));
        if (f.CorrelationId is { } c) q = q.Where(e => e.CorrelationId == c);
        if (f.Desde is { } d) { var ini = d.ToDateTime(TimeOnly.MinValue); q = q.Where(e => e.FechaUtc >= ini); }
        if (f.Hasta is { } h) { var fin = h.AddDays(1).ToDateTime(TimeOnly.MinValue); q = q.Where(e => e.FechaUtc < fin); }
        if (!string.IsNullOrWhiteSpace(f.Buscar))
        {
            var b = f.Buscar.Trim().ToLower();
            q = q.Where(e => e.Mensaje.ToLower().Contains(b) || (e.Codigo != null && e.Codigo.ToLower().Contains(b)) || (e.Ruta != null && e.Ruta.ToLower().Contains(b)));
        }
        var pagina = await q.OrderByDescending(e => e.FechaUtc).ThenByDescending(e => e.ErrorTecnicoId).ToPagedAsync(f, ct);
        return new PagedResult<ErrorTecnicoDto>
        {
            Page = pagina.Page, PageSize = pagina.PageSize, Total = pagina.Total,
            Items = pagina.Items.Select(e => new ErrorTecnicoDto
            {
                ErrorTecnicoId = e.ErrorTecnicoId, FechaUtc = e.FechaUtc, Nivel = e.Nivel, Codigo = e.Codigo, Mensaje = e.Mensaje,
                CorrelationId = e.CorrelationId, Ruta = e.Ruta, DetalleTecnico = e.DetalleTecnico, UsuarioId = e.UsuarioId
            }).ToList()
        };
    }
}
