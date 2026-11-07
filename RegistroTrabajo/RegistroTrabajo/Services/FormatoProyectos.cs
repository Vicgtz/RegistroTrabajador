using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

/// <summary>Textos y clases de presentación de proyectos y tareas.</summary>
public static class FormatoProyectos
{
    public static readonly byte[] EstadosTarea =
    {
        EstadoTarea.Pendiente, EstadoTarea.EnProceso, EstadoTarea.EnRevision, EstadoTarea.Completada, EstadoTarea.Reabierta, EstadoTarea.Cancelada
    };

    public static readonly byte[] Prioridades = { PrioridadTrabajo.Baja, PrioridadTrabajo.Media, PrioridadTrabajo.Alta, PrioridadTrabajo.Urgente };

    public static string EstadoDeTarea(byte e) => e switch
    {
        EstadoTarea.Pendiente => "Pendiente",
        EstadoTarea.EnProceso => "En proceso",
        EstadoTarea.EnRevision => "En revisión",
        EstadoTarea.Completada => "Completada",
        EstadoTarea.Reabierta => "Reabierta",
        EstadoTarea.Cancelada => "Cancelada",
        _ => "—"
    };

    /// <summary>Sufijo de la clase text-bg-* de Bootstrap.</summary>
    public static string ClaseEstadoTarea(byte e) => e switch
    {
        EstadoTarea.Pendiente => "secondary",
        EstadoTarea.EnProceso => "primary",
        EstadoTarea.EnRevision => "warning",
        EstadoTarea.Completada => "success",
        EstadoTarea.Reabierta => "danger",
        _ => "dark"
    };

    public static string Prioridad(byte p) => p switch
    {
        PrioridadTrabajo.Baja => "Baja",
        PrioridadTrabajo.Media => "Media",
        PrioridadTrabajo.Alta => "Alta",
        PrioridadTrabajo.Urgente => "Urgente",
        _ => "—"
    };

    public static string ClasePrioridad(byte p) => p switch
    {
        PrioridadTrabajo.Baja => "secondary",
        PrioridadTrabajo.Media => "info",
        PrioridadTrabajo.Alta => "warning",
        _ => "danger"
    };

    public static string Fecha(DateOnly? d) => d is { } f ? f.ToString("dd/MM/yyyy") : "—";

    public static string FechaHora(DateTime utc) => FormatoEmpresa.FechaHora(utc);

    public static string Pct(int p) => $"{Math.Clamp(p, 0, 100)}%";

    /// <summary>Clase de la barra de progreso según el avance.</summary>
    public static string ClaseBarra(int p) => p >= 100 ? "bg-success" : p >= 50 ? "" : "bg-warning";
}
