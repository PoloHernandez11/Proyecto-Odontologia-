using Microsoft.AspNetCore.Identity;
using Odontia.Domain.Common;
using Odontia.Domain.Enums;

namespace Odontia.Domain.Entities;

/// <summary>Usuario global (Identity). La pertenencia a consultorios se modela con <see cref="ConsultorioMember"/>.</summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public ApplicationUser()
    {
        Id = Guid.CreateVersion7();
        SecurityStamp = Guid.NewGuid().ToString();
    }

    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    /// <summary>Tratamiento profesional (p. ej. "Dra.", "Dr.").</summary>
    public string? Title { get; set; }
    public string? Specialty { get; set; }
    /// <summary>Matrícula profesional (p. ej. "M.P. 4521").</summary>
    public string? LicenseNumber { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }
    public Guid? LastConsultorioId { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();
    public string DisplayName => string.IsNullOrWhiteSpace(Title) ? FullName : $"{Title} {FullName}";
}

/// <summary>Pertenencia de un usuario a un consultorio, con sus roles dentro de él.</summary>
public class ConsultorioMember : TenantEntity
{
    private readonly List<MemberRole> _roles = [];

    private ConsultorioMember() { }

    public ConsultorioMember(Guid consultorioId, Guid userId, bool isProfessional)
    {
        ConsultorioId = consultorioId;
        UserId = userId;
        IsProfessional = isProfessional;
        IsActive = true;
    }

    public Guid UserId { get; private set; }
    public bool IsActive { get; private set; }
    /// <summary>Habilita al miembro como profesional que atiende turnos (agenda propia).</summary>
    public bool IsProfessional { get; private set; }
    /// <summary>Duración de turno por defecto del profesional (null = la del consultorio).</summary>
    public int? DefaultAppointmentMinutes { get; private set; }
    /// <summary>Color de agenda (hex) opcional para la UI.</summary>
    public string? AgendaColor { get; private set; }
    /// <summary>Contador usado como bloqueo de fila para serializar reservas concurrentes del profesional.</summary>
    public long LockVersion { get; private set; }
    public IReadOnlyCollection<MemberRole> Roles => _roles;

    public ApplicationUser? User { get; private set; }

    public void SetActive(bool active) => IsActive = active;
    public void SetProfessional(bool isProfessional) => IsProfessional = isProfessional;

    public void SetAgendaPreferences(int? defaultMinutes, string? color)
    {
        if (defaultMinutes is not null and (< 5 or > 480))
            throw new DomainException("La duración de turno debe estar entre 5 y 480 minutos.");
        DefaultAppointmentMinutes = defaultMinutes;
        AgendaColor = Guard.Optional(color, "Color", 9);
    }

    public void ReplaceRoles(IEnumerable<Guid> roleIds)
    {
        var ids = roleIds.Distinct().ToList();
        if (ids.Count == 0) throw new DomainException("El usuario debe tener al menos un rol.");
        _roles.RemoveAll(r => !ids.Contains(r.RoleId));
        foreach (var id in ids.Where(id => _roles.All(r => r.RoleId != id)))
            _roles.Add(new MemberRole(Id, id));
    }
}

/// <summary>Rol: conjunto de permisos. ConsultorioId nulo = rol de sistema compartido.</summary>
public class Role : AuditableEntity
{
    private readonly List<RolePermission> _permissions = [];

    private Role() { }

    public Role(Guid? consultorioId, string name, string? description, bool isSystem, Guid? id = null)
    {
        if (id.HasValue) Id = id.Value;
        ConsultorioId = consultorioId;
        IsSystem = isSystem;
        Update(name, description);
    }

    public Guid? ConsultorioId { get; private set; }
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public bool IsSystem { get; private set; }
    public IReadOnlyCollection<RolePermission> Permissions => _permissions;

    public void Update(string name, string? description)
    {
        Name = Guard.Required(name, "Nombre", 64);
        Description = Guard.Optional(description, "Descripción", 250);
    }

    public void SetPermissions(IEnumerable<string> permissions)
    {
        var list = permissions.Distinct().ToList();
        foreach (var p in list)
            if (!Authorization.Permissions.IsValid(p))
                throw new DomainException($"Permiso desconocido: {p}.", "invalid_permission");
        _permissions.RemoveAll(rp => !list.Contains(rp.Permission));
        foreach (var p in list.Where(p => _permissions.All(rp => rp.Permission != p)))
            _permissions.Add(new RolePermission(Id, p));
    }
}

public class RolePermission
{
    private RolePermission() { }
    public RolePermission(Guid roleId, string permission) { RoleId = roleId; Permission = permission; }
    public Guid RoleId { get; private set; }
    public string Permission { get; private set; } = default!;
}

public class MemberRole
{
    private MemberRole() { }
    public MemberRole(Guid memberId, Guid roleId) { MemberId = memberId; RoleId = roleId; }
    public Guid MemberId { get; private set; }
    public Guid RoleId { get; private set; }
    public Role? Role { get; private set; }
}

/// <summary>Invitación para incorporar un usuario a un consultorio. El token sólo se guarda hasheado.</summary>
public class Invitation : TenantEntity
{
    private readonly List<InvitationRole> _roles = [];

    private Invitation() { }

    public Invitation(Guid consultorioId, string email, string tokenHash, DateTime expiresAtUtc, Guid invitedByUserId,
        bool isProfessional, IEnumerable<Guid> roleIds)
    {
        ConsultorioId = consultorioId;
        Email = Guard.Required(email, "Correo", 256).ToLowerInvariant();
        TokenHash = tokenHash;
        ExpiresAtUtc = expiresAtUtc;
        InvitedByUserId = invitedByUserId;
        IsProfessional = isProfessional;
        Status = InvitationStatus.Pending;
        foreach (var r in roleIds.Distinct()) _roles.Add(new InvitationRole(Id, r));
        if (_roles.Count == 0) throw new DomainException("La invitación debe incluir al menos un rol.");
    }

    public string Email { get; private set; } = default!;
    public string TokenHash { get; private set; } = default!;
    public DateTime ExpiresAtUtc { get; private set; }
    public Guid InvitedByUserId { get; private set; }
    public bool IsProfessional { get; private set; }
    public InvitationStatus Status { get; private set; }
    public DateTime? AcceptedAtUtc { get; private set; }
    public Guid? AcceptedByUserId { get; private set; }
    public IReadOnlyCollection<InvitationRole> Roles => _roles;

    public bool IsUsable(DateTime nowUtc) => Status == InvitationStatus.Pending && ExpiresAtUtc > nowUtc;

    public void Accept(Guid userId, DateTime nowUtc)
    {
        if (!IsUsable(nowUtc)) throw new DomainException("La invitación no es válida o expiró.", "invitation_invalid");
        Status = InvitationStatus.Accepted;
        AcceptedAtUtc = nowUtc;
        AcceptedByUserId = userId;
    }

    public void Revoke()
    {
        if (Status != InvitationStatus.Pending) throw new DomainException("Sólo pueden revocarse invitaciones pendientes.");
        Status = InvitationStatus.Revoked;
    }
}

public class InvitationRole
{
    private InvitationRole() { }
    public InvitationRole(Guid invitationId, Guid roleId) { InvitationId = invitationId; RoleId = roleId; }
    public Guid InvitationId { get; private set; }
    public Guid RoleId { get; private set; }
}

/// <summary>
/// Refresh token rotativo. Sólo se guarda el hash SHA-256. Los tokens de una misma "familia"
/// representan una sesión/dispositivo; el reuso de un token rotado revoca toda la familia.
/// </summary>
public class RefreshToken : Entity
{
    private RefreshToken() { }

    public RefreshToken(Guid userId, Guid familyId, string tokenHash, DateTime createdAtUtc, DateTime expiresAtUtc,
        Guid? consultorioId, string? ipAddress, string? userAgent)
    {
        UserId = userId;
        FamilyId = familyId;
        TokenHash = tokenHash;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        ConsultorioId = consultorioId;
        IpAddress = ipAddress?.Length > 64 ? ipAddress[..64] : ipAddress;
        UserAgent = userAgent?.Length > 300 ? userAgent[..300] : userAgent;
    }

    public Guid UserId { get; private set; }
    public Guid FamilyId { get; private set; }
    public string TokenHash { get; private set; } = default!;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public Guid? ConsultorioId { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public string? RevokedReason { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }
    public DateTime? LastUsedAtUtc { get; private set; }

    public bool IsActive(DateTime nowUtc) => RevokedAtUtc is null && ExpiresAtUtc > nowUtc;

    public void Revoke(DateTime nowUtc, string reason, Guid? replacedBy = null)
    {
        if (RevokedAtUtc is not null) return;
        RevokedAtUtc = nowUtc;
        RevokedReason = reason.Length > 64 ? reason[..64] : reason;
        ReplacedByTokenId = replacedBy;
    }

    public void MarkUsed(DateTime nowUtc) => LastUsedAtUtc = nowUtc;
}

/// <summary>Preferencias de notificación del usuario.</summary>
public class UserNotificationSettings
{
    private UserNotificationSettings() { }
    public UserNotificationSettings(Guid userId) { UserId = userId; }

    public Guid UserId { get; private set; }
    public bool EmailEnabled { get; set; } = true;
    public bool InAppEnabled { get; set; } = true;
    public bool NotifyTaskAssigned { get; set; } = true;
    public bool NotifyTaskDue { get; set; } = true;
    public bool NotifyLabUpdates { get; set; } = true;
    public bool NotifyAppointmentChanges { get; set; } = true;
    public bool DailyAgendaSummary { get; set; }
}
