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
using Odontia.Application.Validation;
using Odontia.Domain.Entities;
using Odontia.Domain.Enums;

namespace Odontia.UnitTests.QA;

public class QaAuthValidatorTests
{
    private static RegisterRequest Reg(string email = "ana@clinica.com", string pwd = "Segura12345", string first = "Ana",
        string consultorio = "Clínica") => new(email, pwd, first, "Pérez", consultorio, null, null, null, null);

    [Fact]
    public void Valid_register_passes() => Assert.True(new RegisterRequestValidator().Validate(Reg()).IsValid);

    [Theory]
    [InlineData("")]
    [InlineData("no-es-correo")]
    [InlineData("@dominio.com")]
    public void Register_rejects_invalid_email(string email) =>
        Assert.Contains(new RegisterRequestValidator().Validate(Reg(email: email)).Errors, e => e.PropertyName == "Email");

    [Fact]
    public void Register_email_over_256_is_rejected()
    {
        var email = new string('a', 250) + "@x.com"; // 256 caracteres
        Assert.True(new RegisterRequestValidator().Validate(Reg(email: email)).IsValid);
        Assert.False(new RegisterRequestValidator().Validate(Reg(email: "a" + email)).IsValid);
    }

    [Theory]
    [InlineData("123456789", false)]
    [InlineData("1234567890", true)]
    public void Register_password_minimum_is_10(string pwd, bool valid) =>
        Assert.Equal(valid, new RegisterRequestValidator().Validate(Reg(pwd: pwd)).IsValid);

    [Fact]
    public void Register_password_max_128()
    {
        Assert.True(new RegisterRequestValidator().Validate(Reg(pwd: new string('a', 128))).IsValid);
        Assert.False(new RegisterRequestValidator().Validate(Reg(pwd: new string('a', 129))).IsValid);
    }

    [Fact]
    public void Register_requires_names_and_consultorio()
    {
        var r = new RegisterRequestValidator().Validate(Reg(first: " ", consultorio: ""));
        Assert.Contains(r.Errors, e => e.PropertyName == "FirstName");
        Assert.Contains(r.Errors, e => e.PropertyName == "ConsultorioName");
        Assert.False(new RegisterRequestValidator().Validate(Reg(first: new string('a', 81))).IsValid);
    }

    [Fact]
    public void Two_factor_requires_code_or_recovery_code()
    {
        var v = new TwoFactorLoginRequestValidator();
        Assert.False(v.Validate(new TwoFactorLoginRequest("tok", null, " ")).IsValid);
        Assert.True(v.Validate(new TwoFactorLoginRequest("tok", "123456", null)).IsValid);
        Assert.True(v.Validate(new TwoFactorLoginRequest("tok", null, "ABCD-EFGH")).IsValid);
        Assert.False(v.Validate(new TwoFactorLoginRequest("", "123456", null)).IsValid);
        Assert.False(v.Validate(new TwoFactorLoginRequest("tok", "12345678901", null)).IsValid);
    }

    [Fact]
    public void Change_password_must_differ_and_follow_policy()
    {
        var v = new ChangePasswordRequestValidator();
        Assert.False(v.Validate(new ChangePasswordRequest("Segura12345", "Segura12345")).IsValid);
        Assert.False(v.Validate(new ChangePasswordRequest("Segura12345", "corta")).IsValid);
        Assert.True(v.Validate(new ChangePasswordRequest("Segura12345", "OtraSegura123")).IsValid);
    }

    [Fact]
    public void Reset_password_requires_token_and_policy()
    {
        var v = new ResetPasswordRequestValidator();
        Assert.False(v.Validate(new ResetPasswordRequest("a@b.com", "", "Segura12345")).IsValid);
        Assert.False(v.Validate(new ResetPasswordRequest("a@b.com", new string('t', 2049), "Segura12345")).IsValid);
        Assert.True(v.Validate(new ResetPasswordRequest("a@b.com", "tok", "Segura12345")).IsValid);
    }

    [Fact]
    public void Login_requires_email_and_password()
    {
        var v = new LoginRequestValidator();
        Assert.False(v.Validate(new LoginRequest("", "x", null)).IsValid);
        Assert.False(v.Validate(new LoginRequest("a@b.com", "", null)).IsValid);
        Assert.True(v.Validate(new LoginRequest("a@b.com", "x", null)).IsValid);
    }

    [Fact]
    public void Accept_invitation_enforces_same_password_policy_as_register()
    {
        Assert.False(new AcceptInvitationRequestValidator().Validate(new AcceptInvitationRequest("tok", "Ana", "Pérez", "123")).IsValid);
    }
}

public class QaOrganizationValidatorTests
{
    private static UpdateConsultorioRequest Req(string name = "Clínica", string? email = null, string? website = null, string? city = null,
        string country = "AR") => new(name, null, null, null, email, website, null, city, null, null, country);

    [Fact]
    public void Consultorio_valid() => Assert.True(new UpdateConsultorioRequestValidator().Validate(Req()).IsValid);

    [Theory]
    [InlineData("A")]
    [InlineData("ARG")]
    [InlineData("")]
    public void Consultorio_country_must_have_two_chars(string c) => Assert.False(new UpdateConsultorioRequestValidator().Validate(Req(country: c)).IsValid);

    [Fact]
    public void Consultorio_invalid_email_rejected_and_blank_allowed()
    {
        Assert.False(new UpdateConsultorioRequestValidator().Validate(Req(email: "x")).IsValid);
        Assert.True(new UpdateConsultorioRequestValidator().Validate(Req(email: " ")).IsValid);
    }

    [Fact]
    public void Consultorio_validator_limits_all_text_fields()
    {
        Assert.False(new UpdateConsultorioRequestValidator().Validate(Req(website: "https://" + new string('w', 260))).IsValid);
        Assert.False(new UpdateConsultorioRequestValidator().Validate(Req(city: new string('c', 101))).IsValid);
    }

    [Theory]
    [InlineData(5, 5, 0, true)]
    [InlineData(480, 60, 168, true)]
    [InlineData(4, 15, 24, false)]
    [InlineData(30, 25, 24, false)]
    [InlineData(30, 15, 169, false)]
    [InlineData(30, 15, -1, false)]
    public void Settings_ranges(int def, int slot, int rem, bool valid) =>
        Assert.Equal(valid, new UpdateConsultorioSettingsRequestValidator().Validate(new UpdateConsultorioSettingsRequest("UTC", def, slot, rem)).IsValid);

    [Theory]
    [InlineData("#A1B2C3", true)]
    [InlineData("#a1b2c3", true)]
    [InlineData("A1B2C3", false)]
    [InlineData("#FFF", false)]
    [InlineData("#GGGGGG", false)]
    [InlineData("", false)]
    public void Member_agenda_color(string color, bool valid) =>
        Assert.Equal(valid, new UpdateMemberRequestValidator().Validate(new UpdateMemberRequest(true, null, color)).IsValid);

    [Fact]
    public void Role_requires_permissions()
    {
        Assert.False(new SaveRoleRequestValidator().Validate(new SaveRoleRequest("Rol", null, [])).IsValid);
        Assert.True(new SaveRoleRequestValidator().Validate(new SaveRoleRequest("Rol", null, ["patients.read"])).IsValid);
    }

    [Fact]
    public void Invitation_requires_role_and_valid_email()
    {
        Assert.False(new CreateInvitationRequestValidator().Validate(new CreateInvitationRequest("a@b.com", [], false)).IsValid);
        Assert.False(new CreateInvitationRequestValidator().Validate(new CreateInvitationRequest("ab", [Guid.NewGuid()], false)).IsValid);
    }
}

public class QaPatientValidatorTests
{
    private static SavePatientRequest P(string doc = "30.123.456", string? phone = null, string? email = null, Sex sex = Sex.Female, string first = "Ana") =>
        new(first, "Pérez", doc, null, sex, null, phone, email, null, null, null, null, null, null, null, null, null, null, null);

    [Fact]
    public void Valid_patient_passes() => Assert.True(new SavePatientRequestValidator().Validate(P()).IsValid);

    [Theory]
    [InlineData("")]
    [InlineData("30/123/456")]
    [InlineData("30123456_")]
    [InlineData("123456789012345678901")]
    public void Invalid_document_is_rejected(string doc) => Assert.False(new SavePatientRequestValidator().Validate(P(doc: doc)).IsValid);

    [Fact]
    public void Document_validator_matches_domain_length_rule()
    {
        Assert.False(new SavePatientRequestValidator().Validate(P(doc: "1.2.3")).IsValid);
    }

    [Theory]
    [InlineData("+54 (11) 4555-1234", true)]
    [InlineData("11 4555 1234", true)]
    [InlineData("11-4555-1234 int 2", false)]
    [InlineData("abc", false)]
    public void Phone_characters(string phone, bool valid) => Assert.Equal(valid, new SavePatientRequestValidator().Validate(P(phone: phone)).IsValid);

    [Fact]
    public void Undefined_sex_and_invalid_email_are_rejected()
    {
        Assert.False(new SavePatientRequestValidator().Validate(P(sex: (Sex)9)).IsValid);
        Assert.False(new SavePatientRequestValidator().Validate(P(email: "ana@")).IsValid);
        Assert.True(new SavePatientRequestValidator().Validate(P(email: "")).IsValid);
    }

    [Fact]
    public void Create_patient_validates_nested_medical_history()
    {
        var mh = new MedicalHistoryRequest(false, false, false, false, false, false, false, false, new string('a', 501), null, null);
        var r = new CreatePatientRequestValidator().Validate(new CreatePatientRequest(P(), mh));
        Assert.False(r.IsValid);
        Assert.True(new CreatePatientRequestValidator().Validate(new CreatePatientRequest(P(), null)).IsValid);
        Assert.False(new CreatePatientRequestValidator().Validate(new CreatePatientRequest(P(first: ""), null)).IsValid);
    }

    [Fact]
    public void Alert_and_insurance_validators()
    {
        Assert.False(new SaveAlertRequestValidator().Validate(new SaveAlertRequest("", null, AlertSeverity.Info, false)).IsValid);
        Assert.False(new SaveAlertRequestValidator().Validate(new SaveAlertRequest("Marcapasos", null, (AlertSeverity)0, false)).IsValid);
        Assert.False(new SaveHealthInsuranceRequestValidator().Validate(new SaveHealthInsuranceRequest(new string('n', 121), null, null, null)).IsValid);
    }
}

public class QaSchedulingValidatorTests
{
    private static readonly DateTimeOffset T = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Working_hours_slots_need_end_after_start_and_max_50()
    {
        var v = new SetWorkingHoursRequestValidator();
        Assert.False(v.Validate(new SetWorkingHoursRequest([new WorkingHoursSlotRequest(DayOfWeek.Monday, new TimeOnly(10, 0), new TimeOnly(10, 0), null)])).IsValid);
        Assert.False(v.Validate(new SetWorkingHoursRequest([new WorkingHoursSlotRequest((DayOfWeek)7, new TimeOnly(9, 0), new TimeOnly(10, 0), null)])).IsValid);
        var many = Enumerable.Range(0, 51).Select(_ => new WorkingHoursSlotRequest(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(10, 0), null)).ToList();
        Assert.False(v.Validate(new SetWorkingHoursRequest(many)).IsValid);
        Assert.True(v.Validate(new SetWorkingHoursRequest([])).IsValid);
    }

    [Fact]
    public void Schedule_block_end_after_start()
    {
        var v = new SaveScheduleBlockRequestValidator();
        Assert.False(v.Validate(new SaveScheduleBlockRequest(ScheduleBlockKind.Blocked, T, T, null, null, null)).IsValid);
        Assert.False(v.Validate(new SaveScheduleBlockRequest((ScheduleBlockKind)0, T, T.AddHours(1), null, null, null)).IsValid);
        Assert.True(v.Validate(new SaveScheduleBlockRequest(ScheduleBlockKind.Blocked, T, T.AddHours(1), null, null, null)).IsValid);
    }

    [Theory]
    [InlineData(4, false)]
    [InlineData(5, true)]
    [InlineData(480, true)]
    [InlineData(481, false)]
    public void Appointment_duration_range(int minutes, bool valid) =>
        Assert.Equal(valid, new CreateAppointmentRequestValidator().Validate(new CreateAppointmentRequest(Guid.NewGuid(), Guid.NewGuid(), null, T, minutes, null, null)).IsValid);

    [Fact]
    public void Appointment_requires_patient_professional_and_start()
    {
        var r = new CreateAppointmentRequestValidator().Validate(new CreateAppointmentRequest(Guid.Empty, Guid.Empty, null, default, null, null, null));
        Assert.Contains(r.Errors, e => e.PropertyName == "PatientId");
        Assert.Contains(r.Errors, e => e.PropertyName == "ProfessionalUserId");
        Assert.Contains(r.Errors, e => e.PropertyName == "Start");
    }

    [Fact]
    public void Update_and_reschedule_require_version()
    {
        Assert.False(new UpdateAppointmentRequestValidator().Validate(new UpdateAppointmentRequest(Guid.NewGuid(), null, null, Guid.Empty)).IsValid);
        Assert.False(new RescheduleAppointmentRequestValidator().Validate(new RescheduleAppointmentRequest(T, null, null, null, false, null, Guid.Empty)).IsValid);
    }

    [Fact]
    public void Status_change_rejects_undefined_status()
    {
        Assert.False(new ChangeAppointmentStatusRequestValidator().Validate(new ChangeAppointmentStatusRequest((AppointmentStatus)42, null)).IsValid);
        Assert.False(new ChangeAppointmentStatusRequestValidator().Validate(new ChangeAppointmentStatusRequest(AppointmentStatus.Cancelled, new string('r', 251))).IsValid);
    }

    [Fact]
    public void Task_validator()
    {
        var v = new SaveTaskRequestValidator();
        Assert.False(v.Validate(new SaveTaskRequest("", null, TaskPriority.Low, TaskCategory.General, null, null, null, null)).IsValid);
        Assert.False(v.Validate(new SaveTaskRequest("T", null, (TaskPriority)0, TaskCategory.General, null, null, null, null)).IsValid);
        Assert.False(v.Validate(new SaveTaskRequest("T", null, TaskPriority.Low, (TaskCategory)99, null, null, null, null)).IsValid);
        Assert.False(v.Validate(new SaveTaskRequest(new string('t', 201), null, TaskPriority.Low, TaskCategory.General, null, null, null, null)).IsValid);
        Assert.True(v.Validate(new SaveTaskRequest(new string('t', 200), null, TaskPriority.Low, TaskCategory.General, null, null, null, null)).IsValid);
    }
}

public class QaClinicalValidatorTests
{
    [Fact]
    public void Clinical_entry_rejects_invalid_fdi_and_type()
    {
        var v = new CreateClinicalEntryRequestValidator();
        Assert.False(v.Validate(new CreateClinicalEntryRequest(ClinicalEntryType.Evolution, null, "T", null, null, [11, 19], null)).IsValid);
        Assert.False(v.Validate(new CreateClinicalEntryRequest((ClinicalEntryType)0, null, "T", null, null, null, null)).IsValid);
        Assert.True(v.Validate(new CreateClinicalEntryRequest(ClinicalEntryType.Evolution, null, "T", null, null, null, null)).IsValid);
    }

    [Fact]
    public void Amend_requires_reason()
    {
        Assert.False(new AmendClinicalEntryRequestValidator().Validate(new AmendClinicalEntryRequest("T", null, null, null, " ", 1)).IsValid);
        Assert.False(new VoidClinicalEntryRequestValidator().Validate(new VoidClinicalEntryRequest("")).IsValid);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(36, true)]
    [InlineData(0, false)]
    [InlineData(39, false)]
    public void Diagnosis_tooth(int? tooth, bool valid) =>
        Assert.Equal(valid, new CreateDiagnosisRequestValidator().Validate(new CreateDiagnosisRequest("Caries", null, tooth, null, null, null, null)).IsValid);

    [Fact]
    public void Apply_conditions_limits_batch_and_validates_each()
    {
        var ok = new ApplyConditionRequest(16, ToothSurface.Occlusal, Guid.NewGuid(), FindingStatus.Existing, null, null);
        var v = new ApplyConditionsRequestValidator();
        Assert.True(v.Validate(new ApplyConditionsRequest([ok])).IsValid);
        Assert.False(v.Validate(new ApplyConditionsRequest([])).IsValid);
        Assert.False(v.Validate(new ApplyConditionsRequest(Enumerable.Repeat(ok, 65).ToList())).IsValid);
        Assert.False(v.Validate(new ApplyConditionsRequest([ok with { ToothCode = 99 }])).IsValid);
        Assert.False(v.Validate(new ApplyConditionsRequest([ok with { ConditionId = Guid.Empty }])).IsValid);
        Assert.False(v.Validate(new ApplyConditionsRequest([ok with { Status = (FindingStatus)3 }])).IsValid);
    }

    [Theory]
    [InlineData("CARIES_2", true)]
    [InlineData("caries 2", false)]
    [InlineData("CÁRIES", false)]
    public void Condition_code_charset(string code, bool valid) =>
        Assert.Equal(valid, new SaveConditionRequestValidator().Validate(new SaveConditionRequest(code, "Nombre", ConditionScope.Tooth, null, false)).IsValid);

    [Fact]
    public void Tooth_note_and_remove_finding_limits()
    {
        Assert.False(new AddToothNoteRequestValidator().Validate(new AddToothNoteRequest(new string('x', 1001))).IsValid);
        Assert.False(new RemoveFindingRequestValidator().Validate(new RemoveFindingRequest(new string('x', 251))).IsValid);
    }

    [Fact]
    public void Procedure_and_plan_validators()
    {
        Assert.False(new SaveProcedureRequestValidator().Validate(new SaveProcedureRequest("A B", "N", null, 10, false, null)).IsValid);
        Assert.False(new SaveProcedureRequestValidator().Validate(new SaveProcedureRequest("A1", "N", null, -1, false, null)).IsValid);
        Assert.False(new SaveProcedureRequestValidator().Validate(new SaveProcedureRequest("A1", "N", null, 10.001m, false, null)).IsValid);
        Assert.True(new SaveProcedureRequestValidator().Validate(new SaveProcedureRequest("RES.01-A", "N", null, 0, false, 30)).IsValid);
        var plan = new CreateTreatmentPlanRequest("Plan", null, null, [new AddPlanItemRequest(Guid.Empty, 99, null, null, null, null)]);
        var r = new CreateTreatmentPlanRequestValidator().Validate(plan);
        Assert.True(r.Errors.Count >= 2);
        Assert.False(new CancelPlanRequestValidator().Validate(new CancelPlanRequest(" ")).IsValid);
    }
}

public class QaFinanceValidatorTests
{
    private static BudgetItemRequest Item(int qty = 1, decimal? price = 100, decimal disc = 0, int? tooth = null, string? desc = "Resina", Guid? proc = null) =>
        new(proc, desc, tooth, qty, price, disc, null);

    [Theory]
    [InlineData(0)]
    [InlineData(1001)]
    public void Budget_item_quantity_range(int qty) => Assert.False(new BudgetItemRequestValidator().Validate(Item(qty: qty)).IsValid);

    [Theory]
    [InlineData("-1")]
    [InlineData("100.01")]
    [InlineData("10.125")]
    public void Budget_item_discount(string raw) =>
        Assert.False(new BudgetItemRequestValidator().Validate(Item(disc: decimal.Parse(raw, System.Globalization.CultureInfo.InvariantCulture))).IsValid);

    [Fact]
    public void Budget_item_needs_procedure_or_description()
    {
        Assert.False(new BudgetItemRequestValidator().Validate(Item(desc: " ")).IsValid);
        Assert.True(new BudgetItemRequestValidator().Validate(Item(desc: null, proc: Guid.NewGuid(), price: null)).IsValid);
    }

    [Fact]
    public void Budget_item_price_money_rules()
    {
        Assert.False(new BudgetItemRequestValidator().Validate(Item(price: -0.01m)).IsValid);
        Assert.False(new BudgetItemRequestValidator().Validate(Item(price: 1_000_000_000m)).IsValid);
        Assert.True(new BudgetItemRequestValidator().Validate(Item(price: 999_999_999.99m)).IsValid);
    }

    [Fact]
    public void Budget_dates_and_import()
    {
        var d = new DateOnly(2026, 11, 19);
        var v = new SaveBudgetRequestValidator();
        Assert.False(v.Validate(new SaveBudgetRequest(null, d, d.AddDays(-1), 0, null, [Item()])).IsValid);
        Assert.True(v.Validate(new SaveBudgetRequest(null, d, d, 0, null, [Item()])).IsValid);
        Assert.True(v.Validate(new SaveBudgetRequest(Guid.NewGuid(), null, null, 0, null, null, ImportPlanItems: true)).IsValid);
        Assert.False(v.Validate(new SaveBudgetRequest(null, null, null, 0, null, null)).IsValid);
    }

    [Theory]
    [InlineData("0.01", true)]
    [InlineData("999999999.99", true)]
    [InlineData("1000000000", false)]
    public void Payment_amount_bounds(string raw, bool valid) =>
        Assert.Equal(valid, new RegisterPaymentRequestValidator().Validate(new RegisterPaymentRequest(Guid.NewGuid(), null,
            decimal.Parse(raw, System.Globalization.CultureInfo.InvariantCulture), PaymentMethod.Cash, null, null, null)).IsValid);

    [Fact]
    public void Expense_validator()
    {
        var v = new RegisterExpenseRequestValidator();
        Assert.False(v.Validate(new RegisterExpenseRequest("", ExpenseCategory.Rent, 10, PaymentMethod.Cash, null, null, null, null, null)).IsValid);
        Assert.False(v.Validate(new RegisterExpenseRequest("Alquiler", (ExpenseCategory)50, 10, PaymentMethod.Cash, null, null, null, null, null)).IsValid);
        Assert.False(v.Validate(new RegisterExpenseRequest("Alquiler", ExpenseCategory.Rent, 0, PaymentMethod.Cash, null, null, null, null, null)).IsValid);
        Assert.True(v.Validate(new RegisterExpenseRequest("Alquiler", ExpenseCategory.Rent, 10, PaymentMethod.Other, null, null, null, null, null)).IsValid);
        Assert.False(new VoidExpenseRequestValidator().Validate(new VoidExpenseRequest("")).IsValid);
        Assert.False(new VoidPaymentRequestValidator().Validate(new VoidPaymentRequest(new string('r', 501))).IsValid);
    }
}

public class QaLabAndDocumentValidatorTests
{
    private static SaveLabWorkRequest W(IReadOnlyList<int>? teeth = null, int? tooth = null, decimal? cost = null, Guid? appt = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), null, "Corona", tooth, null, null, cost, null, null, appt, teeth);

    [Fact]
    public void Lab_work_valid() => Assert.True(new SaveLabWorkRequestValidator().Validate(W(teeth: [11, 21])).IsValid);

    [Fact]
    public void Lab_work_rejects_invalid_fdi_in_list_or_single()
    {
        Assert.False(new SaveLabWorkRequestValidator().Validate(W(teeth: [11, 90])).IsValid);
        Assert.False(new SaveLabWorkRequestValidator().Validate(W(tooth: 10)).IsValid);
    }

    [Fact]
    public void Lab_work_max_teeth()
    {
        var all = Odontia.Domain.Dental.Fdi.All.Where(t => t.Dentition == Dentition.Permanent).Select(t => t.Code).ToList();
        Assert.True(new SaveLabWorkRequestValidator().Validate(W(teeth: all)).IsValid);
        Assert.False(new SaveLabWorkRequestValidator().Validate(W(teeth: all.Append(51).ToList())).IsValid);
    }

    [Fact]
    public void Lab_work_cost_and_appointment()
    {
        Assert.False(new SaveLabWorkRequestValidator().Validate(W(cost: -5)).IsValid);
        Assert.False(new SaveLabWorkRequestValidator().Validate(W(appt: Guid.Empty)).IsValid);
        Assert.True(new SaveLabWorkRequestValidator().Validate(W(cost: 0)).IsValid);
    }

    [Fact]
    public void Resolve_tooth_codes_prefers_list()
    {
        Assert.Equal([21], W(teeth: [21], tooth: 11).ResolveToothCodes());
        Assert.Equal([11], W(tooth: 11).ResolveToothCodes());
        Assert.Empty(W().ResolveToothCodes());
    }

    [Fact]
    public void Lab_status_and_laboratory_validators()
    {
        Assert.False(new ChangeLabWorkStatusRequestValidator().Validate(new ChangeLabWorkStatusRequest((LabWorkStatus)0, null, null, null)).IsValid);
        Assert.False(new SaveLaboratoryRequestValidator().Validate(new SaveLaboratoryRequest("Lab", null, null, "mal", null, null)).IsValid);
    }

    [Fact]
    public void Consent_validators()
    {
        Assert.False(new CreateConsentRequestValidator().Validate(new CreateConsentRequest("T", "", null)).IsValid);
        Assert.False(new RecordConsentAcceptanceRequestValidator().Validate(new RecordConsentAcceptanceRequest("", null, null)).IsValid);
        Assert.False(new AttachSignedConsentRequestValidator().Validate(new AttachSignedConsentRequest(Guid.Empty, "Ana")).IsValid);
        Assert.False(new DeleteDocumentRequestValidator().Validate(new DeleteDocumentRequest(" ")).IsValid);
    }
}
