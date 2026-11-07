using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegistroTrabajo.Api.Infrastructure;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Api.Controllers;

// Bloque 10. La empresa y el usuario salen siempre del token. El dashboard y los reportes se acotan al alcance de cada usuario;
// los indicadores no abren registros: al consultar un elemento, su módulo vuelve a validar el acceso.

[ApiController, Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _svc;
    public DashboardController(IDashboardService svc) => _svc = svc;

    [HttpGet("api/dashboard")]
    public async Task<DashboardDto> Obtener(CancellationToken ct) => await _svc.ObtenerAsync(ct);
}
