namespace RegistroTrabajo.Data.Entities;

public class Empresa
{
    public Guid EmpresaId { get; set; }
    public string Nombre { get; set; } = null!;
    public string? NombreComercial { get; set; }
    public string? RFC { get; set; }
    public string? CorreoContacto { get; set; }
    public string? TelefonoContacto { get; set; }
    public string? LogoUrl { get; set; }
    public string? ColorPrimario { get; set; }
    public string? ColorSecundario { get; set; }
    public byte Estado { get; set; }
    public bool EsPlataforma { get; set; }
    public DateTime FechaCreacionUtc { get; set; }
    public DateTime? FechaActualizacionUtc { get; set; }

    public ConfiguracionEmpresa? Configuracion { get; set; }
    public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
    public ICollection<Rol> Roles { get; set; } = new List<Rol>();
}

public class ConfiguracionEmpresa
{
    public Guid EmpresaId { get; set; }
    public string ZonaHorariaId { get; set; } = null!;
    public string Cultura { get; set; } = null!;
    public string FormatoFecha { get; set; } = null!;
    public string FormatoHora { get; set; } = null!;
    public short CierreSesionInactividadMinutos { get; set; }
    public short DuracionSesionMaximaHoras { get; set; }
    public bool PermitirValidacionGps { get; set; }
    public DateTime FechaCreacionUtc { get; set; }
    public DateTime? FechaActualizacionUtc { get; set; }

    public Empresa Empresa { get; set; } = null!;
}

public class Usuario
{
    public Guid UsuarioId { get; set; }
    public Guid EmpresaId { get; set; }
    public Guid? TrabajadorId { get; set; }
    public string Correo { get; set; } = null!;
    public string CorreoNormalizado { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string Apellido { get; set; } = null!;
    public string HashContrasena { get; set; } = null!;
    public bool DebeCambiarContrasena { get; set; }
    public byte Estado { get; set; }
    public short IntentosFallidos { get; set; }
    public DateTime? BloqueadoHastaUtc { get; set; }
    public DateTime? UltimoAccesoUtc { get; set; }
    public DateTime? CorreoConfirmadoUtc { get; set; }
    public DateTime FechaCreacionUtc { get; set; }
    public DateTime? FechaActualizacionUtc { get; set; }
    public DateTime? FechaBajaUtc { get; set; }
    public byte[] RowVersion { get; set; } = null!;

    public Empresa Empresa { get; set; } = null!;
    public Trabajador? Trabajador { get; set; }
    public ICollection<UsuarioRol> UsuarioRoles { get; set; } = new List<UsuarioRol>();
    public ICollection<SesionUsuario> Sesiones { get; set; } = new List<SesionUsuario>();
}

public class Rol
{
    public Guid RolId { get; set; }
    public Guid EmpresaId { get; set; }
    public string Nombre { get; set; } = null!;
    public string? Descripcion { get; set; }
    public bool EsSistema { get; set; }
    public bool Estado { get; set; }
    public DateTime FechaCreacionUtc { get; set; }

    public Empresa Empresa { get; set; } = null!;
    public ICollection<UsuarioRol> UsuarioRoles { get; set; } = new List<UsuarioRol>();
    public ICollection<RolPermiso> RolPermisos { get; set; } = new List<RolPermiso>();
}

public class PermisoSistema
{
    public int PermisoId { get; set; }
    public string Codigo { get; set; } = null!;
    public string Modulo { get; set; } = null!;
    public string Accion { get; set; } = null!;
    public string? Descripcion { get; set; }

    public ICollection<RolPermiso> RolPermisos { get; set; } = new List<RolPermiso>();
}

public class UsuarioRol
{
    public Guid EmpresaId { get; set; }
    public Guid UsuarioId { get; set; }
    public Guid RolId { get; set; }
    public DateTime AsignadoUtc { get; set; }
    public Guid? AsignadoPorUsuarioId { get; set; }

    public Usuario Usuario { get; set; } = null!;
    public Rol Rol { get; set; } = null!;
    public Usuario? AsignadoPor { get; set; }
}

public class RolPermiso
{
    public Guid EmpresaId { get; set; }
    public Guid RolId { get; set; }
    public int PermisoId { get; set; }
    public bool Permitido { get; set; }
    public DateTime FechaActualizacionUtc { get; set; }
    public Guid? ActualizadoPorUsuarioId { get; set; }

    public Rol Rol { get; set; } = null!;
    public PermisoSistema Permiso { get; set; } = null!;
    public Usuario? ActualizadoPor { get; set; }
}

public class SesionUsuario
{
    public Guid SesionId { get; set; }
    public Guid EmpresaId { get; set; }
    public Guid UsuarioId { get; set; }
    public byte[] TokenRefreshHash { get; set; } = null!;
    public string? Dispositivo { get; set; }
    public string? UserAgent { get; set; }
    public string? DireccionIp { get; set; }
    public DateTime FechaCreacionUtc { get; set; }
    public DateTime UltimaActividadUtc { get; set; }
    public DateTime ExpiraUtc { get; set; }
    public DateTime? RevocadaUtc { get; set; }
    public string? MotivoRevocacion { get; set; }

    public Usuario Usuario { get; set; } = null!;
}

public class HistorialAcceso
{
    public long HistorialAccesoId { get; set; }
    public Guid? EmpresaId { get; set; }
    public Guid? UsuarioId { get; set; }
    public string? CorreoIntentado { get; set; }
    public DateTime FechaUtc { get; set; }
    public byte Resultado { get; set; }
    public string? DireccionIp { get; set; }
    public string? UserAgent { get; set; }
    public string? Detalle { get; set; }

    public Empresa? Empresa { get; set; }
    public Usuario? Usuario { get; set; }
}

public class Auditoria
{
    public long AuditoriaId { get; set; }
    public Guid? EmpresaId { get; set; }
    public Guid? UsuarioId { get; set; }
    public DateTime FechaUtc { get; set; }
    public string Modulo { get; set; } = null!;
    public string Entidad { get; set; } = null!;
    public string? EntidadId { get; set; }
    public string Accion { get; set; } = null!;
    public string? DatosAntesJson { get; set; }
    public string? DatosDespuesJson { get; set; }
    public string? DireccionIp { get; set; }
    public Guid? CorrelationId { get; set; }

    public Empresa? Empresa { get; set; }
    public Usuario? Usuario { get; set; }
}
