using System.ComponentModel.DataAnnotations;

namespace RegistroTrabajo.Models;

/// <summary>Significado de <c>OperacionRespaldo.TipoOperacion</c> (la BD solo limita el rango 1..4).</summary>
public static class TipoOperacionRespaldo
{
    public const byte Completo = 1;
    public const byte Diferencial = 2;
    public const byte Restauracion = 3;
    public const byte Verificacion = 4;

    public static string Nombre(byte tipo) => tipo switch
    {
        Completo => "Respaldo completo",
        Diferencial => "Respaldo diferencial",
        Restauracion => "Restauración",
        Verificacion => "Verificación",
        _ => "Desconocido"
    };
}

/// <summary>Significado de <c>OperacionRespaldo.Estado</c> (la BD solo limita el rango 1..3).</summary>
public static class EstadoOperacionRespaldo
{
    public const byte Iniciado = 1;
    public const byte Completado = 2;
    public const byte Fallido = 3;

    public static string Nombre(byte estado) => estado switch
    {
        Iniciado => "Iniciado",
        Completado => "Completado",
        Fallido => "Fallido",
        _ => "Desconocido"
    };
}

public class SolicitarRespaldoRequest
{
    /// <summary>1 Completo (predeterminado) o 2 Diferencial.</summary>
    [Range(1, 2)] public byte Tipo { get; set; } = TipoOperacionRespaldo.Completo;
}

public class RespaldoFiltro : PageQuery
{
    public byte? Tipo { get; set; }
    public byte? Estado { get; set; }
    public DateOnly? Desde { get; set; }
    public DateOnly? Hasta { get; set; }
}

/// <summary>Registro del historial. No incluye la ruta completa del servidor: solo el nombre del archivo.</summary>
public class RespaldoDto
{
    public Guid OperacionRespaldoId { get; set; }
    public byte Tipo { get; set; }
    public string TipoNombre { get; set; } = null!;
    public byte Estado { get; set; }
    public string EstadoNombre { get; set; } = null!;
    public Guid UsuarioId { get; set; }
    public string? UsuarioNombre { get; set; }
    public DateTime FechaInicioUtc { get; set; }
    public DateTime? FechaFinUtc { get; set; }
    public string? NombreArchivo { get; set; }
    public long? TamanoBytes { get; set; }
    public string? HashArchivo { get; set; }
    public string? Mensaje { get; set; }
}
