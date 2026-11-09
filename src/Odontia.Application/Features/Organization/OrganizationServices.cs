using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Odontia.Application.Abstractions;
using Odontia.Application.Common;
using Odontia.Application.Features.Auth;
using Odontia.Domain.Authorization;
using Odontia.Domain.Entities;
using Odontia.Domain.Enums;

namespace Odontia.Application.Features.Organization;

public interface IConsultorioService
{
    Task<ConsultorioDto> GetCurrentAsync(CancellationToken ct);
    Task<ConsultorioDto> UpdateAsync(UpdateConsultorioRequest request, CancellationToken ct);
    Task<ConsultorioDto> UpdateSettingsAsync(UpdateConsultorioSettingsRequest request, CancellationToken ct);
    Task<IReadOnlyList<GabineteDto>> GetGabinetesAsync(bool includeInactive, CancellationToken ct);
    Task<GabineteDto> CreateGabineteAsync(SaveGabineteRequest request, CancellationToken ct);
    Task<GabineteDto> UpdateGabineteAsync(Guid id, SaveGabineteRequest request, CancellationToken ct);
    Task<IReadOnlyList<ProfessionalDto>> GetProfessionalsAsync(CancellationToken ct);
}

internal sealed class ConsultorioService(IApplicationDbContext db, ITenantContext tenant) : IConsultorioService
{
    public async Task<ConsultorioDto> GetCurrentAsync(CancellationToken ct)
    {
        tenant.Require(Permissions.ConsultorioRead);
        return ToDto(await LoadAsync(ct));
    }

    public async Task<ConsultorioDto> UpdateAsync(UpdateConsultorioRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.ConsultorioManage);
        var c = await LoadAsync(ct);
        c.UpdateDetails(r.Name, r.LegalName, r.TaxId, r.Phone, r.Email, r.Website, r.AddressLine, r.City, r.Province, r.PostalCode, r.Country);
        await db.SaveChangesAsync(ct);
        return ToDto(c);
    }

    public async Task<ConsultorioDto> UpdateSettingsAsync(UpdateConsultorioSettingsRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.ConsultorioManage);
        AuthService.EnsureTimeZone(r.TimeZoneId);
        var c = await LoadAsync(ct);
        c.UpdateSettings(r.TimeZoneId, r.DefaultAppointmentMinutes, r.SlotIntervalMinutes, r.ReminderHoursBefore);
        await db.SaveChangesAsync(ct);
        return ToDto(c);
    }

    public async Task<IReadOnlyList<GabineteDto>> GetGabinetesAsync(bool includeInactive, CancellationToken ct)
    {
        tenant.Require(Permissions.ConsultorioRead);
        return await db.Gabinetes.Where(g => includeInactive || g.IsActive).OrderBy(g => g.Name)
            .Select(g => new GabineteDto(g.Id, g.Name, g.Description, g.IsActive)).ToListAsync(ct);
    }

    public async Task<GabineteDto> CreateGabineteAsync(SaveGabineteRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.ConsultorioManage);
        if (await db.Gabinetes.AnyAsync(g => g.Name == r.Name.Trim(), ct)) throw new ConflictException("Ya existe un consultorio físico con ese nombre.");
        var g = new Gabinete(tenant.ConsultorioId, r.Name, r.Description);
        g.SetActive(r.IsActive);
        db.Gabinetes.Add(g);
        await db.SaveChangesAsync(ct);
        return new GabineteDto(g.Id, g.Name, g.Description, g.IsActive);
    }

    public async Task<GabineteDto> UpdateGabineteAsync(Guid id, SaveGabineteRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.ConsultorioManage);
        var g = await db.Gabinetes.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException();
        if (await db.Gabinetes.AnyAsync(x => x.Id != id && x.Name == r.Name.Trim(), ct)) throw new ConflictException("Ya existe un consultorio físico con ese nombre.");
        g.Rename(r.Name, r.Description);
        g.SetActive(r.IsActive);
        await db.SaveChangesAsync(ct);
        return new GabineteDto(g.Id, g.Name, g.Description, g.IsActive);
    }

    public async Task<IReadOnlyList<ProfessionalDto>> GetProfessionalsAsync(CancellationToken ct)
    {
        tenant.Require(Permissions.AgendaRead);
        var defaultMinutes = await db.Consultorios.Where(c => c.Id == tenant.ConsultorioId).Select(c => c.DefaultAppointmentMinutes).FirstAsync(ct);
        var list = await db.Members.Where(m => m.IsActive && m.IsProfessional && m.User!.IsActive)
            .Select(m => new { m.UserId, m.User!.FirstName, m.User.LastName, m.User.Title, m.User.Specialty, m.User.LicenseNumber, m.DefaultAppointmentMinutes, m.AgendaColor })
            .ToListAsync(ct);
        return list
            .Where(m => tenant.CanActOnProfessional(m.UserId))
            .Select(m => new ProfessionalDto(m.UserId, Display(m.Title, m.FirstName, m.LastName), m.Title, m.Specialty, m.LicenseNumber,
                m.DefaultAppointmentMinutes ?? defaultMinutes, m.AgendaColor))
            .OrderBy(p => p.DisplayName).ToList();
    }

    internal static string Display(string? title, string first, string last) =>
        string.IsNullOrWhiteSpace(title) ? $"{first} {last}" : $"{title} {first} {last}";

    private async Task<Consultorio> LoadAsync(CancellationToken ct) =>
        await db.Consultorios.FirstOrDefaultAsync(c => c.Id == tenant.ConsultorioId, ct) ?? throw new NotFoundException();

    private static ConsultorioDto ToDto(Consultorio c) => new(c.Id, c.Name, c.LegalName, c.TaxId, c.Phone, c.Email, c.Website, c.AddressLine,
        c.City, c.Province, c.PostalCode, c.Country, c.TimeZoneId, c.DefaultAppointmentMinutes, c.SlotIntervalMinutes, c.ReminderHoursBefore, c.IsActive);
}

public interface IMemberService
{
    Task<IReadOnlyList<MemberDto>> GetMembersAsync(bool includeInactive, CancellationToken ct);
    Task<MemberDto> GetMemberAsync(Guid memberId, CancellationToken ct);
    Task<MemberDto> UpdateMemberAsync(Guid memberId, UpdateMemberRequest request, CancellationToken ct);
    Task<MemberDto> UpdateRolesAsync(Guid memberId, UpdateMemberRolesRequest request, CancellationToken ct);
    Task<MemberDto> SetActiveAsync(Guid memberId, bool isActive, CancellationToken ct);
    Task<IReadOnlyList<RoleDto>> GetRolesAsync(CancellationToken ct);
    Task<RoleDto> CreateRoleAsync(SaveRoleRequest request, CancellationToken ct);
    Task<RoleDto> UpdateRoleAsync(Guid roleId, SaveRoleRequest request, CancellationToken ct);
    Task DeleteRoleAsync(Guid roleId, CancellationToken ct);
    IReadOnlyList<PermissionInfoDto> GetPermissionCatalog();
    Task<IReadOnlyList<InvitationDto>> GetInvitationsAsync(CancellationToken ct);
    Task<InvitationDto> InviteAsync(CreateInvitationRequest request, CancellationToken ct);
    Task RevokeInvitationAsync(Guid invitationId, CancellationToken ct);
}

internal sealed class MemberService(
    IApplicationDbContext db,
    ITenantContext tenant,
    IMessageDispatcher messages,
    IAuditWriter audit,
    TimeProvider clock,
    IOptions<AuthOptions> authOptions,
    IOptions<FrontendOptions> frontendOptions) : IMemberService
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<IReadOnlyList<MemberDto>> GetMembersAsync(bool includeInactive, CancellationToken ct)
    {
        tenant.Require(Permissions.UsersRead);
        var members = await MembersQuery().Where(m => includeInactive || m.IsActive).ToListAsync(ct);
        return members.Select(ToDto).OrderBy(m => m.LastName).ThenBy(m => m.FirstName).ToList();
    }

    public async Task<MemberDto> GetMemberAsync(Guid memberId, CancellationToken ct)
    {
        tenant.Require(Permissions.UsersRead);
        return ToDto(await MembersQuery().FirstOrDefaultAsync(m => m.Id == memberId, ct) ?? throw new NotFoundException());
    }

    public async Task<MemberDto> UpdateMemberAsync(Guid memberId, UpdateMemberRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.UsersManage);
        var m = await MembersQuery().FirstOrDefaultAsync(x => x.Id == memberId, ct) ?? throw new NotFoundException();
        m.SetProfessional(r.IsProfessional);
        m.SetAgendaPreferences(r.DefaultAppointmentMinutes, r.AgendaColor);
        await db.SaveChangesAsync(ct);
        return ToDto(m);
    }

    public async Task<MemberDto> UpdateRolesAsync(Guid memberId, UpdateMemberRolesRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.UsersManage);
        var m = await MembersQuery().FirstOrDefaultAsync(x => x.Id == memberId, ct) ?? throw new NotFoundException();
        if (m.UserId == tenant.UserId) throw new ForbiddenAccessException("No puede modificar sus propios roles.");

        var roles = await LoadAssignableRolesAsync(r.RoleIds, ct);
        PermissionPolicy.EnsureCanGrant(tenant, roles.SelectMany(x => x.Permissions.Select(p => p.Permission)));
        // Tampoco puede quitar roles que otorgan permisos que el actor no posee (evita degradar a superiores).
        PermissionPolicy.EnsureCanGrant(tenant, m.Roles.SelectMany(x => x.Role!.Permissions.Select(p => p.Permission)));

        var before = string.Join(", ", m.Roles.Select(x => x.Role!.Name));
        m.ReplaceRoles(roles.Select(x => x.Id));
        if (roles.Any(x => SystemRoles.IsProfessionalRole(x.Id))) m.SetProfessional(true);
        await EnsureAdministratorRemainsAsync(m.Id, roles.SelectMany(x => x.Permissions.Select(p => p.Permission)).ToHashSet(), m.IsActive, ct);
        await db.SaveChangesAsync(ct);
        audit.Write(AuditAction.RolesChanged, nameof(ConsultorioMember), m.Id.ToString(),
            $"Roles: [{before}] → [{string.Join(", ", roles.Select(x => x.Name))}]");
        return ToDto(await MembersQuery().FirstAsync(x => x.Id == memberId, ct));
    }

    public async Task<MemberDto> SetActiveAsync(Guid memberId, bool isActive, CancellationToken ct)
    {
        tenant.Require(Permissions.UsersManage);
        var m = await MembersQuery().FirstOrDefaultAsync(x => x.Id == memberId, ct) ?? throw new NotFoundException();
        if (m.UserId == tenant.UserId) throw new ForbiddenAccessException("No puede desactivar su propio usuario.");
        PermissionPolicy.EnsureCanGrant(tenant, m.Roles.SelectMany(x => x.Role!.Permissions.Select(p => p.Permission)));
        m.SetActive(isActive);
        if (!isActive) await EnsureAdministratorRemainsAsync(m.Id, [], false, ct);
        await db.SaveChangesAsync(ct);
        if (!isActive)
        {
            // Revoca las sesiones del usuario asociadas a este consultorio.
            var tokens = await db.RefreshTokens.Where(t => t.UserId == m.UserId && t.ConsultorioId == tenant.ConsultorioId && t.RevokedAtUtc == null).ToListAsync(ct);
            foreach (var t in tokens) t.Revoke(Now, "member_deactivated");
            await db.SaveChangesAsync(ct);
        }
        audit.Write(AuditAction.MemberActivationChanged, nameof(ConsultorioMember), m.Id.ToString(), isActive ? "Activado" : "Desactivado");
        return ToDto(m);
    }

    /// <summary>Impide dejar el consultorio sin al menos un administrador activo.</summary>
    private async Task EnsureAdministratorRemainsAsync(Guid changedMemberId, HashSet<string> newPermissions, bool stillActive, CancellationToken ct)
    {
        if (stillActive && newPermissions.Contains(Permissions.UsersManage) && newPermissions.Contains(Permissions.RolesManage)) return;
        var others = await db.Members.Where(m => m.Id != changedMemberId && m.IsActive && m.User!.IsActive)
            .Where(m => m.Roles.Any(r => r.Role!.Permissions.Any(p => p.Permission == Permissions.UsersManage))
                        && m.Roles.Any(r => r.Role!.Permissions.Any(p => p.Permission == Permissions.RolesManage)))
            .AnyAsync(ct);
        if (!others) throw new BusinessRuleException("El consultorio debe conservar al menos un administrador activo.", "last_administrator");
    }

    private async Task<List<Role>> LoadAssignableRolesAsync(IReadOnlyList<Guid> roleIds, CancellationToken ct)
    {
        var ids = roleIds.Distinct().ToList();
        if (ids.Count == 0) throw new RequestValidationException("roleIds", "Debe indicar al menos un rol.");
        var cid = tenant.ConsultorioId;
        var roles = await db.AppRoles.Include(r => r.Permissions)
            .Where(r => ids.Contains(r.Id) && (r.ConsultorioId == null || r.ConsultorioId == cid)).ToListAsync(ct);
        if (roles.Count != ids.Count) throw new RequestValidationException("roleIds", "Uno o más roles no existen en el consultorio.");
        return roles;
    }

    // ---------------- Roles ----------------

    public async Task<IReadOnlyList<RoleDto>> GetRolesAsync(CancellationToken ct)
    {
        tenant.Require(Permissions.UsersRead);
        var cid = tenant.ConsultorioId;
        var roles = await db.AppRoles.Include(r => r.Permissions).Where(r => r.ConsultorioId == null || r.ConsultorioId == cid)
            .OrderByDescending(r => r.IsSystem).ThenBy(r => r.Name).ToListAsync(ct);
        return roles.Select(ToRoleDto).ToList();
    }

    public async Task<RoleDto> CreateRoleAsync(SaveRoleRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.RolesManage);
        PermissionPolicy.EnsureCanGrant(tenant, r.Permissions);
        var cid = tenant.ConsultorioId;
        if (await db.AppRoles.AnyAsync(x => (x.ConsultorioId == cid || x.ConsultorioId == null) && x.Name == r.Name.Trim(), ct))
            throw new ConflictException("Ya existe un rol con ese nombre.");
        var role = new Role(cid, r.Name, r.Description, false);
        role.SetPermissions(r.Permissions);
        db.AppRoles.Add(role);
        await db.SaveChangesAsync(ct);
        return ToRoleDto(role);
    }

    public async Task<RoleDto> UpdateRoleAsync(Guid roleId, SaveRoleRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.RolesManage);
        var cid = tenant.ConsultorioId;
        var role = await db.AppRoles.Include(x => x.Permissions).FirstOrDefaultAsync(x => x.Id == roleId && x.ConsultorioId == cid, ct)
                   ?? throw new NotFoundException("El rol no existe o es un rol de sistema no editable.");
        PermissionPolicy.EnsureCanGrant(tenant, r.Permissions);
        PermissionPolicy.EnsureCanGrant(tenant, role.Permissions.Select(p => p.Permission));
        if (await db.AppRoles.AnyAsync(x => x.Id != roleId && (x.ConsultorioId == cid || x.ConsultorioId == null) && x.Name == r.Name.Trim(), ct))
            throw new ConflictException("Ya existe un rol con ese nombre.");
        if (await db.MemberRoles.AnyAsync(mr => mr.RoleId == roleId && db.Members.Any(m => m.Id == mr.MemberId && m.UserId == tenant.UserId), ct))
            throw new ForbiddenAccessException("No puede modificar un rol que tiene asignado.");
        role.Update(r.Name, r.Description);
        role.SetPermissions(r.Permissions);
        await db.SaveChangesAsync(ct);
        audit.Write(AuditAction.RolesChanged, nameof(Role), role.Id.ToString(), $"Permisos: {string.Join(",", r.Permissions)}");
        return ToRoleDto(role);
    }

    public async Task DeleteRoleAsync(Guid roleId, CancellationToken ct)
    {
        tenant.Require(Permissions.RolesManage);
        var cid = tenant.ConsultorioId;
        var role = await db.AppRoles.Include(x => x.Permissions).FirstOrDefaultAsync(x => x.Id == roleId && x.ConsultorioId == cid, ct)
                   ?? throw new NotFoundException("El rol no existe o es un rol de sistema.");
        if (await db.MemberRoles.AnyAsync(mr => mr.RoleId == roleId, ct))
            throw new ConflictException("El rol está asignado a usuarios; reasígnelos antes de eliminarlo.");
        if (await db.Invitations.AnyAsync(i => i.Status == InvitationStatus.Pending && i.Roles.Any(x => x.RoleId == roleId), ct))
            throw new ConflictException("El rol está incluido en invitaciones pendientes.");
        db.AppRoles.Remove(role);
        await db.SaveChangesAsync(ct);
    }

    public IReadOnlyList<PermissionInfoDto> GetPermissionCatalog()
    {
        tenant.Require(Permissions.UsersRead);
        return Permissions.All.Select(p => new PermissionInfoDto(p, p.Split('.')[0])).ToList();
    }

    // ---------------- Invitaciones ----------------

    public async Task<IReadOnlyList<InvitationDto>> GetInvitationsAsync(CancellationToken ct)
    {
        tenant.Require(Permissions.UsersManage);
        var list = await db.Invitations.Include(i => i.Roles).OrderByDescending(i => i.CreatedAtUtc).Take(200).ToListAsync(ct);
        return await ToInvitationDtosAsync(list, ct);
    }

    public async Task<InvitationDto> InviteAsync(CreateInvitationRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.UsersManage);
        var email = r.Email.Trim().ToLowerInvariant();
        var roles = await LoadAssignableRolesAsync(r.RoleIds, ct);
        PermissionPolicy.EnsureCanGrant(tenant, roles.SelectMany(x => x.Permissions.Select(p => p.Permission)));

        var normalized = email.ToUpperInvariant();
        if (await db.Members.AnyAsync(m => m.IsActive && m.User!.NormalizedEmail == normalized, ct))
            throw new ConflictException("El usuario ya pertenece al consultorio.");

        foreach (var pending in await db.Invitations.Where(i => i.Email == email && i.Status == InvitationStatus.Pending).ToListAsync(ct))
            pending.Revoke();

        var raw = SecureTokens.Create(32);
        var inv = new Invitation(tenant.ConsultorioId, email, SecureTokens.Hash(raw), Now.AddDays(authOptions.Value.InvitationDays),
            tenant.UserId, r.IsProfessional || roles.Any(x => SystemRoles.IsProfessionalRole(x.Id)), roles.Select(x => x.Id));
        db.Invitations.Add(inv);
        await db.SaveChangesAsync(ct);

        var consultorio = await db.Consultorios.Where(c => c.Id == tenant.ConsultorioId).Select(c => c.Name).FirstAsync(ct);
        var f = frontendOptions.Value;
        var link = $"{f.BaseUrl.TrimEnd('/')}{f.AcceptInvitationPath}?token={Uri.EscapeDataString(raw)}";
        await messages.SendEmailAsync(tenant.ConsultorioId, email, $"Invitación a {consultorio} - Odontia",
            $"<p>Fue invitado/a a sumarse a <strong>{HtmlEncoder.Default.Encode(consultorio)}</strong> en Odontia. Acepte la invitación aquí: <a href=\"{HtmlEncoder.Default.Encode(link)}\">aceptar invitación</a>.</p>",
            $"Fue invitado/a a sumarse a {consultorio} en Odontia. Acepte la invitación: {link}", "invitation", true, nameof(Invitation), inv.Id, ct);
        audit.Write(AuditAction.InvitationSent, nameof(Invitation), inv.Id.ToString(), $"Roles: {string.Join(", ", roles.Select(x => x.Name))}");
        return (await ToInvitationDtosAsync([inv], ct))[0];
    }

    public async Task RevokeInvitationAsync(Guid invitationId, CancellationToken ct)
    {
        tenant.Require(Permissions.UsersManage);
        var inv = await db.Invitations.FirstOrDefaultAsync(i => i.Id == invitationId, ct) ?? throw new NotFoundException();
        inv.Revoke();
        await db.SaveChangesAsync(ct);
    }

    private async Task<List<InvitationDto>> ToInvitationDtosAsync(List<Invitation> list, CancellationToken ct)
    {
        var roleIds = list.SelectMany(i => i.Roles.Select(r => r.RoleId)).Distinct().ToList();
        var roleNames = await db.AppRoles.Where(r => roleIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.Name, ct);
        var inviterIds = list.Select(i => i.InvitedByUserId).Distinct().ToList();
        var inviters = await db.Users.Where(u => inviterIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FirstName + " " + u.LastName, ct);
        return list.Select(i => new InvitationDto(i.Id, i.Email,
            i.Status == InvitationStatus.Pending && i.ExpiresAtUtc <= Now ? "Expired" : i.Status.ToString(),
            i.ExpiresAtUtc, i.IsProfessional, i.Roles.Select(r => roleNames.GetValueOrDefault(r.RoleId, "?")).ToList(), i.CreatedAtUtc,
            inviters.GetValueOrDefault(i.InvitedByUserId, "?"))).ToList();
    }

    private IQueryable<ConsultorioMember> MembersQuery() =>
        db.Members.Include(m => m.User).Include(m => m.Roles).ThenInclude(r => r.Role).ThenInclude(r => r!.Permissions);

    private static MemberDto ToDto(ConsultorioMember m) => new(m.Id, m.UserId, m.User!.Email!, m.User.FirstName, m.User.LastName,
        m.User.DisplayName, m.IsActive, m.IsProfessional, m.User.IsActive,
        m.Roles.Select(r => new RoleSummaryDto(r.RoleId, r.Role?.Name ?? "?", r.Role?.IsSystem ?? false)).ToList(),
        m.User.LastLoginAtUtc, m.DefaultAppointmentMinutes, m.AgendaColor);

    private static RoleDto ToRoleDto(Role r) => new(r.Id, r.Name, r.Description, r.IsSystem, r.Permissions.Select(p => p.Permission).Order().ToList());
}

/// <summary>Reglas anti-escalada de privilegios.</summary>
public static class PermissionPolicy
{
    /// <summary>Un usuario sólo puede otorgar (o quitar) permisos que él mismo posee.</summary>
    public static void EnsureCanGrant(ITenantContext tenant, IEnumerable<string> permissions)
    {
        var missing = permissions.Distinct().Where(p => !tenant.Has(p)).ToList();
        if (missing.Count > 0)
            throw new ForbiddenAccessException($"No puede otorgar permisos que no posee: {string.Join(", ", missing)}.");
    }
}

public interface IProfileService
{
    Task<ProfileDto> GetAsync(CancellationToken ct);
    Task<ProfileDto> UpdateAsync(UpdateProfileRequest request, CancellationToken ct);
    Task<ProfileDto> UpdateProfessionalAsync(UpdateProfessionalProfileRequest request, CancellationToken ct);
    Task<MessageResponse> ChangeEmailAsync(ChangeEmailRequest request, CancellationToken ct);
    Task<NotificationSettingsDto> UpdateNotificationSettingsAsync(NotificationSettingsDto request, CancellationToken ct);
}

internal sealed class ProfileService(UserManager<ApplicationUser> users, IApplicationDbContext db, ITenantContext tenant,
    IMessageDispatcher messages, IOptions<FrontendOptions> front) : IProfileService
{
    public async Task<ProfileDto> GetAsync(CancellationToken ct)
    {
        var user = await LoadUserAsync();
        return await ToDtoAsync(user, ct);
    }

    public async Task<ProfileDto> UpdateAsync(UpdateProfileRequest r, CancellationToken ct)
    {
        var user = await LoadUserAsync();
        user.FirstName = r.FirstName.Trim();
        user.LastName = r.LastName.Trim();
        user.Title = string.IsNullOrWhiteSpace(r.Title) ? null : r.Title.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(r.Phone) ? null : r.Phone.Trim();
        await users.UpdateAsync(user);
        return await ToDtoAsync(user, ct);
    }

    public async Task<ProfileDto> UpdateProfessionalAsync(UpdateProfessionalProfileRequest r, CancellationToken ct)
    {
        var user = await LoadUserAsync();
        user.Specialty = string.IsNullOrWhiteSpace(r.Specialty) ? null : r.Specialty.Trim();
        user.LicenseNumber = string.IsNullOrWhiteSpace(r.LicenseNumber) ? null : r.LicenseNumber.Trim();
        await users.UpdateAsync(user);
        if (tenant.HasTenant)
        {
            var member = await db.Members.FirstAsync(m => m.Id == tenant.MemberId, ct);
            member.SetAgendaPreferences(r.DefaultAppointmentMinutes, member.AgendaColor);
            await db.SaveChangesAsync(ct);
        }
        return await ToDtoAsync(user, ct);
    }

    public async Task<MessageResponse> ChangeEmailAsync(ChangeEmailRequest r, CancellationToken ct)
    {
        var user = await LoadUserAsync();
        if (!await users.CheckPasswordAsync(user, r.Password))
            throw new RequestValidationException("password", "La contraseña es incorrecta.");
        var newEmail = r.NewEmail.Trim().ToLowerInvariant();
        const string msg = "Si el correo está disponible, se enviará un enlace de confirmación a la nueva dirección.";
        if (await users.FindByEmailAsync(newEmail) is not null) return new MessageResponse(msg);
        var token = await users.GenerateChangeEmailTokenAsync(user, newEmail);
        // Se aplica de inmediato con el token (el usuario ya se autenticó con contraseña); se notifica a ambas direcciones.
        var result = await users.ChangeEmailAsync(user, newEmail, token);
        if (!result.Succeeded) throw new BusinessRuleException("No fue posible cambiar el correo.");
        await users.SetUserNameAsync(user, newEmail);
        user.EmailConfirmed = false;
        await users.UpdateAsync(user);
        var confirm = await users.GenerateEmailConfirmationTokenAsync(user);
        var enc = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(confirm)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var link = $"{front.Value.BaseUrl.TrimEnd('/')}{front.Value.ConfirmEmailPath}?userId={user.Id}&token={Uri.EscapeDataString(enc)}";
        await messages.SendEmailAsync(null, newEmail, "Confirme su nuevo correo - Odontia", $"<p>Confirme su nuevo correo: <a href=\"{HtmlEncoder.Default.Encode(link)}\">confirmar</a></p>",
            $"Confirme su nuevo correo: {link}", "email_change", true, "User", user.Id, ct);
        return new MessageResponse(msg);
    }

    public async Task<NotificationSettingsDto> UpdateNotificationSettingsAsync(NotificationSettingsDto r, CancellationToken ct)
    {
        var uid = tenant.UserId;
        var s = await db.NotificationSettings.FirstOrDefaultAsync(x => x.UserId == uid, ct);
        if (s is null) { s = new UserNotificationSettings(uid); db.NotificationSettings.Add(s); }
        s.EmailEnabled = r.EmailEnabled;
        s.InAppEnabled = r.InAppEnabled;
        s.NotifyTaskAssigned = r.NotifyTaskAssigned;
        s.NotifyTaskDue = r.NotifyTaskDue;
        s.NotifyLabUpdates = r.NotifyLabUpdates;
        s.NotifyAppointmentChanges = r.NotifyAppointmentChanges;
        s.DailyAgendaSummary = r.DailyAgendaSummary;
        await db.SaveChangesAsync(ct);
        return ToSettings(s);
    }

    private async Task<ApplicationUser> LoadUserAsync() =>
        await users.FindByIdAsync(tenant.UserId.ToString()) ?? throw new UnauthorizedException();

    private async Task<ProfileDto> ToDtoAsync(ApplicationUser u, CancellationToken ct)
    {
        var settings = await db.NotificationSettings.FirstOrDefaultAsync(x => x.UserId == u.Id, ct) ?? new UserNotificationSettings(u.Id);
        int? minutes = tenant.HasTenant
            ? await db.Members.Where(m => m.Id == tenant.MemberId).Select(m => m.DefaultAppointmentMinutes).FirstOrDefaultAsync(ct)
            : null;
        return new ProfileDto(u.Id, u.Email!, u.EmailConfirmed, u.FirstName, u.LastName, u.DisplayName, u.Title, u.Specialty, u.LicenseNumber,
            u.PhoneNumber, u.TwoFactorEnabled, minutes, ToSettings(settings));
    }

    private static NotificationSettingsDto ToSettings(UserNotificationSettings s) => new(s.EmailEnabled, s.InAppEnabled, s.NotifyTaskAssigned,
        s.NotifyTaskDue, s.NotifyLabUpdates, s.NotifyAppointmentChanges, s.DailyAgendaSummary);
}

public interface IAuditQueryService
{
    Task<PagedResult<AuditLogDto>> SearchAsync(AuditQuery query, CancellationToken ct);
}

internal sealed class AuditQueryService(IApplicationDbContext db, ITenantContext tenant) : IAuditQueryService
{
    public async Task<PagedResult<AuditLogDto>> SearchAsync(AuditQuery q, CancellationToken ct)
    {
        tenant.Require(Permissions.AuditRead);
        var cid = tenant.ConsultorioId;
        var query = db.AuditLogs.Where(a => a.ConsultorioId == cid);
        if (!string.IsNullOrWhiteSpace(q.EntityType)) query = query.Where(a => a.EntityType == q.EntityType);
        if (!string.IsNullOrWhiteSpace(q.EntityId)) query = query.Where(a => a.EntityId == q.EntityId);
        if (q.UserId is not null) query = query.Where(a => a.UserId == q.UserId);
        if (q.FromUtc is not null) query = query.Where(a => a.OccurredAtUtc >= q.FromUtc);
        if (q.ToUtc is not null) query = query.Where(a => a.OccurredAtUtc < q.ToUtc);
        var page = await query.OrderByDescending(a => a.OccurredAtUtc).ToPagedAsync(q, ct);
        var ids = page.Items.Where(a => a.UserId != null).Select(a => a.UserId!.Value).Distinct().ToList();
        var names = await db.Users.Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FirstName + " " + u.LastName, ct);
        return new PagedResult<AuditLogDto>(page.Items.Select(a => new AuditLogDto(a.Id, a.OccurredAtUtc, a.Action.ToString(), a.EntityType,
            a.EntityId, a.UserId, a.UserId is null ? null : names.GetValueOrDefault(a.UserId.Value), a.Details, a.IpAddress)).ToList(),
            page.Page, page.PageSize, page.TotalCount);
    }
}
