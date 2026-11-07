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
