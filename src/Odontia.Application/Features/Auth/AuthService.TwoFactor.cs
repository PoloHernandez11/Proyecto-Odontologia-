using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using Odontia.Application.Common;
using Odontia.Domain.Entities;
using Odontia.Domain.Enums;

namespace Odontia.Application.Features.Auth;

internal sealed partial class AuthService
{
    /// <summary>
    /// Genera (si no existe) la clave del autenticador. No usa <c>ResetAuthenticatorKeyAsync</c>: éste rota el security
    /// stamp e invalidaba el access token de la propia sesión que está configurando 2FA (QA API-07).
    /// Con 2FA ya activo no se vuelve a revelar la clave (un access token robado no debe permitir clonar el segundo factor).
    /// </summary>
    public async Task<AuthenticatorSetupDto> GetAuthenticatorSetupAsync(CancellationToken ct)
    {
        var user = await users.FindByIdAsync(tenant.UserId.ToString()) ?? throw new UnauthorizedException();
        if (user.TwoFactorEnabled)
            throw new BusinessRuleException("La verificación en dos pasos ya está activa. Desactívela para configurar una nueva app.", "two_factor_already_enabled");
        var key = await users.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key))
        {
            await AuthenticatorStore().SetAuthenticatorKeyAsync(user, users.GenerateNewAuthenticatorKey(), ct);
            ThrowIfFailed(await users.UpdateAsync(user));
            key = await users.GetAuthenticatorKeyAsync(user);
        }
        var uri = $"otpauth://totp/{UrlEncoder.Default.Encode("Odentix")}:{UrlEncoder.Default.Encode(user.Email!)}?secret={key}&issuer=Odentix&digits=6";
        return new AuthenticatorSetupDto(key!, uri);
    }

    /// <summary>
    /// Activa 2FA sin rotar el security stamp (la sesión actual sigue operativa) y revoca las demás sesiones,
    /// que se habían abierto sin segundo factor; su access token deja de valer de inmediato (control de sesión por solicitud).
    /// </summary>
    public async Task<RecoveryCodesDto> EnableTwoFactorAsync(EnableTwoFactorRequest request, Guid currentSessionId, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(tenant.UserId.ToString()) ?? throw new UnauthorizedException();
        if (user.TwoFactorEnabled)
            throw new BusinessRuleException("La verificación en dos pasos ya está activa.", "two_factor_already_enabled");
        var valid = await users.VerifyTwoFactorTokenAsync(user, users.Options.Tokens.AuthenticatorTokenProvider, request.Code.Replace(" ", ""));
        if (!valid) throw new RequestValidationException(nameof(request.Code), "El código de verificación es inválido.");
        if (userStore is not IUserTwoFactorStore<ApplicationUser> twoFactorStore)
            throw new InvalidOperationException("El almacén de usuarios no admite 2FA.");
        await twoFactorStore.SetTwoFactorEnabledAsync(user, true, ct);
        ThrowIfFailed(await users.UpdateAsync(user));
        await RevokeAllSessionsAsync(user.Id, currentSessionId, ct);
        var codes = await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        await audit.WriteAsync(AuditAction.TwoFactorChanged, "User", user.Id.ToString(), "2FA habilitado", tenant.ConsultorioIdOrNull, user.Id);
        return new RecoveryCodesDto(codes?.ToList() ?? []);
    }

    public async Task DisableTwoFactorAsync(DisableTwoFactorRequest request, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(tenant.UserId.ToString()) ?? throw new UnauthorizedException();
        if (!await users.CheckPasswordAsync(user, request.Password))
            throw new RequestValidationException(nameof(request.Password), "La contraseña es incorrecta.");
        await users.SetTwoFactorEnabledAsync(user, false);
        await users.ResetAuthenticatorKeyAsync(user);
        await audit.WriteAsync(AuditAction.TwoFactorChanged, "User", user.Id.ToString(), "2FA deshabilitado", tenant.ConsultorioIdOrNull, user.Id);
    }
}
