namespace RegistroTrabajo.Data.Entities;

/// <summary>Token de un solo uso para confirmar el correo de un usuario. Solo se guarda el hash.</summary>
public class TokenConfirmacionCorreo
{
    public Guid TokenConfirmacionId { get; set; }
    public Guid EmpresaId { get; set; }
    public Guid UsuarioId { get; set; }
    public byte[] TokenHash { get; set; } = null!;
    public DateTime CreadoUtc { get; set; }
    public DateTime ExpiraUtc { get; set; }
    public DateTime? UsadoUtc { get; set; }
    public DateTime? RevocadoUtc { get; set; }

    public Usuario Usuario { get; set; } = null!;
}
