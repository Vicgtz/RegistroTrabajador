using System.IO.Compression;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using RegistroTrabajo.Data;
using RegistroTrabajo.Data.Entities;
using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public record CurriculumArchivo(byte[] Contenido, string TipoContenido, string NombreArchivo);

public interface ICurriculumService
{
    /// <summary>Guarda (o reemplaza) el currículum. El tipo se deduce de los bytes (firma real), no del nombre ni del encabezado del cliente.</summary>
    Task<CurriculumInfoDto> GuardarAsync(Guid trabajadorId, string nombreArchivo, byte[] contenido, CancellationToken ct);
    Task<CurriculumArchivo?> ObtenerAsync(Guid trabajadorId, CancellationToken ct);
    Task EliminarAsync(Guid trabajadorId, CancellationToken ct);
}

/// <summary>
/// Bloque 14: currículum del trabajador (PDF, DOC o DOCX, máx. 5 MB) guardado en la BD (mismo patrón que el logotipo de la empresa).
/// Lo sube/reemplaza/elimina quien tiene expediente.editar o el propio trabajador; lo descarga quien tiene expediente.consultar o el propio trabajador.
/// </summary>
public class CurriculumService : ICurriculumService
{
    internal const string TipoPdf = "application/pdf";
    internal const string TipoDoc = "application/msword";
    internal const string TipoDocx = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private readonly RegistroTrabajoDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IPermisoService _permisos;
    private readonly IAuditoriaService _auditoria;
    private readonly TimeProvider _clock;

    public CurriculumService(RegistroTrabajoDbContext db, ICurrentUser current, IPermisoService permisos, IAuditoriaService auditoria, TimeProvider clock)
    {
        _db = db; _current = current; _permisos = permisos; _auditoria = auditoria; _clock = clock;
    }

    public async Task<CurriculumInfoDto> GuardarAsync(Guid trabajadorId, string nombreArchivo, byte[] contenido, CancellationToken ct)
    {
        var empresaId = await AsegurarEscrituraAsync(trabajadorId, ct);
        if (contenido is null || contenido.Length == 0) throw ServiceException.Validation("El archivo está vacío.");
        if (contenido.Length > LimitesCurriculum.MaxBytes) throw ServiceException.Validation("El currículum no puede pesar más de 5 MB.");
        var tipo = DetectarTipo(contenido) ?? throw ServiceException.Validation("Formato no permitido: use PDF, DOC o DOCX.");

        var cv = await _db.CurriculosTrabajador.FirstOrDefaultAsync(c => c.EmpresaId == empresaId && c.TrabajadorId == trabajadorId, ct);
        var nuevo = cv is null;
        cv ??= _db.CurriculosTrabajador.Add(new CurriculumTrabajador { TrabajadorId = trabajadorId, EmpresaId = empresaId }).Entity;
        cv.NombreArchivo = NombreSeguro(nombreArchivo, tipo);
        cv.TipoContenido = tipo;
        cv.Contenido = contenido;
        cv.TamanoBytes = contenido.Length;
        cv.HashSha256 = Convert.ToHexString(SHA256.HashData(contenido)).ToLowerInvariant();
        cv.FechaActualizacionUtc = _clock.GetUtcNow().UtcDateTime;
        // Se audita el hecho y el tamaño, nunca el contenido.
        _auditoria.Registrar("Expediente", nameof(CurriculumTrabajador), trabajadorId.ToString(), nuevo ? "SubirCurriculum" : "ReemplazarCurriculum",
            null, new { Tipo = tipo, Bytes = contenido.Length });
        await _db.SaveSafeAsync(ct);
        return new CurriculumInfoDto { NombreArchivo = cv.NombreArchivo, TipoContenido = tipo, TamanoBytes = cv.TamanoBytes, FechaActualizacionUtc = cv.FechaActualizacionUtc };
    }

    public async Task<CurriculumArchivo?> ObtenerAsync(Guid trabajadorId, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        await TrabajadorHelper.CargarAsync(_db, empresaId, trabajadorId, false, ct);
        var propio = await TrabajadorHelper.IdDeUsuarioAsync(_db, empresaId, _current.RequireUsuarioId(), ct) == trabajadorId;
        if (!propio && !await _permisos.TienePermisoAsync(Permisos.ExpedienteConsultar, ct)
            && !await _permisos.TienePermisoAsync(Permisos.ExpedienteEditar, ct))
            throw ServiceException.Forbidden("No tiene permiso para consultar el currículum.");
        var c = await _db.CurriculosTrabajador.AsNoTracking().FirstOrDefaultAsync(x => x.EmpresaId == empresaId && x.TrabajadorId == trabajadorId, ct);
        return c is null ? null : new CurriculumArchivo(c.Contenido, c.TipoContenido, c.NombreArchivo);
    }

    public async Task EliminarAsync(Guid trabajadorId, CancellationToken ct)
    {
        var empresaId = await AsegurarEscrituraAsync(trabajadorId, ct);
        var cv = await _db.CurriculosTrabajador.FirstOrDefaultAsync(c => c.EmpresaId == empresaId && c.TrabajadorId == trabajadorId, ct);
        if (cv is null) return;
        _db.CurriculosTrabajador.Remove(cv);
        _auditoria.Registrar("Expediente", nameof(CurriculumTrabajador), trabajadorId.ToString(), "EliminarCurriculum");
        await _db.SaveSafeAsync(ct);
    }

    private async Task<Guid> AsegurarEscrituraAsync(Guid trabajadorId, CancellationToken ct)
    {
        var empresaId = _current.RequireEmpresaId();
        var t = await TrabajadorHelper.CargarAsync(_db, empresaId, trabajadorId, false, ct);
        if (await _permisos.TienePermisoAsync(Permisos.ExpedienteEditar, ct)) return empresaId;
        var propio = await TrabajadorHelper.IdDeUsuarioAsync(_db, empresaId, _current.RequireUsuarioId(), ct) == trabajadorId;
        if (!propio) throw ServiceException.Forbidden("No tiene permiso para modificar el currículum de este trabajador.");
        if (t.EstadoLaboral == EstadoLaboral.Baja) throw ServiceException.Validation("El trabajador está dado de baja.");
        return empresaId;
    }

    /// <summary>Firma de archivo: PDF, DOC (OLE) o DOCX (zip con word/document.xml).</summary>
    internal static string? DetectarTipo(byte[] b)
    {
        if (b.Length >= 5 && b[0] == '%' && b[1] == 'P' && b[2] == 'D' && b[3] == 'F' && b[4] == '-') return TipoPdf;
        if (b.Length >= 8 && b[0] == 0xD0 && b[1] == 0xCF && b[2] == 0x11 && b[3] == 0xE0 && b[4] == 0xA1 && b[5] == 0xB1 && b[6] == 0x1A && b[7] == 0xE1)
            return TipoDoc;
        if (b.Length >= 4 && b[0] == 'P' && b[1] == 'K' && b[2] == 0x03 && b[3] == 0x04 && EsDocx(b)) return TipoDocx;
        return null;
    }

    private static bool EsDocx(byte[] b)
    {
        try
        {
            using var zip = new ZipArchive(new MemoryStream(b, writable: false), ZipArchiveMode.Read);
            return zip.GetEntry("word/document.xml") is not null;
        }
        catch (InvalidDataException) { return false; }
    }

    /// <summary>Nombre sin ruta ni caracteres de control, con la extensión que corresponde al tipo real.</summary>
    internal static string NombreSeguro(string? nombre, string tipo)
    {
        var ext = tipo switch { TipoPdf => ".pdf", TipoDoc => ".doc", _ => ".docx" };
        var baseName = Path.GetFileNameWithoutExtension(Path.GetFileName((nombre ?? "").Replace('\\', '/')));
        var limpio = new string(baseName.Where(c => !char.IsControl(c) && Path.GetInvalidFileNameChars().All(i => i != c)).ToArray()).Trim();
        if (limpio.Length == 0) limpio = "curriculum";
        if (limpio.Length > 150) limpio = limpio[..150];
        return limpio + ext;
    }
}
