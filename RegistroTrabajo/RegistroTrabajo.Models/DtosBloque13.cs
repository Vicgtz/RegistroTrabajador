using System.ComponentModel.DataAnnotations;

namespace RegistroTrabajo.Models;

/// <summary>Registro público de una cuenta de trabajador con la clave de registro de la empresa (Bloque 13).</summary>
public class RegistroConClaveRequest
{
    [Required, StringLength(64, MinimumLength = 8)] public string Clave { get; set; } = null!;
    [Required, EmailAddress, StringLength(254)] public string Correo { get; set; } = null!;
    [Required, StringLength(100, MinimumLength = 1)] public string Nombre { get; set; } = null!;
    [Required, StringLength(150, MinimumLength = 1)] public string Apellido { get; set; } = null!;
    [Required, StringLength(128, MinimumLength = 10)] public string Contrasena { get; set; } = null!;
}

public class ClaveRegistroEstadoDto
{
    public bool Activa { get; set; }
    public DateTime? GeneradaUtc { get; set; }
}

/// <summary>La clave en claro solo se devuelve al generarla; después únicamente se conoce su hash.</summary>
public class ClaveRegistroGeneradaDto
{
    public string Clave { get; set; } = null!;
    public DateTime GeneradaUtc { get; set; }
}
