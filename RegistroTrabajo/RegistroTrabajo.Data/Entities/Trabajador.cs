
namespace RegistroTrabajo.Data.Entities;

public class Trabajador
{
    public Guid TrabajadorId { get; set; }

    public Guid EmpresaId { get; set; }

    public Guid? UsuarioId { get; set; }

    public string? NumeroEmpleado { get; set; }

    public string Nombres { get; set; } = null!;

    public string Apellidos { get; set; } = null!;

    public DateOnly? FechaNacimiento { get; set; }

    public string? CorreoContacto { get; set; }

    public string? Telefono { get; set; }

    public string? Direccion { get; set; }

    public Guid? DepartamentoId { get; set; }

    public Guid? PuestoId { get; set; }

    public DateOnly FechaIngreso { get; set; }

    public DateOnly? FechaBaja { get; set; }

    public string? MotivoBaja { get; set; }

    public byte EstadoLaboral { get; set; }

    public DateTime FechaCreacionUtc { get; set; }

    public DateTime? FechaActualizacionUtc { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public ICollection<AsignacionHorario> AsignacionesHorario { get; set; } = new List<AsignacionHorario>();

    public ICollection<AsignacionProyecto> AsignacionesProyecto { get; set; } = new List<AsignacionProyecto>();

    public ICollection<AsignacionTarea> AsignacionesTarea { get; set; } = new List<AsignacionTarea>();

    public ICollection<ContactoEmergencia> ContactosEmergencia { get; set; } = new List<ContactoEmergencia>();

    public Departamento? Departamento { get; set; }

    public ICollection<DescansoTrabajador> DescansosTrabajador { get; set; } = new List<DescansoTrabajador>();

    public Empresa Empresa { get; set; } = null!;

    public ICollection<HistorialLaboral> HistorialLaboral { get; set; } = new List<HistorialLaboral>();

    public ICollection<Proyecto> Proyectos { get; set; } = new List<Proyecto>();

    public Puesto? Puesto { get; set; }

    public ICollection<RegistroAsistencia> RegistrosAsistencia { get; set; } = new List<RegistroAsistencia>();

    public ICollection<SaldoPermiso> SaldosPermiso { get; set; } = new List<SaldoPermiso>();

    public ICollection<SolicitudCambioPerfil> SolicitudesCambioPerfil { get; set; } = new List<SolicitudCambioPerfil>();

    public ICollection<SolicitudPermiso> SolicitudesPermiso { get; set; } = new List<SolicitudPermiso>();

    public ICollection<Tarea> Tareas { get; set; } = new List<Tarea>();

    public ICollection<TrabajadorSupervisor> SupervisionesComoSupervisor { get; set; } = new List<TrabajadorSupervisor>();

    public ICollection<TrabajadorSupervisor> SupervisionesComoSupervisado { get; set; } = new List<TrabajadorSupervisor>();

    public Usuario? Usuario { get; set; }

    public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
}
