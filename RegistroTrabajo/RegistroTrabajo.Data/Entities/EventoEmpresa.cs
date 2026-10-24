
namespace RegistroTrabajo.Data.Entities;

public class EventoEmpresa
{
    public Guid EventoEmpresaId { get; set; }

    public Guid EmpresaId { get; set; }

    public string Titulo { get; set; } = null!;

    public string? Descripcion { get; set; }

    public DateTime FechaInicio { get; set; }

    public DateTime? FechaFin { get; set; }

    public bool TodoElDia { get; set; }

    public byte Tipo { get; set; }

    public Guid CreadoPorUsuarioId { get; set; }

    public DateTime FechaCreacionUtc { get; set; }

    public Empresa Empresa { get; set; } = null!;

    public Usuario Usuario { get; set; } = null!;
}
