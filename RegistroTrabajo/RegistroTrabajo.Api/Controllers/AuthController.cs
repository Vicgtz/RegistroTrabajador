using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RegistroTrabajo.Models;
using RegistroTrabajo.Services;

namespace RegistroTrabajo.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly ICuentaService _cuentas;

    public AuthController(IAuthService auth, ICuentaService cuentas)
    {
        _auth = auth; _cuentas = cuentas;
    }

    [HttpPost("login"), AllowAnonymous, EnableRateLimiting("login")]
    public async Task<ActionResult<TokenResponse>> Login(LoginRequest request, CancellationToken ct) =>
        await _auth.LoginAsync(request, ct);

    [HttpPost("refresh"), AllowAnonymous, EnableRateLimiting("login")]
    public async Task<ActionResult<TokenResponse>> Refresh(RefreshRequest request, CancellationToken ct) =>
        await _auth.RefreshAsync(request, ct);

    [HttpPost("logout"), Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await _auth.LogoutAsync(ct);
        return NoContent();
    }

    [HttpGet("me"), Authorize]
    public async Task<PerfilDto> Perfil(CancellationToken ct) => await _auth.ObtenerPerfilAsync(ct);

    [HttpPost("cambiar-contrasena"), Authorize]
    public async Task<IActionResult> CambiarContrasena(CambiarContrasenaRequest request, CancellationToken ct)
    {
        await _auth.CambiarContrasenaAsync(request, ct);
        return NoContent();
    }

    // ---- Recuperación de contraseña y confirmación de correo (públicos; respuestas que no revelan si la cuenta existe) ----
    [HttpPost("olvide-contrasena"), AllowAnonymous, EnableRateLimiting("login")]
    public async Task<IActionResult> OlvideContrasena(OlvideContrasenaRequest request, CancellationToken ct)
    {
        await _cuentas.SolicitarRecuperacionAsync(request.Correo, ct);
        return Accepted(new MensajeResponse { Mensaje = "Si la cuenta cumple las condiciones, enviaremos las instrucciones a tu correo. El enlace tiene vigencia limitada." });
    }

    [HttpPost("restablecer-contrasena"), AllowAnonymous, EnableRateLimiting("login")]
    public async Task<IActionResult> RestablecerContrasena(RestablecerContrasenaRequest request, CancellationToken ct)
    {
        await _cuentas.RestablecerContrasenaAsync(request.Token, request.ContrasenaNueva, ct);
        return NoContent();
    }

    [HttpPost("confirmar-correo"), AllowAnonymous, EnableRateLimiting("login")]
    public async Task<IActionResult> ConfirmarCorreo(ConfirmarCorreoRequest request, CancellationToken ct)
    {
        await _cuentas.ConfirmarCorreoAsync(request.Token, ct);
        return NoContent();
    }

    [HttpPost("reenviar-confirmacion"), AllowAnonymous, EnableRateLimiting("login")]
    public async Task<IActionResult> ReenviarConfirmacion(ReenviarConfirmacionRequest request, CancellationToken ct)
    {
        await _cuentas.ReenviarConfirmacionAsync(request.Correo, ct);
        return Accepted(new MensajeResponse { Mensaje = "Si el correo está registrado y pendiente de confirmar, recibirás un nuevo enlace." });
    }
}
