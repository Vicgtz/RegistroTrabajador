using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegistroTrabajo.Api.Infrastructure;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Api.Controllers;

[ApiController, Authorize, Route("api/usuarios")]
public class PermisosEfectivosController : ControllerBase
{
    private readonly IPermisosEfectivosService _svc;
    public PermisosEfectivosController(IPermisosEfectivosService svc) => _svc = svc;

    [HttpGet("{id:guid}/permisos-efectivos"), RequierePermiso(Permisos.EmpresaVerPermisosEfectivos)]
    public async Task<PermisosEfectivosDto> Obtener(Guid id, CancellationToken ct) => await _svc.ObtenerAsync(id, ct);
}
