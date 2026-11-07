using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegistroTrabajo.Api.Infrastructure;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Api.Controllers;

[ApiController, Authorize, Route("api/errores-tecnicos")]
public class ErroresTecnicosController : ControllerBase
{
    private readonly IErrorTecnicoService _svc;
    public ErroresTecnicosController(IErrorTecnicoService svc) => _svc = svc;

    [HttpGet, RequierePermiso(Permisos.ErroresConsultar)]
    public async Task<PagedResult<ErrorTecnicoDto>> Listar([FromQuery] ErrorTecnicoFiltro f, CancellationToken ct) => await _svc.ListarAsync(f, ct);
}
