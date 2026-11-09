namespace Odontia.Application.Features.Organization;

public sealed record ConsultorioDto(Guid Id, string Name, string? LegalName, string? TaxId, string? Phone, string? Email, string? Website,
    string? AddressLine, string? City, string? Province, string? PostalCode, string Country, string TimeZoneId,
    int DefaultAppointmentMinutes, int SlotIntervalMinutes, int ReminderHoursBefore, bool IsActive);

public sealed record UpdateConsultorioRequest(string Name, string? LegalName, string? TaxId, string? Phone, string? Email, string? Website,
    string? AddressLine, string? City, string? Province, string? PostalCode, string Country);

public sealed record UpdateConsultorioSettingsRequest(string TimeZoneId, int DefaultAppointmentMinutes, int SlotIntervalMinutes, int ReminderHoursBefore);

public sealed record GabineteDto(Guid Id, string Name, string? Description, bool IsActive);

public sealed record SaveGabineteRequest(string Name, string? Description, bool IsActive = true);

public sealed record ProfessionalDto(Guid UserId, string DisplayName, string? Title, string? Specialty, string? LicenseNumber,
    int DefaultAppointmentMinutes, string? AgendaColor);

public sealed record MemberDto(Guid MemberId, Guid UserId, string Email, string FirstName, string LastName, string DisplayName,
    bool IsActive, bool IsProfessional, bool UserIsActive, IReadOnlyList<RoleSummaryDto> Roles, DateTime? LastLoginAtUtc,
    int? DefaultAppointmentMinutes, string? AgendaColor);

public sealed record RoleSummaryDto(Guid Id, string Name, bool IsSystem);

public sealed record UpdateMemberRolesRequest(IReadOnlyList<Guid> RoleIds);

public sealed record UpdateMemberRequest(bool IsProfessional, int? DefaultAppointmentMinutes, string? AgendaColor);

public sealed record SetActiveRequest(bool IsActive);

public sealed record RoleDto(Guid Id, string Name, string? Description, bool IsSystem, IReadOnlyList<string> Permissions);

public sealed record SaveRoleRequest(string Name, string? Description, IReadOnlyList<string> Permissions);

public sealed record PermissionInfoDto(string Key, string Group);

public sealed record CreateInvitationRequest(string Email, IReadOnlyList<Guid> RoleIds, bool IsProfessional);

public sealed record InvitationDto(Guid Id, string Email, string Status, DateTime ExpiresAtUtc, bool IsProfessional,
    IReadOnlyList<string> Roles, DateTime CreatedAtUtc, string InvitedBy);

// ---------- Perfil ----------

public sealed record ProfileDto(Guid UserId, string Email, bool EmailConfirmed, string FirstName, string LastName, string DisplayName,
    string? Title, string? Specialty, string? LicenseNumber, string? Phone, bool TwoFactorEnabled,
    int? DefaultAppointmentMinutes, NotificationSettingsDto NotificationSettings);

public sealed record UpdateProfileRequest(string FirstName, string LastName, string? Title, string? Phone);

public sealed record UpdateProfessionalProfileRequest(string? Specialty, string? LicenseNumber, int? DefaultAppointmentMinutes);

public sealed record ChangeEmailRequest(string NewEmail, string Password);

public sealed record NotificationSettingsDto(bool EmailEnabled, bool InAppEnabled, bool NotifyTaskAssigned, bool NotifyTaskDue,
    bool NotifyLabUpdates, bool NotifyAppointmentChanges, bool DailyAgendaSummary);

public sealed record AuditLogDto(Guid Id, DateTime OccurredAtUtc, string Action, string EntityType, string? EntityId,
    Guid? UserId, string? UserName, string? Details, string? IpAddress);

public sealed class AuditQuery : Common.PageQuery
{
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public Guid? UserId { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
}
