using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Odontia.Application.Abstractions;
using Odontia.Domain.Common;
using Odontia.Domain.Entities;
using Odontia.Domain.Enums;

namespace Odontia.Infrastructure.Persistence;

public class OdontiaDbContext(DbContextOptions<OdontiaDbContext> options, ITenantContext tenant, TimeProvider clock)
    : IdentityUserContext<ApplicationUser, Guid>(options), IApplicationDbContext
{
    /// <summary>Consultorio activo usado por el filtro global (Guid.Empty = sin acceso a datos de consultorio).</summary>
    public Guid CurrentConsultorioId => tenant.ConsultorioIdOrNull ?? Guid.Empty;

    public DbSet<Consultorio> Consultorios => Set<Consultorio>();
    public DbSet<Gabinete> Gabinetes => Set<Gabinete>();
    public DbSet<ConsultorioMember> Members => Set<ConsultorioMember>();
    public DbSet<Role> AppRoles => Set<Role>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<MemberRole> MemberRoles => Set<MemberRole>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserNotificationSettings> NotificationSettings => Set<UserNotificationSettings>();
    public DbSet<HealthInsurance> HealthInsurances => Set<HealthInsurance>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<MedicalHistory> MedicalHistories => Set<MedicalHistory>();
    public DbSet<PatientAlert> PatientAlerts => Set<PatientAlert>();
    public DbSet<WorkingHours> WorkingHours => Set<WorkingHours>();
    public DbSet<ScheduleBlock> ScheduleBlocks => Set<ScheduleBlock>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<AppointmentHistory> AppointmentHistories => Set<AppointmentHistory>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<ClinicalRecord> ClinicalRecords => Set<ClinicalRecord>();
    public DbSet<ClinicalEntry> ClinicalEntries => Set<ClinicalEntry>();
    public DbSet<ClinicalEntryRevision> ClinicalEntryRevisions => Set<ClinicalEntryRevision>();
    public DbSet<Diagnosis> Diagnoses => Set<Diagnosis>();
    public DbSet<DentalPiece> DentalPieces => Set<DentalPiece>();
    public DbSet<DentalSurface> DentalSurfaces => Set<DentalSurface>();
    public DbSet<OdontogramCondition> OdontogramConditions => Set<OdontogramCondition>();
    public DbSet<Odontogram> Odontograms => Set<Odontogram>();
    public DbSet<ToothFinding> ToothFindings => Set<ToothFinding>();
    public DbSet<OdontogramHistoryEntry> OdontogramHistory => Set<OdontogramHistoryEntry>();
    public DbSet<ToothNote> ToothNotes => Set<ToothNote>();
    public DbSet<ProcedureCatalogItem> Procedures => Set<ProcedureCatalogItem>();
    public DbSet<TreatmentPlan> TreatmentPlans => Set<TreatmentPlan>();
    public DbSet<TreatmentPlanItem> TreatmentPlanItems => Set<TreatmentPlanItem>();
    public DbSet<TreatmentChange> TreatmentChanges => Set<TreatmentChange>();
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<BudgetItem> BudgetItems => Set<BudgetItem>();
    public DbSet<BudgetStatusChange> BudgetStatusChanges => Set<BudgetStatusChange>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<AccountMovement> AccountMovements => Set<AccountMovement>();
    public DbSet<Laboratory> Laboratories => Set<Laboratory>();
    public DbSet<LabWork> LabWorks => Set<LabWork>();
    public DbSet<LabWorkStatusChange> LabWorkStatusChanges => Set<LabWorkStatusChange>();
    public DbSet<StoredDocument> Documents => Set<StoredDocument>();
    public DbSet<Consent> Consents => Set<Consent>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<OutboundMessage> OutboundMessages => Set<OutboundMessage>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ConsultorioSequence> Sequences => Set<ConsultorioSequence>();

    protected override void ConfigureConventions(ModelConfigurationBuilder b)
    {
        b.Properties<string>().HaveMaxLength(256);
        b.Properties<decimal>().HavePrecision(14, 2);
        b.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        b.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
        // MySql.EntityFrameworkCore lee 'date'/'time' como DateTime/TimeSpan: se convierten explícitamente.
        b.Properties<DateOnly>().HaveConversion<DateOnlyConverter>().HaveColumnType("date");
        b.Properties<DateOnly?>().HaveConversion<NullableDateOnlyConverter>().HaveColumnType("date");
        b.Properties<TimeOnly>().HaveConversion<TimeOnlyConverter>().HaveColumnType("time");
    }

    private sealed class DateOnlyConverter() : ValueConverter<DateOnly, DateTime>(
        d => d.ToDateTime(TimeOnly.MinValue), v => DateOnly.FromDateTime(v));

    private sealed class NullableDateOnlyConverter() : ValueConverter<DateOnly?, DateTime?>(
        d => d == null ? null : d.Value.ToDateTime(TimeOnly.MinValue), v => v == null ? null : DateOnly.FromDateTime(v.Value));

    private sealed class TimeOnlyConverter() : ValueConverter<TimeOnly, TimeSpan>(
        t => t.ToTimeSpan(), v => TimeOnly.FromTimeSpan(v));

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(OdontiaDbContext).Assembly);

        // Los GUID (v7, ordenables) se generan en el dominio: EF no debe considerarlos generados por la base,
        // así las entidades nuevas alcanzadas por navegación se insertan (Added) en lugar de actualizarse.
        foreach (var et in builder.Model.GetEntityTypes())
        {
            var pk = et.FindPrimaryKey();
            if (pk is { Properties.Count: 1 } && pk.Properties[0].ClrType == typeof(Guid))
                pk.Properties[0].ValueGenerated = ValueGenerated.Never;
        }

        // Filtro global de aislamiento por consultorio para todas las entidades de consultorio.
        foreach (var et in builder.Model.GetEntityTypes().Where(t => typeof(ITenantEntity).IsAssignableFrom(t.ClrType) && t.BaseType is null))
        {
            GetType().GetMethod(nameof(ApplyTenantFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .MakeGenericMethod(et.ClrType).Invoke(this, [builder]);
        }

        ShortenIdentifiers(builder.Model);
    }

    private void ApplyTenantFilter<T>(ModelBuilder builder) where T : class, ITenantEntity =>
        builder.Entity<T>().HasQueryFilter(e => e.ConsultorioId == CurrentConsultorioId);

    /// <summary>MySQL limita identificadores a 64 caracteres: se acortan nombres largos de FK/índices/claves de forma determinística.</summary>
    private static void ShortenIdentifiers(IMutableModel model)
    {
        static string Shorten(string name)
        {
            if (name.Length <= 64) return name;
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name)))[..8];
            return $"{name[..55]}_{hash}";
        }

        foreach (var et in model.GetEntityTypes())
        {
            foreach (var fk in et.GetForeignKeys())
                if (fk.GetConstraintName() is { } n && n.Length > 64) fk.SetConstraintName(Shorten(n));
            foreach (var ix in et.GetIndexes())
                if (ix.GetDatabaseName() is { } n && n.Length > 64) ix.SetDatabaseName(Shorten(n));
            foreach (var key in et.GetKeys())
                if (key.GetName() is { } n && n.Length > 64) key.SetName(Shorten(n));
        }
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var audits = PrepareChanges();
        if (audits.Count > 0) AuditLogs.AddRange(audits);
        return await base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        var audits = PrepareChanges();
        if (audits.Count > 0) AuditLogs.AddRange(audits);
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    /// <summary>
    /// Completa campos de auditoría, impone el consultorio activo en escrituras (impide escrituras
    /// cruzadas entre consultorios) y genera registros de auditoría automáticos.
    /// </summary>
    private List<AuditLog> PrepareChanges()
    {
        var now = clock.GetUtcNow().UtcDateTime;
        Guid? userId = tenant.IsAuthenticated ? tenant.UserId : null;
        var current = tenant.ConsultorioIdOrNull;
        var audits = new List<AuditLog>();

        foreach (var entry in ChangeTracker.Entries().ToList())
        {
            if (entry.State is EntityState.Detached or EntityState.Unchanged) continue;
            if (entry.Entity is AuditLog) continue;

            if (entry.Entity is ITenantEntity tenantEntity)
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        if (tenantEntity.ConsultorioId == Guid.Empty)
                            tenantEntity.ConsultorioId = current ?? throw new InvalidOperationException("No hay consultorio activo para registrar datos.");
                        else if (current is not null && tenantEntity.ConsultorioId != current)
                            throw new InvalidOperationException("Intento de escritura en un consultorio distinto al activo.");
                        break;
                    case EntityState.Modified:
                    case EntityState.Deleted:
                        var original = (Guid)entry.Property(nameof(ITenantEntity.ConsultorioId)).OriginalValue!;
                        if (entry.Property(nameof(ITenantEntity.ConsultorioId)).IsModified && original != tenantEntity.ConsultorioId)
                            throw new InvalidOperationException("No se permite cambiar el consultorio de un registro.");
                        if (current is not null && original != current)
                            throw new InvalidOperationException("Intento de modificación en un consultorio distinto al activo.");
                        break;
                }
            }

            if (entry.Entity is IAuditable auditable)
            {
                if (entry.State == EntityState.Added)
                {
                    if (auditable.CreatedAtUtc == default) auditable.CreatedAtUtc = now;
                    auditable.CreatedByUserId ??= userId;
                }
                else if (entry.State == EntityState.Modified)
                {
                    auditable.UpdatedAtUtc = now;
                    auditable.UpdatedByUserId = userId;
                    entry.Property(nameof(IAuditable.CreatedAtUtc)).IsModified = false;
                    entry.Property(nameof(IAuditable.CreatedByUserId)).IsModified = false;
                }
            }
            else if (entry.Entity is ApplicationUser user && entry.State == EntityState.Added && user.CreatedAtUtc == default)
            {
                user.CreatedAtUtc = now;
            }

            if (ShouldAudit(entry)) audits.Add(BuildAudit(entry, userId, now));
        }
        return audits;
    }

    private static readonly HashSet<Type> NotAudited =
    [
        typeof(RefreshToken), typeof(OutboundMessage), typeof(Notification), typeof(ConsultorioSequence),
        typeof(AppointmentHistory), typeof(OdontogramHistoryEntry), typeof(TreatmentChange), typeof(BudgetStatusChange),
        typeof(LabWorkStatusChange), typeof(ClinicalEntryRevision), typeof(RolePermission), typeof(MemberRole),
        typeof(InvitationRole), typeof(UserNotificationSettings),
    ];

    private static bool ShouldAudit(EntityEntry entry) =>
        entry.Entity is ITenantEntity or Consultorio or Role && !NotAudited.Contains(entry.Entity.GetType());

    /// <summary>
    /// Detalle de auditoría: nombres de propiedades modificadas. Para entidades sensibles (clínicas)
    /// nunca se registran valores; para el resto, sólo valores no textuales libres.
    /// </summary>
    private AuditLog BuildAudit(EntityEntry entry, Guid? userId, DateTime now)
    {
        var action = entry.State switch
        {
            EntityState.Added => AuditAction.Created,
            EntityState.Deleted => AuditAction.Deleted,
            _ => AuditAction.Updated,
        };
        var props = entry.Properties
            .Where(p => entry.State != EntityState.Modified || p.IsModified)
            .Select(p => p.Metadata.Name)
            .Where(n => n is not (nameof(IAuditable.UpdatedAtUtc) or nameof(IAuditable.UpdatedByUserId) or "Version" or "LockVersion"))
            .ToList();
        var details = JsonSerializer.Serialize(new { changed = props });
        var id = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "Id")?.CurrentValue?.ToString();
        var consultorioId = entry.Entity is ITenantEntity t ? t.ConsultorioId : entry.Entity is Consultorio c ? c.Id : tenant.ConsultorioIdOrNull;
        return new AuditLog(consultorioId, userId, action, entry.Entity.GetType().Name, id, details, tenant.IpAddress, now);
    }

    private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    private sealed class NullableUtcDateTimeConverter() : ValueConverter<DateTime?, DateTime?>(
        v => v == null ? v : (v.Value.Kind == DateTimeKind.Utc ? v : v.Value.ToUniversalTime()),
        v => v == null ? v : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc));
}

internal static class TenantModelBuilderExtensions
{
    /// <summary>
    /// Relación con FK compuesta (ConsultorioId, FK) → principal (ConsultorioId, Id): la base de datos
    /// impide que un registro referencie entidades de otro consultorio.
    /// </summary>
    public static void HasTenantReference<TDependent, TPrincipal>(this Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TDependent> b,
        string foreignKey, Expression<Func<TDependent, TPrincipal?>>? navigation = null, bool required = true, string principalKey = "Id")
        where TDependent : class where TPrincipal : class
    {
        var rel = navigation is null ? b.HasOne<TPrincipal>() : b.HasOne(navigation);
        rel.WithMany()
            .HasForeignKey(nameof(ITenantEntity.ConsultorioId), foreignKey)
            .HasPrincipalKey(nameof(ITenantEntity.ConsultorioId), principalKey)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(required);
    }

    /// <summary>Referencia a un profesional: debe ser miembro del mismo consultorio (FK a (ConsultorioId, UserId)).</summary>
    public static void HasProfessionalReference<TDependent>(this Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TDependent> b,
        string foreignKey, bool required = true) where TDependent : class =>
        b.HasTenantReference<TDependent, ConsultorioMember>(foreignKey, null, required, nameof(ConsultorioMember.UserId));

    public static void HasConsultorio<TDependent>(this Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TDependent> b)
        where TDependent : class, ITenantEntity
    {
        b.HasOne<Consultorio>().WithMany().HasForeignKey(e => e.ConsultorioId).OnDelete(DeleteBehavior.Restrict);
    }

    /// <summary>Columna de texto largo (fuera del límite de fila de InnoDB).</summary>
    public static Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<T> LongText<T>(
        this Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<T> p) => p.HasColumnType("text");
}
