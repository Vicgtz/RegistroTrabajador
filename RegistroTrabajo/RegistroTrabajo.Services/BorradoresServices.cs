using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RegistroTrabajo.Data;
using RegistroTrabajo.Data.Entities;
using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public interface IBorradoresService
{
    /// <summary>Borradores propios vigentes, del más reciente al más antiguo.</summary>
    Task<PagedResult<BorradorDto>> ListarAsync(BorradorFiltro filtro, CancellationToken ct);
    /// <summary>Un borrador ajeno responde 404, aunque se conozca el identificador.</summary>
    Task<BorradorDto> ObtenerAsync(Guid borradorId, CancellationToken ct);
    Task<BorradorDto> CrearAsync(GuardarBorradorRequest request, CancellationToken ct);
    Task<BorradorDto> ActualizarAsync(Guid borradorId, GuardarBorradorRequest request, CancellationToken ct);
    /// <summary>Elimina el borrador (es temporal). Se usa al enviar el formulario o al descartarlo.</summary>
    Task EliminarAsync(Guid borradorId, CancellationToken ct);
}

/// <summary>
/// Captura sin enviar. Un borrador no es una solicitud, un permiso ni un evento: ningún otro servicio lo lee, así que no aparece
/// en saldos, aprobaciones, calendarios ni notificaciones. Solo su creador lo consulta.
/// </summary>
public class BorradoresService : IBorradoresService
{
    internal const int MaxPorUsuario = 50;
    internal const int MaxCaracteres = 20_000;
    internal const int DiasVigencia = 30;
    private static readonly Regex TipoValido = new(@"^[\p{L}\p{N} _\-\.]{1,100}$", RegexOptions.Compiled);
    private static readonly string[] ClavesProhibidas = { "password", "contrasena", "contraseña", "token", "secret", "clave", "apikey", "hash" };

    private readonly RegistroTrabajoDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IPermisoService _permisos;
    private readonly TimeProvider _clock;

    public BorradoresService(RegistroTrabajoDbContext db, ICurrentUser current, IPermisoService permisos, TimeProvider clock)
    {
        _db = db; _current = current; _permisos = permisos; _clock = clock;
    }

    private DateTime Ahora => _clock.GetUtcNow().UtcDateTime;

    private IQueryable<Borrador> Propios(bool tracking)
    {
        var empresaId = _current.RequireEmpresaId();
        var usuarioId = _current.RequireUsuarioId();
        var ahora = Ahora;
        var q = tracking ? _db.Borradores : _db.Borradores.AsNoTracking();
        return q.Where(b => b.EmpresaId == empresaId && b.UsuarioCreadorId == usuarioId && (b.ExpiraUtc == null || b.ExpiraUtc > ahora));
    }

    public async Task<PagedResult<BorradorDto>> ListarAsync(BorradorFiltro f, CancellationToken ct)
    {
        var q = Propios(false);
        if (!string.IsNullOrWhiteSpace(f.Modulo)) q = q.Where(b => b.Modulo == f.Modulo);
        var pagina = await q.OrderByDescending(b => b.FechaActualizacionUtc).ThenBy(b => b.BorradorId).ToPagedAsync(f, ct);
        return new PagedResult<BorradorDto> { Page = pagina.Page, PageSize = pagina.PageSize, Total = pagina.Total, Items = pagina.Items.Select(Dto).ToList() };
    }

    public async Task<BorradorDto> ObtenerAsync(Guid borradorId, CancellationToken ct) =>
        Dto(await Propios(false).FirstOrDefaultAsync(b => b.BorradorId == borradorId, ct) ?? throw ServiceException.NotFound("Borrador no encontrado."));

    public async Task<BorradorDto> CrearAsync(GuardarBorradorRequest r, CancellationToken ct)
    {
        var (modulo, tipo, datos) = await ValidarAsync(r, ct);
        if (await Propios(false).CountAsync(ct) >= MaxPorUsuario)
            throw ServiceException.Conflict($"Alcanzó el máximo de {MaxPorUsuario} borradores. Elimine alguno para guardar otro.");

        var now = Ahora;
        var b = new Borrador
        {
            BorradorId = Guid.NewGuid(), EmpresaId = _current.RequireEmpresaId(), UsuarioCreadorId = _current.RequireUsuarioId(),
            Modulo = modulo, TipoFormulario = tipo, DatosJson = datos, FechaCreacionUtc = now, FechaActualizacionUtc = now, ExpiraUtc = now.AddDays(DiasVigencia)
        };
        _db.Borradores.Add(b);
        await _db.SaveSafeAsync(ct);
        return Dto(b);
    }

    public async Task<BorradorDto> ActualizarAsync(Guid borradorId, GuardarBorradorRequest r, CancellationToken ct)
    {
        var (modulo, tipo, datos) = await ValidarAsync(r, ct);
        var b = await Propios(true).FirstOrDefaultAsync(x => x.BorradorId == borradorId, ct) ?? throw ServiceException.NotFound("Borrador no encontrado.");
        if (b.Modulo != modulo || b.TipoFormulario != tipo) throw ServiceException.Validation("No se puede cambiar el módulo ni el formulario de un borrador.");
        var now = Ahora;
        b.DatosJson = datos;
        b.FechaActualizacionUtc = now;
        b.ExpiraUtc = now.AddDays(DiasVigencia);
        await _db.SaveSafeAsync(ct);
        return Dto(b);
    }

    public async Task EliminarAsync(Guid borradorId, CancellationToken ct)
    {
        var b = await Propios(true).FirstOrDefaultAsync(x => x.BorradorId == borradorId, ct) ?? throw ServiceException.NotFound("Borrador no encontrado.");
        _db.Borradores.Remove(b);
        await _db.SaveSafeAsync(ct);
    }

    // ---------------- Validación ----------------
    private async Task<(string Modulo, string Tipo, string Datos)> ValidarAsync(GuardarBorradorRequest r, CancellationToken ct)
    {
        var modulo = ModuloBorrador.Todos.FirstOrDefault(m => string.Equals(m, r.Modulo?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw ServiceException.Validation("El módulo indicado no admite borradores.");
        var tipo = r.TipoFormulario?.Trim() ?? "";
        if (!TipoValido.IsMatch(tipo)) throw ServiceException.Validation("El tipo de formulario no es válido.");

        // Solo guarda borradores de lo que el usuario podría enviar después.
        var puede = modulo switch
        {
            ModuloBorrador.Permisos => await _permisos.TienePermisoAsync(Permisos.PermisosSolicitar, ct),
            ModuloBorrador.Trabajadores => await _permisos.TienePermisoAsync(Permisos.TrabajadoresCrear, ct) || await _permisos.TienePermisoAsync(Permisos.TrabajadoresEditar, ct),
            ModuloBorrador.Proyectos => await _permisos.TienePermisoAsync(Permisos.ProyectosCrear, ct),
            _ => await _permisos.TienePermisoAsync(Permisos.TareasGestionar, ct)
        };
        if (!puede) throw ServiceException.Forbidden("No tiene permiso para capturar este tipo de formulario.");

        var datos = r.DatosJson?.Trim() ?? "";
        if (datos.Length == 0 || datos.Length > MaxCaracteres) throw ServiceException.Validation($"Los datos del borrador deben tener entre 1 y {MaxCaracteres} caracteres.");
        try
        {
            using var doc = JsonDocument.Parse(datos);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) throw ServiceException.Validation("Los datos del borrador deben ser un objeto JSON.");
            if (TieneClaveProhibida(doc.RootElement)) throw ServiceException.Validation("El borrador no puede contener contraseñas, tokens ni claves.");
        }
        catch (JsonException) { throw ServiceException.Validation("Los datos del borrador no son un JSON válido."); }
        return (modulo, tipo, datos);
    }

    private static bool TieneClaveProhibida(JsonElement e)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var p in e.EnumerateObject())
                    if (ClavesProhibidas.Any(k => p.Name.Contains(k, StringComparison.OrdinalIgnoreCase)) || TieneClaveProhibida(p.Value)) return true;
                return false;
            case JsonValueKind.Array:
                return e.EnumerateArray().Any(TieneClaveProhibida);
            default:
                return false;
        }
    }

    private static BorradorDto Dto(Borrador b) => new()
    {
        BorradorId = b.BorradorId, Modulo = b.Modulo, TipoFormulario = b.TipoFormulario, Resumen = Resumen(b.DatosJson), DatosJson = b.DatosJson,
        FechaCreacionUtc = b.FechaCreacionUtc, FechaActualizacionUtc = b.FechaActualizacionUtc, ExpiraUtc = b.ExpiraUtc
    };

    private static string? Resumen(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            foreach (var clave in new[] { "titulo", "nombre", "asunto", "Titulo", "Nombre", "Asunto" })
                if (doc.RootElement.TryGetProperty(clave, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString()))
                    return ProyectoHelper.Recortar(v.GetString()!.Trim(), 80);
        }
        catch (JsonException) { }
        return null;
    }
}
