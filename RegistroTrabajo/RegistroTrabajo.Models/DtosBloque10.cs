using System.ComponentModel.DataAnnotations;

namespace RegistroTrabajo.Models;

// ---------- Tipos ----------

/// <summary>Códigos de reporte (segmento de ruta de <c>api/reportes/{tipo}</c>).</summary>
public static class TipoReporte
{
    public const string Trabajadores = "trabajadores";
    public const string Movimientos = "movimientos";
    public const string Permisos = "permisos";
    public const string Asistencia = "asistencia";
    public const string Proyectos = "proyectos";
    public const string Tareas = "tareas";
    public const string CargaTrabajo = "carga-trabajo";

    public static readonly string[] Todos = { Trabajadores, Movimientos, Permisos, Asistencia, Proyectos, Tareas, CargaTrabajo };

    public static string Nombre(string tipo) => tipo switch
    {
        Trabajadores => "Trabajadores",
        Movimientos => "Cambios laborales",
        Permisos => "Permisos y ausencias",
        Asistencia => "Asistencia",
        Proyectos => "Proyectos",
        Tareas => "Tareas",
        CargaTrabajo => "Carga de trabajo",
        _ => tipo
    };
}

public static class FormatoExportacion
{
    public const string Excel = "xlsx";
    public const string Pdf = "pdf";
}

/// <summary>Módulos y formularios que admiten borrador. Cada uno exige el permiso de crear del módulo correspondiente.</summary>
public static class ModuloBorrador
{
    public const string Permisos = "Permisos";
    public const string Trabajadores = "Trabajadores";
    public const string Proyectos = "Proyectos";
    public const string Tareas = "Tareas";

    public static readonly string[] Todos = { Permisos, Trabajadores, Proyectos, Tareas };
}

// ---------- Dashboard ----------

public class DashboardDto
{
    /// <summary>Rol principal con el que se adaptó el contenido: Administrador, Recursos Humanos, Supervisor o Trabajador.</summary>
    public string Perfil { get; set; } = null!;
    /// <summary>"empresa", "equipo" o "propio": hasta dónde llegan los indicadores.</summary>
    public string Ambito { get; set; } = null!;
    public DateTime GeneradoUtc { get; set; }
    public IndicadorTrabajadoresDto? Trabajadores { get; set; }
    public IndicadorPermisosDto? Permisos { get; set; }
    public IndicadorProyectosDto? Proyectos { get; set; }
    public IndicadorTareasDto? Tareas { get; set; }
    public IndicadorAsistenciaDto? Asistencia { get; set; }
    public int? UsuariosActivos { get; set; }
    public int NotificacionesNoLeidas { get; set; }
    public int NotificacionesPrioritarias { get; set; }
    public List<ActividadRecienteDto> Actividad { get; set; } = new();
    public List<AccesoRapidoDto> Accesos { get; set; } = new();
}

public class IndicadorTrabajadoresDto
{
    public int Activos { get; set; }
    public int Inactivos { get; set; }
    public int Baja { get; set; }
    public int AltasUltimos30Dias { get; set; }
}

public class IndicadorPermisosDto
{
    /// <summary>Solicitudes de otros trabajadores que el usuario puede resolver (vacío para quien no revisa).</summary>
    public int? PendientesPorRevisar { get; set; }
    /// <summary>Solicitudes del propio usuario que siguen pendientes.</summary>
    public int MisPendientes { get; set; }
    /// <summary>Permisos aprobados que comienzan en los próximos 14 días dentro del alcance.</summary>
    public int ProximosAprobados { get; set; }
}

public class IndicadorProyectosDto
{
    public int Activos { get; set; }
    public int Pausados { get; set; }
    public int Finalizados { get; set; }
}

public class IndicadorTareasDto
{
    public int Pendientes { get; set; }
    public int EnProceso { get; set; }
    public int EnRevision { get; set; }
    public int Reabiertas { get; set; }
    public int Completadas { get; set; }
    public int Vencidas { get; set; }
    public int ProximasAVencer { get; set; }
}

public class IndicadorAsistenciaDto
{
    /// <summary>Trabajadores del alcance con jornada registrada hoy.</summary>
    public int ConRegistroHoy { get; set; }
    /// <summary>Jornadas abiertas (entrada sin salida) de días anteriores.</summary>
    public int IncompletasAnteriores { get; set; }
}

public class ActividadRecienteDto
{
    public DateTime FechaUtc { get; set; }
    public string Texto { get; set; } = null!;
    /// <summary>Ruta de la aplicación; el destino vuelve a validar el acceso al abrirse.</summary>
    public string? Ruta { get; set; }
}

public class AccesoRapidoDto
{
    public string Titulo { get; set; } = null!;
    public string Ruta { get; set; } = null!;
}

// ---------- Reportes ----------

public class ReporteFiltro : PageQuery
{
    public DateOnly? Desde { get; set; }
    public DateOnly? Hasta { get; set; }
    /// <summary>"mes", "trimestre" o "anio"; junto con <see cref="Anio"/> y <see cref="Numero"/> sustituye a Desde/Hasta.</summary>
    [StringLength(10)] public string? Periodo { get; set; }
    [Range(2000, 2100)] public int? Anio { get; set; }
    /// <summary>Mes (1..12) o trimestre (1..4) según <see cref="Periodo"/>.</summary>
    [Range(1, 12)] public int? Numero { get; set; }
    public Guid? TrabajadorId { get; set; }
    public Guid? DepartamentoId { get; set; }
    public Guid? PuestoId { get; set; }
    public Guid? SupervisorId { get; set; }
    public Guid? TipoPermisoId { get; set; }
    public Guid? ProyectoId { get; set; }
    public Guid? EstadoProyectoId { get; set; }
    /// <summary>Estado según el reporte (estado laboral, de la solicitud, de la jornada, de la tarea o del proyecto).</summary>
    public byte? Estado { get; set; }
    [StringLength(100)] public string? Buscar { get; set; }
    [StringLength(40)] public string? OrdenarPor { get; set; }
    public bool Descendente { get; set; }
    /// <summary>Solo asistencia: jornadas con entrada y sin salida.</summary>
    public bool SoloIncompletos { get; set; }
    /// <summary>Solo tareas: vencidas según los días hábiles de la empresa.</summary>
    public bool SoloVencidas { get; set; }
}

public class ColumnaReporteDto
{
    public string Clave { get; set; } = null!;
    public string Titulo { get; set; } = null!;
    /// <summary>"texto", "numero", "fecha" o "fechahora".</summary>
    public string Tipo { get; set; } = "texto";
    public bool Ordenable { get; set; }
}

public class ResumenReporteDto
{
    public string Etiqueta { get; set; } = null!;
    public string Valor { get; set; } = null!;
}

public class ReporteDto
{
    public string Tipo { get; set; } = null!;
    public string Titulo { get; set; } = null!;
    public string? Nota { get; set; }
    public DateTime GeneradoUtc { get; set; }
    public List<string> FiltrosAplicados { get; set; } = new();
    public List<ColumnaReporteDto> Columnas { get; set; } = new();
    /// <summary>Celdas ya formateadas, en el orden de <see cref="Columnas"/>.</summary>
    public List<List<string?>> Filas { get; set; } = new();
    public List<ResumenReporteDto> Resumen { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
}

/// <summary>Archivo generado por la exportación (nunca incluye rutas del servidor).</summary>
public class ArchivoExportadoDto
{
    public string NombreArchivo { get; set; } = null!;
    public string TipoContenido { get; set; } = null!;
    public byte[] Contenido { get; set; } = Array.Empty<byte>();
}

// ---------- Borradores ----------

public class BorradorDto
{
    public Guid BorradorId { get; set; }
    public string Modulo { get; set; } = null!;
    public string TipoFormulario { get; set; } = null!;
    /// <summary>Titulo corto para la lista, tomado de los datos si traen "titulo", "nombre" o "asunto".</summary>
    public string? Resumen { get; set; }
    public string DatosJson { get; set; } = null!;
    public DateTime FechaCreacionUtc { get; set; }
    public DateTime FechaActualizacionUtc { get; set; }
    public DateTime? ExpiraUtc { get; set; }
}

public class GuardarBorradorRequest
{
    [Required, StringLength(80)] public string Modulo { get; set; } = null!;
    [Required, StringLength(100)] public string TipoFormulario { get; set; } = null!;
    [Required] public string DatosJson { get; set; } = null!;
}

public class BorradorFiltro : PageQuery
{
    [StringLength(80)] public string? Modulo { get; set; }
}

// ---------- Errores técnicos ----------

public class ErrorTecnicoDto
{
    public long ErrorTecnicoId { get; set; }
    public DateTime FechaUtc { get; set; }
    public string Nivel { get; set; } = null!;
    /// <summary>Tipo de la excepción.</summary>
    public string? Codigo { get; set; }
    public string Mensaje { get; set; } = null!;
    public Guid? CorrelationId { get; set; }
    public string? Ruta { get; set; }
    public string? DetalleTecnico { get; set; }
    public Guid? UsuarioId { get; set; }
}

public class ErrorTecnicoFiltro : PageQuery
{
    public Guid? CorrelationId { get; set; }
    public DateOnly? Desde { get; set; }
    public DateOnly? Hasta { get; set; }
    [StringLength(100)] public string? Buscar { get; set; }
}
