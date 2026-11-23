using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Odontia.Application.Abstractions;
using Odontia.Application.Common;
using Odontia.Application.Features.Auth;
using Odontia.Application.Features.Organization;
using Odontia.Domain.Authorization;
using Odontia.Domain.Entities;
using Odontia.Domain.Enums;
using Odontia.Infrastructure.Persistence;
using Odontia.UnitTests.QA;

namespace Odontia.UnitTests.Application;

/// <summary>
/// Editar los permisos de un rol no puede dejar el consultorio sin administrador activo (misma regla y bloqueo que
/// UpdateRolesAsync/SetActiveAsync). Por las validaciones anti-escalada, el caso sólo se alcanza cuando los permisos
/// del actor quedaron desactualizados (p. ej. otro administrador lo degradó en paralelo): se reproduce con un actor
/// cuyo contexto tiene todos los permisos pero cuya membresía ya no tiene roles.
/// </summary>
public sealed class RoleUpdateLastAdministratorTests : IDisposable
{
    private readonly QaSqliteDb _qa = new();

    public void Dispose() => _qa.Dispose();

    private static readonly string[] WithoutUsersManage = Permissions.All.Where(p => p != Permissions.UsersManage).ToArray();

    private IMemberService Service(OdontiaDbContext db, ITenantContext tenant) => new MemberService(db, tenant, new NoMessages(), new NullAudit(),
        TimeProvider.System, Options.Create(new AuthOptions()), Options.Create(new FrontendOptions()));

    /// <summary>Crea un rol personalizado con todos los permisos asignado a un nuevo miembro activo.</summary>
    private async Task<Guid> CustomAdministratorRoleAsync()
    {
        await using var db = _qa.Create(_qa.Tenant());
        var role = new Role(_qa.ConsultorioId, "Administración propia", null, false);
        role.SetPermissions(Permissions.All);
        db.AppRoles.Add(role);
        await db.SaveChangesAsync();
        await AddMemberAsync(db, "custom.admin@test.local", role.Id);
        return role.Id;
    }

    private async Task AddMemberAsync(OdontiaDbContext db, string email, Guid roleId)
    {
        var user = new ApplicationUser { Email = email, UserName = email, NormalizedEmail = email.ToUpperInvariant(), NormalizedUserName = email.ToUpperInvariant(), FirstName = "A", LastName = "B" };
        typeof(ApplicationUser).GetProperty(nameof(ApplicationUser.Id))!.SetValue(user, Guid.CreateVersion7());
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var member = new ConsultorioMember(_qa.ConsultorioId, user.Id, false);
        member.ReplaceRoles([roleId]);
        db.Members.Add(member);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Removing_users_manage_from_the_role_of_the_last_administrator_is_rejected()
    {
        var roleId = await CustomAdministratorRoleAsync();

        await using (var db = _qa.Create(_qa.Tenant()))
        {
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                Service(db, _qa.Tenant()).UpdateRoleAsync(roleId, new SaveRoleRequest("Administración propia", null, WithoutUsersManage), CancellationToken.None));
            Assert.Equal("last_administrator", ex.Code);
            Assert.Equal(422, ex.StatusCode);
        }

        await using var check = _qa.Create(_qa.Tenant());
        Assert.True(await check.RolePermissions.AnyAsync(p => p.RoleId == roleId && p.Permission == Permissions.UsersManage));
    }

    [Fact]
    public async Task Removing_users_manage_is_allowed_while_another_administrator_remains()
    {
        var roleId = await CustomAdministratorRoleAsync();
        await using (var setup = _qa.Create(_qa.Tenant()))
            await AddMemberAsync(setup, "system.admin@test.local", SystemRoles.AdministradorId);

        await using var db = _qa.Create(_qa.Tenant());
        var dto = await Service(db, _qa.Tenant()).UpdateRoleAsync(roleId, new SaveRoleRequest("Administración propia", null, WithoutUsersManage), CancellationToken.None);

        Assert.DoesNotContain(Permissions.UsersManage, dto.Permissions);
    }

    private sealed class NoMessages : IMessageDispatcher
    {
        public bool IsChannelConfigured(MessageChannel channel) => false;

        public Task<OutboundMessage> SendEmailAsync(Guid? consultorioId, string to, string subject, string html, string text, string purpose,
            bool containsSecret, string? relatedType, Guid? relatedId, CancellationToken ct) => throw new NotSupportedException();

        public Task<OutboundMessage> SendWhatsAppAsync(Guid? consultorioId, string toPhone, string text, string purpose,
            string? relatedType, Guid? relatedId, CancellationToken ct) => throw new NotSupportedException();
    }
}
