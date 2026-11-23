using Microsoft.EntityFrameworkCore;
using Odontia.Application.Common;
using Odontia.Domain.Entities;
using Odontia.Domain.Enums;

namespace Odontia.Application.Features.Auth;

internal sealed partial class AuthService
{
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
        await using var tx = await db.BeginTransactionAsync(ct);

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
        await audit.WriteAsync(AuditAction.InvitationAccepted, nameof(Invitation), inv.Id.ToString(), null, inv.ConsultorioId, user.Id);
        return new MessageResponse("Invitación aceptada. Ya puede iniciar sesión.");
    }

    private async Task<Invitation> FindUsableInvitationAsync(string token, CancellationToken ct)
    {
        var hash = SecureTokens.Hash(token ?? "");
        var inv = await db.Invitations.IgnoreQueryFilters().Include(i => i.Roles).FirstOrDefaultAsync(i => i.TokenHash == hash, ct);
        if (inv is null || !inv.IsUsable(Now)) throw new BusinessRuleException("La invitación es inválida, fue revocada o expiró.", "invitation_invalid");
        return inv;
    }
}
