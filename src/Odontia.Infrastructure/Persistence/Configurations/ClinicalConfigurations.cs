using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Odontia.Domain.Dental;
using Odontia.Domain.Entities;

namespace Odontia.Infrastructure.Persistence.Configurations;

internal sealed class ClinicalRecordConfiguration : IEntityTypeConfiguration<ClinicalRecord>
{
    public void Configure(EntityTypeBuilder<ClinicalRecord> b)
    {
        b.ToTable("ClinicalRecords");
        b.HasConsultorio();
        b.HasTenantReference<ClinicalRecord, Patient>(nameof(ClinicalRecord.PatientId));
        b.HasIndex(x => new { x.ConsultorioId, x.PatientId }).IsUnique();
    }
}

internal sealed class ClinicalEntryConfiguration : IEntityTypeConfiguration<ClinicalEntry>
{
    public void Configure(EntityTypeBuilder<ClinicalEntry> b)
    {
        b.ToTable("ClinicalEntries");
        b.HasConsultorio();
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).LongText();
        b.Property(x => x.Indications).LongText();
        b.Property(x => x.VoidReason).HasMaxLength(500);
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasTenantReference<ClinicalEntry, ClinicalRecord>(nameof(ClinicalEntry.ClinicalRecordId));
        b.HasTenantReference<ClinicalEntry, Patient>(nameof(ClinicalEntry.PatientId));
        b.HasProfessionalReference(nameof(ClinicalEntry.ProfessionalUserId));
        b.HasTenantReference<ClinicalEntry, Appointment>(nameof(ClinicalEntry.AppointmentId), required: false);
        b.HasIndex(x => new { x.ConsultorioId, x.PatientId, x.OccurredAtUtc });
        b.HasMany(x => x.Teeth).WithOne().HasForeignKey(x => x.ClinicalEntryId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Revisions).WithOne().HasForeignKey(nameof(ClinicalEntryRevision.ConsultorioId), nameof(ClinicalEntryRevision.ClinicalEntryId))
            .HasPrincipalKey(nameof(ClinicalEntry.ConsultorioId), nameof(ClinicalEntry.Id)).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.Teeth).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Navigation(x => x.Revisions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ClinicalEntryToothConfiguration : IEntityTypeConfiguration<ClinicalEntryTooth>
{
    public void Configure(EntityTypeBuilder<ClinicalEntryTooth> b)
    {
        b.ToTable("ClinicalEntryTeeth");
        b.HasKey(x => new { x.ClinicalEntryId, x.ToothCode });
        b.HasOne<DentalPiece>().WithMany().HasForeignKey(x => x.ToothCode).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ClinicalEntryRevisionConfiguration : IEntityTypeConfiguration<ClinicalEntryRevision>
{
    public void Configure(EntityTypeBuilder<ClinicalEntryRevision> b)
    {
        b.ToTable("ClinicalEntryRevisions");
        b.HasConsultorio();
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).LongText();
        b.Property(x => x.Indications).LongText();
        b.Property(x => x.Teeth).HasMaxLength(200).IsRequired();
        b.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.EditedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.ClinicalEntryId, x.Version }).IsUnique();
    }
}

internal sealed class DiagnosisConfiguration : IEntityTypeConfiguration<Diagnosis>
{
    public void Configure(EntityTypeBuilder<Diagnosis> b)
    {
        b.ToTable("Diagnoses");
        b.HasConsultorio();
        b.Property(x => x.Description).HasMaxLength(300).IsRequired();
        b.Property(x => x.Code).HasMaxLength(20);
        b.Property(x => x.Notes).LongText();
        b.HasTenantReference<Diagnosis, Patient>(nameof(Diagnosis.PatientId));
        b.HasProfessionalReference(nameof(Diagnosis.ProfessionalUserId));
        b.HasTenantReference<Diagnosis, ClinicalEntry>(nameof(Diagnosis.ClinicalEntryId), required: false);
        b.HasOne<DentalPiece>().WithMany().HasForeignKey(x => x.ToothCode).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<DentalSurface>().WithMany().HasForeignKey(x => x.Surface).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.ConsultorioId, x.PatientId, x.DiagnosedAtUtc });
    }
}

internal sealed class DentalPieceConfiguration : IEntityTypeConfiguration<DentalPiece>
{
    public void Configure(EntityTypeBuilder<DentalPiece> b)
    {
        b.ToTable("DentalPieces");
        b.HasKey(x => x.Code);
        b.Property(x => x.Code).ValueGeneratedNever();
        b.Property(x => x.Name).HasMaxLength(80).IsRequired();
        b.HasData(Fdi.All.Select(t => new
        {
            t.Code, t.Quadrant, t.Position, t.Dentition, t.Type, t.Name,
        }));
    }
}

internal sealed class DentalSurfaceConfiguration : IEntityTypeConfiguration<DentalSurface>
{
    public void Configure(EntityTypeBuilder<DentalSurface> b)
    {
        b.ToTable("DentalSurfaces");
        b.HasKey(x => x.Code);
        b.Property(x => x.Code).ValueGeneratedNever();
        b.Property(x => x.Name).HasMaxLength(30).IsRequired();
        b.Property(x => x.Abbreviation).HasMaxLength(2).IsRequired();
        b.HasData(DentalSurface.Seed.Select(s => new { s.Code, s.Name, s.Abbreviation }));
    }
}

internal sealed class OdontogramConditionConfiguration : IEntityTypeConfiguration<OdontogramCondition>
{
    public void Configure(EntityTypeBuilder<OdontogramCondition> b)
    {
        b.ToTable("OdontogramConditions");
        b.Property(x => x.Code).HasMaxLength(30).IsRequired();
        b.Property(x => x.Name).HasMaxLength(80).IsRequired();
        b.Property(x => x.Color).HasMaxLength(9);
        b.HasOne<Consultorio>().WithMany().HasForeignKey(x => x.ConsultorioId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.ConsultorioId, x.Code }).IsUnique();
        var seedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        b.HasData(OdontogramCondition.SystemSeed.Select(c => new
        {
            c.Id, ConsultorioId = (Guid?)null, c.Code, c.Name, c.Scope, c.Color, c.MarksToothAbsent, c.IsSystem, c.IsActive, CreatedAtUtc = seedDate,
        }));
    }
}

internal sealed class OdontogramConfiguration : IEntityTypeConfiguration<Odontogram>
{
    public void Configure(EntityTypeBuilder<Odontogram> b)
    {
        b.ToTable("Odontograms");
        b.HasConsultorio();
        b.HasTenantReference<Odontogram, Patient>(nameof(Odontogram.PatientId));
        b.HasIndex(x => new { x.ConsultorioId, x.PatientId }).IsUnique();
        b.HasMany(x => x.Findings).WithOne().HasForeignKey(nameof(ToothFinding.ConsultorioId), nameof(ToothFinding.OdontogramId))
            .HasPrincipalKey(nameof(Odontogram.ConsultorioId), nameof(Odontogram.Id)).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.History).WithOne().HasForeignKey(nameof(OdontogramHistoryEntry.ConsultorioId), nameof(OdontogramHistoryEntry.OdontogramId))
            .HasPrincipalKey(nameof(Odontogram.ConsultorioId), nameof(Odontogram.Id)).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Notes).WithOne().HasForeignKey(nameof(ToothNote.ConsultorioId), nameof(ToothNote.OdontogramId))
            .HasPrincipalKey(nameof(Odontogram.ConsultorioId), nameof(Odontogram.Id)).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.Findings).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Navigation(x => x.History).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Navigation(x => x.Notes).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Ignore(x => x.CurrentFindings);
    }
}

internal sealed class ToothFindingConfiguration : IEntityTypeConfiguration<ToothFinding>
{
    public void Configure(EntityTypeBuilder<ToothFinding> b)
    {
        b.ToTable("ToothFindings");
        b.HasConsultorio();
        b.Property(x => x.Notes).HasMaxLength(500);
        b.Property(x => x.ClosedReason).HasMaxLength(250);
        b.HasOne<DentalPiece>().WithMany().HasForeignKey(x => x.ToothCode).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<DentalSurface>().WithMany().HasForeignKey(x => x.Surface).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Condition).WithMany().HasForeignKey(x => x.ConditionId).OnDelete(DeleteBehavior.Restrict);
        b.HasTenantReference<ToothFinding, Patient>(nameof(ToothFinding.PatientId));
        b.HasProfessionalReference(nameof(ToothFinding.RecordedByUserId));
        b.HasTenantReference<ToothFinding, TreatmentPlanItem>(nameof(ToothFinding.TreatmentPlanItemId), required: false);
        b.HasIndex(x => new { x.OdontogramId, x.IsCurrent, x.ToothCode });
    }
}

internal sealed class OdontogramHistoryEntryConfiguration : IEntityTypeConfiguration<OdontogramHistoryEntry>
{
    public void Configure(EntityTypeBuilder<OdontogramHistoryEntry> b)
    {
        b.ToTable("OdontogramHistory");
        b.HasConsultorio();
        b.Property(x => x.Notes).HasMaxLength(500);
        b.HasOne<DentalPiece>().WithMany().HasForeignKey(x => x.ToothCode).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<DentalSurface>().WithMany().HasForeignKey(x => x.Surface).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OdontogramCondition>().WithMany().HasForeignKey(x => x.ConditionId).OnDelete(DeleteBehavior.Restrict);
        b.HasProfessionalReference(nameof(OdontogramHistoryEntry.ProfessionalUserId));
        b.HasIndex(x => new { x.OdontogramId, x.ToothCode, x.ChangedAtUtc });
    }
}

internal sealed class ToothNoteConfiguration : IEntityTypeConfiguration<ToothNote>
{
    public void Configure(EntityTypeBuilder<ToothNote> b)
    {
        b.ToTable("ToothNotes");
        b.HasConsultorio();
        b.Property(x => x.Text).HasMaxLength(1000).IsRequired();
        b.HasOne<DentalPiece>().WithMany().HasForeignKey(x => x.ToothCode).OnDelete(DeleteBehavior.Restrict);
        b.HasProfessionalReference(nameof(ToothNote.ProfessionalUserId));
        b.HasIndex(x => new { x.OdontogramId, x.ToothCode });
    }
}
