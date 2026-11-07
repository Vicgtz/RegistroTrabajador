using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public static class FormatoPermisos
{
    public static string Estado(byte e) => e switch
    {
        EstadoSolicitudPermiso.Pendiente => "Pendiente", EstadoSolicitudPermiso.Aprobada => "Aprobada",
        EstadoSolicitudPermiso.Rechazada => "Rechazada", EstadoSolicitudPermiso.Cancelada => "Cancelada", _ => "Concluida"
    };

    public static string Clase(byte e) => e switch
    {
        EstadoSolicitudPermiso.Pendiente => "text-bg-warning", EstadoSolicitudPermiso.Aprobada => "text-bg-success",
        EstadoSolicitudPermiso.Rechazada => "text-bg-danger", _ => "text-bg-secondary"
    };

    public static string EstadoCancelacion(byte e) => e switch
    {
        EstadoCancelacionPermiso.Pendiente => "Pendiente", EstadoCancelacionPermiso.Aprobada => "Aprobada",
        EstadoCancelacionPermiso.Rechazada => "Rechazada", _ => "Cancelada"
    };

    public static string Unidad(byte unidad) => unidad == UnidadPermiso.Horas ? "horas" : "días";

    public static string Cantidad(decimal cantidad, byte unidad) => $"{cantidad:0.##} {Unidad(unidad)}";

    /// <summary>Las fechas del permiso son locales de la empresa (no UTC): se muestran tal cual.</summary>
    public static string Rango(DateTime ini, DateTime fin, bool porHoras) => porHoras
        ? $"{ini:dd/MM/yyyy} {ini:HH:mm}–{fin:HH:mm}"
        : ini.Date == fin.Date ? $"{ini:dd/MM/yyyy}" : $"{ini:dd/MM/yyyy} – {fin:dd/MM/yyyy}";

    public static string Movimiento(byte tipo) => tipo switch
    {
        TipoMovimientoSaldo.Asignacion => "Asignación", TipoMovimientoSaldo.Consumo => "Consumo",
        TipoMovimientoSaldo.Devolucion => "Devolución", _ => "Ajuste a la baja"
    };
}
