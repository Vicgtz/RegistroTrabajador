using System.Reflection;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using RegistroTrabajo.Data;
using RegistroTrabajo.Data.Entities;
using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

public interface IAparienciaService
{
    Task<AparienciaEmpresaDto> ObtenerAsync(CancellationToken ct);
}

public class AparienciaService : IAparienciaService
{
    private readonly RegistroTrabajoDbContext _db;
    private readonly ICurrentUser _current;

    public AparienciaService(RegistroTrabajoDbContext db, ICurrentUser current) { _db = db; _current = current; }

    public async Task<AparienciaEmpresaDto> ObtenerAsync(CancellationToken ct)
    {
        var id = _current.RequireEmpresaId();
        var e = await _db.Empresas.AsNoTracking().Include(x => x.Configuracion).FirstOrDefaultAsync(x => x.EmpresaId == id, ct)
            ?? throw ServiceException.NotFound("Empresa no encontrada.");
        var c = e.Configuracion ?? throw ServiceException.NotFound("Configuración no encontrada.");
        var hash = await _db.LogosEmpresa.AsNoTracking().Where(l => l.EmpresaId == id).Select(l => l.HashSha256).FirstOrDefaultAsync(ct);
        return new AparienciaEmpresaDto
        {
            Nombre = c.NombreMostrado ?? e.NombreComercial ?? e.Nombre,
            ColorPrimario = e.ColorPrimario, ColorSecundario = e.ColorSecundario,
            TieneLogo = hash is not null, VersionLogo = hash?[..12], TextoInstitucional = c.TextoInstitucional,
            ZonaHorariaId = c.ZonaHorariaId, Cultura = c.Cultura, FormatoFecha = c.FormatoFecha, FormatoHora = c.FormatoHora,
            PrimerDiaSemana = c.PrimerDiaSemana
        };
    }
}
