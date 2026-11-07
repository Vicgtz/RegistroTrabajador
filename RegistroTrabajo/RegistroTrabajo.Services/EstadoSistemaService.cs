using System.Reflection;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using RegistroTrabajo.Data;
using RegistroTrabajo.Data.Entities;
using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public interface IEstadoSistemaService
{
    Task<EstadoSistemaDto> ObtenerAsync(CancellationToken ct);
    /// <summary>Solo disponibilidad (sin autenticación): verdadero si la base de datos responde.</summary>
    Task<bool> DisponibleAsync(CancellationToken ct);
}

public class EstadoSistemaService : IEstadoSistemaService
{
    private readonly RegistroTrabajoDbContext _db;
    private readonly IPermisoService _permisos;
    private readonly TimeProvider _clock;

    public EstadoSistemaService(RegistroTrabajoDbContext db, IPermisoService permisos, TimeProvider clock)
    {
        _db = db; _permisos = permisos; _clock = clock;
    }

    public async Task<bool> DisponibleAsync(CancellationToken ct)
    {
        try { return await _db.Database.CanConnectAsync(ct); }
        catch (Exception) { return false; }
    }

    public async Task<EstadoSistemaDto> ObtenerAsync(CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.PlataformaSistemaConsultar, ct);
        var ahora = _clock.GetUtcNow().UtcDateTime;
        var ok = await DisponibleAsync(ct);
        DateTime? ultimo = null; var errores = 0;
        if (ok)
        {
            ultimo = await _db.OperacionesRespaldo.AsNoTracking()
                .Where(o => o.Estado == EstadoOperacionRespaldo.Completado && (o.TipoOperacion == TipoOperacionRespaldo.Completo || o.TipoOperacion == TipoOperacionRespaldo.Diferencial))
                .Select(o => o.FechaFinUtc).OrderByDescending(f => f).FirstOrDefaultAsync(ct);
            var desde = ahora.AddHours(-24);
            errores = await _db.ErroresTecnicos.AsNoTracking().CountAsync(e => e.FechaUtc >= desde, ct);
        }
        var version = typeof(EstadoSistemaService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? typeof(EstadoSistemaService).Assembly.GetName().Version?.ToString() ?? "desconocida";
        return new EstadoSistemaDto
        {
            Version = version.Split('+')[0], Entorno = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
            FechaUtc = ahora, BaseDatosDisponible = ok, UltimoRespaldoCompletadoUtc = ultimo, ErroresTecnicosUltimas24Horas = errores
        };
    }
}
