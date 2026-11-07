using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegistroTrabajo.Api.Infrastructure;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Api.Controllers;

[ApiController, Authorize, Route("api/empresa")]
public class EmpresaController : ControllerBase
{
    private readonly IEmpresaService _svc;
    public EmpresaController(IEmpresaService svc) => _svc = svc;

    [HttpGet, RequierePermiso(Permisos.EmpresaConsultar)]
    public async Task<EmpresaDto> Obtener(CancellationToken ct) => await _svc.ObtenerAsync(ct);

    [HttpPut, RequierePermiso(Permisos.EmpresaConfigurar)]
    public async Task<EmpresaDto> Actualizar(ActualizarEmpresaRequest r, CancellationToken ct) => await _svc.ActualizarAsync(r, ct);

    [HttpGet("configuracion"), RequierePermiso(Permisos.EmpresaConsultar)]
    public async Task<ConfiguracionEmpresaDto> ObtenerConfiguracion(CancellationToken ct) => await _svc.ObtenerConfiguracionAsync(ct);

    [HttpPut("configuracion"), RequierePermiso(Permisos.EmpresaConfigurar)]
    public async Task<ConfiguracionEmpresaDto> ActualizarConfiguracion(ConfiguracionEmpresaDto r, CancellationToken ct) =>
        await _svc.ActualizarConfiguracionAsync(r, ct);
}
