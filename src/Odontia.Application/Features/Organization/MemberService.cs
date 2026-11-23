using System.Text.Encodings.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Odontia.Application.Abstractions;
using Odontia.Application.Common;
using Odontia.Application.Features.Auth;
using Odontia.Domain.Authorization;
using Odontia.Domain.Entities;
using Odontia.Domain.Enums;

namespace Odontia.Application.Features.Organization;

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
        await using var tx = await LockAdministratorsAsync(ct);
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
        await tx.CommitAsync(ct);
        audit.Write(AuditAction.RolesChanged, nameof(ConsultorioMember), m.Id.ToString(),
            $"Roles: [{before}] → [{string.Join(", ", roles.Select(x => x.Name))}]");
        return ToDto(await MembersQuery().FirstAsync(x => x.Id == memberId, ct));
    }

    public async Task<MemberDto> SetActiveAsync(Guid memberId, bool isActive, CancellationToken ct)
    {
        tenant.Require(Permissions.UsersManage);
        await using var tx = await LockAdministratorsAsync(ct);
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
        await tx.CommitAsync(ct);
        audit.Write(AuditAction.MemberActivationChanged, nameof(ConsultorioMember), m.Id.ToString(), isActive ? "Activado" : "Desactivado");
        return ToDto(m);
    }

    /// <summary>
    /// Serializa los cambios de roles/activación del consultorio activo: el UPDATE sobre las filas de sus miembros toma
    /// bloqueos exclusivos (InnoDB) hasta el commit, de modo que dos degradaciones simultáneas no puedan validar cada una
    /// que "queda otro administrador" y dejar el consultorio sin ninguno. Debe ser la primera sentencia de la transacción
    /// para que las lecturas posteriores vean lo confirmado por la operación anterior.
    /// </summary>
    private async Task<IApplicationTransaction> LockAdministratorsAsync(CancellationToken ct)
    {
        var cid = tenant.ConsultorioId;
        var tx = await db.BeginTransactionAsync(ct);
        try
        {
            // Mismo mecanismo que AgendaLock: un UPDATE real (LockVersion + 1) sobre los miembros del consultorio.
            await db.Members.Where(m => m.ConsultorioId == cid)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.LockVersion, m => m.LockVersion + 1), ct);
            return tx;
        }
        catch
        {
            await tx.DisposeAsync();
            throw;
        }
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

    /// <summary>
    /// Impide que editar los permisos de un rol deje el consultorio sin administrador activo: si el rol pierde
    /// users.manage o roles.manage, debe quedar algún miembro activo con ambos permisos considerando los nuevos permisos del rol.
    /// </summary>
    private async Task EnsureAdministratorRemainsAfterRoleChangeAsync(Role role, IEnumerable<string> newPermissions, CancellationToken ct)
    {
        var next = newPermissions.ToHashSet();
        var keepsUsers = next.Contains(Permissions.UsersManage);
        var keepsRoles = next.Contains(Permissions.RolesManage);
        var hadUsers = role.Permissions.Any(p => p.Permission == Permissions.UsersManage);
        var hadRoles = role.Permissions.Any(p => p.Permission == Permissions.RolesManage);
        if ((!hadUsers || keepsUsers) && (!hadRoles || keepsRoles)) return;

        var roleId = role.Id;
        var active = db.Members.Where(m => m.IsActive && m.User!.IsActive);
        var remains = await active
            .Where(m => m.Roles.Any(x => x.RoleId == roleId ? keepsUsers : x.Role!.Permissions.Any(p => p.Permission == Permissions.UsersManage))
                        && m.Roles.Any(x => x.RoleId == roleId ? keepsRoles : x.Role!.Permissions.Any(p => p.Permission == Permissions.RolesManage)))
            .AnyAsync(ct);
        if (remains) return;
        // Sólo se rechaza si el cambio degrada al último administrador (no si el consultorio ya no tenía ninguno).
        var hadAdministrator = await active
            .Where(m => m.Roles.Any(x => x.Role!.Permissions.Any(p => p.Permission == Permissions.UsersManage))
                        && m.Roles.Any(x => x.Role!.Permissions.Any(p => p.Permission == Permissions.RolesManage)))
            .AnyAsync(ct);
        if (hadAdministrator) throw new BusinessRuleException("El consultorio debe conservar al menos un administrador activo.", "last_administrator");
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
        // Mismo bloqueo que UpdateRolesAsync/SetActiveAsync: editar los permisos de un rol puede degradar a sus miembros.
        await using var tx = await LockAdministratorsAsync(ct);
        var role = await db.AppRoles.Include(x => x.Permissions).FirstOrDefaultAsync(x => x.Id == roleId && x.ConsultorioId == cid, ct)
                   ?? throw new NotFoundException("El rol no existe o es un rol de sistema no editable.");
        PermissionPolicy.EnsureCanGrant(tenant, r.Permissions);
        PermissionPolicy.EnsureCanGrant(tenant, role.Permissions.Select(p => p.Permission));
        if (await db.AppRoles.AnyAsync(x => x.Id != roleId && (x.ConsultorioId == cid || x.ConsultorioId == null) && x.Name == r.Name.Trim(), ct))
            throw new ConflictException("Ya existe un rol con ese nombre.");
        if (await db.MemberRoles.AnyAsync(mr => mr.RoleId == roleId && db.Members.Any(m => m.Id == mr.MemberId && m.UserId == tenant.UserId), ct))
            throw new ForbiddenAccessException("No puede modificar un rol que tiene asignado.");
        await EnsureAdministratorRemainsAfterRoleChangeAsync(role, r.Permissions, ct);
        role.Update(r.Name, r.Description);
        role.SetPermissions(r.Permissions);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
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
        await messages.SendEmailAsync(tenant.ConsultorioId, email, $"Invitación a {consultorio} - Odentix",
            $"<p>Fue invitado/a a sumarse a <strong>{HtmlEncoder.Default.Encode(consultorio)}</strong> en Odentix. Acepte la invitación aquí: <a href=\"{HtmlEncoder.Default.Encode(link)}\">aceptar invitación</a>.</p>",
            $"Fue invitado/a a sumarse a {consultorio} en Odentix. Acepte la invitación: {link}", "invitation", true, nameof(Invitation), inv.Id, ct);
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
