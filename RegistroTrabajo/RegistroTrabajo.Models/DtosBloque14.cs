using System.ComponentModel.DataAnnotations;

namespace RegistroTrabajo.Models;

// Bloque 14: expediente profesional (estudios, habilidades y currículum). La BD solo limita rangos; el significado es de la aplicación.

/// <summary>Estado de un estudio o habilidad: Pendiente = propuesto por el trabajador y aún sin revisar.</summary>
public static class EstadoExpediente
{
    public const byte Aprobado = 1;
    public const byte Pendiente = 2;
    public const byte Rechazado = 3;

    public static string Nombre(byte e) => e switch { Aprobado => "Aprobado", Pendiente => "Pendiente", Rechazado => "Rechazado", _ => "—" };
}

public static class TipoEstudio
{
    public const byte Tecnico = 1;
    public const byte Licenciatura = 2;
    public const byte Maestria = 3;
    public const byte Doctorado = 4;
    public const byte Certificado = 5;
    public const byte Curso = 6;

    public static readonly byte[] Todos = { Tecnico, Licenciatura, Maestria, Doctorado, Certificado, Curso };

    public static string Nombre(byte t) => t switch
    {
        Tecnico => "Técnico", Licenciatura => "Licenciatura", Maestria => "Maestría", Doctorado => "Doctorado",
        Certificado => "Certificado", Curso => "Curso", _ => "—"
    };
}

public static class NivelHabilidad
{
    public const byte Basico = 1;
    public const byte Intermedio = 2;
    public const byte Avanzado = 3;

    public static string Nombre(byte n) => n switch { Basico => "Básico", Intermedio => "Intermedio", Avanzado => "Avanzado", _ => "—" };
}

public static class LimitesCurriculum
{
    public const int MaxBytes = 5 * 1024 * 1024;
    public const int MaxHabilidadesPorBusqueda = 10;
}

// ---------- Catálogo de habilidades ----------
public class HabilidadDto
{
    public Guid HabilidadId { get; set; }
    public string Nombre { get; set; } = null!;
    public bool Activa { get; set; }
    /// <summary>Trabajadores con la habilidad aprobada.</summary>
    public int Trabajadores { get; set; }
}

public class ActualizarHabilidadRequest
{
    [Required(ErrorMessage = "Ingresa el nombre"), StringLength(80, MinimumLength = 2)] public string Nombre { get; set; } = "";
    public bool Activa { get; set; } = true;
}

// ---------- Habilidades del trabajador ----------
public class TrabajadorHabilidadDto
{
    public Guid TrabajadorHabilidadId { get; set; }
    public Guid HabilidadId { get; set; }
    public string Nombre { get; set; } = null!;
    public byte Nivel { get; set; }
    public byte? AniosExperiencia { get; set; }
    public string? Notas { get; set; }
    public byte Estado { get; set; }
    public string? MotivoRechazo { get; set; }
}

/// <summary>Agrega o actualiza una habilidad. Si el nombre no existe en el catálogo de la empresa se crea.</summary>
public class GuardarHabilidadRequest
{
    [Required(ErrorMessage = "Ingresa la habilidad"), StringLength(80, MinimumLength = 2)] public string Nombre { get; set; } = "";
    [Range(1, 3, ErrorMessage = "Nivel no válido")] public byte Nivel { get; set; } = NivelHabilidad.Intermedio;
    [Range(0, 60)] public byte? AniosExperiencia { get; set; }
    [StringLength(300)] public string? Notas { get; set; }
}

// ---------- Estudios ----------
public class EstudioDto
{
    public Guid EstudioId { get; set; }
    public byte Tipo { get; set; }
    public string Titulo { get; set; } = null!;
    public string? Institucion { get; set; }
    public DateOnly? FechaInicio { get; set; }
    public DateOnly? FechaFin { get; set; }
    public bool EnCurso { get; set; }
    public string? Notas { get; set; }
    public byte Estado { get; set; }
    public string? MotivoRechazo { get; set; }
}

public class GuardarEstudioRequest
{
    [Range(1, 6, ErrorMessage = "Tipo de estudio no válido")] public byte Tipo { get; set; } = TipoEstudio.Licenciatura;
    [Required(ErrorMessage = "Ingresa el título"), StringLength(200, MinimumLength = 2)] public string Titulo { get; set; } = "";
    [StringLength(200)] public string? Institucion { get; set; }
    public DateOnly? FechaInicio { get; set; }
    public DateOnly? FechaFin { get; set; }
    public bool EnCurso { get; set; }
    [StringLength(500)] public string? Notas { get; set; }
}

// ---------- Currículum ----------
public class CurriculumInfoDto
{
    public string NombreArchivo { get; set; } = null!;
    public string TipoContenido { get; set; } = null!;
    public int TamanoBytes { get; set; }
    public DateTime FechaActualizacionUtc { get; set; }
}

// ---------- Expediente completo ----------
public class ExpedienteProfesionalDto
{
    public Guid TrabajadorId { get; set; }
    /// <summary>El usuario actual es el propio trabajador (sus cambios quedan pendientes de aprobación).</summary>
    public bool EsPropio { get; set; }
    /// <summary>Puede registrar y editar sin aprobación (permiso expediente.editar).</summary>
    public bool PuedeEditar { get; set; }
    public bool PuedeAprobar { get; set; }
    public IReadOnlyList<EstudioDto> Estudios { get; set; } = Array.Empty<EstudioDto>();
    public IReadOnlyList<TrabajadorHabilidadDto> Habilidades { get; set; } = Array.Empty<TrabajadorHabilidadDto>();
    public CurriculumInfoDto? Curriculum { get; set; }
}

// ---------- Aprobación ----------
public class ResolverExpedienteRequest
{
    public bool Aprobar { get; set; }
    [StringLength(300)] public string? MotivoRechazo { get; set; }
}

public class PendienteExpedienteDto
{
    /// <summary>"Estudio" o "Habilidad".</summary>
    public string Clase { get; set; } = null!;
    public Guid Id { get; set; }
    public Guid TrabajadorId { get; set; }
    public string TrabajadorNombre { get; set; } = null!;
    public string Descripcion { get; set; } = null!;
    public DateTime FechaCreacionUtc { get; set; }
}

// ---------- Búsqueda por perfil ----------
public class PerfilFiltro : PageQuery
{
    /// <summary>Habilidades buscadas (catálogo). Máximo 10.</summary>
    public List<Guid>? HabilidadIds { get; set; }
    /// <summary>true: el trabajador debe tener todas las habilidades; false: basta con alguna.</summary>
    public bool TodasLasHabilidades { get; set; } = true;
    [Range(1, 3)] public byte? NivelMinimo { get; set; }
    [Range(1, 6)] public byte? TipoEstudio { get; set; }
    /// <summary>Texto en título o institución de un estudio aprobado.</summary>
    [StringLength(100)] public string? Estudio { get; set; }
    public bool SoloActivos { get; set; } = true;
}

public class PerfilResultadoDto : TrabajadorResumenDto
{
    public IReadOnlyList<string> Habilidades { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Estudios { get; set; } = Array.Empty<string>();
}
