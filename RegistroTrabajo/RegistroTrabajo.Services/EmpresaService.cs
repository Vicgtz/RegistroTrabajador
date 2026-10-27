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

        var colorPrimario = Helpers.Limpiar(r.ColorPrimario);
        if (colorPrimario is not null && !ContrasteSuficienteConBlanco(colorPrimario))
            throw ServiceException.Validation("El color principal es demasiado claro: el texto blanco de los botones no se leería bien.");

        var antes = ToDto(e);
        e.Nombre = r.Nombre.Trim();
        e.NombreComercial = Helpers.Limpiar(r.NombreComercial);
        e.RFC = rfc;
        e.CorreoContacto = Helpers.Limpiar(r.CorreoContacto);
        e.TelefonoContacto = Helpers.Limpiar(r.TelefonoContacto);
        // El logotipo solo se cambia con LogoEmpresaService (subida validada); LogoUrl del request se ignora.
        e.ColorPrimario = colorPrimario;
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
        c.NombreMostrado = Helpers.Limpiar(r.NombreMostrado);
        c.TextoInstitucional = Helpers.Limpiar(r.TextoInstitucional);
        c.PrimerDiaSemana = r.PrimerDiaSemana;
        c.Moneda = Helpers.Limpiar(r.Moneda)?.ToUpperInvariant();
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
        if (!FormatosRegionales.Fecha.Contains(r.FormatoFecha.Trim()))
            throw ServiceException.Validation("Formato de fecha no permitido.");
        if (!FormatosRegionales.Hora.Contains(r.FormatoHora.Trim()))
            throw ServiceException.Validation("Formato de hora no permitido.");
        if (r.PrimerDiaSemana > 6) throw ServiceException.Validation("El primer día de la semana debe estar entre 0 y 6.");
        var moneda = Helpers.Limpiar(r.Moneda);
        if (moneda is not null && (moneda.Length != 3 || !moneda.All(char.IsAsciiLetter)))
            throw ServiceException.Validation("La moneda debe ser un código de tres letras (por ejemplo MXN).");
    }

    /// <summary>Contraste mínimo 4.5:1 del color con texto blanco (WCAG AA).</summary>
    internal static bool ContrasteSuficienteConBlanco(string hex)
    {
        static double Canal(int v) { var c = v / 255.0; return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4); }
        var rgb = Convert.ToInt32(hex.TrimStart('#'), 16);
        var l = 0.2126 * Canal(rgb >> 16 & 0xFF) + 0.7152 * Canal(rgb >> 8 & 0xFF) + 0.0722 * Canal(rgb & 0xFF);
        return 1.05 / (l + 0.05) >= 4.5;
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
        DuracionSesionMaximaHoras = c.DuracionSesionMaximaHoras, PermitirValidacionGps = c.PermitirValidacionGps,
        NombreMostrado = c.NombreMostrado, TextoInstitucional = c.TextoInstitucional, PrimerDiaSemana = c.PrimerDiaSemana, Moneda = c.Moneda
    };
}
