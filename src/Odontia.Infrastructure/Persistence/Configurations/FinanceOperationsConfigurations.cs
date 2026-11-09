using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Odontia.Domain.Entities;

namespace Odontia.Infrastructure.Persistence.Configurations;

internal sealed class ProcedureCatalogItemConfiguration : IEntityTypeConfiguration<ProcedureCatalogItem>
{
    public void Configure(EntityTypeBuilder<ProcedureCatalogItem> b)
    {
        b.ToTable("Procedures");
        b.HasConsultorio();
        b.Property(x => x.Code).HasMaxLength(30).IsRequired();
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.HasIndex(x => new { x.ConsultorioId, x.Code }).IsUnique();
    }
}

internal sealed class TreatmentPlanConfiguration : IEntityTypeConfiguration<TreatmentPlan>
{
    public void Configure(EntityTypeBuilder<TreatmentPlan> b)
    {
        b.ToTable("TreatmentPlans");
        b.HasConsultorio();
        b.Property(x => x.Title).HasMaxLength(150).IsRequired();
        b.Property(x => x.Notes).LongText();
        b.Ignore(x => x.ProgressPercent);
        b.HasTenantReference<TreatmentPlan, Patient>(nameof(TreatmentPlan.PatientId));
        b.HasProfessionalReference(nameof(TreatmentPlan.ProfessionalUserId));
        b.HasIndex(x => new { x.ConsultorioId, x.PatientId, x.Status });
        b.HasMany(x => x.Items).WithOne().HasForeignKey(nameof(TreatmentPlanItem.ConsultorioId), nameof(TreatmentPlanItem.TreatmentPlanId))
            .HasPrincipalKey(nameof(TreatmentPlan.ConsultorioId), nameof(TreatmentPlan.Id)).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Changes).WithOne().HasForeignKey(nameof(TreatmentChange.ConsultorioId), nameof(TreatmentChange.TreatmentPlanId))
            .HasPrincipalKey(nameof(TreatmentPlan.ConsultorioId), nameof(TreatmentPlan.Id)).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Navigation(x => x.Changes).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class TreatmentPlanItemConfiguration : IEntityTypeConfiguration<TreatmentPlanItem>
{
    public void Configure(EntityTypeBuilder<TreatmentPlanItem> b)
    {
        b.ToTable("TreatmentPlanItems");
        b.HasConsultorio();
        b.Property(x => x.ProcedureName).HasMaxLength(150).IsRequired();
        b.Property(x => x.Surfaces).HasMaxLength(7);
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.HasTenantReference<TreatmentPlanItem, ProcedureCatalogItem>(nameof(TreatmentPlanItem.ProcedureId), x => x.Procedure);
        b.HasProfessionalReference(nameof(TreatmentPlanItem.ProfessionalUserId));
        b.HasTenantReference<TreatmentPlanItem, Diagnosis>(nameof(TreatmentPlanItem.DiagnosisId), required: false);
        b.HasOne<DentalPiece>().WithMany().HasForeignKey(x => x.ToothCode).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.ConsultorioId, x.Status });
    }
}

internal sealed class TreatmentChangeConfiguration : IEntityTypeConfiguration<TreatmentChange>
{
    public void Configure(EntityTypeBuilder<TreatmentChange> b)
    {
        b.ToTable("TreatmentChanges");
        b.HasConsultorio();
        b.Property(x => x.Note).HasMaxLength(500);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ChangedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.TreatmentPlanId, x.ChangedAtUtc });
    }
}

internal sealed class BudgetConfiguration : IEntityTypeConfiguration<Budget>
{
    public void Configure(EntityTypeBuilder<Budget> b)
    {
        b.ToTable("Budgets");
        b.HasConsultorio();
        b.Property(x => x.DiscountPercent).HasPrecision(5, 2);
        b.Property(x => x.Notes).LongText();
        b.Property(x => x.StatusReason).HasMaxLength(500);
        b.Ignore(x => x.IsEditable);
        b.HasIndex(x => new { x.ConsultorioId, x.Number }).IsUnique();
        b.HasIndex(x => new { x.ConsultorioId, x.PatientId, x.Status });
        b.HasTenantReference<Budget, Patient>(nameof(Budget.PatientId));
        b.HasTenantReference<Budget, TreatmentPlan>(nameof(Budget.TreatmentPlanId), required: false);
        b.HasMany(x => x.Items).WithOne().HasForeignKey(nameof(BudgetItem.ConsultorioId), nameof(BudgetItem.BudgetId))
            .HasPrincipalKey(nameof(Budget.ConsultorioId), nameof(Budget.Id)).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.StatusChanges).WithOne().HasForeignKey(nameof(BudgetStatusChange.ConsultorioId), nameof(BudgetStatusChange.BudgetId))
            .HasPrincipalKey(nameof(Budget.ConsultorioId), nameof(Budget.Id)).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Navigation(x => x.StatusChanges).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class BudgetItemConfiguration : IEntityTypeConfiguration<BudgetItem>
{
    public void Configure(EntityTypeBuilder<BudgetItem> b)
    {
        b.ToTable("BudgetItems");
        b.HasConsultorio();
        b.Property(x => x.Description).HasMaxLength(200).IsRequired();
        b.Property(x => x.DiscountPercent).HasPrecision(5, 2);
        b.HasTenantReference<BudgetItem, ProcedureCatalogItem>(nameof(BudgetItem.ProcedureId), required: false);
        b.HasTenantReference<BudgetItem, TreatmentPlanItem>(nameof(BudgetItem.TreatmentPlanItemId), required: false);
        b.HasOne<DentalPiece>().WithMany().HasForeignKey(x => x.ToothCode).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class BudgetStatusChangeConfiguration : IEntityTypeConfiguration<BudgetStatusChange>
{
    public void Configure(EntityTypeBuilder<BudgetStatusChange> b)
    {
        b.ToTable("BudgetStatusChanges");
        b.HasConsultorio();
        b.Property(x => x.Reason).HasMaxLength(500);
        b.HasIndex(x => new { x.BudgetId, x.ChangedAtUtc });
    }
}

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.ToTable("Payments");
        b.HasConsultorio();
        b.Property(x => x.Reference).HasMaxLength(100);
        b.Property(x => x.Notes).HasMaxLength(500);
        b.Property(x => x.VoidReason).HasMaxLength(500);
        b.HasTenantReference<Payment, Patient>(nameof(Payment.PatientId));
        b.HasTenantReference<Payment, Budget>(nameof(Payment.BudgetId), required: false);
        b.HasTenantReference<Payment, ConsultorioMember>(nameof(Payment.ReceivedByUserId), principalKey: nameof(ConsultorioMember.UserId));
        b.HasIndex(x => new { x.ConsultorioId, x.ReceiptNumber }).IsUnique();
        b.HasIndex(x => new { x.ConsultorioId, x.PatientId, x.PaidAtUtc });
    }
}

internal sealed class AccountMovementConfiguration : IEntityTypeConfiguration<AccountMovement>
{
    public void Configure(EntityTypeBuilder<AccountMovement> b)
    {
        b.ToTable("AccountMovements");
        b.HasConsultorio();
        b.Property(x => x.Description).HasMaxLength(250).IsRequired();
        b.Ignore(x => x.SignedAmount);
        b.HasTenantReference<AccountMovement, Patient>(nameof(AccountMovement.PatientId));
        b.HasTenantReference<AccountMovement, Budget>(nameof(AccountMovement.BudgetId), required: false);
        b.HasTenantReference<AccountMovement, Payment>(nameof(AccountMovement.PaymentId), required: false);
        b.HasTenantReference<AccountMovement, AccountMovement>(nameof(AccountMovement.ReversesMovementId), required: false);
        b.HasIndex(x => new { x.ConsultorioId, x.PatientId, x.OccurredAtUtc });
    }
}

internal sealed class LaboratoryConfiguration : IEntityTypeConfiguration<Laboratory>
{
    public void Configure(EntityTypeBuilder<Laboratory> b)
    {
        b.ToTable("Laboratories");
        b.HasConsultorio();
        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.Property(x => x.ContactName).HasMaxLength(120);
        b.Property(x => x.Phone).HasMaxLength(40);
        b.Property(x => x.AddressLine).HasMaxLength(250);
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.HasIndex(x => new { x.ConsultorioId, x.Name }).IsUnique();
    }
}

internal sealed class LabWorkConfiguration : IEntityTypeConfiguration<LabWork>
{
    public void Configure(EntityTypeBuilder<LabWork> b)
    {
        b.ToTable("LabWorks");
        b.HasConsultorio();
        b.Property(x => x.WorkType).HasMaxLength(120).IsRequired();
        b.Property(x => x.Description).HasMaxLength(1000);
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.Ignore(x => x.IsOpen);
        b.HasTenantReference<LabWork, Patient>(nameof(LabWork.PatientId), x => x.Patient);
        b.HasTenantReference<LabWork, Laboratory>(nameof(LabWork.LaboratoryId), x => x.Laboratory);
        b.HasProfessionalReference(nameof(LabWork.ProfessionalUserId));
        b.HasTenantReference<LabWork, TreatmentPlanItem>(nameof(LabWork.TreatmentPlanItemId), required: false);
        b.HasOne<DentalPiece>().WithMany().HasForeignKey(x => x.ToothCode).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.ConsultorioId, x.Status, x.EstimatedDeliveryDate });
        b.HasIndex(x => new { x.ConsultorioId, x.PatientId });
        b.HasMany(x => x.Changes).WithOne().HasForeignKey(nameof(LabWorkStatusChange.ConsultorioId), nameof(LabWorkStatusChange.LabWorkId))
            .HasPrincipalKey(nameof(LabWork.ConsultorioId), nameof(LabWork.Id)).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.Changes).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class LabWorkStatusChangeConfiguration : IEntityTypeConfiguration<LabWorkStatusChange>
{
    public void Configure(EntityTypeBuilder<LabWorkStatusChange> b)
    {
        b.ToTable("LabWorkStatusChanges");
        b.HasConsultorio();
        b.Property(x => x.Note).HasMaxLength(500);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ChangedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.LabWorkId, x.ChangedAtUtc });
    }
}

internal sealed class StoredDocumentConfiguration : IEntityTypeConfiguration<StoredDocument>
{
    public void Configure(EntityTypeBuilder<StoredDocument> b)
    {
        b.ToTable("Documents");
        b.HasConsultorio();
        b.Property(x => x.FileName).HasMaxLength(200).IsRequired();
        b.Property(x => x.StorageKey).HasMaxLength(300).IsRequired();
        b.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        b.Property(x => x.Sha256).HasMaxLength(64).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.DeletionReason).HasMaxLength(250);
        b.Ignore(x => x.IsClinical);
        b.HasIndex(x => x.StorageKey).IsUnique();
        b.HasIndex(x => new { x.ConsultorioId, x.PatientId, x.IsDeleted });
        b.HasTenantReference<StoredDocument, Patient>(nameof(StoredDocument.PatientId));
        b.HasTenantReference<StoredDocument, ClinicalEntry>(nameof(StoredDocument.ClinicalEntryId), required: false);
        b.HasTenantReference<StoredDocument, TreatmentPlan>(nameof(StoredDocument.TreatmentPlanId), required: false);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ConsentConfiguration : IEntityTypeConfiguration<Consent>
{
    public void Configure(EntityTypeBuilder<Consent> b)
    {
        b.ToTable("Consents");
        b.HasConsultorio();
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Content).LongText().IsRequired();
        b.Property(x => x.AcceptedByName).HasMaxLength(150);
        b.Property(x => x.AcceptedByDocument).HasMaxLength(20);
        b.Property(x => x.AcceptedByRelationship).HasMaxLength(60);
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.HasTenantReference<Consent, Patient>(nameof(Consent.PatientId));
        b.HasTenantReference<Consent, TreatmentPlan>(nameof(Consent.TreatmentPlanId), required: false);
        b.HasTenantReference<Consent, StoredDocument>(nameof(Consent.SignedDocumentId), required: false);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ResponsibleUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.ConsultorioId, x.PatientId, x.Status });
    }
}
