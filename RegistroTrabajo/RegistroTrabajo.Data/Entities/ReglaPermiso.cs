
namespace RegistroTrabajo.Data.Entities;

public class ReglaPermiso
{
    public Guid ReglaPermisoId { get; set; }

    public Guid EmpresaId { get; set; }

    public Guid TipoPermisoId { get; set; }

    public int? AntiguedadMinimaMeses { get; set; }

    public decimal? CantidadAnual { get; set; }

    public decimal? LimitePorSolicitud { get; set; }

    public bool PermiteSaldoNegativo { get; set; }

    public bool ReiniciaAnualmente { get; set; }

    public DateOnly? ActivaDesde { get; set; }

    public DateOnly? ActivaHasta { get; set; }

    public bool Estado { get; set; }

    public TipoPermiso TipoPermiso { get; set; } = null!;
}
