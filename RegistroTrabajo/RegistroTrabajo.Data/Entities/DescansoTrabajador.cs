
namespace RegistroTrabajo.Data.Entities;

public class DescansoTrabajador
{
    public Guid DescansoTrabajadorId { get; set; }

    public Guid EmpresaId { get; set; }

    public Guid TrabajadorId { get; set; }

    public DateOnly FechaDesde { get; set; }

    public DateOnly FechaHasta { get; set; }

    public byte Tipo { get; set; }

    public string? Descripcion { get; set; }

    public Guid? RegistradoPorUsuarioId { get; set; }

    public Trabajador Trabajador { get; set; } = null!;

    public Usuario? Usuario { get; set; }
}
