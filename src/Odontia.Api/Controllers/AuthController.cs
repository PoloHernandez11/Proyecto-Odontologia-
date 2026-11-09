using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Odontia.Api.Infrastructure;
using Odontia.Application.Common;
using Odontia.Application.Features.Auth;

namespace Odontia.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase;

/// <summary>Respuesta de sesión para el frontend (access token + token CSRF para renovar).</summary>
public sealed record SessionResponse(AuthSessionDto Session, string CsrfToken);

public sealed record LoginResponseDto(bool RequiresTwoFactor, string? TwoFactorChallengeToken, SessionResponse? Result);

/// <summary>Autenticación, sesiones, contraseñas, 2FA e invitaciones.</summary>
[Route("api/v{version:apiVersion}/auth")]
public sealed class AuthController(IAuthService auth, AuthCookieManager cookies) : ApiControllerBase
{
    private string? UserAgent => Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null;

    /// <summary>Alta de un nuevo consultorio y su usuario administrador (si el registro público está habilitado).</summary>
    [HttpPost("register"), AllowAnonymous, EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct) => Accepted(await auth.RegisterAsync(request, ct));

    /// <summary>Inicio de sesión. Si el usuario tiene 2FA, devuelve un desafío para completar en /auth/login/2fa.</summary>
    [HttpPost("login"), AllowAnonymous, EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<LoginResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<LoginResponseDto> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await auth.LoginAsync(request, UserAgent, ct);
        if (result.RequiresTwoFactor) return new LoginResponseDto(true, result.TwoFactorChallengeToken, null);
        return new LoginResponseDto(false, null, IssueCookies(result.Session!));
    }

    /// <summary>Completa el inicio de sesión con código TOTP o código de recuperación.</summary>
    [HttpPost("login/2fa"), AllowAnonymous, EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<SessionResponse> LoginTwoFactor(TwoFactorLoginRequest request, CancellationToken ct) =>
        IssueCookies(await auth.LoginTwoFactorAsync(request, UserAgent, ct));

    /// <summary>Renueva la sesión (rotación del refresh token de la cookie). Requiere encabezado X-CSRF-Token.</summary>
    [HttpPost("refresh"), AllowAnonymous, EnableRateLimiting(RateLimitPolicies.Refresh)]
    public async Task<SessionResponse> Refresh(CancellationToken ct)
    {
        cookies.ValidateCsrf(Request);
        var token = cookies.ReadRefreshToken(Request) ?? throw new UnauthorizedException("Sesión inválida.", "invalid_refresh_token");
        try
        {
            return IssueCookies(await auth.RefreshAsync(token, UserAgent, ct));
        }
        catch (UnauthorizedException)
        {
            cookies.Clear(Response);
            throw;
        }
    }

    /// <summary>Cambia el consultorio activo (emite nuevos tokens para ese consultorio).</summary>
    [HttpPost("switch-consultorio"), EnableRateLimiting(RateLimitPolicies.Refresh)]
    public async Task<SessionResponse> SwitchConsultorio(SwitchConsultorioRequest request, CancellationToken ct)
    {
        cookies.ValidateCsrf(Request);
        var token = cookies.ReadRefreshToken(Request) ?? throw new UnauthorizedException("Sesión inválida.", "invalid_refresh_token");
        return IssueCookies(await auth.SwitchConsultorioAsync(token, request.ConsultorioId, UserAgent, ct));
    }

    /// <summary>Cierra la sesión actual (revoca la familia de refresh tokens y borra las cookies).</summary>
    [HttpPost("logout"), AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        cookies.ValidateCsrf(Request);
        await auth.LogoutAsync(cookies.ReadRefreshToken(Request), ct);
        cookies.Clear(Response);
        return NoContent();
    }

    /// <summary>Usuario autenticado, consultorio activo, permisos efectivos y membresías.</summary>
    [HttpGet("me")]
    public Task<AuthSessionDto> Me(CancellationToken ct) => auth.GetCurrentAsync(User.SessionId(), ct);

    /// <summary>Sesiones/dispositivos activos del usuario.</summary>
    [HttpGet("sessions")]
    public Task<IReadOnlyList<SessionInfoDto>> Sessions(CancellationToken ct) => auth.GetSessionsAsync(User.SessionId(), ct);

    /// <summary>Revoca una sesión/dispositivo.</summary>
    [HttpDelete("sessions/{sessionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RevokeSession(Guid sessionId, CancellationToken ct)
    {
        await auth.RevokeSessionAsync(sessionId, ct);
        return NoContent();
    }

    /// <summary>Revoca todas las sesiones excepto la actual.</summary>
    [HttpPost("sessions/revoke-others")]
    public async Task<object> RevokeOthers(CancellationToken ct) => new { revoked = await auth.RevokeOtherSessionsAsync(User.SessionId(), ct) };

    /// <summary>Solicita el correo de recuperación. Responde siempre igual (protección contra enumeración).</summary>
    [HttpPost("forgot-password"), AllowAnonymous, EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct)
    {
        await auth.ForgotPasswordAsync(request, ct);
        return Accepted(new MessageResponse("Si la cuenta existe, recibirá un correo con instrucciones."));
    }

    /// <summary>Restablece la contraseña con el token recibido por correo. Revoca todas las sesiones.</summary>
    [HttpPost("reset-password"), AllowAnonymous, EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<MessageResponse> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    {
        await auth.ResetPasswordAsync(request, ct);
        return new MessageResponse("Contraseña restablecida. Inicie sesión nuevamente.");
    }

    /// <summary>Confirma el correo electrónico.</summary>
    [HttpPost("confirm-email"), AllowAnonymous, EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<MessageResponse> ConfirmEmail(ConfirmEmailRequest request, CancellationToken ct)
    {
        await auth.ConfirmEmailAsync(request, ct);
        return new MessageResponse("Correo confirmado.");
    }

    /// <summary>Reenvía el correo de confirmación (respuesta idéntica exista o no la cuenta).</summary>
    [HttpPost("resend-confirmation"), AllowAnonymous, EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ResendConfirmation(ResendConfirmationRequest request, CancellationToken ct)
    {
        await auth.ResendConfirmationAsync(request, ct);
        return Accepted(new MessageResponse("Si corresponde, se reenviará el correo de confirmación."));
    }

    /// <summary>Cambio de contraseña del usuario autenticado. Revoca las demás sesiones.</summary>
    [HttpPost("change-password"), EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<MessageResponse> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        await auth.ChangePasswordAsync(request, User.SessionId(), ct);
        return new MessageResponse("Contraseña actualizada. Renueve la sesión para continuar.");
    }

    /// <summary>Clave y URI otpauth para configurar una app autenticadora.</summary>
    [HttpGet("2fa/setup")]
    public Task<AuthenticatorSetupDto> TwoFactorSetup(CancellationToken ct) => auth.GetAuthenticatorSetupAsync(ct);

    /// <summary>Activa 2FA verificando un código. Devuelve códigos de recuperación (se muestran una única vez).</summary>
    [HttpPost("2fa/enable"), EnableRateLimiting(RateLimitPolicies.Auth)]
    public Task<RecoveryCodesDto> EnableTwoFactor(EnableTwoFactorRequest request, CancellationToken ct) => auth.EnableTwoFactorAsync(request, ct);

    /// <summary>Desactiva 2FA (requiere contraseña).</summary>
    [HttpPost("2fa/disable"), EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DisableTwoFactor(DisableTwoFactorRequest request, CancellationToken ct)
    {
        await auth.DisableTwoFactorAsync(request, ct);
        return NoContent();
    }

    /// <summary>Datos públicos de una invitación (para la pantalla de aceptación).</summary>
    [HttpGet("invitations/{token}"), AllowAnonymous, EnableRateLimiting(RateLimitPolicies.Auth)]
    public Task<InvitationInfoDto> InvitationInfo(string token, CancellationToken ct) => auth.GetInvitationInfoAsync(token, ct);

    /// <summary>Acepta una invitación: crea/activa la cuenta y la incorpora al consultorio.</summary>
    [HttpPost("invitations/accept"), AllowAnonymous, EnableRateLimiting(RateLimitPolicies.Auth)]
    public Task<MessageResponse> AcceptInvitation(AcceptInvitationRequest request, CancellationToken ct) => auth.AcceptInvitationAsync(request, ct);

    private SessionResponse IssueCookies(IssuedSession s)
    {
        var csrf = cookies.Issue(Response, s.RefreshToken, s.RefreshTokenExpiresAtUtc);
        return new SessionResponse(s.Session, csrf);
    }
}
