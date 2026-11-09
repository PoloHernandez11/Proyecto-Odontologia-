using FluentValidation;
using Odontia.Application.Features.Auth;
using Odontia.Application.Features.Clinical;
using Odontia.Application.Features.Documents;
using Odontia.Application.Features.Finance;
using Odontia.Application.Features.Lab;
using Odontia.Application.Features.Odontograms;
using Odontia.Application.Features.Organization;
using Odontia.Application.Features.Patients;
using Odontia.Application.Features.Scheduling;
using Odontia.Application.Features.Tasks;
using Odontia.Application.Features.Treatments;
using Odontia.Domain.Authorization;
using Odontia.Domain.Dental;

namespace Odontia.Application.Validation;

internal static class Rules
{
    public static IRuleBuilderOptions<T, string> Email<T>(this IRuleBuilder<T, string> r) =>
        r.NotEmpty().WithMessage("El correo es obligatorio.").MaximumLength(256).EmailAddress().WithMessage("El correo no es válido.");

    public static IRuleBuilderOptions<T, string> Password<T>(this IRuleBuilder<T, string> r) =>
        r.NotEmpty().WithMessage("La contraseña es obligatoria.").MinimumLength(10).WithMessage("La contraseña debe tener al menos 10 caracteres.")
            .MaximumLength(128);

    public static IRuleBuilderOptions<T, string?> Text<T>(this IRuleBuilder<T, string?> r, int max) => r.MaximumLength(max);

    public static IRuleBuilderOptions<T, string> Required<T>(this IRuleBuilder<T, string> r, int max, string label) =>
        r.NotEmpty().WithMessage($"{label} es obligatorio.").MaximumLength(max);

    public static IRuleBuilderOptions<T, decimal> Money<T>(this IRuleBuilder<T, decimal> r) =>
        r.GreaterThanOrEqualTo(0).LessThanOrEqualTo(999_999_999.99m).PrecisionScale(14, 2, true).WithMessage("Importe inválido (máximo 2 decimales).");

    public static IRuleBuilderOptions<T, int?> Tooth<T>(this IRuleBuilder<T, int?> r) =>
        r.Must(t => t is null || Fdi.IsValid(t.Value)).WithMessage("Pieza dental FDI inválida.");
}

// ---------------- Auth ----------------
public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email).Email();
        RuleFor(x => x.Password).Password();
        RuleFor(x => x.FirstName).Required(80, "El nombre");
        RuleFor(x => x.LastName).Required(80, "El apellido");
        RuleFor(x => x.ConsultorioName).Required(150, "El nombre del consultorio");
        RuleFor(x => x.Title).Text(20);
        RuleFor(x => x.Specialty).Text(120);
        RuleFor(x => x.LicenseNumber).Text(40);
        RuleFor(x => x.TimeZoneId).Text(64);
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
    }
}

public sealed class TwoFactorLoginRequestValidator : AbstractValidator<TwoFactorLoginRequest>
{
    public TwoFactorLoginRequestValidator()
    {
        RuleFor(x => x.ChallengeToken).NotEmpty().MaximumLength(2048);
        RuleFor(x => x).Must(x => !string.IsNullOrWhiteSpace(x.Code) || !string.IsNullOrWhiteSpace(x.RecoveryCode))
            .WithName("code").WithMessage("Indique el código o un código de recuperación.");
        RuleFor(x => x.Code).Text(10);
        RuleFor(x => x.RecoveryCode).Text(20);
    }
}

public sealed class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator() => RuleFor(x => x.Email).Email();
}

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Email).Email();
        RuleFor(x => x.Token).NotEmpty().MaximumLength(2048);
        RuleFor(x => x.NewPassword).Password();
    }
}

public sealed class ConfirmEmailRequestValidator : AbstractValidator<ConfirmEmailRequest>
{
    public ConfirmEmailRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Token).NotEmpty().MaximumLength(2048);
    }
}

public sealed class ResendConfirmationRequestValidator : AbstractValidator<ResendConfirmationRequest>
{
    public ResendConfirmationRequestValidator() => RuleFor(x => x.Email).Email();
}

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().MaximumLength(128);
        RuleFor(x => x.NewPassword).Password().NotEqual(x => x.CurrentPassword).WithMessage("La nueva contraseña debe ser distinta de la actual.");
    }
}

public sealed class EnableTwoFactorRequestValidator : AbstractValidator<EnableTwoFactorRequest>
{
    public EnableTwoFactorRequestValidator() => RuleFor(x => x.Code).NotEmpty().MaximumLength(10);
}

public sealed class AcceptInvitationRequestValidator : AbstractValidator<AcceptInvitationRequest>
{
    public AcceptInvitationRequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
        RuleFor(x => x.FirstName).Text(80);
        RuleFor(x => x.LastName).Text(80);
    }
}

// ---------------- Organización ----------------
public sealed class UpdateConsultorioRequestValidator : AbstractValidator<UpdateConsultorioRequest>
{
    public UpdateConsultorioRequestValidator()
    {
        RuleFor(x => x.Name).Required(150, "El nombre");
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Country).NotEmpty().Length(2);
        RuleFor(x => x.LegalName).Text(200);
        RuleFor(x => x.TaxId).Text(20);
        RuleFor(x => x.Phone).Text(40);
        RuleFor(x => x.AddressLine).Text(250);
    }
}

public sealed class UpdateConsultorioSettingsRequestValidator : AbstractValidator<UpdateConsultorioSettingsRequest>
{
    public UpdateConsultorioSettingsRequestValidator()
    {
        RuleFor(x => x.TimeZoneId).NotEmpty().MaximumLength(64);
        RuleFor(x => x.DefaultAppointmentMinutes).InclusiveBetween(5, 480);
        RuleFor(x => x.SlotIntervalMinutes).Must(v => v is 5 or 10 or 15 or 20 or 30 or 60).WithMessage("Intervalo inválido.");
        RuleFor(x => x.ReminderHoursBefore).InclusiveBetween(0, 168);
    }
}

public sealed class SaveGabineteRequestValidator : AbstractValidator<SaveGabineteRequest>
{
    public SaveGabineteRequestValidator()
    {
        RuleFor(x => x.Name).Required(80, "El nombre");
        RuleFor(x => x.Description).Text(250);
    }
}

public sealed class SaveRoleRequestValidator : AbstractValidator<SaveRoleRequest>
{
    public SaveRoleRequestValidator()
    {
        RuleFor(x => x.Name).Required(64, "El nombre");
        RuleFor(x => x.Description).Text(250);
        RuleFor(x => x.Permissions).NotEmpty().WithMessage("Debe incluir al menos un permiso.");
        RuleForEach(x => x.Permissions).Must(Permissions.IsValid).WithMessage("Permiso desconocido: {PropertyValue}.");
    }
}

public sealed class CreateInvitationRequestValidator : AbstractValidator<CreateInvitationRequest>
{
    public CreateInvitationRequestValidator()
    {
        RuleFor(x => x.Email).Email();
        RuleFor(x => x.RoleIds).NotEmpty().WithMessage("Debe indicar al menos un rol.");
    }
}

public sealed class UpdateMemberRolesRequestValidator : AbstractValidator<UpdateMemberRolesRequest>
{
    public UpdateMemberRolesRequestValidator() => RuleFor(x => x.RoleIds).NotEmpty().WithMessage("Debe indicar al menos un rol.");
}

public sealed class UpdateMemberRequestValidator : AbstractValidator<UpdateMemberRequest>
{
    public UpdateMemberRequestValidator()
    {
        RuleFor(x => x.DefaultAppointmentMinutes).InclusiveBetween(5, 480).When(x => x.DefaultAppointmentMinutes is not null);
        RuleFor(x => x.AgendaColor).Matches("^#[0-9A-Fa-f]{6}$").When(x => x.AgendaColor is not null).WithMessage("Color hexadecimal inválido (#RRGGBB).");
    }
}

public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.FirstName).Required(80, "El nombre");
        RuleFor(x => x.LastName).Required(80, "El apellido");
        RuleFor(x => x.Title).Text(20);
        RuleFor(x => x.Phone).Text(40);
    }
}

public sealed class UpdateProfessionalProfileRequestValidator : AbstractValidator<UpdateProfessionalProfileRequest>
{
    public UpdateProfessionalProfileRequestValidator()
    {
        RuleFor(x => x.Specialty).Text(120);
        RuleFor(x => x.LicenseNumber).Text(40);
        RuleFor(x => x.DefaultAppointmentMinutes).InclusiveBetween(5, 480).When(x => x.DefaultAppointmentMinutes is not null);
    }
}

public sealed class ChangeEmailRequestValidator : AbstractValidator<ChangeEmailRequest>
{
    public ChangeEmailRequestValidator()
    {
        RuleFor(x => x.NewEmail).Email();
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
    }
}

// ---------------- Pacientes ----------------
public sealed class SavePatientRequestValidator : AbstractValidator<SavePatientRequest>
{
    public SavePatientRequestValidator()
    {
        RuleFor(x => x.FirstName).Required(80, "El nombre");
        RuleFor(x => x.LastName).Required(80, "El apellido");
        RuleFor(x => x.DocumentNumber).NotEmpty().WithMessage("El DNI es obligatorio.").MaximumLength(20)
            .Matches(@"^[0-9A-Za-z.\- ]+$").WithMessage("El DNI contiene caracteres inválidos.");
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email)).WithMessage("El correo no es válido.");
        RuleFor(x => x.Phone).Text(40).Matches(@"^[0-9+()\-\s]*$").When(x => x.Phone is not null).WithMessage("Teléfono inválido.");
        RuleFor(x => x.EmergencyContactPhone).Text(40);
        RuleFor(x => x.Occupation).Text(100);
        RuleFor(x => x.AddressLine).Text(250);
        RuleFor(x => x.City).Text(100);
        RuleFor(x => x.Province).Text(100);
        RuleFor(x => x.PostalCode).Text(20);
        RuleFor(x => x.EmergencyContactName).Text(120);
        RuleFor(x => x.EmergencyContactRelationship).Text(60);
        RuleFor(x => x.InsurancePlan).Text(60);
        RuleFor(x => x.AffiliateNumber).Text(40);
        RuleFor(x => x.AdministrativeNotes).Text(2000);
        RuleFor(x => x.Sex).IsInEnum();
    }
}

public sealed class CreatePatientRequestValidator : AbstractValidator<CreatePatientRequest>
{
    public CreatePatientRequestValidator()
    {
        RuleFor(x => x.Patient).NotNull().SetValidator(new SavePatientRequestValidator());
        RuleFor(x => x.MedicalHistory!).SetValidator(new MedicalHistoryRequestValidator()).When(x => x.MedicalHistory is not null);
    }
}

public sealed class MedicalHistoryRequestValidator : AbstractValidator<MedicalHistoryRequest>
{
    public MedicalHistoryRequestValidator()
    {
        RuleFor(x => x.Allergies).Text(500);
        RuleFor(x => x.CurrentMedication).Text(1000);
        RuleFor(x => x.OtherConditions).Text(2000);
    }
}

public sealed class SaveAlertRequestValidator : AbstractValidator<SaveAlertRequest>
{
    public SaveAlertRequestValidator()
    {
        RuleFor(x => x.Title).Required(150, "El título");
        RuleFor(x => x.Details).Text(1000);
        RuleFor(x => x.Severity).IsInEnum();
    }
}

public sealed class SaveHealthInsuranceRequestValidator : AbstractValidator<SaveHealthInsuranceRequest>
{
    public SaveHealthInsuranceRequestValidator()
    {
        RuleFor(x => x.Name).Required(120, "El nombre");
        RuleFor(x => x.Code).Text(30);
        RuleFor(x => x.Phone).Text(40);
        RuleFor(x => x.Notes).Text(500);
    }
}

// ---------------- Agenda / turnos ----------------
public sealed class SetWorkingHoursRequestValidator : AbstractValidator<SetWorkingHoursRequest>
{
    public SetWorkingHoursRequestValidator()
    {
        RuleFor(x => x.Slots).NotNull().Must(s => s.Count <= 50).WithMessage("Máximo 50 franjas.");
        RuleForEach(x => x.Slots).ChildRules(s =>
        {
            s.RuleFor(x => x.DayOfWeek).IsInEnum();
            s.RuleFor(x => x.EndTime).GreaterThan(x => x.StartTime).WithMessage("La hora de fin debe ser posterior a la de inicio.");
        });
    }
}

public sealed class SaveScheduleBlockRequestValidator : AbstractValidator<SaveScheduleBlockRequest>
{
    public SaveScheduleBlockRequestValidator()
    {
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.End).GreaterThan(x => x.Start).WithMessage("El fin debe ser posterior al inicio.");
        RuleFor(x => x.Reason).Text(200);
    }
}

public sealed class NonWorkingDayRequestValidator : AbstractValidator<NonWorkingDayRequest>
{
    public NonWorkingDayRequestValidator() => RuleFor(x => x.Reason).Text(200);
}

public sealed class CreateAppointmentRequestValidator : AbstractValidator<CreateAppointmentRequest>
{
    public CreateAppointmentRequestValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty();
        RuleFor(x => x.ProfessionalUserId).NotEmpty();
        RuleFor(x => x.Start).NotEmpty();
        RuleFor(x => x.DurationMinutes).InclusiveBetween(5, 480).When(x => x.DurationMinutes is not null);
        RuleFor(x => x.Reason).Text(250);
        RuleFor(x => x.AdministrativeNotes).Text(1000);
    }
}

public sealed class UpdateAppointmentRequestValidator : AbstractValidator<UpdateAppointmentRequest>
{
    public UpdateAppointmentRequestValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty();
        RuleFor(x => x.Version).NotEmpty();
        RuleFor(x => x.Reason).Text(250);
        RuleFor(x => x.AdministrativeNotes).Text(1000);
    }
}

public sealed class RescheduleAppointmentRequestValidator : AbstractValidator<RescheduleAppointmentRequest>
{
    public RescheduleAppointmentRequestValidator()
    {
        RuleFor(x => x.Start).NotEmpty();
        RuleFor(x => x.Version).NotEmpty();
        RuleFor(x => x.DurationMinutes).InclusiveBetween(5, 480).When(x => x.DurationMinutes is not null);
        RuleFor(x => x.Reason).Text(250);
    }
}

public sealed class ChangeAppointmentStatusRequestValidator : AbstractValidator<ChangeAppointmentStatusRequest>
{
    public ChangeAppointmentStatusRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Reason).Text(250);
    }
}

public sealed class SaveTaskRequestValidator : AbstractValidator<SaveTaskRequest>
{
    public SaveTaskRequestValidator()
    {
        RuleFor(x => x.Title).Required(200, "El título");
        RuleFor(x => x.Description).Text(2000);
        RuleFor(x => x.Priority).IsInEnum();
        RuleFor(x => x.Category).IsInEnum();
    }
}

// ---------------- Clínica ----------------
public sealed class CreateClinicalEntryRequestValidator : AbstractValidator<CreateClinicalEntryRequest>
{
    public CreateClinicalEntryRequestValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Title).Required(200, "El procedimiento / título");
        RuleFor(x => x.Description).Text(8000);
        RuleFor(x => x.Indications).Text(2000);
        RuleForEach(x => x.Teeth).Must(Fdi.IsValid).WithMessage("Pieza dental FDI inválida: {PropertyValue}.");
    }
}

public sealed class AmendClinicalEntryRequestValidator : AbstractValidator<AmendClinicalEntryRequest>
{
    public AmendClinicalEntryRequestValidator()
    {
        RuleFor(x => x.Title).Required(200, "El procedimiento / título");
        RuleFor(x => x.Description).Text(8000);
        RuleFor(x => x.Indications).Text(2000);
        RuleFor(x => x.Reason).Required(500, "El motivo de la corrección");
        RuleForEach(x => x.Teeth).Must(Fdi.IsValid).WithMessage("Pieza dental FDI inválida: {PropertyValue}.");
    }
}

public sealed class VoidClinicalEntryRequestValidator : AbstractValidator<VoidClinicalEntryRequest>
{
    public VoidClinicalEntryRequestValidator() => RuleFor(x => x.Reason).Required(500, "El motivo");
}

public sealed class CreateDiagnosisRequestValidator : AbstractValidator<CreateDiagnosisRequest>
{
    public CreateDiagnosisRequestValidator()
    {
        RuleFor(x => x.Description).Required(300, "El diagnóstico");
        RuleFor(x => x.Code).Text(20);
        RuleFor(x => x.ToothCode).Tooth();
        RuleFor(x => x.Notes).Text(2000);
    }
}

public sealed class ApplyConditionRequestValidator : AbstractValidator<ApplyConditionRequest>
{
    public ApplyConditionRequestValidator()
    {
        RuleFor(x => x.ToothCode).Must(Fdi.IsValid).WithMessage("Pieza dental FDI inválida.");
        RuleFor(x => x.ConditionId).NotEmpty();
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Notes).Text(500);
    }
}

public sealed class ApplyConditionsRequestValidator : AbstractValidator<ApplyConditionsRequest>
{
    public ApplyConditionsRequestValidator()
    {
        RuleFor(x => x.Items).NotEmpty().Must(i => i.Count <= 64);
        RuleForEach(x => x.Items).SetValidator(new ApplyConditionRequestValidator());
    }
}

public sealed class SaveConditionRequestValidator : AbstractValidator<SaveConditionRequest>
{
    public SaveConditionRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(30).Matches("^[A-Za-z0-9_]+$");
        RuleFor(x => x.Name).Required(80, "El nombre");
        RuleFor(x => x.Scope).IsInEnum();
        RuleFor(x => x.Color).Matches("^#[0-9A-Fa-f]{6}$").When(x => x.Color is not null);
    }
}

public sealed class AddToothNoteRequestValidator : AbstractValidator<AddToothNoteRequest>
{
    public AddToothNoteRequestValidator() => RuleFor(x => x.Text).Required(1000, "La nota");
}

public sealed class RemoveFindingRequestValidator : AbstractValidator<RemoveFindingRequest>
{
    public RemoveFindingRequestValidator() => RuleFor(x => x.Reason).Required(250, "El motivo");
}

// ---------------- Tratamientos ----------------
public sealed class SaveProcedureRequestValidator : AbstractValidator<SaveProcedureRequest>
{
    public SaveProcedureRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(30).Matches("^[A-Za-z0-9_.\\-]+$");
        RuleFor(x => x.Name).Required(150, "El nombre");
        RuleFor(x => x.Description).Text(500);
        RuleFor(x => x.Price).Money();
        RuleFor(x => x.DefaultDurationMinutes).InclusiveBetween(5, 480).When(x => x.DefaultDurationMinutes is not null);
    }
}

public sealed class AddPlanItemRequestValidator : AbstractValidator<AddPlanItemRequest>
{
    public AddPlanItemRequestValidator()
    {
        RuleFor(x => x.ProcedureId).NotEmpty();
        RuleFor(x => x.ToothCode).Tooth();
        RuleFor(x => x.Notes).Text(1000);
    }
}

public sealed class CreateTreatmentPlanRequestValidator : AbstractValidator<CreateTreatmentPlanRequest>
{
    public CreateTreatmentPlanRequestValidator()
    {
        RuleFor(x => x.Title).Required(150, "El título");
        RuleFor(x => x.Notes).Text(2000);
        RuleForEach(x => x.Items).SetValidator(new AddPlanItemRequestValidator());
    }
}

public sealed class ChangeItemStatusRequestValidator : AbstractValidator<ChangeItemStatusRequest>
{
    public ChangeItemStatusRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Note).Text(500);
    }
}

public sealed class CancelPlanRequestValidator : AbstractValidator<CancelPlanRequest>
{
    public CancelPlanRequestValidator() => RuleFor(x => x.Reason).Required(500, "El motivo");
}

// ---------------- Finanzas ----------------
public sealed class BudgetItemRequestValidator : AbstractValidator<BudgetItemRequest>
{
    public BudgetItemRequestValidator()
    {
        RuleFor(x => x).Must(x => x.ProcedureId is not null || !string.IsNullOrWhiteSpace(x.Description))
            .WithName("description").WithMessage("Indique una prestación o una descripción.");
        RuleFor(x => x.Description).Text(200);
        RuleFor(x => x.Quantity).InclusiveBetween(1, 1000);
        RuleFor(x => x.UnitPrice!.Value).Money().When(x => x.UnitPrice is not null);
        RuleFor(x => x.DiscountPercent).InclusiveBetween(0, 100).PrecisionScale(5, 2, true);
        RuleFor(x => x.ToothCode).Tooth();
    }
}

public sealed class SaveBudgetRequestValidator : AbstractValidator<SaveBudgetRequest>
{
    public SaveBudgetRequestValidator()
    {
        RuleFor(x => x.DiscountPercent).InclusiveBetween(0, 100).PrecisionScale(5, 2, true);
        RuleFor(x => x.Notes).Text(2000);
        RuleFor(x => x.ExpirationDate).GreaterThanOrEqualTo(x => x.IssueDate).When(x => x.IssueDate is not null && x.ExpirationDate is not null);
        RuleForEach(x => x.Items).SetValidator(new BudgetItemRequestValidator());
        RuleFor(x => x).Must(x => x.ImportPlanItems || (x.Items?.Count ?? 0) > 0).WithName("items").WithMessage("El presupuesto debe tener al menos una línea.");
    }
}

public sealed class RegisterPaymentRequestValidator : AbstractValidator<RegisterPaymentRequest>
{
    public RegisterPaymentRequestValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("El importe debe ser mayor que cero.").Money();
        RuleFor(x => x.Method).IsInEnum();
        RuleFor(x => x.Reference).Text(100);
        RuleFor(x => x.Notes).Text(500);
    }
}

public sealed class VoidPaymentRequestValidator : AbstractValidator<VoidPaymentRequest>
{
    public VoidPaymentRequestValidator() => RuleFor(x => x.Reason).Required(500, "El motivo de anulación");
}

// ---------------- Laboratorio ----------------
public sealed class SaveLaboratoryRequestValidator : AbstractValidator<SaveLaboratoryRequest>
{
    public SaveLaboratoryRequestValidator()
    {
        RuleFor(x => x.Name).Required(120, "El nombre");
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.ContactName).Text(120);
        RuleFor(x => x.Phone).Text(40);
        RuleFor(x => x.AddressLine).Text(250);
        RuleFor(x => x.Notes).Text(1000);
    }
}

public sealed class SaveLabWorkRequestValidator : AbstractValidator<SaveLabWorkRequest>
{
    public SaveLabWorkRequestValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty();
        RuleFor(x => x.LaboratoryId).NotEmpty();
        RuleFor(x => x.WorkType).Required(120, "El tipo de trabajo");
        RuleFor(x => x.ToothCode).Tooth();
        RuleFor(x => x.Description).Text(1000);
        RuleFor(x => x.Notes).Text(1000);
        RuleFor(x => x.Cost!.Value).Money().When(x => x.Cost is not null);
    }
}

public sealed class ChangeLabWorkStatusRequestValidator : AbstractValidator<ChangeLabWorkStatusRequest>
{
    public ChangeLabWorkStatusRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Note).Text(500);
    }
}

// ---------------- Documentos ----------------
public sealed class CreateConsentRequestValidator : AbstractValidator<CreateConsentRequest>
{
    public CreateConsentRequestValidator()
    {
        RuleFor(x => x.Title).Required(200, "El título");
        RuleFor(x => x.Content).Required(20000, "El texto del consentimiento");
    }
}

public sealed class RecordConsentAcceptanceRequestValidator : AbstractValidator<RecordConsentAcceptanceRequest>
{
    public RecordConsentAcceptanceRequestValidator()
    {
        RuleFor(x => x.AcceptedByName).Required(150, "El nombre de quien acepta");
        RuleFor(x => x.AcceptedByDocument).Text(20);
        RuleFor(x => x.Relationship).Text(60);
    }
}

public sealed class AttachSignedConsentRequestValidator : AbstractValidator<AttachSignedConsentRequest>
{
    public AttachSignedConsentRequestValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty();
        RuleFor(x => x.SignedByName).Required(150, "El nombre de quien firma");
    }
}

public sealed class DeleteDocumentRequestValidator : AbstractValidator<DeleteDocumentRequest>
{
    public DeleteDocumentRequestValidator() => RuleFor(x => x.Reason).Required(250, "El motivo");
}
