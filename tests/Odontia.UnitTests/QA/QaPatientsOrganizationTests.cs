using Odontia.Domain.Authorization;
using Odontia.Domain.Common;
using Odontia.Domain.Entities;
using Odontia.Domain.Enums;

namespace Odontia.UnitTests.QA;

public class QaPatientTests
{
    private static readonly DateOnly Today = new(2026, 11, 19);
    private static Patient New(string doc = "30.123.456") => new(Guid.NewGuid(), " Ana ", " Pérez ", doc);

    [Fact]
    public void Identity_is_trimmed_normalized_and_searchable_without_accents()
    {
        var p = New();
        Assert.Equal("Ana", p.FirstName);
        Assert.Equal("30123456", p.DocumentNumber);
        Assert.Equal("perez ana", p.SearchName);
        Assert.Equal(PatientStatus.New, p.Status);
    }

    [Theory]
    [InlineData("12345", false)]
    [InlineData("123456", true)]
    [InlineData("123456789012345", true)]
    [InlineData("1234567890123456", false)]
    [InlineData("1.2.3.4.5", false)]
    [InlineData("ab-12.34-56", true)]
    public void Document_length_rules_apply_after_normalization(string doc, bool valid)
    {
        if (valid) Assert.Equal(Patient.NormalizeDocument(doc), New(doc).DocumentNumber);
        else Assert.ThrowsAny<DomainException>(() => New(doc));
    }

    [Fact]
    public void Document_is_uppercased()
    {
        Assert.Equal("AB123456", Patient.NormalizeDocument("ab-123.456"));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    [InlineData("---", null)]
    [InlineData("+54 (11) 4555-1234", "541145551234")]
    public void Phone_normalization(string? raw, string? expected) => Assert.Equal(expected, Patient.NormalizePhone(raw));

    [Fact]
    public void Normalize_text_removes_accents_and_case()
    {
        Assert.Equal("munoz jose", Patient.NormalizeText("  MUÑOZ José "));
    }

    [Fact]
    public void Birthdate_boundaries()
    {
        var p = New();
        p.SetPersonalData(Today, Sex.Female, null, Today);
        Assert.Equal(0, p.AgeAt(Today));
        p.SetPersonalData(Today.AddYears(-130), Sex.Female, null, Today);
        Assert.Throws<DomainException>(() => p.SetPersonalData(Today.AddYears(-130).AddDays(-1), Sex.Female, null, Today));
        Assert.Throws<DomainException>(() => p.SetPersonalData(Today.AddDays(1), Sex.Female, null, Today));
    }

    [Fact]
    public void Age_changes_on_birthday_and_null_without_birthdate()
    {
        var p = New();
        Assert.Null(p.AgeAt(Today));
        p.SetPersonalData(new DateOnly(1990, 11, 20), Sex.Male, null, Today);
        Assert.Equal(35, p.AgeAt(Today));
        Assert.Equal(36, p.AgeAt(new DateOnly(2026, 11, 20)));
    }

    [Fact]
    public void Contact_email_is_lowercased_and_phone_normalized()
    {
        var p = New();
        p.SetContact(" 11-4444-5555 ", " Ana@Mail.COM ", null, null, null, null);
        Assert.Equal("ana@mail.com", p.Email);
        Assert.Equal("1144445555", p.PhoneNormalized);
        p.SetContact(null, null, null, null, null, null);
        Assert.Null(p.PhoneNormalized);
    }

    [Fact]
    public void Coverage_details_are_cleared_without_insurance()
    {
        var p = New();
        p.SetCoverage(Guid.NewGuid(), "310", "123");
        Assert.Equal("310", p.InsurancePlan);
        p.SetCoverage(null, "310", "123");
        Assert.Null(p.InsurancePlan);
        Assert.Null(p.AffiliateNumber);
    }

    [Fact]
    public void Invalid_document_does_not_change_names()
    {
        var p = New();
        Assert.Throws<DomainException>(() => p.SetIdentity("Otra", "Persona", "12"));
        Assert.Equal("Ana", p.FirstName);
        Assert.Equal("perez ana", p.SearchName);
    }

    [Fact]
    public void Medical_history_validation_trims_and_limits()
    {
        var h = new MedicalHistory(Guid.NewGuid(), Guid.NewGuid()) { Allergies = "  ", CurrentMedication = " Aspirina " };
        h.Validate();
        Assert.Null(h.Allergies);
        Assert.Equal("Aspirina", h.CurrentMedication);
        h.OtherConditions = new string('x', 2001);
        Assert.Throws<DomainException>(() => h.Validate());
    }

    [Theory]
    [InlineData("ninguna")]
    [InlineData("NIEGA")]
    [InlineData("Sin alergias")]
    [InlineData("-")]
    [InlineData("  No  ")]
    public void Known_none_allergy_answers_produce_no_alert(string text)
    {
        Assert.Empty(new MedicalHistory(Guid.NewGuid(), Guid.NewGuid()) { Allergies = text }.DerivedAlerts());
    }

    [Theory]
    [InlineData("Ninguna.")]
    [InlineData("No.")]
    [InlineData("No refiere")]
    public void Negative_allergy_answers_with_punctuation_produce_no_alert(string text)
    {
        Assert.Empty(new MedicalHistory(Guid.NewGuid(), Guid.NewGuid()) { Allergies = text }.DerivedAlerts());
    }

    [Fact]
    public void Derived_alerts_severities()
    {
        var h = new MedicalHistory(Guid.NewGuid(), Guid.NewGuid())
        { Allergies = "Látex", Anticoagulated = true, HeartDisease = true, Diabetes = true, Hypertension = true, Pregnant = true, Asthma = true, Hepatitis = true, Smoker = true };
        var alerts = h.DerivedAlerts().ToList();
        Assert.Equal(8, alerts.Count); // fumador no genera alerta
        Assert.Equal(2, alerts.Count(a => a.Severity == AlertSeverity.Critical));
        Assert.Single(alerts, a => a.Severity == AlertSeverity.Info);
    }

    [Fact]
    public void Patient_alert_and_insurance_validation()
    {
        var a = new PatientAlert(Guid.NewGuid(), Guid.NewGuid(), " Marcapasos ", null, AlertSeverity.Critical, true);
        Assert.Equal("Marcapasos", a.Title);
        Assert.True(a.IsActive);
        a.Deactivate();
        Assert.False(a.IsActive);
        Assert.Throws<DomainException>(() => a.Update("", null, AlertSeverity.Info, false));
        var hi = new HealthInsurance(Guid.NewGuid(), "OSDE", " 210 ");
        Assert.Equal("210", hi.Code);
        Assert.Throws<DomainException>(() => new HealthInsurance(Guid.NewGuid(), new string('n', 121), null));
    }
}

public class QaOrganizationAndUsersTests
{
    private static readonly DateTime Now = new(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Consultorio_defaults()
    {
        var c = new Consultorio(" Clínica ", "America/Argentina/Buenos_Aires");
        Assert.Equal("Clínica", c.Name);
        Assert.Equal(30, c.DefaultAppointmentMinutes);
        Assert.Equal(15, c.SlotIntervalMinutes);
        Assert.Equal(24, c.ReminderHoursBefore);
        Assert.Equal("AR", c.Country);
        Assert.True(c.IsActive);
    }

    [Theory]
    [InlineData(4, 15, 24)]
    [InlineData(481, 15, 24)]
    [InlineData(30, 7, 24)]
    [InlineData(30, 0, 24)]
    [InlineData(30, 15, -1)]
    [InlineData(30, 15, 169)]
    public void Consultorio_settings_are_validated(int def, int slot, int reminder)
    {
        var c = new Consultorio("C", "UTC");
        Assert.Throws<DomainException>(() => c.UpdateSettings("UTC", def, slot, reminder));
        Assert.Equal(30, c.DefaultAppointmentMinutes);
    }

    [Theory]
    [InlineData(5, 5, 0)]
    [InlineData(480, 60, 168)]
    [InlineData(45, 20, 48)]
    public void Consultorio_settings_boundaries_are_accepted(int def, int slot, int reminder)
    {
        var c = new Consultorio("C", "UTC");
        c.UpdateSettings("America/Argentina/Cordoba", def, slot, reminder);
        Assert.Equal(slot, c.SlotIntervalMinutes);
        Assert.Equal("America/Argentina/Cordoba", c.TimeZoneId);
    }

    [Fact]
    public void Consultorio_country_must_be_two_letters_and_is_uppercased()
    {
        var c = new Consultorio("C", "UTC");
        c.UpdateDetails("C", null, null, null, null, null, null, null, null, null, "uy");
        Assert.Equal("UY", c.Country);
        Assert.Throws<DomainException>(() => c.UpdateDetails("C", null, null, null, null, null, null, null, null, null, "ARG"));
    }

    [Fact]
    public void Gabinete_rename_and_activation()
    {
        var g = new Gabinete(Guid.NewGuid(), " Box 1 ", null);
        Assert.Equal("Box 1", g.Name);
        Assert.Throws<DomainException>(() => g.Rename(" ", null));
        g.SetActive(false);
        Assert.False(g.IsActive);
    }

    [Fact]
    public void Member_roles_are_replaced_and_deduplicated()
    {
        var m = new ConsultorioMember(Guid.NewGuid(), Guid.NewGuid(), false);
        m.ReplaceRoles([SystemRoles.SecretariaId, SystemRoles.SecretariaId, SystemRoles.OdontologoId]);
        Assert.Equal(2, m.Roles.Count);
        m.ReplaceRoles([SystemRoles.AdministradorId]);
        Assert.Equal([SystemRoles.AdministradorId], m.Roles.Select(r => r.RoleId));
        Assert.Throws<DomainException>(() => m.ReplaceRoles([]));
        Assert.Single(m.Roles);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(481)]
    public void Member_agenda_preferences_validate_duration(int minutes)
    {
        var m = new ConsultorioMember(Guid.NewGuid(), Guid.NewGuid(), true);
        Assert.Throws<DomainException>(() => m.SetAgendaPreferences(minutes, null));
        m.SetAgendaPreferences(null, "#112233");
        Assert.Null(m.DefaultAppointmentMinutes);
        Assert.Equal("#112233", m.AgendaColor);
    }

    [Fact]
    public void Role_permissions_are_replaced_without_duplicates()
    {
        var r = new Role(Guid.NewGuid(), "Recepción", null, false);
        r.SetPermissions([Permissions.PatientsRead, Permissions.PatientsRead, Permissions.AgendaRead]);
        Assert.Equal(2, r.Permissions.Count);
        r.SetPermissions([Permissions.AgendaRead]);
        Assert.Equal([Permissions.AgendaRead], r.Permissions.Select(p => p.Permission));
        Assert.Throws<DomainException>(() => r.SetPermissions(["PATIENTS.READ"])); // sensible a mayúsculas
        Assert.Single(r.Permissions);
    }

    [Fact]
    public void Role_name_is_required()
    {
        Assert.Throws<DomainException>(() => new Role(null, " ", null, true));
        Assert.Throws<DomainException>(() => new Role(null, new string('r', 65), null, true));
    }

    [Fact]
    public void Invitation_lowercases_email_requires_role_and_revocation_rules()
    {
        Assert.Throws<DomainException>(() => new Invitation(Guid.NewGuid(), "a@b.com", "h", Now.AddDays(1), Guid.NewGuid(), false, []));
        var inv = new Invitation(Guid.NewGuid(), " Ana@Mail.com ", "h", Now.AddDays(1), Guid.NewGuid(), false, [SystemRoles.SecretariaId, SystemRoles.SecretariaId]);
        Assert.Equal("ana@mail.com", inv.Email);
        Assert.Single(inv.Roles);
        Assert.False(inv.IsUsable(Now.AddDays(1))); // expira exactamente en ese instante
        inv.Revoke();
        Assert.Equal(InvitationStatus.Revoked, inv.Status);
        Assert.Throws<DomainException>(() => inv.Revoke());
        Assert.Throws<DomainException>(() => inv.Accept(Guid.NewGuid(), Now));
    }

    [Fact]
    public void Refresh_token_activity_and_truncation()
    {
        var t = new RefreshToken(Guid.NewGuid(), Guid.NewGuid(), "hash", Now, Now.AddDays(7), null, new string('1', 100), new string('u', 400));
        Assert.Equal(64, t.IpAddress!.Length);
        Assert.Equal(300, t.UserAgent!.Length);
        Assert.True(t.IsActive(Now));
        Assert.False(t.IsActive(Now.AddDays(7)));
        var replacement = Guid.NewGuid();
        t.Revoke(Now, new string('r', 100), replacement);
        Assert.Equal(64, t.RevokedReason!.Length);
        Assert.Equal(replacement, t.ReplacedByTokenId);
        Assert.False(t.IsActive(Now));
    }

    [Fact]
    public void User_display_name()
    {
        var u = new ApplicationUser { FirstName = "Laura", LastName = "Gómez" };
        Assert.Equal("Laura Gómez", u.DisplayName);
        u.Title = "Dra.";
        Assert.Equal("Dra. Laura Gómez", u.DisplayName);
        Assert.NotEqual(Guid.Empty, u.Id);
        Assert.False(string.IsNullOrEmpty(u.SecurityStamp));
    }
}

public class QaOperationsEntitiesTests
{
    private static readonly DateTime Now = new(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid U = Guid.NewGuid();

    private static Consent NewConsent() => new(Guid.NewGuid(), Guid.NewGuid(), "Extracción", "Texto", null, U);

    [Fact]
    public void Consent_rejection_and_revocation_rules()
    {
        var c = NewConsent();
        Assert.Equal("invalid_status_transition", Assert.Throws<DomainException>(() => c.Revoke("x", U, Now)).Code);
        c.Reject("No acepta", U, Now);
        Assert.Equal(ConsentStatus.Rejected, c.Status);
        Assert.Equal("consent_closed", Assert.Throws<DomainException>(() => c.AttachSignedDocument(Guid.NewGuid(), "Ana", U, Now)).Code);
        Assert.Equal("consent_closed", Assert.Throws<DomainException>(() => c.RecordSystemAcceptance("Ana", null, null, U, Now)).Code);
    }

    [Fact]
    public void Accepted_consent_can_be_revoked_with_reason_only()
    {
        var c = NewConsent();
        c.RecordSystemAcceptance("Ana Pérez", "30123456", "Titular", U, Now);
        Assert.Throws<DomainException>(() => c.Revoke(" ", U, Now));
        Assert.Equal(ConsentStatus.AcceptedInSystem, c.Status);
        c.Revoke("Cambió de opinión", U, Now);
        Assert.Equal(ConsentStatus.Revoked, c.Status);
        Assert.Throws<DomainException>(() => c.Revoke("otra", U, Now));
    }

    [Fact]
    public void Signed_document_can_be_attached_after_system_acceptance()
    {
        var c = NewConsent();
        c.RecordSystemAcceptance("Ana", null, null, U, Now);
        var doc = Guid.NewGuid();
        c.AttachSignedDocument(doc, "Ana", U, Now);
        Assert.Equal(ConsentStatus.SignedOnPaperAttached, c.Status);
        Assert.Equal(doc, c.SignedDocumentId);
    }

    [Fact]
    public void Document_clinical_classification_and_soft_delete()
    {
        StoredDocument Doc(DocumentCategory cat, Guid? entry = null, Guid? plan = null) =>
            new(Guid.NewGuid(), Guid.NewGuid(), cat, "a.pdf", "k", "application/pdf", 10, "h", null, entry, plan, U, Now);

        Assert.True(Doc(DocumentCategory.Radiograph).IsClinical);
        Assert.True(Doc(DocumentCategory.ClinicalPhoto).IsClinical);
        Assert.False(Doc(DocumentCategory.Administrative).IsClinical);
        Assert.False(Doc(DocumentCategory.ConsentForm).IsClinical);
        Assert.True(Doc(DocumentCategory.Administrative, entry: Guid.NewGuid()).IsClinical);
        Assert.True(Doc(DocumentCategory.Other, plan: Guid.NewGuid()).IsClinical);

        var d = Doc(DocumentCategory.Other);
        Assert.Throws<DomainException>(() => d.SoftDelete(" ", U, Now));
        Assert.False(d.IsDeleted);
        d.SoftDelete("Duplicado", U, Now);
        d.SoftDelete("Otro", Guid.NewGuid(), Now.AddDays(1));
        Assert.Equal("Duplicado", d.DeletionReason);
    }

    [Fact]
    public void Notification_read_is_idempotent()
    {
        var n = new Notification(Guid.NewGuid(), U, NotificationType.General, "Hola", null, null, null, Now);
        n.MarkRead(Now.AddMinutes(1));
        n.MarkRead(Now.AddMinutes(5));
        Assert.Equal(Now.AddMinutes(1), n.ReadAtUtc);
        Assert.Throws<DomainException>(() => new Notification(Guid.NewGuid(), U, NotificationType.General, " ", null, null, null, Now));
    }

    [Fact]
    public void Outbound_message_never_stores_secret_body_and_tracks_results()
    {
        var m = new OutboundMessage(null, MessageChannel.Email, "a@b.com", "Reset", "https://x/reset?token=SECRET", "reset", null, null, Now, containsSecret: true);
        Assert.DoesNotContain("SECRET", m.Body);
        Assert.Equal(OutboundMessageStatus.Pending, m.Status);

        m.RecordResult("smtp", accepted: true, providerConfigured: false, "id", null, Now);
        Assert.Equal(OutboundMessageStatus.NotSentNoProvider, m.Status);
        Assert.Null(m.SentAtUtc);

        m.RecordResult("smtp", accepted: false, providerConfigured: true, null, new string('e', 900), Now);
        Assert.Equal(OutboundMessageStatus.Failed, m.Status);
        Assert.Equal(500, m.LastError!.Length);

        m.RecordResult("smtp", accepted: true, providerConfigured: true, "msg-1", null, Now);
        Assert.Equal(OutboundMessageStatus.Sent, m.Status);
        Assert.Null(m.LastError);
        Assert.Equal(3, m.Attempts);
    }

    [Fact]
    public void Outbound_message_without_secret_requires_body()
    {
        Assert.Throws<DomainException>(() => new OutboundMessage(null, MessageChannel.WhatsApp, "+5491100000000", null, " ", "p", null, null, Now, false));
    }

    [Fact]
    public void Audit_log_truncates_fields()
    {
        var a = new AuditLog(null, null, AuditAction.LoginFailed, new string('e', 150), null, new string('d', 5000), new string('i', 100), Now);
        Assert.Equal(100, a.EntityType.Length);
        Assert.Equal(4000, a.Details!.Length);
        Assert.Equal(64, a.IpAddress!.Length);
    }
}
