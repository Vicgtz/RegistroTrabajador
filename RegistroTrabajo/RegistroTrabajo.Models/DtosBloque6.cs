using System.ComponentModel.DataAnnotations;

namespace RegistroTrabajo.Models;

// ---------- Eventos de la empresa ----------
// Las fechas y horas de estos DTO son SIEMPRE la hora local de la empresa (sin zona): el servidor convierte desde/hacia UTC
// con la zona horaria configurada, de modo que el navegador no desplaza los eventos de día completo.

public class EventoEmpresaDto
{
    public Guid EventoEmpresaId { get; set; }
    public string Titulo { get; set; } = null!;
    public string? Descripcion { get; set; }
    /// <summary><see cref="TipoEventoEmpresa"/>: 1..5.</summary>
    public byte Tipo { get; set; }
    public bool TodoElDia { get; set; }
    /// <summary>Hora local de la empresa. En eventos de día completo solo importa la fecha.</summary>
    public DateTime FechaInicio { get; set; }
    /// <summary>Hora local de la empresa; en día completo, último día inclusive. Nulo = sin hora de fin.</summary>
    public DateTime? FechaFin { get; set; }
    /// <summary><see cref="EstadoEventoEmpresa"/>.</summary>
    public byte Estado { get; set; }
    /// <summary><see cref="VisibilidadEvento"/>.</summary>
    public byte Visibilidad { get; set; }
    public Guid? DepartamentoId { get; set; }
    public string? Departamento { get; set; }
    public string CreadoPor { get; set; } = null!;
    public DateTime FechaCreacionUtc { get; set; }
    public DateTime? FechaModificacionUtc { get; set; }
    public string? ModificadoPor { get; set; }
    public DateTime? FechaCancelacionUtc { get; set; }
    public string? CanceladoPor { get; set; }
    public string? MotivoCancelacion { get; set; }
    public string RowVersion { get; set; } = null!;
}

public class GuardarEventoEmpresaRequest : IValidatableObject
{
    [Required, StringLength(180, MinimumLength = 2)] public string Titulo { get; set; } = null!;
    [StringLength(1000)] public string? Descripcion { get; set; }
    [Range(1, 5)] public byte Tipo { get; set; } = TipoEventoEmpresa.ReunionGeneral;
    public bool TodoElDia { get; set; } = true;
    public DateTime FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
    [Range(1, 2)] public byte Visibilidad { get; set; } = VisibilidadEvento.Empresa;
    /// <summary>Obligatorio con visibilidad por departamento; debe ser nulo para toda la empresa.</summary>
    public Guid? DepartamentoId { get; set; }
    /// <summary>Solo al editar: versión que el cliente vio (detecta ediciones concurrentes).</summary>
    public string? RowVersion { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (Visibilidad == VisibilidadEvento.Departamento && DepartamentoId is null)
            yield return new ValidationResult("Seleccione el departamento que puede ver el evento.", new[] { nameof(DepartamentoId) });
    }
}

public class CancelarEventoEmpresaRequest
{
    [Required, StringLength(300, MinimumLength = 3)] public string Motivo { get; set; } = null!;
    [Required] public string RowVersion { get; set; } = null!;
}

public class EventoEmpresaFiltro : PageQuery
{
    public DateOnly? Desde { get; set; }
    public DateOnly? Hasta { get; set; }
    public byte? Tipo { get; set; }
    /// <summary>Nulo = activos y cancelados; 1 = solo activos; 2 = solo cancelados.</summary>
    public byte? Estado { get; set; }
}

// ---------- Calendario unificado ----------
/// <summary>
/// Un elemento del calendario, sea cual sea su origen (evento, festivo, ausencia aprobada, cumpleaños o aniversario).
/// Las fechas son locales de la empresa; un elemento de varios días aparece una sola vez con su periodo completo.
/// </summary>
public class CalendarioItemDto
{
    /// <summary>Clave estable dentro de la respuesta, p. ej. "evento:{guid}" o "festivo:{guid}:{fecha}".</summary>
    public string Clave { get; set; } = null!;
    /// <summary><see cref="CategoriaCalendario"/>.</summary>
    public byte Categoria { get; set; }
    public string Titulo { get; set; } = null!;
    public string? Descripcion { get; set; }
    /// <summary>Tipo de evento (<see cref="TipoEventoEmpresa"/>) cuando la categoría es Evento.</summary>
    public byte? TipoEvento { get; set; }
    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }
    public bool TodoElDia { get; set; }
    public TimeOnly? HoraInicio { get; set; }
    public TimeOnly? HoraFin { get; set; }
    public bool Cancelado { get; set; }
    /// <summary>Con valor solo si el usuario puede abrir el recurso (evento visible, permiso dentro de su alcance).</summary>
    public Guid? RecursoId { get; set; }
    public Guid? TrabajadorId { get; set; }
    /// <summary>Años cumplidos (solo aniversarios laborales).</summary>
    public int? Anios { get; set; }
}

public class CalendarioDto
{
    public DateOnly Desde { get; set; }
    public DateOnly Hasta { get; set; }
    public string ZonaHoraria { get; set; } = null!;
    public bool MuestraCumpleanos { get; set; }
    public bool MuestraAniversarios { get; set; }
    public bool PuedeGestionarEventos { get; set; }
    public List<CalendarioItemDto> Items { get; set; } = new();
}

public class ConfiguracionCalendarioDto
{
    public bool MostrarCumpleanos { get; set; }
    public bool MostrarAniversarios { get; set; }
}
