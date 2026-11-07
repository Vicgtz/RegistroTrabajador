using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RegistroTrabajo.Data;
using RegistroTrabajo.Data.Entities;
using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

/// <summary>Administración de la clave de registro de la empresa (requiere empresa.configurar; la empresa sale del token).</summary>
public interface IClaveRegistroService
{
    Task<ClaveRegistroEstadoDto> EstadoAsync(CancellationToken ct);
    /// <summary>Genera una clave nueva (reemplaza la anterior) y la devuelve en claro por única vez.</summary>
    Task<ClaveRegistroGeneradaDto> GenerarAsync(CancellationToken ct);
    /// <summary>Desactiva el registro: ninguna clave vuelve a ser válida hasta generar una nueva.</summary>
    Task DesactivarAsync(CancellationToken ct);
}

public class ClaveRegistroService : IClaveRegistroService
{
    private readonly RegistroTrabajoDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IPermisoService _permisos;
    private readonly IAuditoriaService _auditoria;
    private readonly TimeProvider _clock;

    public ClaveRegistroService(RegistroTrabajoDbContext db, ICurrentUser current, IPermisoService permisos, IAuditoriaService auditoria, TimeProvider clock)
    {
        _db = db; _current = current; _permisos = permisos; _auditoria = auditoria; _clock = clock;
    }

    public async Task<ClaveRegistroEstadoDto> EstadoAsync(CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.EmpresaConfigurar, ct);
        var empresaId = _current.RequireEmpresaId();
        var c = await _db.ConfiguracionEmpresa.AsNoTracking().FirstOrDefaultAsync(x => x.EmpresaId == empresaId, ct)
            ?? throw ServiceException.NotFound("Configuración de la empresa no encontrada.");
        return new ClaveRegistroEstadoDto { Activa = c.ClaveRegistroHash is not null, GeneradaUtc = c.ClaveRegistroGeneradaUtc };
    }

    public async Task<ClaveRegistroGeneradaDto> GenerarAsync(CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.EmpresaConfigurar, ct);
        var empresaId = _current.RequireEmpresaId();
        var c = await _db.ConfiguracionEmpresa.FirstOrDefaultAsync(x => x.EmpresaId == empresaId, ct)
            ?? throw ServiceException.NotFound("Configuración de la empresa no encontrada.");

        var clave = ClaveRegistroHelper.Nueva();
        var now = _clock.GetUtcNow().UtcDateTime;
        var estabaActiva = c.ClaveRegistroHash is not null;
        c.ClaveRegistroHash = ClaveRegistroHelper.Hash(clave);
        c.ClaveRegistroGeneradaUtc = now;
        c.FechaActualizacionUtc = now;
        // Nunca se audita la clave ni su hash.
        _auditoria.Registrar("Empresa", nameof(ConfiguracionEmpresa), empresaId.ToString(), "GenerarClaveRegistro",
            new { activa = estabaActiva }, new { activa = true });
        await _db.SaveSafeAsync(ct);
        return new ClaveRegistroGeneradaDto { Clave = clave, GeneradaUtc = now };
    }

    public async Task DesactivarAsync(CancellationToken ct)
    {
        await _permisos.AsegurarPermisoAsync(Permisos.EmpresaConfigurar, ct);
        var empresaId = _current.RequireEmpresaId();
        var c = await _db.ConfiguracionEmpresa.FirstOrDefaultAsync(x => x.EmpresaId == empresaId, ct)
            ?? throw ServiceException.NotFound("Configuración de la empresa no encontrada.");
        if (c.ClaveRegistroHash is null) return;

        c.ClaveRegistroHash = null;
        c.ClaveRegistroGeneradaUtc = null;
        c.FechaActualizacionUtc = _clock.GetUtcNow().UtcDateTime;
        _auditoria.Registrar("Empresa", nameof(ConfiguracionEmpresa), empresaId.ToString(), "DesactivarClaveRegistro",
            new { activa = true }, new { activa = false });
        await _db.SaveSafeAsync(ct);
    }
}
