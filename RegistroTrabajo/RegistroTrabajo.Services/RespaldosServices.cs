using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RegistroTrabajo.Data;
using RegistroTrabajo.Data.Entities;
using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public class RespaldosOptions
{
    /// <summary>Carpeta (en el servidor SQL) donde se escriben los .bak. Debe estar fuera de cualquier sitio web y con permisos restringidos.</summary>
    public string Carpeta { get; set; } = @"C:\RegistroTrabajoRespaldos";
    public bool Comprimir { get; set; } = true;
    /// <summary>Una operación "Iniciado" más antigua que esto se considera abandonada y no bloquea nuevos respaldos.</summary>
    public int MinutosOperacionVigente { get; set; } = 360;
}

public record ResultadoArchivoRespaldo(long TamanoBytes, string Hash);

/// <summary>Operaciones físicas contra SQL Server. Se abstrae para poder probar el servicio sin ejecutar BACKUP.</summary>
public interface IRespaldoEjecutor
{
    /// <summary>Ejecuta BACKUP DATABASE (completo o diferencial) con CHECKSUM y devuelve tamaño y SHA-256 del archivo.</summary>
    Task<ResultadoArchivoRespaldo> RespaldarAsync(bool diferencial, string archivo, CancellationToken ct);
    /// <summary>Ejecuta RESTORE VERIFYONLY ... WITH CHECKSUM y devuelve tamaño y SHA-256 actuales del archivo.</summary>
    Task<ResultadoArchivoRespaldo> VerificarAsync(string archivo, CancellationToken ct);
}

public class SqlServerRespaldoEjecutor : IRespaldoEjecutor
{
    private readonly string _connectionString;
    private readonly RespaldosOptions _opciones;

    public SqlServerRespaldoEjecutor(RegistroTrabajoDbContext db, IOptions<RespaldosOptions> opciones)
    {
        _connectionString = db.Database.GetConnectionString() ?? throw new InvalidOperationException("Sin cadena de conexión.");
        _opciones = opciones.Value;
    }

    public async Task<ResultadoArchivoRespaldo> RespaldarAsync(bool diferencial, string archivo, CancellationToken ct)
    {
        var nombreBd = new SqlConnectionStringBuilder(_connectionString).InitialCatalog;
        var opts = "CHECKSUM, STATS = 10" + (diferencial ? ", DIFFERENTIAL" : "") + (_opciones.Comprimir ? ", COMPRESSION" : "") + ", INIT";
        await EjecutarAsync($"BACKUP DATABASE [{nombreBd.Replace("]", "]]")}] TO DISK = @archivo WITH {opts}", archivo, ct);
        return await LeerArchivoAsync(archivo, ct);
    }

    public async Task<ResultadoArchivoRespaldo> VerificarAsync(string archivo, CancellationToken ct)
    {
        await EjecutarAsync("RESTORE VERIFYONLY FROM DISK = @archivo WITH CHECKSUM", archivo, ct);
        return await LeerArchivoAsync(archivo, ct);
    }

    private async Task EjecutarAsync(string sql, string archivo, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 0 };
        cmd.Parameters.AddWithValue("@archivo", archivo);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<ResultadoArchivoRespaldo> LeerArchivoAsync(string archivo, CancellationToken ct)
    {
        var info = new FileInfo(archivo);
        if (!info.Exists || info.Length <= 0) throw new FileNotFoundException("El archivo de respaldo no existe o está vacío.");
        await using var fs = new FileStream(archivo, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, useAsync: true);
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(fs, ct));
        return new ResultadoArchivoRespaldo(info.Length, hash);
    }
}

public interface IRespaldoService
{
    Task<RespaldoDto> SolicitarAsync(SolicitarRespaldoRequest r, CancellationToken ct);
    Task<RespaldoDto> VerificarAsync(Guid operacionId, CancellationToken ct);
    Task<PagedResult<RespaldoDto>> ListarAsync(RespaldoFiltro f, CancellationToken ct);
    Task<RespaldoDto> ObtenerAsync(Guid operacionId, CancellationToken ct);
}

public class RespaldoService : IRespaldoService
{
    private readonly RegistroTrabajoDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IPermisoService _permisos;
    private readonly IAuditoriaService _auditoria;
    private readonly IRespaldoEjecutor _ejecutor;
    private readonly RespaldosOptions _opciones;
    private readonly TimeProvider _clock;

    public RespaldoService(RegistroTrabajoDbContext db, ICurrentUser current, IPermisoService permisos, IAuditoriaService auditoria,
        IRespaldoEjecutor ejecutor, IOptions<RespaldosOptions> opciones, TimeProvider clock)
    {
        _db = db; _current = current; _permisos = permisos; _auditoria = auditoria; _ejecutor = ejecutor; _opciones = opciones.Value; _clock = clock;
    }

    public async Task<RespaldoDto> SolicitarAsync(SolicitarRespaldoRequest r, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.PlataformaRespaldosSolicitar, ct);
        var usuarioId = _current.RequireUsuarioId();
        var tipo = r.Tipo;
        if (tipo != TipoOperacionRespaldo.Completo && tipo != TipoOperacionRespaldo.Diferencial)
            throw ServiceException.Validation("Tipo de respaldo no válido.");
        if (tipo == TipoOperacionRespaldo.Diferencial &&
            !await _db.OperacionesRespaldo.AnyAsync(o => o.TipoOperacion == TipoOperacionRespaldo.Completo && o.Estado == EstadoOperacionRespaldo.Completado, ct))
            throw ServiceException.Validation("Un respaldo diferencial requiere un respaldo completo previo.");

        var ahora = _clock.GetUtcNow().UtcDateTime;
        await AsegurarSinOperacionEnCursoAsync(ahora, ct);

        var nombre = $"RegistroTrabajo_{(tipo == TipoOperacionRespaldo.Completo ? "FULL" : "DIFF")}_{ahora:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..8]}.bak";
        var ruta = Path.Combine(_opciones.Carpeta, nombre);
        var op = new OperacionRespaldo
        {
            EmpresaId = null, EjecutadaPorUsuarioId = usuarioId, TipoOperacion = tipo, Estado = EstadoOperacionRespaldo.Iniciado,
            FechaInicioUtc = ahora, UbicacionArchivo = ruta
        };
        _db.OperacionesRespaldo.Add(op);
        _auditoria.Registrar("Respaldos", "OperacionRespaldo", null, "Iniciar", null, new { Tipo = TipoOperacionRespaldo.Nombre(tipo), Archivo = nombre });
        await _db.SaveSafeAsync(ct);

        try
        {
            var res = await _ejecutor.RespaldarAsync(tipo == TipoOperacionRespaldo.Diferencial, ruta, ct);
            // Comprobación posterior: el archivo debe ser utilizable, no solo existir.
            var ver = await _ejecutor.VerificarAsync(ruta, ct);
            if (ver.TamanoBytes <= 0 || ver.Hash != res.Hash) throw new InvalidDataException("El archivo cambió tras generarse.");
            await CerrarAsync(op, EstadoOperacionRespaldo.Completado, res.TamanoBytes, res.Hash, null, "Completar", CancellationToken.None);
        }
        catch (Exception ex)
        {
            await CerrarAsync(op, EstadoOperacionRespaldo.Fallido, null, null, MensajeSeguro(ex), "Fallar", CancellationToken.None);
        }
        return await ObtenerAsync(op.OperacionRespaldoId, ct);
    }

    public async Task<RespaldoDto> VerificarAsync(Guid operacionId, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.PlataformaRespaldosSolicitar, ct);
        var usuarioId = _current.RequireUsuarioId();
        var origen = await _db.OperacionesRespaldo.AsNoTracking().FirstOrDefaultAsync(o => o.OperacionRespaldoId == operacionId, ct)
            ?? throw ServiceException.NotFound("Respaldo no encontrado.");
        if ((origen.TipoOperacion != TipoOperacionRespaldo.Completo && origen.TipoOperacion != TipoOperacionRespaldo.Diferencial)
            || origen.Estado != EstadoOperacionRespaldo.Completado || string.IsNullOrEmpty(origen.UbicacionArchivo))
            throw ServiceException.Validation("Solo se pueden verificar respaldos completados.");
        var ruta = RutaDentroDeCarpeta(origen.UbicacionArchivo);

        var ahora = _clock.GetUtcNow().UtcDateTime;
        var op = new OperacionRespaldo
        {
            EmpresaId = null, EjecutadaPorUsuarioId = usuarioId, TipoOperacion = TipoOperacionRespaldo.Verificacion,
            Estado = EstadoOperacionRespaldo.Iniciado, FechaInicioUtc = ahora, UbicacionArchivo = ruta
        };
        _db.OperacionesRespaldo.Add(op);
        _auditoria.Registrar("Respaldos", "OperacionRespaldo", origen.OperacionRespaldoId.ToString(), "IniciarVerificacion");
        await _db.SaveSafeAsync(ct);

        try
        {
            var res = await _ejecutor.VerificarAsync(ruta, ct);
            if (!string.IsNullOrEmpty(origen.HashArchivo) && !string.Equals(origen.HashArchivo, res.Hash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("La huella del archivo no coincide con la registrada.");
            await CerrarAsync(op, EstadoOperacionRespaldo.Completado, res.TamanoBytes, res.Hash, null, "CompletarVerificacion", CancellationToken.None);
        }
        catch (Exception ex)
        {
            await CerrarAsync(op, EstadoOperacionRespaldo.Fallido, null, null, MensajeSeguro(ex), "FallarVerificacion", CancellationToken.None);
        }
        return await ObtenerAsync(op.OperacionRespaldoId, ct);
    }

    public async Task<PagedResult<RespaldoDto>> ListarAsync(RespaldoFiltro f, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.PlataformaRespaldosConsultar, ct);
        var q = _db.OperacionesRespaldo.AsNoTracking().AsQueryable();
        if (f.Tipo is { } t) q = q.Where(o => o.TipoOperacion == t);
        if (f.Estado is { } e) q = q.Where(o => o.Estado == e);
        if (f.Desde is { } d) { var ini = d.ToDateTime(TimeOnly.MinValue); q = q.Where(o => o.FechaInicioUtc >= ini); }
        if (f.Hasta is { } h) { var fin = h.AddDays(1).ToDateTime(TimeOnly.MinValue); q = q.Where(o => o.FechaInicioUtc < fin); }
        var pagina = await q.Include(o => o.EjecutadaPorUsuario).OrderByDescending(o => o.FechaInicioUtc).ThenByDescending(o => o.OperacionRespaldoId).ToPagedAsync(f, ct);
        return new PagedResult<RespaldoDto>
        {
            Page = pagina.Page, PageSize = pagina.PageSize, Total = pagina.Total, Items = pagina.Items.Select(ADto).ToList()
        };
    }

    public async Task<RespaldoDto> ObtenerAsync(Guid operacionId, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.PlataformaRespaldosConsultar, ct);
        var o = await _db.OperacionesRespaldo.AsNoTracking().Include(x => x.EjecutadaPorUsuario)
            .FirstOrDefaultAsync(x => x.OperacionRespaldoId == operacionId, ct) ?? throw ServiceException.NotFound("Respaldo no encontrado.");
        return ADto(o);
    }

    private async Task AsegurarSinOperacionEnCursoAsync(DateTime ahora, CancellationToken ct)
    {
        var limite = ahora.AddMinutes(-_opciones.MinutosOperacionVigente);
        if (await _db.OperacionesRespaldo.AnyAsync(o => o.Estado == EstadoOperacionRespaldo.Iniciado && o.FechaInicioUtc > limite, ct))
            throw ServiceException.Conflict("Ya hay una operación de respaldo en curso.");
    }

    private async Task CerrarAsync(OperacionRespaldo op, byte estado, long? tamano, string? hash, string? mensaje, string accion, CancellationToken ct)
    {
        op.Estado = estado;
        op.FechaFinUtc = _clock.GetUtcNow().UtcDateTime;
        if (op.FechaFinUtc < op.FechaInicioUtc) op.FechaFinUtc = op.FechaInicioUtc;
        op.TamanoBytes = tamano;
        op.HashArchivo = hash;
        op.Mensaje = mensaje;
        _auditoria.Registrar("Respaldos", "OperacionRespaldo", op.OperacionRespaldoId.ToString(), accion,
            null, new { Estado = EstadoOperacionRespaldo.Nombre(estado), op.TamanoBytes, Mensaje = mensaje });
        await _db.SaveSafeAsync(ct);
    }

    /// <summary>Resumen sin el mensaje de la excepción (puede contener rutas o nombres internos) ni secretos.</summary>
    internal static string MensajeSeguro(Exception ex) => ex switch
    {
        SqlException s => $"SQL Server rechazó la operación (error {s.Number}).",
        FileNotFoundException => "No se encontró el archivo de respaldo o está vacío.",
        UnauthorizedAccessException or IOException => "No se pudo acceder al archivo de respaldo.",
        InvalidDataException => "El archivo de respaldo no pasó la verificación de integridad.",
        OperationCanceledException => "La operación fue cancelada.",
        _ => $"La operación falló ({ex.GetType().Name})."
    };

    /// <summary>Impide usar rutas fuera de la carpeta de respaldos (la ruta viene de la BD, pero se revalida).</summary>
    private string RutaDentroDeCarpeta(string ruta)
    {
        var raiz = Path.GetFullPath(_opciones.Carpeta).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var completa = Path.GetFullPath(ruta);
        if (!completa.StartsWith(raiz, StringComparison.OrdinalIgnoreCase))
            throw ServiceException.Validation("La ubicación del respaldo no es válida.");
        return completa;
    }

    private static RespaldoDto ADto(OperacionRespaldo o) => new()
    {
        OperacionRespaldoId = o.OperacionRespaldoId, Tipo = o.TipoOperacion, TipoNombre = TipoOperacionRespaldo.Nombre(o.TipoOperacion),
        Estado = o.Estado, EstadoNombre = EstadoOperacionRespaldo.Nombre(o.Estado), UsuarioId = o.EjecutadaPorUsuarioId,
        UsuarioNombre = o.EjecutadaPorUsuario is null ? null : $"{o.EjecutadaPorUsuario.Nombre} {o.EjecutadaPorUsuario.Apellido}",
        FechaInicioUtc = o.FechaInicioUtc, FechaFinUtc = o.FechaFinUtc,
        NombreArchivo = string.IsNullOrEmpty(o.UbicacionArchivo) ? null : Path.GetFileName(o.UbicacionArchivo),
        TamanoBytes = o.TamanoBytes, HashArchivo = o.HashArchivo, Mensaje = o.Mensaje
    };
}
