using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

/// <summary>Textos de presentación de reportes, dashboard y borradores.</summary>
public static class FormatoReportes
{
    public static string RutaBorrador(string modulo) => modulo switch
    {
        ModuloBorrador.Permisos => "permisos",
        ModuloBorrador.Proyectos => "proyectos",
        ModuloBorrador.Tareas => "proyectos",
        ModuloBorrador.Trabajadores => "trabajadores",
        _ => ""
    };
}
