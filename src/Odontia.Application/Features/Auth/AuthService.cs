using System.Text;
using Microsoft.AspNetCore.Identity;
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
    Task<RecoveryCodesDto> EnableTwoFactorAsync(EnableTwoFactorRequest request, Guid currentSessionId, CancellationToken ct);
    Task DisableTwoFactorAsync(DisableTwoFactorRequest request, CancellationToken ct);
    Task<InvitationInfoDto> GetInvitationInfoAsync(string token, CancellationToken ct);
    Task<MessageResponse> AcceptInvitationAsync(AcceptInvitationRequest request, CancellationToken ct);
}

internal sealed partial class AuthService(
    UserManager<ApplicationUser> users,
    IApplicationDbContext db,
    IJwtTokenService jwt,
    ITenantContext tenant,
    IUserStore<ApplicationUser> userStore,
    IEmailQueue emails,
    IOneTimeUseStore oneTimeUse,
    IAuditWriter audit,
    TimeProvider clock,
    IOptions<AuthOptions> authOptions,
    IOptions<FrontendOptions> frontendOptions) : IAuthService
{
    private const string GenericLoginError = "Credenciales inválidas o cuenta bloqueada temporalmente.";
    /// <summary>Retención del registro de códigos TOTP usados: cubre la ventana de validación de Identity (±2 pasos de 30 s).</summary>
    private static readonly TimeSpan TotpReuseWindow = TimeSpan.FromMinutes(5);
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
            // Protección contra enumeración: misma respuesta y costo similar (hash de la contraseña, como en el alta);
            // el aviso al titular real se envía en segundo plano.
            users.PasswordHasher.HashPassword(new ApplicationUser(), request.Password);
            emails.Enqueue(new DeferredEmail(null, email, "Intento de registro en Odentix",
                "<p>Se intentó registrar una cuenta con su correo. Si fue usted, inicie sesión o recupere su contraseña.</p>",
                "Se intentó registrar una cuenta con su correo. Si fue usted, inicie sesión o recupere su contraseña.",
                "registration_duplicate", false, null, null));
            return new MessageResponse(accepted);
        }

        var tz = string.IsNullOrWhiteSpace(request.TimeZoneId) ? _opt.DefaultTimeZone : request.TimeZoneId.Trim();
        EnsureTimeZone(tz);

        await using var tx = await db.BeginTransactionAsync(ct);
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

        await audit.WriteAsync(AuditAction.Created, nameof(Consultorio), consultorio.Id.ToString(), "Alta de consultorio por registro público", consultorio.Id, user.Id);
        if (_opt.RequireConfirmedEmail) await SendConfirmationAsync(user);
        return new MessageResponse(accepted);
    }

    // ---------------- Utilidades ----------------

    private IUserAuthenticatorKeyStore<ApplicationUser> AuthenticatorStore() =>
        userStore as IUserAuthenticatorKeyStore<ApplicationUser>
        ?? throw new InvalidOperationException("El almacén de usuarios no admite claves de autenticador.");

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
