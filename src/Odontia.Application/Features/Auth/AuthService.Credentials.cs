using System.Text.Encodings.Web;
using Odontia.Application.Common;
using Odontia.Domain.Entities;
using Odontia.Domain.Enums;

namespace Odontia.Application.Features.Auth;

internal sealed partial class AuthService
{
    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null || !user.IsActive || (_opt.RequireConfirmedEmail && !user.EmailConfirmed)) return; // respuesta idéntica
        var token = await users.GeneratePasswordResetTokenAsync(user);
        var link = BuildLink(_front.ResetPasswordPath, ("email", user.Email!), ("token", Encode(token)));
        // En segundo plano: la respuesta no espera al SMTP (el tiempo no revela si la cuenta existe).
        emails.Enqueue(new DeferredEmail(null, user.Email!, "Restablecer contraseña - Odentix",
            $"<p>Para restablecer su contraseña ingrese a: <a href=\"{HtmlEncoder.Default.Encode(link)}\">restablecer contraseña</a>. El enlace vence en 2 horas.</p>",
            $"Para restablecer su contraseña ingrese a: {link} (vence en 2 horas).", "password_reset", true, "User", user.Id));
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        const string invalid = "El enlace de restablecimiento es inválido o expiró.";
        if (user is null) throw new BusinessRuleException(invalid, "invalid_reset_token");
        var result = await users.ResetPasswordAsync(user, Decode(request.Token), request.NewPassword);
        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Code == "InvalidToken")) throw new BusinessRuleException(invalid, "invalid_reset_token");
            ThrowIfFailed(result);
        }
        await users.ResetAccessFailedCountAsync(user);
        await users.SetLockoutEndDateAsync(user, null);
        await RevokeAllSessionsAsync(user.Id, null, ct);
        await audit.WriteAsync(AuditAction.PasswordReset, "User", user.Id.ToString(), null, null, user.Id);
    }

    public async Task ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(request.UserId.ToString())
                   ?? throw new BusinessRuleException("El enlace de confirmación es inválido.", "invalid_confirmation_token");
        var result = await users.ConfirmEmailAsync(user, Decode(request.Token));
        if (!result.Succeeded) throw new BusinessRuleException("El enlace de confirmación es inválido o expiró.", "invalid_confirmation_token");
    }

    public async Task ResendConfirmationAsync(ResendConfirmationRequest request, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null || user.EmailConfirmed) return;
        await SendConfirmationAsync(user);
    }

    private async Task SendConfirmationAsync(ApplicationUser user)
    {
        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        var link = BuildLink(_front.ConfirmEmailPath, ("userId", user.Id.ToString()), ("token", Encode(token)));
        emails.Enqueue(new DeferredEmail(null, user.Email!, "Confirme su correo - Odentix",
            $"<p>Bienvenido/a a Odentix. Confirme su correo ingresando a: <a href=\"{HtmlEncoder.Default.Encode(link)}\">confirmar correo</a>.</p>",
            $"Bienvenido/a a Odentix. Confirme su correo: {link}", "email_confirmation", true, "User", user.Id));
    }

    public async Task ChangePasswordAsync(ChangePasswordRequest request, Guid currentSessionId, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(tenant.UserId.ToString()) ?? throw new UnauthorizedException();
        var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Code == "PasswordMismatch"))
                throw new RequestValidationException(nameof(request.CurrentPassword), "La contraseña actual es incorrecta.");
            ThrowIfFailed(result);
        }
        await RevokeAllSessionsAsync(user.Id, currentSessionId, ct);
        await audit.WriteAsync(AuditAction.PasswordChanged, "User", user.Id.ToString(), null, tenant.ConsultorioIdOrNull, user.Id);
    }
}
