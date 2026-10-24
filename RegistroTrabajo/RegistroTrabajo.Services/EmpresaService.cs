using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RegistroTrabajo.Data;
using RegistroTrabajo.Data.Entities;
using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public interface IEmpresaService
{
    Task<EmpresaDto> ObtenerAsync(CancellationToken ct);
    Task<EmpresaDto> ActualizarAsync(ActualizarEmpresaRequest request, CancellationToken ct);
    Task<ConfiguracionEmpresaDto> ObtenerConfiguracionAsync(CancellationToken ct);
    Task<ConfiguracionEmpresaDto> ActualizarConfiguracionAsync(ConfiguracionEmpresaDto request, CancellationToken ct);
}

public class EmpresaService : IEmpresaService
{
    private readonly RegistroTrabajoDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IAuditoriaService _auditoria;
    private readonly TimeProvider _clock;

    public EmpresaService(RegistroTrabajoDbContext db, ICurrentUser current, IAuditoriaService auditoria, TimeProvider clock)
    {
        _db = db; _current = current; _auditoria = auditoria; _clock = clock;
    }

    public async Task<EmpresaDto> ObtenerAsync(CancellationToken ct)
    {
        var id = _current.RequireEmpresaId();
        var e = await _db.Empresas.AsNoTracking().FirstOrDefaultAsync(x => x.EmpresaId == id, ct)
            ?? throw ServiceException.NotFound("Empresa no encontrada.");
        return ToDto(e);
    }

    public async Task<EmpresaDto> ActualizarAsync(ActualizarEmpresaRequest r, CancellationToken ct)
    {
        var id = _current.RequireEmpresaId();
        var e = await _db.Empresas.FirstOrDefaultAsync(x => x.EmpresaId == id, ct)
            ?? throw ServiceException.NotFound("Empresa no encontrada.");

        var rfc = Helpers.Limpiar(r.RFC)?.ToUpperInvariant();
        if (rfc is not null && await _db.Empresas.AnyAsync(x => x.RFC == rfc && x.EmpresaId != id, ct))
            throw ServiceException.Conflict("El RFC ya está registrado.");

        var antes = ToDto(e);
        e.Nombre = r.Nombre.Trim();
        e.NombreComercial = Helpers.Limpiar(r.NombreComercial);
        e.RFC = rfc;
        e.CorreoContacto = Helpers.Limpiar(r.CorreoContacto);
        e.TelefonoContacto = Helpers.Limpiar(r.TelefonoContacto);
        e.LogoUrl = Helpers.Limpiar(r.LogoUrl);
        e.ColorPrimario = Helpers.Limpiar(r.ColorPrimario);
        e.ColorSecundario = Helpers.Limpiar(r.ColorSecundario);
        e.FechaActualizacionUtc = _clock.GetUtcNow().UtcDateTime;

        _auditoria.Registrar("Empresa", nameof(Empresa), id.ToString(), "Actualizar", antes, ToDto(e));
        await _db.SaveSafeAsync(ct);
        return ToDto(e);
    }

    public async Task<ConfiguracionEmpresaDto> ObtenerConfiguracionAsync(CancellationToken ct)
    {
        var id = _current.RequireEmpresaId();
        var c = await _db.ConfiguracionEmpresa.AsNoTracking().FirstOrDefaultAsync(x => x.EmpresaId == id, ct)
            ?? throw ServiceException.NotFound("Configuración no encontrada.");
        return ToDto(c);
    }

    public async Task<ConfiguracionEmpresaDto> ActualizarConfiguracionAsync(ConfiguracionEmpresaDto r, CancellationToken ct)
    {
        var id = _current.RequireEmpresaId();
        ValidarConfiguracion(r);
        var c = await _db.ConfiguracionEmpresa.FirstOrDefaultAsync(x => x.EmpresaId == id, ct)
            ?? throw ServiceException.NotFound("Configuración no encontrada.");

        var antes = ToDto(c);
        c.ZonaHorariaId = r.ZonaHorariaId.Trim();
        c.Cultura = r.Cultura.Trim();
        c.FormatoFecha = r.FormatoFecha.Trim();
        c.FormatoHora = r.FormatoHora.Trim();
        c.CierreSesionInactividadMinutos = r.CierreSesionInactividadMinutos;
        c.DuracionSesionMaximaHoras = r.DuracionSesionMaximaHoras;
        c.PermitirValidacionGps = r.PermitirValidacionGps;
        c.FechaActualizacionUtc = _clock.GetUtcNow().UtcDateTime;

        _auditoria.Registrar("Empresa", nameof(ConfiguracionEmpresa), id.ToString(), "Actualizar", antes, ToDto(c));
        await _db.SaveSafeAsync(ct);
        return ToDto(c);
    }

    internal static void ValidarConfiguracion(ConfiguracionEmpresaDto r)
    {
        if (r.CierreSesionInactividadMinutos is < 5 or > 1440)
            throw ServiceException.Validation("La inactividad debe estar entre 5 y 1440 minutos.");
        if (r.DuracionSesionMaximaHoras is < 1 or > 168)
            throw ServiceException.Validation("La duración máxima de sesión debe estar entre 1 y 168 horas.");
        try { TimeZoneInfo.FindSystemTimeZoneById(r.ZonaHorariaId.Trim()); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        { throw ServiceException.Validation("Zona horaria no válida."); }
        try { CultureInfo.GetCultureInfo(r.Cultura.Trim()); }
        catch (CultureNotFoundException) { throw ServiceException.Validation("Cultura no válida."); }
        foreach (var f in new[] { r.FormatoFecha, r.FormatoHora })
        {
            try { _ = DateTime.UtcNow.ToString(f.Trim(), CultureInfo.InvariantCulture); }
            catch (FormatException) { throw ServiceException.Validation("Formato de fecha u hora no válido."); }
        }
    }

    internal static EmpresaDto ToDto(Empresa e) => new()
    {
        EmpresaId = e.EmpresaId, Nombre = e.Nombre, NombreComercial = e.NombreComercial, RFC = e.RFC,
        CorreoContacto = e.CorreoContacto, TelefonoContacto = e.TelefonoContacto, LogoUrl = e.LogoUrl,
        ColorPrimario = e.ColorPrimario, ColorSecundario = e.ColorSecundario, Estado = e.Estado
    };

    internal static ConfiguracionEmpresaDto ToDto(ConfiguracionEmpresa c) => new()
    {
        ZonaHorariaId = c.ZonaHorariaId, Cultura = c.Cultura, FormatoFecha = c.FormatoFecha, FormatoHora = c.FormatoHora,
        CierreSesionInactividadMinutos = c.CierreSesionInactividadMinutos,
        DuracionSesionMaximaHoras = c.DuracionSesionMaximaHoras, PermitirValidacionGps = c.PermitirValidacionGps
    };
}
