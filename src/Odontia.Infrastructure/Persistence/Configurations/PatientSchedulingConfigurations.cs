using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Odontia.Domain.Entities;

namespace Odontia.Infrastructure.Persistence.Configurations;

internal sealed class HealthInsuranceConfiguration : IEntityTypeConfiguration<HealthInsurance>
{
    public void Configure(EntityTypeBuilder<HealthInsurance> b)
    {
        b.ToTable("HealthInsurances");
        b.HasConsultorio();
        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.Property(x => x.Code).HasMaxLength(30);
        b.Property(x => x.Phone).HasMaxLength(40);
        b.Property(x => x.Notes).HasMaxLength(500);
        b.HasIndex(x => new { x.ConsultorioId, x.Name }).IsUnique();
    }
}

internal sealed class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> b)
    {
        b.ToTable("Patients");
        b.HasConsultorio();
        b.Property(x => x.FirstName).HasMaxLength(80).IsRequired();
        b.Property(x => x.LastName).HasMaxLength(80).IsRequired();
        b.Property(x => x.DocumentNumber).HasMaxLength(20).IsRequired();
        b.Property(x => x.Occupation).HasMaxLength(100);
        b.Property(x => x.Phone).HasMaxLength(40);
        b.Property(x => x.PhoneNormalized).HasMaxLength(40);
        b.Property(x => x.AddressLine).HasMaxLength(250);
        b.Property(x => x.City).HasMaxLength(100);
        b.Property(x => x.Province).HasMaxLength(100);
        b.Property(x => x.PostalCode).HasMaxLength(20);
        b.Property(x => x.EmergencyContactName).HasMaxLength(120);
        b.Property(x => x.EmergencyContactPhone).HasMaxLength(40);
        b.Property(x => x.EmergencyContactRelationship).HasMaxLength(60);
        b.Property(x => x.InsurancePlan).HasMaxLength(60);
        b.Property(x => x.AffiliateNumber).HasMaxLength(40);
        b.Property(x => x.AdministrativeNotes).LongText();
        b.Property(x => x.SearchName).HasMaxLength(170).IsRequired();

        // Evita duplicados: un DNI por consultorio.
        b.HasIndex(x => new { x.ConsultorioId, x.DocumentNumber }).IsUnique();
        b.HasIndex(x => new { x.ConsultorioId, x.SearchName });
        b.HasIndex(x => new { x.ConsultorioId, x.PhoneNormalized });
        b.HasIndex(x => new { x.ConsultorioId, x.Status });
        b.HasTenantReference<Patient, HealthInsurance>(nameof(Patient.HealthInsuranceId), x => x.HealthInsurance, required: false);
        b.HasOne(x => x.MedicalHistory).WithOne().HasForeignKey<MedicalHistory>(nameof(MedicalHistory.ConsultorioId), nameof(MedicalHistory.PatientId))
            .HasPrincipalKey<Patient>(nameof(Patient.ConsultorioId), nameof(Patient.Id)).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class MedicalHistoryConfiguration : IEntityTypeConfiguration<MedicalHistory>
{
    public void Configure(EntityTypeBuilder<MedicalHistory> b)
    {
        b.ToTable("MedicalHistories");
        b.HasConsultorio();
        b.HasIndex(x => x.PatientId).IsUnique();
        b.Property(x => x.Allergies).HasMaxLength(500);
        b.Property(x => x.CurrentMedication).LongText();
        b.Property(x => x.OtherConditions).LongText();
    }
}

internal sealed class PatientAlertConfiguration : IEntityTypeConfiguration<PatientAlert>
{
    public void Configure(EntityTypeBuilder<PatientAlert> b)
    {
        b.ToTable("PatientAlerts");
        b.HasConsultorio();
        b.Property(x => x.Title).HasMaxLength(150).IsRequired();
        b.Property(x => x.Details).HasMaxLength(1000);
        b.HasTenantReference<PatientAlert, Patient>(nameof(PatientAlert.PatientId));
        b.HasIndex(x => new { x.ConsultorioId, x.PatientId, x.IsActive });
    }
}

internal sealed class WorkingHoursConfiguration : IEntityTypeConfiguration<WorkingHours>
{
    public void Configure(EntityTypeBuilder<WorkingHours> b)
    {
        b.ToTable("WorkingHours");
        b.HasConsultorio();
        b.HasProfessionalReference(nameof(WorkingHours.ProfessionalUserId));
        b.HasTenantReference<WorkingHours, Gabinete>(nameof(WorkingHours.GabineteId), required: false);
        b.HasIndex(x => new { x.ConsultorioId, x.ProfessionalUserId, x.DayOfWeek });
    }
}

internal sealed class ScheduleBlockConfiguration : IEntityTypeConfiguration<ScheduleBlock>
{
    public void Configure(EntityTypeBuilder<ScheduleBlock> b)
    {
        b.ToTable("ScheduleBlocks");
        b.HasConsultorio();
        b.Property(x => x.Reason).HasMaxLength(200);
        b.HasProfessionalReference(nameof(ScheduleBlock.ProfessionalUserId), required: false);
        b.HasTenantReference<ScheduleBlock, Gabinete>(nameof(ScheduleBlock.GabineteId), required: false);
        b.HasIndex(x => new { x.ConsultorioId, x.StartUtc, x.EndUtc });
    }
}

internal sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> b)
    {
        b.ToTable("Appointments");
        b.HasConsultorio();
        b.Property(x => x.Reason).HasMaxLength(250);
        b.Property(x => x.AdministrativeNotes).HasMaxLength(1000);
        b.Property(x => x.CancellationReason).HasMaxLength(250);
        b.Property(x => x.Version).IsConcurrencyToken();
        b.Ignore(x => x.DurationMinutes);
        b.Ignore(x => x.IsActive);
        b.Ignore(x => x.IsEditable);
        b.HasTenantReference<Appointment, Patient>(nameof(Appointment.PatientId), x => x.Patient);
        b.HasProfessionalReference(nameof(Appointment.ProfessionalUserId));
        b.HasTenantReference<Appointment, Gabinete>(nameof(Appointment.GabineteId), x => x.Gabinete, required: false);
        b.HasIndex(x => new { x.ConsultorioId, x.ProfessionalUserId, x.StartUtc });
        b.HasIndex(x => new { x.ConsultorioId, x.GabineteId, x.StartUtc });
        b.HasIndex(x => new { x.ConsultorioId, x.PatientId, x.StartUtc });
        b.HasIndex(x => new { x.ConsultorioId, x.Status, x.StartUtc });
    }
}

internal sealed class AppointmentHistoryConfiguration : IEntityTypeConfiguration<AppointmentHistory>
{
    public void Configure(EntityTypeBuilder<AppointmentHistory> b)
    {
        b.ToTable("AppointmentHistories");
        b.HasConsultorio();
        b.Property(x => x.Action).HasMaxLength(40).IsRequired();
        b.Property(x => x.Details).HasMaxLength(1000);
        b.HasTenantReference<AppointmentHistory, Appointment>(nameof(AppointmentHistory.AppointmentId));
        b.HasIndex(x => new { x.AppointmentId, x.ChangedAtUtc });
    }
}

internal sealed class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> b)
    {
        b.ToTable("Tasks");
        b.HasConsultorio();
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).LongText();
        b.Ignore(x => x.IsOpen);
        b.HasTenantReference<TaskItem, ConsultorioMember>(nameof(TaskItem.AssignedToUserId), required: false, principalKey: nameof(ConsultorioMember.UserId));
        b.HasTenantReference<TaskItem, Patient>(nameof(TaskItem.PatientId), required: false);
        b.HasTenantReference<TaskItem, Appointment>(nameof(TaskItem.AppointmentId), required: false);
        b.HasIndex(x => new { x.ConsultorioId, x.Status, x.DueAtUtc });
        b.HasIndex(x => new { x.ConsultorioId, x.AssignedToUserId, x.Status });
    }
}
