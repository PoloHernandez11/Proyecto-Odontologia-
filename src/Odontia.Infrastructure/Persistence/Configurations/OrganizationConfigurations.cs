using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Odontia.Domain.Authorization;
using Odontia.Domain.Entities;

namespace Odontia.Infrastructure.Persistence.Configurations;

internal sealed class ConsultorioConfiguration : IEntityTypeConfiguration<Consultorio>
{
    public void Configure(EntityTypeBuilder<Consultorio> b)
    {
        b.ToTable("Consultorios");
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.LegalName).HasMaxLength(200);
        b.Property(x => x.TaxId).HasMaxLength(20);
        b.Property(x => x.Phone).HasMaxLength(40);
        b.Property(x => x.AddressLine).HasMaxLength(250);
        b.Property(x => x.City).HasMaxLength(100);
        b.Property(x => x.Province).HasMaxLength(100);
        b.Property(x => x.PostalCode).HasMaxLength(20);
        b.Property(x => x.Country).HasMaxLength(2).IsRequired();
        b.Property(x => x.TimeZoneId).HasMaxLength(64).IsRequired();
    }
}

internal sealed class GabineteConfiguration : IEntityTypeConfiguration<Gabinete>
{
    public void Configure(EntityTypeBuilder<Gabinete> b)
    {
        b.ToTable("Gabinetes");
        b.HasConsultorio();
        b.Property(x => x.Name).HasMaxLength(80).IsRequired();
        b.Property(x => x.Description).HasMaxLength(250);
        b.HasIndex(x => new { x.ConsultorioId, x.Name }).IsUnique();
    }
}

internal sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> b)
    {
        b.Property(x => x.FirstName).HasMaxLength(80).IsRequired();
        b.Property(x => x.LastName).HasMaxLength(80).IsRequired();
        b.Property(x => x.Title).HasMaxLength(20);
        b.Property(x => x.Specialty).HasMaxLength(120);
        b.Property(x => x.LicenseNumber).HasMaxLength(40);
        b.Property(x => x.PhoneNumber).HasMaxLength(40);
        b.Ignore(x => x.FullName);
        b.Ignore(x => x.DisplayName);
    }
}

internal sealed class ConsultorioMemberConfiguration : IEntityTypeConfiguration<ConsultorioMember>
{
    public void Configure(EntityTypeBuilder<ConsultorioMember> b)
    {
        b.ToTable("ConsultorioMembers");
        b.HasConsultorio();
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        // Clave alternativa: permite FK compuestas (ConsultorioId, UserId) desde turnos, agenda, clínica, etc.
        b.HasAlternateKey(x => new { x.ConsultorioId, x.UserId });
        b.Property(x => x.AgendaColor).HasMaxLength(9);
        b.Property(x => x.LockVersion).IsConcurrencyToken(false);
        b.HasMany(x => x.Roles).WithOne().HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Roles).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> b)
    {
        b.ToTable("Roles");
        b.Property(x => x.Name).HasMaxLength(64).IsRequired();
        b.Property(x => x.Description).HasMaxLength(250);
        b.HasOne<Consultorio>().WithMany().HasForeignKey(x => x.ConsultorioId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.ConsultorioId, x.Name }).IsUnique();
        b.HasMany(x => x.Permissions).WithOne().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Permissions).UsePropertyAccessMode(PropertyAccessMode.Field);

        var seedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        b.HasData(
            new { Id = SystemRoles.AdministradorId, ConsultorioId = (Guid?)null, Name = SystemRoles.Administrador, Description = (string?)"Gestión del consultorio, usuarios, roles y configuración", IsSystem = true, CreatedAtUtc = seedDate },
            new { Id = SystemRoles.OdontologoId, ConsultorioId = (Guid?)null, Name = SystemRoles.Odontologo, Description = (string?)"Atención clínica: agenda propia, historia clínica, odontograma, tratamientos", IsSystem = true, CreatedAtUtc = seedDate },
            new { Id = SystemRoles.SecretariaId, ConsultorioId = (Guid?)null, Name = SystemRoles.Secretaria, Description = (string?)"Recepción: agenda, turnos, pacientes, tareas y administración", IsSystem = true, CreatedAtUtc = seedDate });
    }
}

internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> b)
    {
        b.ToTable("RolePermissions");
        b.HasKey(x => new { x.RoleId, x.Permission });
        b.Property(x => x.Permission).HasMaxLength(64);

        var seed = SystemRoles.AdministradorPermissions.Select(p => new { RoleId = SystemRoles.AdministradorId, Permission = p })
            .Concat(SystemRoles.OdontologoPermissions.Select(p => new { RoleId = SystemRoles.OdontologoId, Permission = p }))
            .Concat(SystemRoles.SecretariaPermissions.Select(p => new { RoleId = SystemRoles.SecretariaId, Permission = p }))
            .ToArray();
        b.HasData(seed);
    }
}

internal sealed class MemberRoleConfiguration : IEntityTypeConfiguration<MemberRole>
{
    public void Configure(EntityTypeBuilder<MemberRole> b)
    {
        b.ToTable("MemberRoles");
        b.HasKey(x => new { x.MemberId, x.RoleId });
        b.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> b)
    {
        b.ToTable("Invitations");
        b.HasConsultorio();
        b.Property(x => x.Email).HasMaxLength(256).IsRequired();
        b.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => new { x.ConsultorioId, x.Email, x.Status });
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.InvitedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Roles).WithOne().HasForeignKey(x => x.InvitationId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Roles).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class InvitationRoleConfiguration : IEntityTypeConfiguration<InvitationRole>
{
    public void Configure(EntityTypeBuilder<InvitationRole> b)
    {
        b.ToTable("InvitationRoles");
        b.HasKey(x => new { x.InvitationId, x.RoleId });
        b.HasOne<Role>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("RefreshTokens");
        b.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => new { x.UserId, x.FamilyId });
        b.Property(x => x.IpAddress).HasMaxLength(64);
        b.Property(x => x.UserAgent).HasMaxLength(300);
        b.Property(x => x.RevokedReason).HasMaxLength(64);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class UserNotificationSettingsConfiguration : IEntityTypeConfiguration<UserNotificationSettings>
{
    public void Configure(EntityTypeBuilder<UserNotificationSettings> b)
    {
        b.ToTable("UserNotificationSettings");
        b.HasKey(x => x.UserId);
        b.HasOne<ApplicationUser>().WithOne().HasForeignKey<UserNotificationSettings>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("AuditLogs");
        b.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
        b.Property(x => x.EntityId).HasMaxLength(64);
        b.Property(x => x.Details).LongText();
        b.Property(x => x.IpAddress).HasMaxLength(64);
        b.HasIndex(x => new { x.ConsultorioId, x.OccurredAtUtc });
        b.HasIndex(x => new { x.EntityType, x.EntityId });
        b.HasIndex(x => x.UserId);
    }
}

internal sealed class ConsultorioSequenceConfiguration : IEntityTypeConfiguration<ConsultorioSequence>
{
    public void Configure(EntityTypeBuilder<ConsultorioSequence> b)
    {
        b.ToTable("ConsultorioSequences");
        b.HasKey(x => new { x.ConsultorioId, x.Name });
        b.Property(x => x.Name).HasMaxLength(32);
        b.HasOne<Consultorio>().WithMany().HasForeignKey(x => x.ConsultorioId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("Notifications");
        b.HasConsultorio();
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Message).HasMaxLength(1000);
        b.Property(x => x.EntityType).HasMaxLength(60);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.ConsultorioId, x.UserId, x.ReadAtUtc });
    }
}

internal sealed class OutboundMessageConfiguration : IEntityTypeConfiguration<OutboundMessage>
{
    public void Configure(EntityTypeBuilder<OutboundMessage> b)
    {
        b.ToTable("OutboundMessages");
        b.Property(x => x.Recipient).HasMaxLength(256).IsRequired();
        b.Property(x => x.Subject).HasMaxLength(250);
        b.Property(x => x.Body).LongText().IsRequired();
        b.Property(x => x.Purpose).HasMaxLength(60).IsRequired();
        b.Property(x => x.RelatedEntityType).HasMaxLength(60);
        b.Property(x => x.Provider).HasMaxLength(60);
        b.Property(x => x.ProviderMessageId).HasMaxLength(200);
        b.Property(x => x.LastError).HasMaxLength(500);
        b.HasOne<Consultorio>().WithMany().HasForeignKey(x => x.ConsultorioId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.ConsultorioId, x.CreatedAtUtc });
        b.HasIndex(x => new { x.RelatedEntityType, x.RelatedEntityId });
    }
}
