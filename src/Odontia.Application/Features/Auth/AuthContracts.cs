namespace Odontia.Application.Features.Auth;

public sealed class AuthOptions
{
    public const string Section = "Auth";
    /// <summary>Permite el alta pública de un nuevo consultorio + administrador (onboarding SaaS).</summary>
    public bool AllowPublicRegistration { get; set; } = true;
    public bool RequireConfirmedEmail { get; set; } = true;
    /// <summary>Vigencia deslizante del refresh token (inactividad máxima).</summary>
    public int RefreshTokenDays { get; set; } = 7;
    /// <summary>Duración absoluta máxima de una sesión (familia de refresh tokens).</summary>
    public int SessionMaxDays { get; set; } = 30;
    public int InvitationDays { get; set; } = 7;
    public string DefaultTimeZone { get; set; } = "America/Argentina/Buenos_Aires";
}

// ---------- Solicitudes ----------

/// <summary>Alta de un nuevo consultorio con su usuario administrador.</summary>
public sealed record RegisterRequest(string Email, string Password, string FirstName, string LastName, string ConsultorioName,
    string? Title, string? Specialty, string? LicenseNumber, string? TimeZoneId, bool IsProfessional = true);

public sealed record LoginRequest(string Email, string Password, Guid? ConsultorioId);

public sealed record TwoFactorLoginRequest(string ChallengeToken, string? Code, string? RecoveryCode);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(string Email, string Token, string NewPassword);

public sealed record ConfirmEmailRequest(Guid UserId, string Token);

public sealed record ResendConfirmationRequest(string Email);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record SwitchConsultorioRequest(Guid ConsultorioId);

public sealed record EnableTwoFactorRequest(string Code);

public sealed record DisableTwoFactorRequest(string Password);

public sealed record AcceptInvitationRequest(string Token, string? FirstName, string? LastName, string Password);

// ---------- Respuestas ----------

public sealed record MembershipDto(Guid ConsultorioId, string ConsultorioName, IReadOnlyList<string> Roles, bool IsProfessional);

public sealed record AuthUserDto(Guid Id, string Email, string FirstName, string LastName, string DisplayName, string? Title,
    string? Specialty, string? LicenseNumber, bool TwoFactorEnabled);

/// <summary>Sesión emitida. El refresh token NO se devuelve en el cuerpo: viaja sólo en una cookie HttpOnly.</summary>
public sealed record AuthSessionDto(string AccessToken, DateTime AccessTokenExpiresAtUtc, Guid SessionId, AuthUserDto User,
    Guid? ActiveConsultorioId, IReadOnlyList<string> Permissions, IReadOnlyList<MembershipDto> Memberships);

/// <summary>Resultado interno de emisión de sesión (incluye el refresh token crudo para la cookie).</summary>
public sealed record IssuedSession(AuthSessionDto Session, string RefreshToken, DateTime RefreshTokenExpiresAtUtc);

public sealed record LoginResult(IssuedSession? Session, bool RequiresTwoFactor, string? TwoFactorChallengeToken);

public sealed record LoginResponse(bool RequiresTwoFactor, string? TwoFactorChallengeToken, AuthSessionDto? Session);

public sealed record SessionInfoDto(Guid SessionId, DateTime StartedAtUtc, DateTime? LastUsedAtUtc, DateTime ExpiresAtUtc,
    string? IpAddress, string? UserAgent, bool IsCurrent);

public sealed record AuthenticatorSetupDto(string SharedKey, string AuthenticatorUri);

public sealed record RecoveryCodesDto(IReadOnlyList<string> RecoveryCodes);

public sealed record InvitationInfoDto(string Email, string ConsultorioName, IReadOnlyList<string> Roles, DateTime ExpiresAtUtc, bool UserExists);

public sealed record MessageResponse(string Message);
