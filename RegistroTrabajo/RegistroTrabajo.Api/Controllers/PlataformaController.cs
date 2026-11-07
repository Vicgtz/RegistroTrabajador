using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegistroTrabajo.Api.Infrastructure;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Api.Controllers;

/// <summary>Administración de empresas: solo para usuarios de la empresa de plataforma.</summary>
[ApiController, Authorize, Route("api/plataforma/empresas")]
public class PlataformaController : ControllerBase
{
    private readonly IPlataformaService _svc;
    public PlataformaController(IPlataformaService svc) => _svc = svc;

    [HttpGet, RequierePermiso(Permisos.PlataformaEmpresasConsultar)]
    public async Task<PagedResult<EmpresaPlataformaDto>> Listar([FromQuery] PageQuery q, [FromQuery] string? buscar, CancellationToken ct) =>
        await _svc.ListarEmpresasAsync(q, buscar, ct);

    [HttpGet("{id:guid}"), RequierePermiso(Permisos.PlataformaEmpresasConsultar)]
    public async Task<EmpresaPlataformaDto> Obtener(Guid id, CancellationToken ct) => await _svc.ObtenerEmpresaAsync(id, ct);

    [HttpPost, RequierePermiso(Permisos.PlataformaEmpresasCrear)]
    public async Task<IActionResult> Crear(CrearEmpresaPlataformaRequest r, CancellationToken ct)
    {
        var e = await _svc.CrearEmpresaAsync(r, ct);
        return CreatedAtAction(nameof(Obtener), new { id = e.EmpresaId }, e);
    }

    [HttpPost("{id:guid}/estado"), RequierePermiso(Permisos.PlataformaEmpresasCambiarEstado)]
    public async Task<EmpresaPlataformaDto> CambiarEstado(Guid id, CambiarEstadoEmpresaRequest r, CancellationToken ct) =>
        await _svc.CambiarEstadoAsync(id, r.Estado, ct);
}
