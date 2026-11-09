using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using Odontia.Domain.Authorization;
using Odontia.IntegrationTests.Infrastructure;

namespace Odontia.IntegrationTests;

public sealed class AppointmentTests(ClinicScenario s) : IClassFixture<ClinicScenario>
{
    private Task<HttpResponseMessage> Book(TestApi api, Guid patient, string start, int minutes = 30, bool overbook = false, Guid? professional = null) =>
        api.Post("/api/v1/appointments", new { patientId = patient, professionalUserId = professional ?? s.Doctor.UserId, start, durationMinutes = minutes, isOverbooking = overbook, reason = "Control" });

    [Fact]
    public async Task Booking_an_occupied_slot_is_rejected()
    {
        var other = await s.Doctor.CreatePatientAsync("25889102", "Jorge", "Castro");
        await TestApi.Ok(await Book(s.Secretary, s.PatientA, TestApi.LocalSlot(1, 10)));
        await TestApi.AssertProblem(await Book(s.Secretary, other, TestApi.LocalSlot(1, 10, 15)), HttpStatusCode.Conflict, "slot_taken");
        // Contiguo (10:30) sí está permitido.
        await TestApi.Ok(await Book(s.Secretary, other, TestApi.LocalSlot(1, 10, 30)));
    }

    [Fact]
    public async Task Same_patient_cannot_be_double_booked_even_with_overbooking()
    {
        await TestApi.Ok(await Book(s.Doctor, s.PatientA, TestApi.LocalSlot(2, 11)));
        await TestApi.AssertProblem(await Book(s.Doctor, s.PatientA, TestApi.LocalSlot(2, 11), overbook: true), HttpStatusCode.Conflict, "patient_double_booking");
    }

    [Fact]
    public async Task Overbooking_allows_overlap_with_permission()
    {
        var p1 = await s.Doctor.CreatePatientAsync("30556871", "Diego", "Romero");
        var p2 = await s.Doctor.CreatePatientAsync("35221640", "Paula", "Gimenez");
        await TestApi.Ok(await Book(s.Secretary, p1, TestApi.LocalSlot(2, 15)));
        await TestApi.Ok(await Book(s.Secretary, p2, TestApi.LocalSlot(2, 15), overbook: true));
    }

    [Fact]
    public async Task Booking_outside_availability_or_in_blocked_time_is_rejected()
    {
        await TestApi.AssertProblem(await Book(s.Doctor, s.PatientA, TestApi.LocalSlot(3, 22)), HttpStatusCode.UnprocessableEntity, "outside_availability");
        var start = DateTimeOffset.Parse(TestApi.LocalSlot(3, 13));
        await s.Doctor.PostOk("/api/v1/agenda/blocks", new { kind = "Blocked", start, end = start.AddHours(1), professionalUserId = s.Doctor.UserId, reason = "Almuerzo" });
        await TestApi.AssertProblem(await Book(s.Doctor, s.PatientA, TestApi.LocalSlot(3, 13, 30)), HttpStatusCode.UnprocessableEntity, "outside_availability");
        await TestApi.AssertProblem(await Book(s.Doctor, s.PatientA, TestApi.LocalSlot(-1, 10)), HttpStatusCode.UnprocessableEntity, "appointment_in_past");
    }

    [Fact]
    public async Task Availability_excludes_booked_slots()
    {
        await TestApi.Ok(await Book(s.Doctor, s.PatientA, TestApi.LocalSlot(4, 9)));
        var date = DateTimeOffset.Parse(TestApi.LocalSlot(4, 0)).ToString("yyyy-MM-dd");
        var av = await s.Secretary.GetOk($"/api/v1/agenda/availability?date={date}&professionalUserId={s.Doctor.UserId}&durationMinutes=30");
        var times = av["slots"]!.AsArray().Select(x => x!["localTime"]!.GetValue<string>()).ToList();
        Assert.DoesNotContain("09:00", times);
        Assert.DoesNotContain("09:15", times);
        Assert.Contains("09:30", times);
        Assert.Contains("07:00", times);
    }

    [Fact]
    public async Task Full_status_flow_with_history_and_reschedule()
    {
        var created = await TestApi.Ok(await Book(s.Secretary, s.PatientA, TestApi.LocalSlot(5, 10)));
        var id = created["id"]!.GetValue<Guid>();
        await s.Secretary.PostOk($"/api/v1/appointments/{id}/confirm");
        var current = await s.Secretary.GetOk($"/api/v1/appointments/{id}");
        Assert.Equal("Confirmed", current["status"]!.GetValue<string>());
        Assert.Contains("Anticoagulado", current["alerts"]!.AsArray().Select(a => a!.GetValue<string>()));

        // Reprogramar con versión vieja falla (concurrencia optimista); con la vigente funciona.
        var stale = await s.Secretary.Post($"/api/v1/appointments/{id}/reschedule", new { start = TestApi.LocalSlot(5, 16), version = created["version"]!.GetValue<Guid>(), isOverbooking = false });
        await TestApi.AssertProblem(stale, HttpStatusCode.Conflict, "concurrency_conflict");
        var moved = await s.Secretary.PostOk($"/api/v1/appointments/{id}/reschedule", new { start = TestApi.LocalSlot(5, 16), version = current["version"]!.GetValue<Guid>(), isOverbooking = false, reason = "Pedido del paciente" });
        Assert.Equal("Pending", moved["status"]!.GetValue<string>());
        Assert.Equal(1, moved["rescheduleCount"]!.GetValue<int>());

        await s.Secretary.PostOk($"/api/v1/appointments/{id}/check-in");
        await s.Secretary.PostOk($"/api/v1/appointments/{id}/waiting-room");
        await s.Doctor.PostOk($"/api/v1/appointments/{id}/start");
        await s.Doctor.PostOk($"/api/v1/appointments/{id}/attend");
        await TestApi.AssertProblem(await s.Secretary.Post($"/api/v1/appointments/{id}/cancel", new { reason = "tarde" }), HttpStatusCode.UnprocessableEntity, "invalid_status_transition");

        var history = (await s.Secretary.GetOk($"/api/v1/appointments/{id}/history")).AsArray();
        Assert.True(history.Count >= 7);
        Assert.Contains(history, h => h!["action"]!.GetValue<string>() == "rescheduled");
    }

    [Fact]
    public async Task Professional_without_global_agenda_cannot_manage_other_professionals_appointments()
    {
        var paz = await s.Doctor.InviteAndLoginAsync("tomas.paz@odontia.test", SystemRoles.OdontologoId, "Tomás", "Paz");
        var created = await TestApi.Ok(await Book(s.Secretary, s.PatientA, TestApi.LocalSlot(7, 9)));
        var id = created["id"]!.GetValue<Guid>();
        await TestApi.AssertProblem(await paz.Get($"/api/v1/appointments/{id}"), HttpStatusCode.NotFound);
        await TestApi.AssertProblem(await paz.Post($"/api/v1/appointments/{id}/cancel", new { reason = "x" }), HttpStatusCode.NotFound);
        await TestApi.AssertProblem(await Book(paz, s.PatientA, TestApi.LocalSlot(7, 12), professional: s.Doctor.UserId), HttpStatusCode.Forbidden);
        var agenda = await paz.GetOk($"/api/v1/agenda?from={DateTime.UtcNow:yyyy-MM-dd}&to={DateTime.UtcNow.AddDays(10):yyyy-MM-dd}");
        Assert.Empty(agenda["appointments"]!.AsArray());
    }

    [Fact]
    public async Task Agenda_month_and_day_summary_views()
    {
        await TestApi.Ok(await Book(s.Doctor, s.PatientA, TestApi.LocalSlot(8, 8)));
        var d = DateTimeOffset.Parse(TestApi.LocalSlot(8, 0));
        var month = await s.Secretary.GetOk($"/api/v1/agenda/month?year={d.Year}&month={d.Month}");
        var day = month["days"]!.AsArray().First(x => x!["date"]!.GetValue<string>() == d.ToString("yyyy-MM-dd"))!;
        Assert.True(day["appointmentCount"]!.GetValue<int>() >= 1);
        var summary = await s.Secretary.GetOk($"/api/v1/agenda/day-summary?date={d:yyyy-MM-dd}&professionalUserId={s.Doctor.UserId}");
        Assert.True(summary["bookedMinutes"]!.GetValue<int>() >= 30);
    }
}

public sealed class FinanceTests(ClinicScenario s) : IClassFixture<ClinicScenario>
{
    [Fact]
    public async Task Invalid_payments_are_rejected()
    {
        await TestApi.AssertProblem(await s.Secretary.Post("/api/v1/payments", new { patientId = s.PatientA, amount = 0, method = "Cash" }), HttpStatusCode.BadRequest, "validation_error");
        await TestApi.AssertProblem(await s.Secretary.Post("/api/v1/payments", new { patientId = s.PatientA, amount = -100, method = "Cash" }), HttpStatusCode.BadRequest, "validation_error");
        await TestApi.AssertProblem(await s.Secretary.Post("/api/v1/payments", new { patientId = s.PatientA, amount = 10.555, method = "Cash" }), HttpStatusCode.BadRequest, "validation_error");
        await TestApi.AssertProblem(await s.Secretary.Post("/api/v1/payments", new { patientId = s.PatientA, amount = 100, method = "Bitcoin" }), HttpStatusCode.BadRequest);
        await TestApi.AssertProblem(await s.Secretary.Post("/api/v1/payments", new { patientId = Guid.NewGuid(), amount = 100, method = "Cash" }), HttpStatusCode.BadRequest);
        await TestApi.AssertProblem(await s.Secretary.Post("/api/v1/payments", new { patientId = s.PatientA, amount = 100, method = "Cash", paidAt = DateTimeOffset.UtcNow.AddDays(2) }), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Budget_payment_and_void_keep_server_side_balance_and_history()
    {
        var proc = await s.Doctor.PostOk("/api/v1/procedures", new { code = "ENDO-M", name = "Endodoncia molar", price = 150000, requiresTooth = true });
        var budget = await s.Doctor.PostOk($"/api/v1/patients/{s.PatientA}/budgets", new
        {
            discountPercent = 10,
            items = new object[]
            {
                new { procedureId = proc["id"], toothCode = 36, quantity = 1, discountPercent = 0 },
                new { description = "Corona de zirconio", toothCode = 21, quantity = 1, unitPrice = 330000, discountPercent = 0 },
            },
        });
        var id = budget["id"]!.GetValue<Guid>();
        Assert.Equal(432000m, budget["total"]!.GetValue<decimal>());

        // Cambiar el precio del catálogo no altera el presupuesto.
        await s.Doctor.PutOk($"/api/v1/procedures/{proc["id"]}", new { code = "ENDO-M", name = "Endodoncia molar", price = 999999, requiresTooth = true });
        Assert.Equal(432000m, (await s.Doctor.GetOk($"/api/v1/budgets/{id}"))["total"]!.GetValue<decimal>());

        await TestApi.AssertProblem(await s.Secretary.Post("/api/v1/payments", new { patientId = s.PatientA, budgetId = id, amount = 1000, method = "Cash" }), HttpStatusCode.UnprocessableEntity, "budget_not_approved");
        await s.Doctor.PostOk($"/api/v1/budgets/{id}/issue");
        await TestApi.AssertProblem(await s.Secretary.Post($"/api/v1/budgets/{id}/approve"), HttpStatusCode.Forbidden);
        await s.Doctor.PostOk($"/api/v1/budgets/{id}/approve");

        var p1 = await s.Secretary.PostOk("/api/v1/payments", new { patientId = s.PatientA, budgetId = id, amount = 150000, method = "BankTransfer", reference = "TRF-001" });
        await TestApi.AssertProblem(await s.Secretary.Post("/api/v1/payments", new { patientId = s.PatientA, budgetId = id, amount = 300000, method = "Cash" }), HttpStatusCode.UnprocessableEntity, "amount_exceeds_outstanding");

        // La secretaria no puede anular; el administrador sí, con motivo y contraasiento.
        await TestApi.AssertProblem(await s.Secretary.Post($"/api/v1/payments/{p1["id"]}/void", new { reason = "Error" }), HttpStatusCode.Forbidden);
        await s.Doctor.PostOk($"/api/v1/payments/{p1["id"]}/void", new { reason = "Transferencia rechazada" });

        var account = await s.Doctor.GetOk($"/api/v1/patients/{s.PatientA}/account");
        var budgetBalance = account["budgets"]!.AsArray().First(b => b!["budgetId"]!.GetValue<Guid>() == id)!;
        Assert.Equal(432000m, budgetBalance["outstanding"]!.GetValue<decimal>());
        var movements = account["movements"]!.AsArray();
        Assert.Contains(movements, m => m!["type"]!.GetValue<string>() == "PaymentReversal");
        var voided = await s.Doctor.GetOk($"/api/v1/payments/{p1["id"]}");
        Assert.Equal("Voided", voided["status"]!.GetValue<string>());

        var history = (await s.Doctor.GetOk($"/api/v1/budgets/{id}/history")).AsArray();
        Assert.Equal(2, history.Count);
    }
}

public sealed class DocumentTests(ClinicScenario s) : IClassFixture<ClinicScenario>
{
    public static readonly byte[] PdfBytes = "%PDF-1.4\n%odontia test\n1 0 obj<<>>endobj\ntrailer<<>>\n%%EOF"u8.ToArray();

    public static async Task<Guid> UploadAsync(TestApi api, Guid patientId, string category, string fileName, byte[] content)
    {
        var r = await UploadRawAsync(api, patientId, category, fileName, content);
        return (await TestApi.Ok(r))["id"]!.GetValue<Guid>();
    }

    public static Task<HttpResponseMessage> UploadRawAsync(TestApi api, Guid patientId, string category, string fileName, byte[] content)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", fileName);
        form.Add(new StringContent(category), "category");
        return api.Http.PostAsync($"/api/v1/patients/{patientId}/documents", form);
    }

    [Fact]
    public async Task Spoofed_or_forbidden_files_are_rejected()
    {
        await TestApi.AssertProblem(await UploadRawAsync(s.Doctor, s.PatientA, "Study", "falso.pdf", "MZ-not-a-pdf"u8.ToArray()), HttpStatusCode.BadRequest, "validation_error");
        await TestApi.AssertProblem(await UploadRawAsync(s.Doctor, s.PatientA, "Study", "script.exe", PdfBytes), HttpStatusCode.BadRequest, "validation_error");
        await TestApi.AssertProblem(await UploadRawAsync(s.Doctor, s.PatientA, "Study", "vacio.pdf", []), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Secretary_cannot_access_clinical_documents_but_can_access_administrative_ones()
    {
        var rx = await UploadAsync(s.Doctor, s.PatientA, "Radiograph", "Rx periapical 36.pdf", PdfBytes);
        var adm = await UploadAsync(s.Secretary, s.PatientA, "Administrative", "credencial.pdf", PdfBytes);
        await TestApi.AssertProblem(await UploadRawAsync(s.Secretary, s.PatientA, "Radiograph", "rx.pdf", PdfBytes), HttpStatusCode.Forbidden);

        await TestApi.AssertProblem(await s.Secretary.Get($"/api/v1/documents/{rx}/download"), HttpStatusCode.Forbidden);
        var list = (await s.Secretary.GetOk($"/api/v1/patients/{s.PatientA}/documents")).AsArray();
        Assert.DoesNotContain(list, d => d!["id"]!.GetValue<Guid>() == rx);
        Assert.Contains(list, d => d!["id"]!.GetValue<Guid>() == adm);

        var download = await s.Doctor.Get($"/api/v1/documents/{rx}/download");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("application/pdf", download.Content.Headers.ContentType!.MediaType);
        Assert.Equal(PdfBytes, await download.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Anonymous_download_is_rejected_and_soft_delete_hides_document()
    {
        var id = await UploadAsync(s.Doctor, s.PatientA, "Administrative", "orden.pdf", PdfBytes);
        Assert.Equal(HttpStatusCode.Unauthorized, (await s.Factory.NewClient().GetAsync($"/api/v1/documents/{id}/download")).StatusCode);
        await TestApi.AssertProblem(await s.Secretary.Delete($"/api/v1/documents/{id}?reason=x"), HttpStatusCode.Forbidden);
        Assert.Equal(HttpStatusCode.NoContent, (await s.Doctor.Delete($"/api/v1/documents/{id}?reason=Duplicado")).StatusCode);
        await TestApi.AssertProblem(await s.Doctor.Get($"/api/v1/documents/{id}/download"), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Consent_records_system_acceptance_distinct_from_signed_document()
    {
        var consent = await s.Secretary.PostOk($"/api/v1/patients/{s.PatientA}/consents", new { title = "Consentimiento endodoncia", content = "Texto informado..." });
        var cid = consent["id"]!.GetValue<Guid>();
        var accepted = await s.Secretary.PostOk($"/api/v1/patients/{s.PatientA}/consents/{cid}/accept", new { acceptedByName = "Martín Rodríguez", acceptedByDocument = "32456789" });
        Assert.Equal("AcceptedInSystem", accepted["status"]!.GetValue<string>());
        Assert.Equal("SystemAcceptance", accepted["signatureType"]!.GetValue<string>());
        var doc = await UploadAsync(s.Secretary, s.PatientA, "ConsentForm", "consentimiento firmado.pdf", PdfBytes);
        var signed = await s.Secretary.PostOk($"/api/v1/patients/{s.PatientA}/consents/{cid}/attach-signed", new { documentId = doc, signedByName = "Martín Rodríguez" });
        Assert.Equal("HandwrittenScanned", signed["signatureType"]!.GetValue<string>());
    }
}

public sealed class ClinicalTests(ClinicScenario s) : IClassFixture<ClinicScenario>
{
    [Fact]
    public async Task Clinical_entries_are_versioned_and_never_silently_overwritten()
    {
        var entry = await s.Doctor.PostOk($"/api/v1/patients/{s.PatientA}/clinical-record/entries", new
        {
            type = "Evolution", title = "Endodoncia · 2.ª sesión", description = "Conductometría con limas K #15 a #30.", indications = "Ibuprofeno 400 mg", teeth = new[] { 36 },
        });
        var id = entry["id"]!.GetValue<Guid>();
        Assert.Equal(1, entry["version"]!.GetValue<int>());

        await TestApi.AssertProblem(await s.Doctor.Post($"/api/v1/patients/{s.PatientA}/clinical-record/entries/{id}/amendments",
            new { title = "x", description = "y", version = 1, reason = "" }), HttpStatusCode.BadRequest);
        var amended = await s.Doctor.PostOk($"/api/v1/patients/{s.PatientA}/clinical-record/entries/{id}/amendments",
            new { title = "Endodoncia · 2.ª sesión", description = "Conductometría con limas K #15 a #35.", teeth = new[] { 36 }, version = 1, reason = "Corrección de calibre" });
        Assert.Equal(2, amended["version"]!.GetValue<int>());
        var revisions = (await s.Doctor.GetOk($"/api/v1/patients/{s.PatientA}/clinical-record/entries/{id}/revisions")).AsArray();
        Assert.Single(revisions);
        Assert.Contains("#30", revisions[0]!["description"]!.GetValue<string>());

        // Otro profesional no puede corregir el registro ajeno.
        var paz = await s.Doctor.InviteAndLoginAsync("tomas.paz2@odontia.test", SystemRoles.OdontologoId, "Tomás", "Paz");
        await TestApi.AssertProblem(await paz.Post($"/api/v1/patients/{s.PatientA}/clinical-record/entries/{id}/amendments",
            new { title = "x", version = 2, reason = "y" }), HttpStatusCode.Forbidden);

        var timeline = await s.Doctor.GetOk($"/api/v1/patients/{s.PatientA}/clinical-record/entries?toothCode=36");
        Assert.True(timeline["totalCount"]!.GetValue<int>() >= 1);
        var audit = await s.Doctor.GetOk($"/api/v1/audit?entityType=ClinicalEntry&entityId={id}");
        Assert.Contains(audit["items"]!.AsArray(), a => a!["action"]!.GetValue<string>() == "ClinicalEntryAmended");
    }

    [Fact]
    public async Task Odontogram_records_conditions_with_history()
    {
        var catalog = await s.Doctor.GetOk("/api/v1/odontogram/catalog");
        Assert.Equal(52, catalog["teeth"]!.AsArray().Count);
        Guid Cond(string code) => catalog["conditions"]!.AsArray().First(c => c!["code"]!.GetValue<string>() == code)!["id"]!.GetValue<Guid>();

        await s.Doctor.PostOk($"/api/v1/patients/{s.PatientA}/odontogram/findings", new
        {
            items = new object[]
            {
                new { toothCode = 36, surface = "Occlusal", conditionId = Cond("CARIES"), status = "Existing" },
                new { toothCode = 36, conditionId = Cond("ROOT_CANAL"), status = "Planned", notes = "Endodoncia indicada" },
                new { toothCode = 46, conditionId = Cond("MISSING"), status = "Existing" },
            },
        });
        var state = await s.Doctor.PostOk($"/api/v1/patients/{s.PatientA}/odontogram/findings", new
        {
            items = new object[] { new { toothCode = 36, surface = "Occlusal", conditionId = Cond("FILLING"), status = "Existing" } },
        });
        var tooth36 = state["teeth"]!.AsArray().First(t => t!["toothCode"]!.GetValue<int>() == 36)!;
        var codes = tooth36["findings"]!.AsArray().Select(f => f!["conditionCode"]!.GetValue<string>()).ToList();
        Assert.DoesNotContain("CARIES", codes);
        Assert.Contains("FILLING", codes);
        Assert.True(state["teeth"]!.AsArray().First(t => t!["toothCode"]!.GetValue<int>() == 46)!["isAbsent"]!.GetValue<bool>());

        var invalid = await s.Doctor.Post($"/api/v1/patients/{s.PatientA}/odontogram/findings", new
        {
            items = new object[] { new { toothCode = 46, surface = "Occlusal", conditionId = Cond("CARIES"), status = "Existing" } },
        });
        await TestApi.AssertProblem(invalid, HttpStatusCode.UnprocessableEntity, "tooth_absent");
        await TestApi.AssertProblem(await s.Doctor.Post($"/api/v1/patients/{s.PatientA}/odontogram/findings", new
        {
            items = new object[] { new { toothCode = 19, conditionId = Cond("CROWN"), status = "Existing" } },
        }), HttpStatusCode.BadRequest);

        var history = await s.Doctor.GetOk($"/api/v1/patients/{s.PatientA}/odontogram/history?toothCode=36");
        var entries = history["items"]!.AsArray();
        Assert.Contains(entries, h => h!["changeType"]!.GetValue<string>() == "Resolved");
        Assert.All(entries, h => Assert.Equal(s.Doctor.UserId, h!["professionalUserId"]!.GetValue<Guid>()));
    }

    [Fact]
    public async Task Treatment_plan_progress_and_patient_summary()
    {
        var proc = await s.Doctor.PostOk("/api/v1/procedures", new { code = "OBT-RES", name = "Obturación resina", price = 45000, requiresTooth = true });
        var plan = await s.Doctor.PostOk($"/api/v1/patients/{s.PatientA}/treatment-plans", new
        {
            title = "Rehabilitación", items = new object[]
            {
                new { procedureId = proc["id"], toothCode = 16, surfaces = new[] { "Occlusal", "Mesial" } },
                new { procedureId = proc["id"], toothCode = 11, surfaces = new[] { "Mesial" } },
            },
        });
        var planId = plan["id"]!.GetValue<Guid>();
        var items = plan["items"]!.AsArray();
        var updated = await s.Doctor.PutOk($"/api/v1/patients/{s.PatientA}/treatment-plans/{planId}/items/{items[0]!["id"]}/status", new { status = "Completed" });
        Assert.Equal(50, updated["progressPercent"]!.GetValue<int>());
        Assert.Equal("InProgress", updated["status"]!.GetValue<string>());
        await TestApi.AssertProblem(await s.Doctor.Post($"/api/v1/patients/{s.PatientA}/treatment-plans/{planId}/items",
            new { procedureId = proc["id"], toothCode = 11, surfaces = new[] { "Occlusal" } }), HttpStatusCode.UnprocessableEntity, "invalid_surface");

        var summary = await s.Doctor.GetOk($"/api/v1/patients/{s.PatientA}/summary");
        Assert.NotNull(summary["activeTreatment"]);
        var secretarySummary = await s.Secretary.GetOk($"/api/v1/patients/{s.PatientA}/summary");
        Assert.Null(secretarySummary["activeTreatment"]);
        Assert.Null(secretarySummary["lastEvolution"]);
    }
}

public sealed class ModulesTests(ClinicScenario s) : IClassFixture<ClinicScenario>
{
    [Fact]
    public async Task Consultorio_settings_gabinetes_and_insurances()
    {
        var settings = await s.Doctor.PutOk("/api/v1/consultorio/settings", new { timeZoneId = "America/Argentina/Buenos_Aires", defaultAppointmentMinutes = 45, slotIntervalMinutes = 15, reminderHoursBefore = 24 });
        Assert.Equal(45, settings["defaultAppointmentMinutes"]!.GetValue<int>());
        await TestApi.AssertProblem(await s.Doctor.Put("/api/v1/consultorio/settings", new { timeZoneId = "Mars/Olympus", defaultAppointmentMinutes = 45, slotIntervalMinutes = 15, reminderHoursBefore = 24 }), HttpStatusCode.BadRequest);
        await TestApi.AssertProblem(await s.Secretary.Put("/api/v1/consultorio/settings", new { timeZoneId = "America/Argentina/Buenos_Aires", defaultAppointmentMinutes = 30, slotIntervalMinutes = 15, reminderHoursBefore = 24 }), HttpStatusCode.Forbidden);

        await s.Doctor.PostOk("/api/v1/consultorio/gabinetes", new { name = "Consultorio 1" });
        await TestApi.AssertProblem(await s.Doctor.Post("/api/v1/consultorio/gabinetes", new { name = "Consultorio 1" }), HttpStatusCode.Conflict);

        var osde = await s.Secretary.PostOk("/api/v1/health-insurances", new { name = "OSDE", code = "OSDE" });
        var p = await s.Secretary.GetOk($"/api/v1/patients/{s.PatientA}");
        var update = await s.Secretary.PutOk($"/api/v1/patients/{s.PatientA}", new
        {
            firstName = "Martín", lastName = "Rodríguez", documentNumber = "32.456.789", sex = "Male", birthDate = "1984-03-02",
            phone = p["phone"]!.GetValue<string>(), healthInsuranceId = osde["id"], insurancePlan = "310", affiliateNumber = "61-123",
        });
        Assert.Equal("OSDE", update["healthInsuranceName"]!.GetValue<string>());
        var search = await s.Secretary.GetOk("/api/v1/patients?search=5523");
        Assert.True(search["totalCount"]!.GetValue<int>() >= 1);
        var byDni = await s.Secretary.GetOk("/api/v1/patients?search=32.456");
        Assert.True(byDni["totalCount"]!.GetValue<int>() >= 1);
        await TestApi.AssertProblem(await s.Secretary.Post("/api/v1/patients", new { patient = new { firstName = "Dup", lastName = "X", documentNumber = "32456789", sex = "Male" } }), HttpStatusCode.Conflict, "duplicate_patient");
    }

    [Fact]
    public async Task Tasks_lab_notifications_dashboards_and_reminders()
    {
        var task = await s.Doctor.PostOk("/api/v1/tasks", new { title = "Confirmar turnos de mañana", priority = "High", category = "Appointments", assignedToUserId = s.Secretary.UserId, dueAt = DateTimeOffset.UtcNow.AddHours(-1), patientId = s.PatientA });
        Assert.True(task["isOverdue"]!.GetValue<bool>());
        var overdue = await s.Secretary.GetOk("/api/v1/tasks?view=overdue");
        Assert.Contains(overdue["items"]!.AsArray(), t => t!["id"]!.GetValue<Guid>() == task["id"]!.GetValue<Guid>());
        var unread = await s.Secretary.GetOk("/api/v1/notifications/unread-count");
        Assert.True(unread["unread"]!.GetValue<int>() >= 1);
        await s.Secretary.PutOk($"/api/v1/tasks/{task["id"]}/status", new { status = "Completed" });

        var lab = await s.Doctor.PostOk("/api/v1/laboratories", new { name = "Dental Sur", phone = "+54 11 4000-0000" });
        var work = await s.Doctor.PostOk("/api/v1/lab-works", new { patientId = s.PatientA, laboratoryId = lab["id"], workType = "Corona de zirconio", toothCode = 21, estimatedDeliveryDate = DateTime.UtcNow.AddDays(3).ToString("yyyy-MM-dd"), cost = 85000 });
        var wid = work["id"]!.GetValue<Guid>();
        await TestApi.AssertProblem(await s.Secretary.Post("/api/v1/lab-works", new { patientId = s.PatientA, laboratoryId = lab["id"], workType = "X" }), HttpStatusCode.Forbidden);
        await s.Secretary.PostOk($"/api/v1/lab-works/{wid}/status", new { status = "Sent" });
        await s.Secretary.PostOk($"/api/v1/lab-works/{wid}/status", new { status = "Received" });
        await TestApi.AssertProblem(await s.Secretary.Post($"/api/v1/lab-works/{wid}/status", new { status = "Placed" }), HttpStatusCode.Forbidden);
        await s.Doctor.PostOk($"/api/v1/lab-works/{wid}/status", new { status = "Placed" });
        Assert.Equal(3, (await s.Doctor.GetOk($"/api/v1/lab-works/{wid}/history")).AsArray().Count);

        var appt = await s.Secretary.PostOk("/api/v1/appointments", new { patientId = s.PatientA, professionalUserId = s.Doctor.UserId, start = TestApi.LocalSlot(9, 10), durationMinutes = 30 });
        var reminder = await s.Secretary.PostOk($"/api/v1/appointments/{appt["id"]}/reminder?channel=WhatsApp");
        Assert.Equal("NotSentNoProvider", reminder["status"]!.GetValue<string>()); // nunca se afirma un envío sin proveedor
        Assert.DoesNotContain("5523", reminder["recipient"]!.GetValue<string>()[..^4]);

        var reception = await s.Secretary.GetOk("/api/v1/dashboard/reception");
        Assert.NotNull(reception["todayAgenda"]);
        Assert.DoesNotContain("Losartán", reception.ToJsonString());
        var clinical = await s.Doctor.GetOk("/api/v1/dashboard/clinical");
        Assert.Equal(6, clinical["weeklyActivity"]!.AsArray().Count);
    }

    [Fact]
    public async Task Validation_errors_have_consistent_problem_details_format()
    {
        var r = await s.Secretary.Post("/api/v1/patients", new { patient = new { firstName = "", lastName = "", documentNumber = "", sex = "Male" } });
        await TestApi.AssertProblem(r, HttpStatusCode.BadRequest, "validation_error");
        var json = JsonNode.Parse(await r.Content.ReadAsStringAsync())!;
        Assert.NotNull(json["traceId"]);
        Assert.NotNull(json["errors"]!["firstName"]);
        Assert.Equal("application/problem+json", r.Content.Headers.ContentType!.MediaType);
        Assert.Equal("nosniff", r.Headers.GetValues("X-Content-Type-Options").First());
    }
}
