using Odontia.Application.Common;
using Odontia.Domain.Enums;

namespace Odontia.Application.Features.Patients;

public sealed record HealthInsuranceDto(Guid Id, string Name, string? Code, string? Phone, string? Notes, bool IsActive);

public sealed record SaveHealthInsuranceRequest(string Name, string? Code, string? Phone, string? Notes, bool IsActive = true);

/// <summary>Datos personales y administrativos. Los antecedentes médicos se gestionan en su propio recurso.</summary>
public sealed record SavePatientRequest(
    string FirstName, string LastName, string DocumentNumber, DateOnly? BirthDate, Sex Sex, string? Occupation,
    string? Phone, string? Email, string? AddressLine, string? City, string? Province, string? PostalCode,
    string? EmergencyContactName, string? EmergencyContactPhone, string? EmergencyContactRelationship,
    Guid? HealthInsuranceId, string? InsurancePlan, string? AffiliateNumber, string? AdministrativeNotes);

/// <summary>Alta con antecedentes opcionales (sólo se aplican si el usuario tiene permiso de antecedentes médicos).</summary>
public sealed record CreatePatientRequest(SavePatientRequest Patient, MedicalHistoryRequest? MedicalHistory);

public sealed record ChangePatientStatusRequest(PatientStatus Status);

public sealed record PatientListItemDto(Guid Id, string FirstName, string LastName, string DocumentNumber, int? Age, string? Phone,
    string? HealthInsurance, string? InsurancePlan, PatientStatus Status, DateTime? LastVisitUtc, DateTime? NextAppointmentUtc);

public sealed record PatientDto(Guid Id, string FirstName, string LastName, string DocumentNumber, DateOnly? BirthDate, int? Age, Sex Sex,
    string? Occupation, string? Phone, string? Email, string? AddressLine, string? City, string? Province, string? PostalCode,
    string? EmergencyContactName, string? EmergencyContactPhone, string? EmergencyContactRelationship,
    Guid? HealthInsuranceId, string? HealthInsuranceName, string? InsurancePlan, string? AffiliateNumber, string? AdministrativeNotes,
    PatientStatus Status, DateTime CreatedAtUtc, IReadOnlyList<AlertDto> Alerts);

/// <summary>Alerta visible para la atención. <see cref="Details"/> sólo se completa con permiso de antecedentes médicos.</summary>
public sealed record AlertDto(Guid? Id, string Title, string? Details, AlertSeverity Severity, bool IsDerived, bool VisibleToReception);

public sealed record MedicalHistoryRequest(bool Diabetes, bool Hypertension, bool HeartDisease, bool Anticoagulated, bool Pregnant,
    bool Asthma, bool Hepatitis, bool Smoker, string? Allergies, string? CurrentMedication, string? OtherConditions);

public sealed record MedicalHistoryDto(bool Diabetes, bool Hypertension, bool HeartDisease, bool Anticoagulated, bool Pregnant,
    bool Asthma, bool Hepatitis, bool Smoker, string? Allergies, string? CurrentMedication, string? OtherConditions,
    DateTime? UpdatedAtUtc, string? UpdatedBy);

public sealed record SaveAlertRequest(string Title, string? Details, AlertSeverity Severity, bool VisibleToReception);

public sealed class PatientQuery : PageQuery
{
    /// <summary>Busca por nombre, apellido, DNI o teléfono.</summary>
    public string? Search { get; set; }
    public PatientStatus? Status { get; set; }
    public Guid? HealthInsuranceId { get; set; }
    /// <summary>Sólo pacientes sin obra social (particulares).</summary>
    public bool? Private { get; set; }
}

public sealed record PatientSummaryDto(PatientDto Patient, IReadOnlyList<UpcomingAppointmentDto> UpcomingAppointments,
    DateTime? LastVisitUtc, int TotalAppointments, int NoShows, AccountSummaryDto? Account, TreatmentSummaryDto? ActiveTreatment,
    LastEvolutionDto? LastEvolution, int OpenLabWorks, int Documents);

public sealed record UpcomingAppointmentDto(Guid Id, DateTime StartUtc, DateTime EndUtc, string Professional, string? Reason, AppointmentStatus Status);

public sealed record AccountSummaryDto(decimal ApprovedBudgets, decimal Paid, decimal Balance);

public sealed record TreatmentSummaryDto(Guid PlanId, string Title, TreatmentStatus Status, int ProgressPercent, int CompletedItems, int TotalItems, DateOnly? StartDate);

public sealed record LastEvolutionDto(Guid Id, DateTime OccurredAtUtc, string Professional, string Title, string? Description);

public sealed record PatientHistoryItemDto(DateTime AtUtc, string Kind, string Description, string? User);
