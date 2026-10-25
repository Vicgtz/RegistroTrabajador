using System.ComponentModel.DataAnnotations;

namespace RegistroTrabajo.Models;

// ---------- Departamentos y puestos ----------
public class CatalogoItemDto
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = null!;
    public string? Descripcion { get; set; }
    public bool Estado { get; set; }
    public int Trabajadores { get; set; }
}

public class GuardarCatalogoRequest
{
    [Required, StringLength(120, MinimumLength = 2)] public string Nombre { get; set; } = null!;
    [StringLength(300)] public string? Descripcion { get; set; }
}

// ---------- Trabajadores ----------
/// <summary>Datos básicos visibles para cualquier trabajador autorizado a consultar.</summary>
public class TrabajadorResumenDto
{
    public Guid TrabajadorId { get; set; }
    public string? NumeroEmpleado { get; set; }
    public string Nombres { get; set; } = null!;
    public string Apellidos { get; set; } = null!;
    public Guid? DepartamentoId { get; set; }
    public string? Departamento { get; set; }
    public Guid? PuestoId { get; set; }
    public string? Puesto { get; set; }
    public byte EstadoLaboral { get; set; }
    public IReadOnlyList<string> Supervisores { get; set; } = Array.Empty<string>();
}

public class TrabajadorFiltro : PageQuery
{
    [StringLength(100)] public string? Buscar { get; set; }
    public Guid? DepartamentoId { get; set; }
    public Guid? PuestoId { get; set; }
    public byte? EstadoLaboral { get; set; }
}

/// <summary>Expediente. Los campos sensibles son nulos si el usuario no tiene permiso para verlos.</summary>
public class TrabajadorDetalleDto : TrabajadorResumenDto
{
    public Guid? UsuarioId { get; set; }
    public DateOnly? FechaIngreso { get; set; }
    public DateOnly? FechaBaja { get; set; }
    public string? MotivoBaja { get; set; }
    public bool VerSensibles { get; set; }
    public DateOnly? FechaNacimiento { get; set; }
    public string? CorreoContacto { get; set; }
    public string? Telefono { get; set; }
    public string? Direccion { get; set; }
    public string RowVersion { get; set; } = null!;
}

public class CrearTrabajadorRequest
{
    [StringLength(40)] public string? NumeroEmpleado { get; set; }
    [Required, StringLength(120, MinimumLength = 1)] public string Nombres { get; set; } = null!;
    [Required, StringLength(160, MinimumLength = 1)] public string Apellidos { get; set; } = null!;
    public DateOnly? FechaNacimiento { get; set; }
    [EmailAddress, StringLength(254)] public string? CorreoContacto { get; set; }
    [StringLength(30)] public string? Telefono { get; set; }
    [StringLength(300)] public string? Direccion { get; set; }
    public Guid? DepartamentoId { get; set; }
    public Guid? PuestoId { get; set; }
    public DateOnly FechaIngreso { get; set; }
    public Guid? UsuarioId { get; set; }
}

public class ActualizarTrabajadorRequest : CrearTrabajadorRequest
{
    /// <summary>Fecha desde la que rige un cambio de puesto o departamento (por defecto, hoy).</summary>
    public DateOnly? FechaEfectiva { get; set; }
    [StringLength(500)] public string? MotivoCambio { get; set; }
    [Required] public string RowVersion { get; set; } = null!;
}

public class BajaTrabajadorRequest
{
    public DateOnly FechaBaja { get; set; }
    [Required, StringLength(500, MinimumLength = 3)] public string Motivo { get; set; } = null!;
}

public class ReingresoRequest
{
    [StringLength(500)] public string? Motivo { get; set; }
}

public class CambiarEstadoLaboralRequest
{
    /// <summary>Solo Activo (1) o Inactivo (2); la baja tiene su propia operación.</summary>
    public byte EstadoLaboral { get; set; }
    [StringLength(500)] public string? Motivo { get; set; }
}

// ---------- Supervisión ----------
public class SupervisorAsignadoDto
{
    public long TrabajadorSupervisorId { get; set; }
    public Guid TrabajadorId { get; set; }
    public Guid SupervisorTrabajadorId { get; set; }
    public string SupervisorNombre { get; set; } = null!;
    public Guid? DepartamentoId { get; set; }
    public DateOnly FechaInicio { get; set; }
    public DateOnly? FechaFin { get; set; }
    public bool EsPrincipal { get; set; }
}

public class AsignarSupervisorRequest
{
    [Required] public Guid SupervisorTrabajadorId { get; set; }
    public Guid? DepartamentoId { get; set; }
    public DateOnly? FechaInicio { get; set; }
    public bool EsPrincipal { get; set; }
}

public class FinalizarSupervisionRequest
{
    public DateOnly? FechaFin { get; set; }
}

// ---------- Historial ----------
public class HistorialLaboralDto
{
    public long HistorialLaboralId { get; set; }
    public DateOnly FechaEfectiva { get; set; }
    public string TipoCambio { get; set; } = null!;
    public string? DatosAntesJson { get; set; }
    public string? DatosDespuesJson { get; set; }
    public string? Motivo { get; set; }
    public DateTime RegistradoUtc { get; set; }
}

// ---------- Contactos de emergencia ----------
public class ContactoEmergenciaDto
{
    public Guid ContactoEmergenciaId { get; set; }
    public string NombreCompleto { get; set; } = null!;
    public string? Parentesco { get; set; }
    public string Telefono { get; set; } = null!;
    public string? TelefonoAlternativo { get; set; }
    public bool EsPrincipal { get; set; }
}

public class GuardarContactoEmergenciaRequest
{
    [Required, StringLength(180, MinimumLength = 2)] public string NombreCompleto { get; set; } = null!;
    [StringLength(80)] public string? Parentesco { get; set; }
    [Required, StringLength(30, MinimumLength = 5)] public string Telefono { get; set; } = null!;
    [StringLength(30)] public string? TelefonoAlternativo { get; set; }
    public bool EsPrincipal { get; set; }
}

// ---------- Solicitudes de cambio de perfil ----------
/// <summary>Campos del perfil que el trabajador puede pedir cambiar; los nulos no se tocan.</summary>
public class CambioPerfilDatos
{
    [EmailAddress, StringLength(254)] public string? CorreoContacto { get; set; }
    [StringLength(30)] public string? Telefono { get; set; }
    [StringLength(300)] public string? Direccion { get; set; }
}

public class SolicitudCambioPerfilDto
{
    public Guid SolicitudCambioPerfilId { get; set; }
    public Guid TrabajadorId { get; set; }
    public string TrabajadorNombre { get; set; } = null!;
    public CambioPerfilDatos Datos { get; set; } = new();
    public byte Estado { get; set; }
    public DateTime SolicitadaUtc { get; set; }
    public Guid? RevisadaPorUsuarioId { get; set; }
    public DateTime? RevisadaUtc { get; set; }
    public string? ComentarioRevision { get; set; }
}

public class SolicitudPerfilFiltro : PageQuery
{
    public byte? Estado { get; set; }
}

public class ResolverSolicitudRequest
{
    public bool Aprobar { get; set; }
    [StringLength(250)] public string? Comentario { get; set; }
}
