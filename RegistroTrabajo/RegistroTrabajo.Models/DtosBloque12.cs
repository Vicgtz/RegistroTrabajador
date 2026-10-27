namespace RegistroTrabajo.Models;

/// <summary>Valores permitidos para la configuración regional (lista cerrada: evita formatos arbitrarios).</summary>
public static class FormatosRegionales
{
    public static readonly string[] Fecha = { "dd/MM/yyyy", "MM/dd/yyyy", "yyyy-MM-dd", "dd-MM-yyyy" };
    public static readonly string[] Hora = { "HH:mm", "hh:mm tt", "h:mm tt" };

    public const int MaxBytesLogo = 512 * 1024;
    public static readonly string[] TiposLogo = { "image/png", "image/jpeg", "image/webp" };
}

/// <summary>Datos visuales y regionales de la empresa del usuario. Los puede leer cualquier usuario autenticado: no contiene datos sensibles.</summary>
public class AparienciaEmpresaDto
{
    public string Nombre { get; set; } = null!;
    public string? ColorPrimario { get; set; }
    public string? ColorSecundario { get; set; }
    public bool TieneLogo { get; set; }
    /// <summary>Cambia cuando se sustituye el logotipo (sirve para romper la caché del navegador).</summary>
    public string? VersionLogo { get; set; }
    public string? TextoInstitucional { get; set; }
    public string ZonaHorariaId { get; set; } = null!;
    public string Cultura { get; set; } = null!;
    public string FormatoFecha { get; set; } = null!;
    public string FormatoHora { get; set; } = null!;
    public byte PrimerDiaSemana { get; set; }
}

public class PermisosEfectivosDto
{
    public Guid UsuarioId { get; set; }
    public string Usuario { get; set; } = null!;
    public byte Estado { get; set; }
    public List<string> Roles { get; set; } = new();
    public List<PermisoEfectivoDto> Permisos { get; set; } = new();
}

public class PermisoEfectivoDto
{
    public string Codigo { get; set; } = null!;
    public string Modulo { get; set; } = null!;
    public string? Descripcion { get; set; }
}

/// <summary>Estado técnico básico. Nunca incluye cadenas de conexión, rutas internas ni credenciales.</summary>
public class EstadoSistemaDto
{
    public string Version { get; set; } = null!;
    public string Entorno { get; set; } = null!;
    public DateTime FechaUtc { get; set; }
    public bool BaseDatosDisponible { get; set; }
    public DateTime? UltimoRespaldoCompletadoUtc { get; set; }
    public int ErroresTecnicosUltimas24Horas { get; set; }
}
