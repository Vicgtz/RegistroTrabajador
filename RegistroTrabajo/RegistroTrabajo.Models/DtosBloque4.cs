using System.ComponentModel.DataAnnotations;

namespace RegistroTrabajo.Models;

// ---------- Tipos de permiso y reglas ----------
public class TipoPermisoDto
{
    public Guid TipoPermisoId { get; set; }
    public string Nombre { get; set; } = null!;
    public string? Descripcion { get; set; }
    /// <summary><see cref="UnidadPermiso"/>: 1 días, 2 horas.</summary>
    public byte UnidadBase { get; set; }
    public bool RequiereAprobacion { get; set; }
    public bool RequiereComprobante { get; set; }
    public bool PermiteFraccionar { get; set; }
    public bool Estado { get; set; }
    public IReadOnlyList<ReglaPermisoDto> Reglas { get; set; } = Array.Empty<ReglaPermisoDto>();
}

public class GuardarTipoPermisoRequest
{
    [Required, StringLength(120, MinimumLength = 2)] public string Nombre { get; set; } = null!;
    [StringLength(300)] public string? Descripcion { get; set; }
    [Range(1, 2)] public byte UnidadBase { get; set; } = UnidadPermiso.Dias;
    public bool RequiereAprobacion { get; set; } = true;
    public bool RequiereComprobante { get; set; }
    public bool PermiteFraccionar { get; set; }
}

public class ReglaPermisoDto
{
    public Guid ReglaPermisoId { get; set; }
    public Guid TipoPermisoId { get; set; }
    public int? AntiguedadMinimaMeses { get; set; }
    /// <summary>Cantidad que se asigna cada año. Nulo = el tipo no controla saldo.</summary>
    public decimal? CantidadAnual { get; set; }
    public decimal? LimitePorSolicitud { get; set; }
    public bool PermiteSaldoNegativo { get; set; }
    public bool ReiniciaAnualmente { get; set; }
    public DateOnly? ActivaDesde { get; set; }
    public DateOnly? ActivaHasta { get; set; }
    public bool Estado { get; set; }
}

public class GuardarReglaPermisoRequest
{
    [Range(0, 600)] public int? AntiguedadMinimaMeses { get; set; }
    [Range(0, 10000)] public decimal? CantidadAnual { get; set; }
    [Range(0.01, 10000)] public decimal? LimitePorSolicitud { get; set; }
    public bool PermiteSaldoNegativo { get; set; }
    public bool ReiniciaAnualmente { get; set; } = true;
    public DateOnly? ActivaDesde { get; set; }
    public DateOnly? ActivaHasta { get; set; }
    public bool Estado { get; set; } = true;
}

// ---------- Solicitudes ----------
public class CalculoDuracionRequest
{
    public Guid TipoPermisoId { get; set; }
    public DateTime FechaInicio { get; set; }
    public DateTime FechaFin { get; set; }
    public bool EsPorHoras { get; set; }
}

public class CalculoDuracionDto
{
    /// <summary>Duración en la unidad del saldo del tipo (días u horas).</summary>
    public decimal Cantidad { get; set; }
    public byte Unidad { get; set; }
    public bool ControlaSaldo { get; set; }
    public decimal? SaldoDisponible { get; set; }
    public decimal? SaldoRestante { get; set; }
    public DateTime FechaInicio { get; set; }
    public DateTime FechaFin { get; set; }
}

public class CrearSolicitudPermisoRequest
{
    public Guid TipoPermisoId { get; set; }
    public DateTime FechaInicio { get; set; }
    public DateTime FechaFin { get; set; }
    public bool EsPorHoras { get; set; }
    /// <summary>Motivo privado: solo lo ven el propio trabajador y quien tenga permisos.ver_motivo_privado.</summary>
    [StringLength(1000)] public string? Motivo { get; set; }
    /// <summary>Clave generada por el cliente; un reintento con la misma clave devuelve la solicitud ya creada.</summary>
    [StringLength(64)] public string? ClaveIdempotencia { get; set; }
}

public class ActualizarSolicitudPermisoRequest
{
    public Guid TipoPermisoId { get; set; }
    public DateTime FechaInicio { get; set; }
    public DateTime FechaFin { get; set; }
    public bool EsPorHoras { get; set; }
    [StringLength(1000)] public string? Motivo { get; set; }
    [Required] public string RowVersion { get; set; } = null!;
}

public class SolicitudPermisoDto
{
    public Guid SolicitudPermisoId { get; set; }
    public Guid TrabajadorId { get; set; }
    public string TrabajadorNombre { get; set; } = null!;
    public Guid TipoPermisoId { get; set; }
    public string TipoPermiso { get; set; } = null!;
    public byte Unidad { get; set; }
    public DateTime FechaInicio { get; set; }
    public DateTime FechaFin { get; set; }
    public bool EsPorHoras { get; set; }
    public decimal Cantidad { get; set; }
    /// <summary>Nulo si el usuario no es el propio trabajador ni tiene permiso para ver motivos privados.</summary>
    public string? Motivo { get; set; }
    public byte Estado { get; set; }
    public DateTime SolicitadaUtc { get; set; }
    public DateTime? ModificadaUtc { get; set; }
    public Guid? RevisorUsuarioId { get; set; }
    public DateTime? FechaDecisionUtc { get; set; }
    public string? ComentarioDecision { get; set; }
    /// <summary>Hay una solicitud de cancelación pendiente sobre este permiso aprobado.</summary>
    public bool CancelacionPendiente { get; set; }
    public string RowVersion { get; set; } = null!;
}

public class SolicitudPermisoFiltro : PageQuery
{
    public Guid? TrabajadorId { get; set; }
    public Guid? TipoPermisoId { get; set; }
    public byte? Estado { get; set; }
    public DateOnly? Desde { get; set; }
    public DateOnly? Hasta { get; set; }
}

public class ResolverPermisoRequest
{
    public bool Aprobar { get; set; }
    [StringLength(500)] public string? Comentario { get; set; }
}

// ---------- Cancelación de permisos aprobados ----------
public class SolicitarCancelacionRequest
{
    [StringLength(500)] public string? Motivo { get; set; }
}

public class SolicitudCancelacionDto
{
    public Guid SolicitudCancelacionId { get; set; }
    public Guid SolicitudPermisoId { get; set; }
    public Guid TrabajadorId { get; set; }
    public string TrabajadorNombre { get; set; } = null!;
    public string TipoPermiso { get; set; } = null!;
    public DateTime FechaInicio { get; set; }
    public DateTime FechaFin { get; set; }
    public decimal Cantidad { get; set; }
    public string? Motivo { get; set; }
    public byte Estado { get; set; }
    public DateTime SolicitadaUtc { get; set; }
    public Guid? RevisadaPorUsuarioId { get; set; }
    public DateTime? RevisadaUtc { get; set; }
    public string? ComentarioRevision { get; set; }
}

public class CancelacionPermisoFiltro : PageQuery
{
    public byte? Estado { get; set; }
}

// ---------- Saldos ----------
public class SaldoPermisoDto
{
    public Guid SaldoPermisoId { get; set; }
    public Guid TrabajadorId { get; set; }
    public string TrabajadorNombre { get; set; } = null!;
    public Guid TipoPermisoId { get; set; }
    public string TipoPermiso { get; set; } = null!;
    public byte Unidad { get; set; }
    public short PeriodoAnio { get; set; }
    public decimal Asignada { get; set; }
    public decimal Utilizada { get; set; }
    public decimal Reservada { get; set; }
    /// <summary>Asignada − Utilizada − Reservada.</summary>
    public decimal Disponible { get; set; }
}

public class MovimientoSaldoDto
{
    public long MovimientoSaldoPermisoId { get; set; }
    public Guid SaldoPermisoId { get; set; }
    public byte TipoMovimiento { get; set; }
    public decimal Cantidad { get; set; }
    public string? Descripcion { get; set; }
    public Guid? SolicitudPermisoId { get; set; }
    public Guid? RegistradoPorUsuarioId { get; set; }
    public DateTime FechaUtc { get; set; }
}

public class SaldoFiltro
{
    public Guid? TrabajadorId { get; set; }
    public short? Anio { get; set; }
}

public class AjustarSaldoRequest
{
    public Guid TrabajadorId { get; set; }
    public Guid TipoPermisoId { get; set; }
    [Range(2000, 2200)] public short PeriodoAnio { get; set; }
    /// <summary>Positivo suma a lo asignado; negativo resta. No puede ser cero.</summary>
    public decimal Cantidad { get; set; }
    [Required, StringLength(300, MinimumLength = 3)] public string Motivo { get; set; } = null!;
}

public class AusenciaCalendarioDto
{
    public Guid TrabajadorId { get; set; }
    public string TrabajadorNombre { get; set; } = null!;
    public DateTime FechaInicio { get; set; }
    public DateTime FechaFin { get; set; }
}
