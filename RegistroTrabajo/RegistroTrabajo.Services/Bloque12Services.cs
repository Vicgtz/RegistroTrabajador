using System.Reflection;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using RegistroTrabajo.Data;
using RegistroTrabajo.Data.Entities;
using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public record LogoArchivo(byte[] Contenido, string TipoContenido, string Hash);

public interface ILogoEmpresaService
{
    /// <summary>Guarda el logotipo de la empresa del usuario. El tipo se deduce de los bytes (firma real), no del nombre ni del encabezado del cliente.</summary>
    Task GuardarAsync(byte[] contenido, CancellationToken ct);
    /// <summary>Logotipo de la empresa del usuario o null. Lo puede leer cualquier usuario de la empresa (se muestra en el menú).</summary>
    Task<LogoArchivo?> ObtenerAsync(CancellationToken ct);
    Task EliminarAsync(CancellationToken ct);
}

public class LogoEmpresaService : ILogoEmpresaService
{
    internal const string RutaLogo = "api/empresa/logo";

    private readonly RegistroTrabajoDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IPermisoService _permisos;
    private readonly IAuditoriaService _auditoria;
    private readonly TimeProvider _clock;

    public LogoEmpresaService(RegistroTrabajoDbContext db, ICurrentUser current, IPermisoService permisos, IAuditoriaService auditoria, TimeProvider clock)
    {
        _db = db; _current = current; _permisos = permisos; _auditoria = auditoria; _clock = clock;
    }

    public async Task GuardarAsync(byte[] contenido, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.EmpresaConfigurar, ct);
        var id = _current.RequireEmpresaId();
        if (contenido is null || contenido.Length == 0) throw ServiceException.Validation("El archivo está vacío.");
        if (contenido.Length > FormatosRegionales.MaxBytesLogo) throw ServiceException.Validation("El logotipo no puede pesar más de 512 KB.");
        var tipo = DetectarTipo(contenido) ?? throw ServiceException.Validation("Formato no permitido: use PNG, JPEG o WebP.");

        var empresa = await _db.Empresas.FirstOrDefaultAsync(e => e.EmpresaId == id, ct) ?? throw ServiceException.NotFound("Empresa no encontrada.");
        var logo = await _db.LogosEmpresa.FirstOrDefaultAsync(l => l.EmpresaId == id, ct);
        var nuevo = logo is null;
        logo ??= _db.LogosEmpresa.Add(new LogoEmpresa { EmpresaId = id }).Entity;
        var ahora = _clock.GetUtcNow().UtcDateTime;
        logo.Contenido = contenido;
        logo.TipoContenido = tipo;
        logo.TamanoBytes = contenido.Length;
        logo.HashSha256 = Convert.ToHexString(SHA256.HashData(contenido)).ToLowerInvariant();
        logo.FechaActualizacionUtc = ahora;
        empresa.LogoUrl = RutaLogo;
        empresa.FechaActualizacionUtc = ahora;
        // Se audita el hecho y el tamaño, nunca el contenido.
        _auditoria.Registrar("Empresa", "Logo", id.ToString(), nuevo ? "SubirLogo" : "ReemplazarLogo", null, new { Tipo = tipo, Bytes = contenido.Length });
        await _db.SaveSafeAsync(ct);
    }

    public async Task<LogoArchivo?> ObtenerAsync(CancellationToken ct)
    {
        var id = _current.RequireEmpresaId();
        var l = await _db.LogosEmpresa.AsNoTracking().FirstOrDefaultAsync(x => x.EmpresaId == id, ct);
        return l is null ? null : new LogoArchivo(l.Contenido, l.TipoContenido, l.HashSha256);
    }

    public async Task EliminarAsync(CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.EmpresaConfigurar, ct);
        var id = _current.RequireEmpresaId();
        var logo = await _db.LogosEmpresa.FirstOrDefaultAsync(l => l.EmpresaId == id, ct);
        if (logo is null) return;
        _db.LogosEmpresa.Remove(logo);
        var empresa = await _db.Empresas.FirstAsync(e => e.EmpresaId == id, ct);
        empresa.LogoUrl = null;
        empresa.FechaActualizacionUtc = _clock.GetUtcNow().UtcDateTime;
        _auditoria.Registrar("Empresa", "Logo", id.ToString(), "EliminarLogo");
        await _db.SaveSafeAsync(ct);
    }

    /// <summary>Firma de archivo: PNG, JPEG o WebP. SVG no se admite (puede contener scripts).</summary>
    internal static string? DetectarTipo(byte[] b)
    {
        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47 && b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A)
            return "image/png";
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return "image/jpeg";
        if (b.Length >= 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P')
            return "image/webp";
        return null;
    }
}

public interface IAparienciaService
{
    Task<AparienciaEmpresaDto> ObtenerAsync(CancellationToken ct);
}

public class AparienciaService : IAparienciaService
{
    private readonly RegistroTrabajoDbContext _db;
    private readonly ICurrentUser _current;

    public AparienciaService(RegistroTrabajoDbContext db, ICurrentUser current) { _db = db; _current = current; }

    public async Task<AparienciaEmpresaDto> ObtenerAsync(CancellationToken ct)
    {
        var id = _current.RequireEmpresaId();
        var e = await _db.Empresas.AsNoTracking().Include(x => x.Configuracion).FirstOrDefaultAsync(x => x.EmpresaId == id, ct)
            ?? throw ServiceException.NotFound("Empresa no encontrada.");
        var c = e.Configuracion ?? throw ServiceException.NotFound("Configuración no encontrada.");
        var hash = await _db.LogosEmpresa.AsNoTracking().Where(l => l.EmpresaId == id).Select(l => l.HashSha256).FirstOrDefaultAsync(ct);
        return new AparienciaEmpresaDto
        {
            Nombre = c.NombreMostrado ?? e.NombreComercial ?? e.Nombre,
            ColorPrimario = e.ColorPrimario, ColorSecundario = e.ColorSecundario,
            TieneLogo = hash is not null, VersionLogo = hash?[..12], TextoInstitucional = c.TextoInstitucional,
            ZonaHorariaId = c.ZonaHorariaId, Cultura = c.Cultura, FormatoFecha = c.FormatoFecha, FormatoHora = c.FormatoHora,
            PrimerDiaSemana = c.PrimerDiaSemana
        };
    }
}

public interface IPermisosEfectivosService
{
    Task<PermisosEfectivosDto> ObtenerAsync(Guid usuarioId, CancellationToken ct);
}

public class PermisosEfectivosService : IPermisosEfectivosService
{
    private readonly RegistroTrabajoDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IPermisoService _permisos;

    public PermisosEfectivosService(RegistroTrabajoDbContext db, ICurrentUser current, IPermisoService permisos)
    {
        _db = db; _current = current; _permisos = permisos;
    }

    public async Task<PermisosEfectivosDto> ObtenerAsync(Guid usuarioId, CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.UsuariosConsultar, ct);
        await _permisos.AsegurarPermisoAsync(Permisos.EmpresaVerPermisosEfectivos, ct);
        var empresaId = _current.RequireEmpresaId();
        // Un usuario de otra empresa responde 404, igual que si no existiera.
        var u = await _db.Usuarios.AsNoTracking().FirstOrDefaultAsync(x => x.UsuarioId == usuarioId && x.EmpresaId == empresaId, ct)
            ?? throw ServiceException.NotFound("Usuario no encontrado.");
        var roles = await _db.UsuarioRoles.AsNoTracking().Where(x => x.EmpresaId == empresaId && x.UsuarioId == usuarioId)
            .Join(_db.Roles, ur => ur.RolId, r => r.RolId, (ur, r) => r.Nombre).OrderBy(n => n).ToListAsync(ct);
        var codigos = await _permisos.ObtenerPermisosAsync(empresaId, usuarioId, ct);
        var catalogo = await _db.PermisosSistema.AsNoTracking().Where(p => codigos.Contains(p.Codigo)).ToListAsync(ct);
        return new PermisosEfectivosDto
        {
            UsuarioId = u.UsuarioId, Usuario = $"{u.Nombre} {u.Apellido}", Estado = u.Estado, Roles = roles,
            Permisos = catalogo.OrderBy(p => p.Modulo).ThenBy(p => p.Codigo)
                .Select(p => new PermisoEfectivoDto { Codigo = p.Codigo, Modulo = p.Modulo, Descripcion = p.Descripcion }).ToList()
        };
    }
}

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
