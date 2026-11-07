using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public static class FormatoLaboral
{
    public static string Estado(byte e) => e switch
    {
        EstadoLaboral.Activo => "Activo", EstadoLaboral.Inactivo => "Inactivo", EstadoLaboral.Baja => "Baja", _ => "Desconocido"
    };

    public static string ClaseEstado(byte e) => e switch
    {
        EstadoLaboral.Activo => "text-bg-success", EstadoLaboral.Inactivo => "text-bg-warning", _ => "text-bg-secondary"
    };

    public static string EstadoSolicitud(byte e) => e switch
    {
        EstadoSolicitudPerfil.Pendiente => "Pendiente", EstadoSolicitudPerfil.Aprobada => "Aprobada",
        EstadoSolicitudPerfil.Rechazada => "Rechazada", EstadoSolicitudPerfil.Cancelada => "Cancelada", _ => "Desconocido"
    };

    public static string ClaseSolicitud(byte e) => e switch
    {
        EstadoSolicitudPerfil.Pendiente => "text-bg-warning", EstadoSolicitudPerfil.Aprobada => "text-bg-success",
        EstadoSolicitudPerfil.Rechazada => "text-bg-danger", _ => "text-bg-secondary"
    };

    public static string Fecha(DateOnly? d) => d is null ? "—" : d.Value.ToString("dd/MM/yyyy");
}
