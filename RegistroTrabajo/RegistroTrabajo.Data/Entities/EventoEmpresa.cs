
namespace RegistroTrabajo.Data.Entities;

public class EventoEmpresa
{
    public Guid EventoEmpresaId { get; set; }

    public Guid EmpresaId { get; set; }

    public string Titulo { get; set; } = null!;

    public string? Descripcion { get; set; }

    /// <summary>Evento de día completo: fecha local de la empresa a las 00:00 (sin zona). Con hora: instante UTC.</summary>
    public DateTime FechaInicio { get; set; }

    /// <summary>Día completo: último día (inclusivo), también como fecha local. Con hora: instante UTC (puede ser nulo).</summary>
    public DateTime? FechaFin { get; set; }

    public bool TodoElDia { get; set; }

    public byte Tipo { get; set; }

    public Guid CreadoPorUsuarioId { get; set; }

    public DateTime FechaCreacionUtc { get; set; }

    /// <summary>1 activo, 2 cancelado (script 007).</summary>
    public byte Estado { get; set; }

    /// <summary>1 toda la empresa, 2 un departamento (script 007).</summary>
    public byte Visibilidad { get; set; }

    public Guid? DepartamentoId { get; set; }

    public Guid? ModificadoPorUsuarioId { get; set; }

    public DateTime? FechaModificacionUtc { get; set; }

    public Guid? CanceladoPorUsuarioId { get; set; }

    public DateTime? FechaCancelacionUtc { get; set; }

    public string? MotivoCancelacion { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public Empresa Empresa { get; set; } = null!;

    public Usuario Usuario { get; set; } = null!;

    public Departamento? Departamento { get; set; }
}
