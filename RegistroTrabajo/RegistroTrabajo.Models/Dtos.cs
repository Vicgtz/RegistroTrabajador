using System.ComponentModel.DataAnnotations;

namespace RegistroTrabajo.Models;

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
}

public class PageQuery
{
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 200)] public int PageSize { get; set; } = 50;
}

// ---------- Empresa ----------
public class EmpresaDto
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
}

public class ActualizarEmpresaRequest
{
    [Required, StringLength(160, MinimumLength = 2)] public string Nombre { get; set; } = null!;
    [StringLength(160)] public string? NombreComercial { get; set; }
    [StringLength(13, MinimumLength = 12)] public string? RFC { get; set; }
    [EmailAddress, StringLength(254)] public string? CorreoContacto { get; set; }
    [StringLength(30)] public string? TelefonoContacto { get; set; }
    [StringLength(500)] public string? LogoUrl { get; set; }
    [RegularExpression("^#[0-9A-Fa-f]{6}$")] public string? ColorPrimario { get; set; }
    [RegularExpression("^#[0-9A-Fa-f]{6}$")] public string? ColorSecundario { get; set; }
}

public class CrearEmpresaRequest : ActualizarEmpresaRequest { }

public class ConfiguracionEmpresaDto
{
    [Required, StringLength(100)] public string ZonaHorariaId { get; set; } = null!;
    [Required, StringLength(20)] public string Cultura { get; set; } = null!;
    [Required, StringLength(30)] public string FormatoFecha { get; set; } = null!;
    [Required, StringLength(20)] public string FormatoHora { get; set; } = null!;
    [Range(5, 1440)] public short CierreSesionInactividadMinutos { get; set; }
    [Range(1, 168)] public short DuracionSesionMaximaHoras { get; set; }
    public bool PermitirValidacionGps { get; set; }
}

// ---------- Usuarios ----------
public class UsuarioDto
{
    public Guid UsuarioId { get; set; }
    public string Correo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string Apellido { get; set; } = null!;
    public byte Estado { get; set; }
    public bool DebeCambiarContrasena { get; set; }
    public Guid? TrabajadorId { get; set; }
    public DateTime? UltimoAccesoUtc { get; set; }
    public DateTime? CorreoConfirmadoUtc { get; set; }
    public DateTime? BloqueadoHastaUtc { get; set; }
    public DateTime FechaCreacionUtc { get; set; }
    public DateTime? FechaActualizacionUtc { get; set; }
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
}

public class CrearUsuarioRequest
{
    [Required, EmailAddress, StringLength(254)] public string Correo { get; set; } = null!;
    [Required, StringLength(100, MinimumLength = 1)] public string Nombre { get; set; } = null!;
    [Required, StringLength(150, MinimumLength = 1)] public string Apellido { get; set; } = null!;
    [Required, StringLength(128, MinimumLength = 10)] public string Contrasena { get; set; } = null!;
    public bool DebeCambiarContrasena { get; set; } = true;
    public Guid? TrabajadorId { get; set; }
    public IReadOnlyList<Guid>? RolIds { get; set; }
}

public class ActualizarUsuarioRequest
{
    [Required, EmailAddress, StringLength(254)] public string Correo { get; set; } = null!;
    [Required, StringLength(100, MinimumLength = 1)] public string Nombre { get; set; } = null!;
    [Required, StringLength(150, MinimumLength = 1)] public string Apellido { get; set; } = null!;
    public bool DebeCambiarContrasena { get; set; }
    public Guid? TrabajadorId { get; set; }
}

public class UsuarioFiltro : PageQuery
{
    public string? Buscar { get; set; }
    [Range(1, 3)] public byte? Estado { get; set; }
}

// ---------- Roles y permisos ----------
public class RolDto
{
    public Guid RolId { get; set; }
    public string Nombre { get; set; } = null!;
    public string? Descripcion { get; set; }
    public bool EsSistema { get; set; }
    public bool Estado { get; set; }
}

public class PermisoDto
{
    public int PermisoId { get; set; }
    public string Codigo { get; set; } = null!;
    public string Modulo { get; set; } = null!;
    public string Accion { get; set; } = null!;
    public string? Descripcion { get; set; }
}

public class RolPermisosDto
{
    public Guid RolId { get; set; }
    public IReadOnlyList<string> Permisos { get; set; } = Array.Empty<string>();
}

public class ConfigurarPermisosRolRequest
{
    [Required] public IReadOnlyList<string> Permisos { get; set; } = Array.Empty<string>();
}

// ---------- Sesiones / accesos / auditoría ----------
public class SesionDto
{
    public Guid SesionId { get; set; }
    public Guid UsuarioId { get; set; }
    public string? UsuarioCorreo { get; set; }
    public bool EsActual { get; set; }
    public string? Dispositivo { get; set; }
    public string? UserAgent { get; set; }
    public string? DireccionIp { get; set; }
    public DateTime FechaCreacionUtc { get; set; }
    public DateTime UltimaActividadUtc { get; set; }
    public DateTime ExpiraUtc { get; set; }
    public DateTime? RevocadaUtc { get; set; }
}

public class HistorialAccesoDto
{
    public long HistorialAccesoId { get; set; }
    public Guid? UsuarioId { get; set; }
    public string? CorreoIntentado { get; set; }
    public DateTime FechaUtc { get; set; }
    public byte Resultado { get; set; }
    public string? DireccionIp { get; set; }
    public string? UserAgent { get; set; }
    public string? Detalle { get; set; }
}

public class AuditoriaDto
{
    public long AuditoriaId { get; set; }
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
}

public class FechaFiltro : PageQuery
{
    public DateTime? Desde { get; set; }
    public DateTime? Hasta { get; set; }
}

public class AuditoriaFiltro : FechaFiltro
{
    public string? Entidad { get; set; }
    public string? EntidadId { get; set; }
}

// ---------- Autenticación ----------
public class LoginRequest
{
    [Required, EmailAddress, StringLength(254)] public string Correo { get; set; } = null!;
    [Required, StringLength(128)] public string Contrasena { get; set; } = null!;
    [StringLength(200)] public string? Dispositivo { get; set; }
}

public class RefreshRequest
{
    [Required, StringLength(200)] public string RefreshToken { get; set; } = null!;
}

public class TokenResponse
{
    public string AccessToken { get; set; } = null!;
    public DateTime AccessTokenExpiraUtc { get; set; }
    public string RefreshToken { get; set; } = null!;
    public DateTime RefreshTokenExpiraUtc { get; set; }
    public bool DebeCambiarContrasena { get; set; }
}

public class CambiarContrasenaRequest
{
    [Required, StringLength(128)] public string ContrasenaActual { get; set; } = null!;
    [Required, StringLength(128, MinimumLength = 10)] public string ContrasenaNueva { get; set; } = null!;
}


public class PerfilDto
{
    public Guid UsuarioId { get; set; }
    public Guid EmpresaId { get; set; }
    public string EmpresaNombre { get; set; } = null!;
    public string Correo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string Apellido { get; set; } = null!;
    public bool DebeCambiarContrasena { get; set; }
    public bool CorreoConfirmado { get; set; }
    public bool EsPlataforma { get; set; }
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Permisos { get; set; } = Array.Empty<string>();
}

// ---------- Plataforma ----------
public class EmpresaPlataformaDto
{
    public Guid EmpresaId { get; set; }
    public string Nombre { get; set; } = null!;
    public string? NombreComercial { get; set; }
    public string? RFC { get; set; }
    public string? CorreoContacto { get; set; }
    public byte Estado { get; set; }
    public bool EsPlataforma { get; set; }
    public DateTime FechaCreacionUtc { get; set; }
    public int UsuariosTotal { get; set; }
}

public class CrearEmpresaPlataformaRequest
{
    [Required, StringLength(160, MinimumLength = 2)] public string Nombre { get; set; } = null!;
    [StringLength(160)] public string? NombreComercial { get; set; }
    [StringLength(13, MinimumLength = 12)] public string? RFC { get; set; }
    [EmailAddress, StringLength(254)] public string? CorreoContacto { get; set; }
    [Required, EmailAddress, StringLength(254)] public string AdminCorreo { get; set; } = null!;
    [Required, StringLength(100, MinimumLength = 1)] public string AdminNombre { get; set; } = null!;
    [Required, StringLength(150, MinimumLength = 1)] public string AdminApellido { get; set; } = null!;
    [Required, StringLength(128, MinimumLength = 10)] public string AdminContrasena { get; set; } = null!;
}

public class CambiarEstadoEmpresaRequest
{
    [Range(1, 3)] public byte Estado { get; set; }
}

// ---------- Roles propios ----------
public class CrearRolRequest
{
    [Required, StringLength(80, MinimumLength = 2)] public string Nombre { get; set; } = null!;
    [StringLength(250)] public string? Descripcion { get; set; }
}

public class ActualizarRolRequest
{
    [Required, StringLength(80, MinimumLength = 2)] public string Nombre { get; set; } = null!;
    [StringLength(250)] public string? Descripcion { get; set; }
}

public class CambiarEstadoRolRequest
{
    public bool Estado { get; set; }
}

// ---------- Contraseñas y correo ----------
public class RestablecerContrasenaUsuarioRequest
{
    [Required, StringLength(128, MinimumLength = 10)] public string ContrasenaNueva { get; set; } = null!;
}

public class OlvideContrasenaRequest
{
    [Required, EmailAddress, StringLength(254)] public string Correo { get; set; } = null!;
}

public class RestablecerContrasenaRequest
{
    [Required, StringLength(200)] public string Token { get; set; } = null!;
    [Required, StringLength(128, MinimumLength = 10)] public string ContrasenaNueva { get; set; } = null!;
}

public class ConfirmarCorreoRequest
{
    [Required, StringLength(200)] public string Token { get; set; } = null!;
}

public class ReenviarConfirmacionRequest
{
    [Required, EmailAddress, StringLength(254)] public string Correo { get; set; } = null!;
}

public class MensajeResponse
{
    public string Mensaje { get; set; } = null!;
}