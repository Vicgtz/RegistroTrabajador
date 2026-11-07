using System.Globalization;
using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

/// <summary>Formato de fechas y horas de la empresa. Valores por defecto = los de siempre; se actualizan al cargar la apariencia.</summary>
public static class FormatoEmpresa
{
    private static string _fecha = "dd/MM/yyyy", _hora = "HH:mm";
    private static CultureInfo _cultura = new("es-MX");
    private static TimeZoneInfo? _zona;

    public static void Aplicar(AparienciaEmpresaDto? a)
    {
        _fecha = a is not null && FormatosRegionales.Fecha.Contains(a.FormatoFecha) ? a.FormatoFecha : "dd/MM/yyyy";
        _hora = a is not null && FormatosRegionales.Hora.Contains(a.FormatoHora) ? a.FormatoHora : "HH:mm";
        try { _cultura = a is null ? new CultureInfo("es-MX") : CultureInfo.GetCultureInfo(a.Cultura); } catch (CultureNotFoundException) { _cultura = new CultureInfo("es-MX"); }
        try { _zona = a is null ? null : TimeZoneInfo.FindSystemTimeZoneById(a.ZonaHorariaId); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException) { _zona = null; }   // sin datos de zona en el navegador: hora local del equipo
    }

    private static DateTime ALocal(DateTime utc)
    {
        utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return _zona is null ? utc.ToLocalTime() : TimeZoneInfo.ConvertTimeFromUtc(utc, _zona);
    }

    public static string Fecha(DateTime utc) => ALocal(utc).ToString(_fecha, _cultura);
    public static string Hora(DateTime utc) => ALocal(utc).ToString(_hora, _cultura);
    public static string FechaHora(DateTime utc) => ALocal(utc).ToString(_fecha + " " + _hora, _cultura);
}
