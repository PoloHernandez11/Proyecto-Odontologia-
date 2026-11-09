using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using Odontia.Domain.Authorization;
using Odontia.IntegrationTests.Infrastructure;

namespace Odontia.IntegrationTests;

/// <summary>Escenario: consultorio A (Dra. Méndez: admin + odontóloga, secretaria Carla) y consultorio B independiente.</summary>
public sealed class ClinicScenario : IAsyncLifetime
{
    public OdontiaFactory Factory { get; } = new();
    public TestApi Doctor { get; private set; } = default!;
    public TestApi Secretary { get; private set; } = default!;
    public TestApi OtherClinic { get; private set; } = default!;
    public Guid PatientA { get; private set; }
    public Guid PatientB { get; private set; }

    public async Task InitializeAsync()
    {
        Doctor = await TestApi.RegisterOwnerAsync(Factory, "laura.mendez@odontia.test", "Consultorio Méndez");
        Secretary = await Doctor.InviteAndLoginAsync("carla.suarez@odontia.test", SystemRoles.SecretariaId);
        OtherClinic = await TestApi.RegisterOwnerAsync(Factory, "otro@odontia.test", "Consultorio Norte");
        PatientA = await Doctor.CreatePatientAsync("32456789", medicalHistory: new
        {
            diabetes = false, hypertension = true, heartDisease = false, anticoagulated = true, pregnant = false, asthma = false,
            hepatitis = false, smoker = false, allergies = "Penicilina", currentMedication = "Losartán 50 mg", otherConditions = "Bruxismo",
        });
        PatientB = await OtherClinic.CreatePatientAsync("40112334", "Sofía", "Herrera");
        await Doctor.SetFullWorkingHoursAsync(Doctor.UserId);
        await OtherClinic.SetFullWorkingHoursAsync(OtherClinic.UserId);
    }

    public Task DisposeAsync()
    {
        Factory.Dispose();
        return Task.CompletedTask;
    }
}

public sealed class RolePermissionTests(ClinicScenario s) : IClassFixture<ClinicScenario>
{
    [Fact]
    public async Task Secretary_cannot_access_clinical_record()
    {
        await TestApi.AssertProblem(await s.Secretary.Get($"/api/v1/patients/{s.PatientA}/clinical-record"), HttpStatusCode.Forbidden);
        await TestApi.AssertProblem(await s.Secretary.Get($"/api/v1/patients/{s.PatientA}/clinical-record/entries"), HttpStatusCode.Forbidden);
        await TestApi.AssertProblem(await s.Secretary.Post($"/api/v1/patients/{s.PatientA}/clinical-record/entries",
            new { type = "Evolution", title = "Intento", description = "x" }), HttpStatusCode.Forbidden);
        await TestApi.AssertProblem(await s.Secretary.Get($"/api/v1/patients/{s.PatientA}/clinical-record/diagnoses"), HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Secretary_cannot_access_odontogram_or_treatments_or_medical_history()
    {
        await TestApi.AssertProblem(await s.Secretary.Get($"/api/v1/patients/{s.PatientA}/odontogram"), HttpStatusCode.Forbidden);
        await TestApi.AssertProblem(await s.Secretary.Get("/api/v1/odontogram/catalog"), HttpStatusCode.Forbidden);
        await TestApi.AssertProblem(await s.Secretary.Get($"/api/v1/patients/{s.PatientA}/treatment-plans"), HttpStatusCode.Forbidden);
        await TestApi.AssertProblem(await s.Secretary.Get($"/api/v1/patients/{s.PatientA}/medical-history"), HttpStatusCode.Forbidden);
        await TestApi.AssertProblem(await s.Secretary.Get("/api/v1/dashboard/clinical"), HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Secretary_sees_only_relevant_alert_titles_without_clinical_details()
    {
        var alerts = (await s.Secretary.GetOk($"/api/v1/patients/{s.PatientA}/alerts")).AsArray();
        var titles = alerts.Select(a => a!["title"]!.GetValue<string>()).ToList();
        Assert.Contains("Anticoagulado", titles);
        Assert.Contains("Alergia: Penicilina", titles);
        Assert.All(alerts, a => Assert.Null(a!["details"]?.GetValue<string>()));
        var patient = await s.Secretary.GetOk($"/api/v1/patients/{s.PatientA}");
        var raw = patient.ToJsonString();
        Assert.DoesNotContain("Losartán", raw);
        Assert.DoesNotContain("Bruxismo", raw);
    }

    [Fact]
    public async Task Doctor_sees_full_medical_history()
    {
        var h = await s.Doctor.GetOk($"/api/v1/patients/{s.PatientA}/medical-history");
        Assert.Equal("Losartán 50 mg", h["currentMedication"]!.GetValue<string>());
        Assert.True(h["anticoagulated"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Secretary_cannot_register_medical_history_when_creating_patient()
    {
        var r = await s.Secretary.Post("/api/v1/patients", new
        {
            patient = new { firstName = "Elena", lastName = "Ruiz", documentNumber = "27660198", sex = "Female" },
            medicalHistory = new { diabetes = true, hypertension = false, heartDisease = false, anticoagulated = false, pregnant = false, asthma = false, hepatitis = false, smoker = false },
        });
        await TestApi.AssertProblem(r, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Secretary_cannot_escalate_privileges()
    {
        var r = await s.Secretary.Post("/api/v1/invitations", new { email = "nuevo.admin@odontia.test", roleIds = new[] { SystemRoles.AdministradorId } });
        await TestApi.AssertProblem(r, HttpStatusCode.Forbidden);
        await TestApi.AssertProblem(await s.Secretary.Post("/api/v1/roles", new { name = "Super", permissions = new[] { "clinical.read" } }), HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_cannot_change_own_roles_and_last_admin_is_protected()
    {
        var members = (await s.Doctor.GetOk("/api/v1/users")).AsArray();
        var me = members.First(m => m!["userId"]!.GetValue<Guid>() == s.Doctor.UserId)!["memberId"]!.GetValue<Guid>();
        await TestApi.AssertProblem(await s.Doctor.Put($"/api/v1/users/{me}/roles", new { roleIds = new[] { SystemRoles.SecretariaId } }), HttpStatusCode.Forbidden);
        await TestApi.AssertProblem(await s.Doctor.Put($"/api/v1/users/{me}/active", new { isActive = false }), HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Custom_role_without_write_permission_cannot_modify_appointments()
    {
        var role = await s.Doctor.PostOk("/api/v1/roles", new { name = "Sólo lectura agenda", permissions = new[] { "agenda.read", "appointments.read", "agenda.all_professionals" } });
        var reader = await s.Doctor.InviteAndLoginAsync("lector@odontia.test", role["id"]!.GetValue<Guid>(), "Javier", "Ortiz");
        var appt = await s.Doctor.PostOk("/api/v1/appointments", new { patientId = s.PatientA, professionalUserId = s.Doctor.UserId, start = TestApi.LocalSlot(6, 15), durationMinutes = 30 });
        var id = appt["id"]!.GetValue<Guid>();
        await reader.GetOk($"/api/v1/appointments/{id}");
        await TestApi.AssertProblem(await reader.Post($"/api/v1/appointments/{id}/cancel", new { reason = "x" }), HttpStatusCode.Forbidden);
        await TestApi.AssertProblem(await reader.Put($"/api/v1/appointments/{id}", new { patientId = s.PatientA, reason = "x", version = appt["version"]!.GetValue<Guid>() }), HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Deactivated_member_loses_access_immediately()
    {
        var temp = await s.Doctor.InviteAndLoginAsync("temporal@odontia.test", SystemRoles.SecretariaId, "Temporal", "Usuario");
        await temp.GetOk("/api/v1/patients");
        var memberId = (await s.Doctor.GetOk("/api/v1/users")).AsArray().First(m => m!["userId"]!.GetValue<Guid>() == temp.UserId)!["memberId"]!.GetValue<Guid>();
        await s.Doctor.PutOk($"/api/v1/users/{memberId}/active", new { isActive = false });
        await TestApi.AssertProblem(await temp.Get("/api/v1/patients"), HttpStatusCode.Forbidden, "membership_inactive");
    }
}

public sealed class TenantIsolationApiTests(ClinicScenario s) : IClassFixture<ClinicScenario>
{
    [Fact]
    public async Task Other_clinic_cannot_read_or_modify_patients()
    {
        await TestApi.AssertProblem(await s.OtherClinic.Get($"/api/v1/patients/{s.PatientA}"), HttpStatusCode.NotFound);
        await TestApi.AssertProblem(await s.OtherClinic.Get($"/api/v1/patients/{s.PatientA}/clinical-record"), HttpStatusCode.NotFound);
        await TestApi.AssertProblem(await s.OtherClinic.Get($"/api/v1/patients/{s.PatientA}/odontogram"), HttpStatusCode.NotFound);
        await TestApi.AssertProblem(await s.OtherClinic.Put($"/api/v1/patients/{s.PatientA}", new { firstName = "X", lastName = "Y", documentNumber = "32456789", sex = "Male" }), HttpStatusCode.NotFound);
        var list = await s.OtherClinic.GetOk("/api/v1/patients?search=Rodriguez");
        Assert.Equal(0, list["totalCount"]!.GetValue<int>());
    }

    [Fact]
    public async Task Other_clinic_cannot_book_with_foreign_patient_or_professional()
    {
        var r1 = await s.OtherClinic.Post("/api/v1/appointments", new { patientId = s.PatientA, professionalUserId = s.OtherClinic.UserId, start = TestApi.LocalSlot(2, 10), durationMinutes = 30 });
        await TestApi.AssertProblem(r1, HttpStatusCode.BadRequest, "validation_error");
        var r2 = await s.OtherClinic.Post("/api/v1/appointments", new { patientId = s.PatientB, professionalUserId = s.Doctor.UserId, start = TestApi.LocalSlot(2, 10), durationMinutes = 30 });
        await TestApi.AssertProblem(r2, HttpStatusCode.BadRequest, "validation_error");
    }

    [Fact]
    public async Task Other_clinic_cannot_see_appointments_payments_or_documents()
    {
        var appt = await s.Doctor.PostOk("/api/v1/appointments", new { patientId = s.PatientA, professionalUserId = s.Doctor.UserId, start = TestApi.LocalSlot(3, 9), durationMinutes = 30 });
        await TestApi.AssertProblem(await s.OtherClinic.Get($"/api/v1/appointments/{appt["id"]}"), HttpStatusCode.NotFound);
        var all = await s.OtherClinic.GetOk("/api/v1/appointments");
        Assert.DoesNotContain(all["items"]!.AsArray(), a => a!["id"]!.GetValue<Guid>() == appt["id"]!.GetValue<Guid>());

        var pay = await s.Doctor.PostOk("/api/v1/payments", new { patientId = s.PatientA, amount = 1000, method = "Cash" });
        await TestApi.AssertProblem(await s.OtherClinic.Get($"/api/v1/payments/{pay["id"]}"), HttpStatusCode.NotFound);
        await TestApi.AssertProblem(await s.OtherClinic.Get($"/api/v1/patients/{s.PatientA}/account"), HttpStatusCode.NotFound);

        var doc = await DocumentTests.UploadAsync(s.Doctor, s.PatientA, "Administrative", "dni.pdf", DocumentTests.PdfBytes);
        await TestApi.AssertProblem(await s.OtherClinic.Get($"/api/v1/documents/{doc}/download"), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Cannot_switch_to_a_consultorio_without_membership()
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/switch-consultorio")
        {
            Content = JsonContent(new { consultorioId = s.Doctor.ConsultorioId }),
        };
        req.Headers.Add("X-CSRF-Token", s.OtherClinic.CsrfToken);
        await TestApi.AssertProblem(await s.OtherClinic.Http.SendAsync(req), HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Token_issued_for_other_consultorio_is_not_accepted_by_tampering()
    {
        // Un token alterado (firma inválida) es rechazado; el consultorio no puede elegirse desde el cliente.
        var parts = s.OtherClinic.AccessToken!.Split('.');
        var tampered = $"{parts[0]}.{parts[1]}x.{parts[2]}";
        var http = s.Factory.NewClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tampered);
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync("/api/v1/patients")).StatusCode);
    }

    private static StringContent JsonContent(object o) => new(System.Text.Json.JsonSerializer.Serialize(o), System.Text.Encoding.UTF8, "application/json");
}
