using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public static class FormatoAsistencia
{
    public static string Hora(DateTime? utc) => utc is null ? "—" : FormatoEmpresa.Hora(utc.Value);
    public static string FechaHora(DateTime? utc) => utc is null ? "—" : FormatoEmpresa.FechaHora(utc.Value);

    public static string Estado(byte estado) => estado switch
    {
        EstadoAsistencia.Abierta => "Incompleta",
        EstadoAsistencia.Completa => "Completa",
        EstadoAsistencia.Anulada => "Anulada",
        _ => "—"
    };

    public static string ClaseEstado(byte estado) => estado switch
    {
        EstadoAsistencia.Completa => "text-bg-success",
        EstadoAsistencia.Abierta => "text-bg-warning",
        _ => "text-bg-secondary"
    };

    public static string Dia(byte d) => d switch
    {
        1 => "Lunes", 2 => "Martes", 3 => "Miércoles", 4 => "Jueves", 5 => "Viernes", 6 => "Sábado", 7 => "Domingo", _ => "—"
    };

    public static string Horario(HorarioDetalleDiaDto d) => d.EsDiaLaboral && d.HoraEntrada is { } e && d.HoraSalida is { } s
        ? $"{e:HH\\:mm} – {s:HH\\:mm}{(d.CruzaMedianoche ? " (+1 día)" : "")}"
        : "Descanso";

    public static DateTime? AUtc(DateTime? localInput) =>
        localInput is null ? null : DateTime.SpecifyKind(localInput.Value, DateTimeKind.Local).ToUniversalTime();
}
