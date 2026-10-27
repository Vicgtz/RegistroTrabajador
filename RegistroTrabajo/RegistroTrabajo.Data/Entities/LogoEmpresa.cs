namespace RegistroTrabajo.Data.Entities;

/// <summary>Bloque 12 (script 013): logotipo de la empresa (PNG, JPEG o WebP, máx. 512 KB), uno por empresa.</summary>
public class LogoEmpresa
{
    public Guid EmpresaId { get; set; }
    public byte[] Contenido { get; set; } = null!;
    public string TipoContenido { get; set; } = null!;
    public int TamanoBytes { get; set; }
    public string HashSha256 { get; set; } = null!;
    public DateTime FechaActualizacionUtc { get; set; }

    public Empresa Empresa { get; set; } = null!;
}
