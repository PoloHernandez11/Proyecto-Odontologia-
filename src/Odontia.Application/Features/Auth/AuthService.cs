using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Odontia.Application.Abstractions;
using Odontia.Application.Common;
using Odontia.Domain.Authorization;
using Odontia.Domain.Entities;
using Odontia.Domain.Enums;

namespace Odontia.Application.Features.Auth;

public interface IAuthService
{
    Task<MessageResponse> RegisterAsync(RegisterRequest request, CancellationToken ct);
    Task<LoginResult> LoginAsync(LoginRequest request, string? userAgent, CancellationToken ct);
    Task<IssuedSession> LoginTwoFactorAsync(TwoFactorLoginRequest request, string? userAgent, CancellationToken ct);
    Task<IssuedSession> RefreshAsync(string refreshToken, string? userAgent, CancellationToken ct);
    Task<IssuedSession> SwitchConsultorioAsync(string refreshToken, Guid consultorioId, string? userAgent, CancellationToken ct);
    Task LogoutAsync(string? refreshToken, CancellationToken ct);
    Task<AuthSessionDto> GetCurrentAsync(Guid sessionId, CancellationToken ct);
    Task<IReadOnlyList<SessionInfoDto>> GetSessionsAsync(Guid currentSessionId, CancellationToken ct);
    Task RevokeSessionAsync(Guid sessionId, CancellationToken ct);
    Task<int> RevokeOtherSessionsAsync(Guid currentSessionId, CancellationToken ct);
    Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct);
    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct);
    Task ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken ct);
    Task ResendConfirmationAsync(ResendConfirmationRequest request, CancellationToken ct);
    Task ChangePasswordAsync(ChangePasswordRequest request, Guid currentSessionId, CancellationToken ct);
    Task<AuthenticatorSetupDto> GetAuthenticatorSetupAsync(CancellationToken ct);
    Task<RecoveryCodesDto> EnableTwoFactorAsync(EnableTwoFactorRequest request, CancellationToken ct);
    Task DisableTwoFactorAsync(DisableTwoFactorRequest request, CancellationToken ct);
    Task<InvitationInfoDto> GetInvitationInfoAsync(string token, CancellationToken ct);
    Task<MessageResponse> AcceptInvitationAsync(AcceptInvitationRequest request, CancellationToken ct);
}

internal sealed class AuthService(
    UserManager<ApplicationUser> users,
    IApplicationDbContext db,
    IJwtTokenService jwt,
    ITenantContext tenant,
    IMessageDispatcher messages,
    IAuditWriter audit,
    TimeProvider clock,
    IOptions<AuthOptions> authOptions,
    IOptions<FrontendOptions> frontendOptions) : IAuthService
{
    private const string GenericLoginError = "Credenciales inválidas o cuenta bloqueada temporalmente.";
    private readonly AuthOptions _opt = authOptions.Value;
    private readonly FrontendOptions _front = frontendOptions.Value;
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    // ---------------- Registro ----------------

    public async Task<MessageResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        if (!_opt.AllowPublicRegistration) throw new ForbiddenAccessException("El registro público está deshabilitado.");
        const string accepted = "Si los datos son válidos, recibirá un correo para confirmar la cuenta.";

        var email = request.Email.Trim().ToLowerInvariant();
        if (await users.FindByEmailAsync(email) is not null)
        {
            // Protección contra enumeración: misma respuesta; se avisa al titular real.
            await messages.SendEmailAsync(null, email, "Intento de registro en Odontia",
                "<p>Se intentó registrar una cuenta con su correo. Si fue usted, inicie sesión o recupere su contraseña.</p>",
                "Se intentó registrar una cuenta con su correo. Si fue usted, inicie sesión o recupere su contraseña.",
                "registration_duplicate", false, null, null, ct);
            return new MessageResponse(accepted);
        }

        var tz = string.IsNullOrWhiteSpace(request.TimeZoneId) ? _opt.DefaultTimeZone : request.TimeZoneId.Trim();
        EnsureTimeZone(tz);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var user = new ApplicationUser
        {
            UserName = email, Email = email, FirstName = request.FirstName.Trim(), LastName = request.LastName.Trim(),
            Title = request.Title?.Trim(), Specialty = request.Specialty?.Trim(), LicenseNumber = request.LicenseNumber?.Trim(),
            EmailConfirmed = !_opt.RequireConfirmedEmail, CreatedAtUtc = Now,
        };
        var result = await users.CreateAsync(user, request.Password);
        ThrowIfFailed(result);

        var consultorio = new Consultorio(request.ConsultorioName, tz) { CreatedByUserId = user.Id };
        db.Consultorios.Add(consultorio);
        var member = new ConsultorioMember(consultorio.Id, user.Id, request.IsProfessional);
        var roles = new List<Guid> { SystemRoles.AdministradorId };
        if (request.IsProfessional) roles.Add(SystemRoles.OdontologoId);
        member.ReplaceRoles(roles);
        db.Members.Add(member);
        db.NotificationSettings.Add(new UserNotificationSettings(user.Id));
        db.Sequences.Add(new ConsultorioSequence(consultorio.Id, ConsultorioSequence.Budget));
        db.Sequences.Add(new ConsultorioSequence(consultorio.Id, ConsultorioSequence.Receipt));
        user.LastConsultorioId = consultorio.Id;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        audit.Write(AuditAction.Created, nameof(Consultorio), consultorio.Id.ToString(), "Alta de consultorio por registro público", consultorio.Id, user.Id);
        if (_opt.RequireConfirmedEmail) await SendConfirmationAsync(user, ct);
        return new MessageResponse(accepted);
    }

    // ---------------- Login ----------------

    public async Task<LoginResult> LoginAsync(LoginRequest request, string? userAgent, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            // Igualar el costo temporal para no revelar la existencia del usuario.
            users.PasswordHasher.HashPassword(new ApplicationUser(), request.Password);
            audit.Write(AuditAction.LoginFailed, "User", null, "Usuario inexistente");
            throw new UnauthorizedException(GenericLoginError, "invalid_credentials");
        }

        if (await users.IsLockedOutAsync(user))
        {
            audit.Write(AuditAction.LoginFailed, "User", user.Id.ToString(), "Cuenta bloqueada", userId: user.Id);
            throw new UnauthorizedException(GenericLoginError, "invalid_credentials");
        }

        if (!await users.CheckPasswordAsync(user, request.Password))
        {
            await users.AccessFailedAsync(user);
            audit.Write(AuditAction.LoginFailed, "User", user.Id.ToString(), "Contraseña incorrecta", userId: user.Id);
            throw new UnauthorizedException(GenericLoginError, "invalid_credentials");
        }

        if (!user.IsActive)
        {
            audit.Write(AuditAction.LoginFailed, "User", user.Id.ToString(), "Usuario desactivado", userId: user.Id);
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
        var userId = jwt.ValidateTwoFactorChallenge(request.ChallengeToken)
                     ?? throw new UnauthorizedException("El desafío de segundo factor expiró. Inicie sesión nuevamente.", "two_factor_expired");
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive || !user.TwoFactorEnabled || await users.IsLockedOutAsync(user))
            throw new UnauthorizedException(GenericLoginError, "invalid_credentials");

        bool ok;
        if (!string.IsNullOrWhiteSpace(request.RecoveryCode))
            ok = (await users.RedeemTwoFactorRecoveryCodeAsync(user, request.RecoveryCode.Replace(" ", ""))).Succeeded;
        else
            ok = await users.VerifyTwoFactorTokenAsync(user, users.Options.Tokens.AuthenticatorTokenProvider,
                (request.Code ?? "").Replace(" ", "").Replace("-", ""));

        if (!ok)
        {
            await users.AccessFailedAsync(user);
            audit.Write(AuditAction.LoginFailed, "User", user.Id.ToString(), "Código 2FA inválido", userId: user.Id);
            throw new UnauthorizedException("Código de verificación inválido.", "invalid_two_factor_code");
        }
        await users.ResetAccessFailedCountAsync(user);
        return await IssueNewSessionAsync(user, null, userAgent, ct);
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
        audit.Write(AuditAction.LoginSucceeded, "User", user.Id.ToString(), null, consultorioId, user.Id);
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

    private IQueryable<ConsultorioMember> ActiveMembershipsQuery(Guid userId) =>
        db.Members.IgnoreQueryFilters()
            .Where(m => m.UserId == userId && m.IsActive)
            .Where(m => db.Consultorios.Any(c => c.Id == m.ConsultorioId && c.IsActive));

    private async Task<IssuedSession> BuildSessionAsync(ApplicationUser user, Guid? consultorioId, Guid familyId, string rawRefresh,
        DateTime refreshExpires, CancellationToken ct)
    {
        var access = jwt.CreateAccessToken(user, consultorioId, familyId);
        var dto = await BuildSessionDtoAsync(user, consultorioId, familyId, access, ct);
        return new IssuedSession(dto, rawRefresh, refreshExpires);
    }

    private async Task<AuthSessionDto> BuildSessionDtoAsync(ApplicationUser user, Guid? consultorioId, Guid familyId, AccessTokenResult access, CancellationToken ct)
    {
        var memberships = await ActiveMembershipsQuery(user.Id)
            .Select(m => new
            {
                m.ConsultorioId,
                Name = db.Consultorios.Where(c => c.Id == m.ConsultorioId).Select(c => c.Name).First(),
                Roles = m.Roles.Select(r => r.Role!.Name).ToList(),
                m.IsProfessional,
                Permissions = m.Roles.SelectMany(r => r.Role!.Permissions.Select(p => p.Permission)).ToList(),
            })
            .ToListAsync(ct);

        var permissions = memberships.FirstOrDefault(m => m.ConsultorioId == consultorioId)?.Permissions.Distinct().Order().ToList() ?? [];
        return new AuthSessionDto(access.Token, access.ExpiresAtUtc, familyId, ToUserDto(user), consultorioId, permissions,
            memberships.Select(m => new MembershipDto(m.ConsultorioId, m.Name, m.Roles, m.IsProfessional)).ToList());
    }

    private static AuthUserDto ToUserDto(ApplicationUser u) =>
        new(u.Id, u.Email!, u.FirstName, u.LastName, u.DisplayName, u.Title, u.Specialty, u.LicenseNumber, u.TwoFactorEnabled);

    // ---------------- Refresh / rotación ----------------

    public async Task<IssuedSession> RefreshAsync(string refreshToken, string? userAgent, CancellationToken ct)
    {
        var (user, current) = await ValidateRefreshAsync(refreshToken, ct);
        var consultorioId = current.ConsultorioId;
        if (consultorioId is not null && !await ActiveMembershipsQuery(user.Id).AnyAsync(m => m.ConsultorioId == consultorioId, ct))
            consultorioId = await ResolveConsultorioAsync(user, null, ct);
        return await RotateAsync(user, current, consultorioId, userAgent, ct);
    }

    public async Task<IssuedSession> SwitchConsultorioAsync(string refreshToken, Guid consultorioId, string? userAgent, CancellationToken ct)
    {
        var (user, current) = await ValidateRefreshAsync(refreshToken, ct);
        if (current.UserId != tenant.UserId) throw new UnauthorizedException("La sesión no corresponde al usuario autenticado.");
        if (!await ActiveMembershipsQuery(user.Id).AnyAsync(m => m.ConsultorioId == consultorioId, ct))
            throw new ForbiddenAccessException("No pertenece al consultorio solicitado.");
        await TouchUserAsync(user, consultorioId, login: false, ct);
        return await RotateAsync(user, current, consultorioId, userAgent, ct);
    }

    private async Task<(ApplicationUser User, RefreshToken Token)> ValidateRefreshAsync(string refreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) throw new UnauthorizedException("Sesión inválida.", "invalid_refresh_token");
        var hash = SecureTokens.Hash(refreshToken);
        var token = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct)
                    ?? throw new UnauthorizedException("Sesión inválida.", "invalid_refresh_token");

        if (token.RevokedAtUtc is not null)
        {
            // Reuso de un token ya rotado: posible robo. Se revoca toda la familia (sesión).
            if (token.ReplacedByTokenId is not null)
            {
                await RevokeFamilyAsync(token.UserId, token.FamilyId, "reuse_detected", ct);
                audit.Write(AuditAction.SessionRevoked, "Session", token.FamilyId.ToString(), "Reuso de refresh token detectado", token.ConsultorioId, token.UserId);
            }
            throw new UnauthorizedException("Sesión inválida.", "invalid_refresh_token");
        }

        var familyStart = await db.RefreshTokens.Where(t => t.FamilyId == token.FamilyId).MinAsync(t => t.CreatedAtUtc, ct);
        if (token.ExpiresAtUtc <= Now || familyStart.AddDays(_opt.SessionMaxDays) <= Now)
            throw new UnauthorizedException("La sesión expiró.", "session_expired");

        var user = await users.FindByIdAsync(token.UserId.ToString());
        if (user is null || !user.IsActive || await users.IsLockedOutAsync(user))
        {
            await RevokeFamilyAsync(token.UserId, token.FamilyId, "user_inactive", ct);
            throw new UnauthorizedException("Sesión inválida.", "invalid_refresh_token");
        }
        return (user, token);
    }

    private async Task<IssuedSession> RotateAsync(ApplicationUser user, RefreshToken current, Guid? consultorioId, string? userAgent, CancellationToken ct)
    {
        var raw = SecureTokens.Create();
        var next = new RefreshToken(user.Id, current.FamilyId, SecureTokens.Hash(raw), Now, Now.AddDays(_opt.RefreshTokenDays),
            consultorioId, tenant.IpAddress, userAgent);
        current.MarkUsed(Now);
        current.Revoke(Now, "rotated", next.Id);
        db.RefreshTokens.Add(next);
        await db.SaveChangesAsync(ct);
        return await BuildSessionAsync(user, consultorioId, current.FamilyId, raw, next.ExpiresAtUtc, ct);
    }

    private async Task RevokeFamilyAsync(Guid userId, Guid familyId, string reason, CancellationToken ct)
    {
        var tokens = await db.RefreshTokens.Where(t => t.UserId == userId && t.FamilyId == familyId && t.RevokedAtUtc == null).ToListAsync(ct);
        foreach (var t in tokens) t.Revoke(Now, reason);
        await db.SaveChangesAsync(ct);
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return;
        var hash = SecureTokens.Hash(refreshToken);
        var token = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (token is null) return;
        await RevokeFamilyAsync(token.UserId, token.FamilyId, "logout", ct);
        audit.Write(AuditAction.Logout, "Session", token.FamilyId.ToString(), null, token.ConsultorioId, token.UserId);
    }

    public async Task<AuthSessionDto> GetCurrentAsync(Guid sessionId, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(tenant.UserId.ToString()) ?? throw new UnauthorizedException();
        var dto = await BuildSessionDtoAsync(user, tenant.ConsultorioIdOrNull, sessionId, new AccessTokenResult("", default), ct);
        return dto with { AccessToken = string.Empty };
    }

    // ---------------- Sesiones / dispositivos ----------------

    public async Task<IReadOnlyList<SessionInfoDto>> GetSessionsAsync(Guid currentSessionId, CancellationToken ct)
    {
        var uid = tenant.UserId;
        var tokens = await db.RefreshTokens.Where(t => t.UserId == uid).ToListAsync(ct);
        return tokens.GroupBy(t => t.FamilyId)
            .Select(g => new { Family = g.Key, Active = g.FirstOrDefault(t => t.IsActive(Now)), Start = g.Min(t => t.CreatedAtUtc) })
            .Where(x => x.Active is not null)
            .Select(x => new SessionInfoDto(x.Family, x.Start, x.Active!.LastUsedAtUtc ?? x.Active.CreatedAtUtc, x.Active.ExpiresAtUtc,
                x.Active.IpAddress, x.Active.UserAgent, x.Family == currentSessionId))
            .OrderByDescending(s => s.LastUsedAtUtc)
            .ToList();
    }

    public async Task RevokeSessionAsync(Guid sessionId, CancellationToken ct)
    {
        var uid = tenant.UserId;
        if (!await db.RefreshTokens.AnyAsync(t => t.UserId == uid && t.FamilyId == sessionId, ct)) throw new NotFoundException("La sesión no existe.");
        await RevokeFamilyAsync(uid, sessionId, "revoked_by_user", ct);
        audit.Write(AuditAction.SessionRevoked, "Session", sessionId.ToString(), null, tenant.ConsultorioIdOrNull, uid);
    }

    public async Task<int> RevokeOtherSessionsAsync(Guid currentSessionId, CancellationToken ct)
    {
        var uid = tenant.UserId;
        var tokens = await db.RefreshTokens.Where(t => t.UserId == uid && t.FamilyId != currentSessionId && t.RevokedAtUtc == null).ToListAsync(ct);
        foreach (var t in tokens) t.Revoke(Now, "revoked_others");
        await db.SaveChangesAsync(ct);
        var families = tokens.Select(t => t.FamilyId).Distinct().Count();
        audit.Write(AuditAction.SessionRevoked, "Session", null, $"Sesiones revocadas: {families}", tenant.ConsultorioIdOrNull, uid);
        return families;
    }

    private async Task RevokeAllSessionsAsync(Guid userId, Guid? exceptFamily, CancellationToken ct)
    {
        var tokens = await db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAtUtc == null && t.FamilyId != exceptFamily).ToListAsync(ct);
        foreach (var t in tokens) t.Revoke(Now, "credentials_changed");
        await db.SaveChangesAsync(ct);
    }

    // ---------------- Contraseñas y correo ----------------

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null || !user.IsActive || (_opt.RequireConfirmedEmail && !user.EmailConfirmed)) return; // respuesta idéntica
        var token = await users.GeneratePasswordResetTokenAsync(user);
        var link = BuildLink(_front.ResetPasswordPath, ("email", user.Email!), ("token", Encode(token)));
        await messages.SendEmailAsync(null, user.Email!, "Restablecer contraseña - Odontia",
            $"<p>Para restablecer su contraseña ingrese a: <a href=\"{HtmlEncoder.Default.Encode(link)}\">restablecer contraseña</a>. El enlace vence en 2 horas.</p>",
            $"Para restablecer su contraseña ingrese a: {link} (vence en 2 horas).", "password_reset", true, "User", user.Id, ct);
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
        audit.Write(AuditAction.PasswordReset, "User", user.Id.ToString(), null, null, user.Id);
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
        await SendConfirmationAsync(user, ct);
    }

    private async Task SendConfirmationAsync(ApplicationUser user, CancellationToken ct)
    {
        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        var link = BuildLink(_front.ConfirmEmailPath, ("userId", user.Id.ToString()), ("token", Encode(token)));
        await messages.SendEmailAsync(null, user.Email!, "Confirme su correo - Odontia",
            $"<p>Bienvenido/a a Odontia. Confirme su correo ingresando a: <a href=\"{HtmlEncoder.Default.Encode(link)}\">confirmar correo</a>.</p>",
            $"Bienvenido/a a Odontia. Confirme su correo: {link}", "email_confirmation", true, "User", user.Id, ct);
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
        audit.Write(AuditAction.PasswordChanged, "User", user.Id.ToString(), null, tenant.ConsultorioIdOrNull, user.Id);
    }

    // ---------------- Segundo factor (TOTP) ----------------

    public async Task<AuthenticatorSetupDto> GetAuthenticatorSetupAsync(CancellationToken ct)
    {
        var user = await users.FindByIdAsync(tenant.UserId.ToString()) ?? throw new UnauthorizedException();
        var key = await users.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key))
        {
            await users.ResetAuthenticatorKeyAsync(user);
            key = await users.GetAuthenticatorKeyAsync(user);
        }
        var uri = $"otpauth://totp/{UrlEncoder.Default.Encode("Odontia")}:{UrlEncoder.Default.Encode(user.Email!)}?secret={key}&issuer=Odontia&digits=6";
        return new AuthenticatorSetupDto(key!, uri);
    }

    public async Task<RecoveryCodesDto> EnableTwoFactorAsync(EnableTwoFactorRequest request, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(tenant.UserId.ToString()) ?? throw new UnauthorizedException();
        var valid = await users.VerifyTwoFactorTokenAsync(user, users.Options.Tokens.AuthenticatorTokenProvider, request.Code.Replace(" ", ""));
        if (!valid) throw new RequestValidationException(nameof(request.Code), "El código de verificación es inválido.");
        await users.SetTwoFactorEnabledAsync(user, true);
        var codes = await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        audit.Write(AuditAction.TwoFactorChanged, "User", user.Id.ToString(), "2FA habilitado", tenant.ConsultorioIdOrNull, user.Id);
        return new RecoveryCodesDto(codes?.ToList() ?? []);
    }

    public async Task DisableTwoFactorAsync(DisableTwoFactorRequest request, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(tenant.UserId.ToString()) ?? throw new UnauthorizedException();
        if (!await users.CheckPasswordAsync(user, request.Password))
            throw new RequestValidationException(nameof(request.Password), "La contraseña es incorrecta.");
        await users.SetTwoFactorEnabledAsync(user, false);
        await users.ResetAuthenticatorKeyAsync(user);
        audit.Write(AuditAction.TwoFactorChanged, "User", user.Id.ToString(), "2FA deshabilitado", tenant.ConsultorioIdOrNull, user.Id);
    }

    // ---------------- Invitaciones (aceptación / activación de cuenta) ----------------

    public async Task<InvitationInfoDto> GetInvitationInfoAsync(string token, CancellationToken ct)
    {
        var inv = await FindUsableInvitationAsync(token, ct);
        var name = await db.Consultorios.Where(c => c.Id == inv.ConsultorioId).Select(c => c.Name).FirstAsync(ct);
        var roleIds = inv.Roles.Select(r => r.RoleId).ToList();
        var roles = await db.AppRoles.Where(r => roleIds.Contains(r.Id)).Select(r => r.Name).ToListAsync(ct);
        var exists = await users.FindByEmailAsync(inv.Email) is not null;
        return new InvitationInfoDto(inv.Email, name, roles, inv.ExpiresAtUtc, exists);
    }

    public async Task<MessageResponse> AcceptInvitationAsync(AcceptInvitationRequest request, CancellationToken ct)
    {
        var inv = await FindUsableInvitationAsync(request.Token, ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var user = await users.FindByEmailAsync(inv.Email);
        if (user is null)
        {
            if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
                throw new RequestValidationException("firstName", "Nombre y apellido son obligatorios para crear la cuenta.");
            user = new ApplicationUser
            {
                UserName = inv.Email, Email = inv.Email, FirstName = request.FirstName.Trim(), LastName = request.LastName.Trim(),
                EmailConfirmed = true, // la invitación prueba la titularidad del correo
                CreatedAtUtc = Now, LastConsultorioId = inv.ConsultorioId,
            };
            ThrowIfFailed(await users.CreateAsync(user, request.Password));
            db.NotificationSettings.Add(new UserNotificationSettings(user.Id));
        }
        else
        {
            if (!user.IsActive || !await users.CheckPasswordAsync(user, request.Password))
                throw new UnauthorizedException("La contraseña de la cuenta existente es incorrecta.", "invalid_credentials");
            if (!user.EmailConfirmed) { user.EmailConfirmed = true; await users.UpdateAsync(user); }
        }

        var existing = await db.Members.IgnoreQueryFilters().Include(m => m.Roles)
            .FirstOrDefaultAsync(m => m.ConsultorioId == inv.ConsultorioId && m.UserId == user.Id, ct);
        if (existing is not null)
        {
            existing.SetActive(true);
            existing.ReplaceRoles(inv.Roles.Select(r => r.RoleId));
            if (inv.IsProfessional) existing.SetProfessional(true);
        }
        else
        {
            var member = new ConsultorioMember(inv.ConsultorioId, user.Id, inv.IsProfessional);
            member.ReplaceRoles(inv.Roles.Select(r => r.RoleId));
            db.Members.Add(member);
        }
        inv.Accept(user.Id, Now);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        audit.Write(AuditAction.InvitationAccepted, nameof(Invitation), inv.Id.ToString(), null, inv.ConsultorioId, user.Id);
        return new MessageResponse("Invitación aceptada. Ya puede iniciar sesión.");
    }

    private async Task<Invitation> FindUsableInvitationAsync(string token, CancellationToken ct)
    {
        var hash = SecureTokens.Hash(token ?? "");
        var inv = await db.Invitations.IgnoreQueryFilters().Include(i => i.Roles).FirstOrDefaultAsync(i => i.TokenHash == hash, ct);
        if (inv is null || !inv.IsUsable(Now)) throw new BusinessRuleException("La invitación es inválida, fue revocada o expiró.", "invitation_invalid");
        return inv;
    }

    // ---------------- Utilidades ----------------

    private string BuildLink(string path, params (string Key, string Value)[] query) =>
        $"{_front.BaseUrl.TrimEnd('/')}{path}?{string.Join('&', query.Select(q => $"{q.Key}={Uri.EscapeDataString(q.Value)}"))}";

    private static string Encode(string token) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(token)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Decode(string token)
    {
        try
        {
            var s = token.Replace('-', '+').Replace('_', '/');
            s = s.PadRight(s.Length + (4 - s.Length % 4) % 4, '=');
            return Encoding.UTF8.GetString(Convert.FromBase64String(s));
        }
        catch (FormatException)
        {
            return string.Empty;
        }
    }

    internal static void EnsureTimeZone(string tz)
    {
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(tz, out _))
            throw new RequestValidationException("timeZoneId", "La zona horaria no es válida.");
    }

    private static void ThrowIfFailed(IdentityResult result)
    {
        if (result.Succeeded) return;
        var errors = result.Errors
            .GroupBy(e => e.Code.StartsWith("Password", StringComparison.Ordinal) ? "password" : e.Code.Contains("Email") || e.Code.Contains("UserName") ? "email" : "general")
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
        throw new RequestValidationException(errors);
    }
}
