using Microsoft.EntityFrameworkCore;
using Odontia.Application.Common;
using Odontia.Domain.Entities;
using Odontia.Domain.Enums;

namespace Odontia.Application.Features.Auth;

internal sealed partial class AuthService
{
    public async Task<LoginResult> LoginAsync(LoginRequest request, string? userAgent, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            // Igualar el costo temporal para no revelar la existencia del usuario.
            users.PasswordHasher.HashPassword(new ApplicationUser(), request.Password);
            await audit.WriteAsync(AuditAction.LoginFailed, "User", null, "Usuario inexistente");
            throw new UnauthorizedException(GenericLoginError, "invalid_credentials");
        }

        if (await users.IsLockedOutAsync(user))
        {
            await audit.WriteAsync(AuditAction.LoginFailed, "User", user.Id.ToString(), "Cuenta bloqueada", userId: user.Id);
            throw new UnauthorizedException(GenericLoginError, "invalid_credentials");
        }

        if (!await users.CheckPasswordAsync(user, request.Password))
        {
            await users.AccessFailedAsync(user);
            await audit.WriteAsync(AuditAction.LoginFailed, "User", user.Id.ToString(), "Contraseña incorrecta", userId: user.Id);
            throw new UnauthorizedException(GenericLoginError, "invalid_credentials");
        }

        if (!user.IsActive)
        {
            await audit.WriteAsync(AuditAction.LoginFailed, "User", user.Id.ToString(), "Usuario desactivado", userId: user.Id);
            throw new UnauthorizedException(GenericLoginError, "invalid_credentials");
        }

        if (_opt.RequireConfirmedEmail && !user.EmailConfirmed)
            throw new UnauthorizedException("Debe confirmar su correo electrónico antes de ingresar.", "email_not_confirmed");

        await users.ResetAccessFailedCountAsync(user);

        if (user.TwoFactorEnabled)
            return new LoginResult(null, true, jwt.CreateTwoFactorChallenge(user.Id));

        var session = await IssueNewSessionAsync(user, request.ConsultorioId, userAgent, ct);
        return new LoginResult(session, false, null);
    }

    public async Task<IssuedSession> LoginTwoFactorAsync(TwoFactorLoginRequest request, string? userAgent, CancellationToken ct)
    {
        const string expired = "El desafío de segundo factor expiró o ya fue utilizado. Inicie sesión nuevamente.";
        var challenge = await jwt.ValidateTwoFactorChallengeAsync(request.ChallengeToken)
                        ?? throw new UnauthorizedException(expired, "two_factor_expired");

        // El desafío es de un solo uso: se reserva de forma atómica (las solicitudes paralelas con el mismo desafío fallan)
        // y sólo se libera si el intento no completó el inicio de sesión, para permitir corregir un código mal tipeado.
        var challengeKey = $"2fa-challenge:{challenge.Jti}";
        if (!oneTimeUse.TryReserve(challengeKey, new DateTimeOffset(challenge.ExpiresAtUtc, TimeSpan.Zero).AddMinutes(1)))
            throw new UnauthorizedException(expired, "two_factor_expired");

        var completed = false;
        try
        {
            var user = await users.FindByIdAsync(challenge.UserId.ToString());
            if (user is null || !user.IsActive || !user.TwoFactorEnabled || await users.IsLockedOutAsync(user))
                throw new UnauthorizedException(GenericLoginError, "invalid_credentials");

            bool ok;
            string? totpKey = null;
            if (!string.IsNullOrWhiteSpace(request.RecoveryCode))
                ok = (await users.RedeemTwoFactorRecoveryCodeAsync(user, request.RecoveryCode.Replace(" ", ""))).Succeeded;
            else
            {
                var code = (request.Code ?? "").Replace(" ", "").Replace("-", "");
                ok = await users.VerifyTwoFactorTokenAsync(user, users.Options.Tokens.AuthenticatorTokenProvider, code);
                totpKey = $"2fa-totp:{user.Id:N}:{code}";
            }

            if (!ok)
            {
                await users.AccessFailedAsync(user);
                await audit.WriteAsync(AuditAction.LoginFailed, "User", user.Id.ToString(), "Código 2FA inválido", userId: user.Id);
                throw new UnauthorizedException("Código de verificación inválido.", "invalid_two_factor_code");
            }

            // Un código TOTP ya aceptado para este usuario no vuelve a servir dentro de su ventana de validez (anti-replay).
            if (totpKey is not null && !oneTimeUse.TryReserve(totpKey, clock.GetUtcNow().Add(TotpReuseWindow)))
            {
                await audit.WriteAsync(AuditAction.LoginFailed, "User", user.Id.ToString(), "Código 2FA reutilizado", userId: user.Id);
                throw new UnauthorizedException("El código ya fue utilizado. Espere el siguiente código de su app autenticadora.", "two_factor_code_reused");
            }

            await users.ResetAccessFailedCountAsync(user);
            var session = await IssueNewSessionAsync(user, null, userAgent, ct);
            completed = true;
            return session;
        }
        finally
        {
            if (!completed) oneTimeUse.Release(challengeKey);
        }
    }

    private async Task<IssuedSession> IssueNewSessionAsync(ApplicationUser user, Guid? requestedConsultorio, string? userAgent, CancellationToken ct)
    {
        var consultorioId = await ResolveConsultorioAsync(user, requestedConsultorio, ct);
        var familyId = Guid.CreateVersion7();
        var raw = SecureTokens.Create();
        var token = new RefreshToken(user.Id, familyId, SecureTokens.Hash(raw), Now, Now.AddDays(_opt.RefreshTokenDays), consultorioId, tenant.IpAddress, userAgent);
        db.RefreshTokens.Add(token);
        await db.SaveChangesAsync(ct);
        await TouchUserAsync(user, consultorioId, login: true, ct);
        await audit.WriteAsync(AuditAction.LoginSucceeded, "User", user.Id.ToString(), null, consultorioId, user.Id);
        return await BuildSessionAsync(user, consultorioId, familyId, raw, token.ExpiresAtUtc, ct);
    }

    /// <summary>
    /// Actualiza último acceso / último consultorio con un UPDATE atómico. No usa UserManager.UpdateAsync:
    /// su control de concurrencia (ConcurrencyStamp) haría fallar inicios de sesión simultáneos del mismo usuario.
    /// </summary>
    private async Task TouchUserAsync(ApplicationUser user, Guid? consultorioId, bool login, CancellationToken ct)
    {
        var now = Now;
        var lastConsultorio = consultorioId ?? user.LastConsultorioId;
        await db.Users.Where(u => u.Id == user.Id).ExecuteUpdateAsync(s =>
        {
            s.SetProperty(u => u.LastConsultorioId, lastConsultorio);
            if (login) s.SetProperty(u => u.LastLoginAtUtc, now);
        }, ct);
        user.LastConsultorioId = lastConsultorio;
        if (login) user.LastLoginAtUtc = now;
    }

    /// <summary>Elige el consultorio activo: el solicitado (si el usuario es miembro activo), el último usado o el primero disponible.</summary>
    private async Task<Guid?> ResolveConsultorioAsync(ApplicationUser user, Guid? requested, CancellationToken ct)
    {
        var active = await ActiveMembershipsQuery(user.Id).Select(m => m.ConsultorioId).ToListAsync(ct);
        if (requested is not null)
        {
            if (!active.Contains(requested.Value)) throw new ForbiddenAccessException("No pertenece al consultorio solicitado.");
            return requested;
        }
        if (user.LastConsultorioId is { } last && active.Contains(last)) return last;
        return active.Count > 0 ? active[0] : null;
    }
}
