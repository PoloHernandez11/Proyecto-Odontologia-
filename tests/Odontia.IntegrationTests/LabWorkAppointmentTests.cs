using System.Net;
using Odontia.IntegrationTests.Infrastructure;

namespace Odontia.IntegrationTests;

/// <summary>Trabajos de laboratorio vinculados a un turno de entrega y visibles en la agenda.</summary>
public sealed class LabWorkAppointmentTests(ClinicScenario s) : IClassFixture<ClinicScenario>
{
    private async Task<Guid> BookAsync(Guid patient, int daysAhead, int hour) =>
        (await s.Secretary.PostOk("/api/v1/appointments", new
        {
            patientId = patient, professionalUserId = s.Doctor.UserId, start = TestApi.LocalSlot(daysAhead, hour), durationMinutes = 30, reason = "Entrega",
        }))["id"]!.GetValue<Guid>();

    private async Task<Guid> LabAsync(string name) => (await s.Doctor.PostOk("/api/v1/laboratories", new { name }))["id"]!.GetValue<Guid>();

    [Fact]
    public async Task Lab_work_with_several_teeth_is_linked_to_delivery_appointment_and_shown_in_agenda()
    {
        var lab = await LabAsync("Laboratorio Andes");
        var appt = await BookAsync(s.PatientA, 5, 9);
        var work = await s.Doctor.PostOk("/api/v1/lab-works", new
        {
            patientId = s.PatientA, laboratoryId = lab, workType = "Puente de zirconio", toothCodes = new[] { 14, 15, 16 }, appointmentId = appt,
        });
        Assert.Equal(appt, work["appointmentId"]!.GetValue<Guid>());
        Assert.NotNull(work["appointmentStartUtc"]);
        Assert.Equal([14, 15, 16], work["toothCodes"]!.AsArray().Select(t => t!.GetValue<int>()));
        Assert.Equal(14, work["toothCode"]!.GetValue<int>());

        // Un trabajo cancelado vinculado al mismo turno no aparece en la agenda.
        var cancelled = await s.Doctor.PostOk("/api/v1/lab-works", new { patientId = s.PatientA, laboratoryId = lab, workType = "Placa", toothCode = 21, appointmentId = appt });
        await s.Doctor.PostOk($"/api/v1/lab-works/{cancelled["id"]}/status", new { status = "Cancelled" });

        var date = DateTimeOffset.Parse(TestApi.LocalSlot(5, 0)).ToString("yyyy-MM-dd");
        foreach (var api in new[] { s.Doctor, s.Secretary })
        {
            var agenda = await api.GetOk($"/api/v1/agenda?from={date}&to={date}");
            var item = agenda["appointments"]!.AsArray().First(a => a!["id"]!.GetValue<Guid>() == appt)!;
            var labWorks = item["labWorks"]!.AsArray();
            var single = Assert.Single(labWorks)!;
            Assert.Equal(work["id"]!.GetValue<Guid>(), single["id"]!.GetValue<Guid>());
            Assert.Equal("Puente de zirconio", single["workType"]!.GetValue<string>());
            Assert.Equal("Laboratorio Andes", single["laboratoryName"]!.GetValue<string>());
            Assert.Equal("Pending", single["status"]!.GetValue<string>());
            Assert.Equal([14, 15, 16], single["toothCodes"]!.AsArray().Select(t => t!.GetValue<int>()));
        }

        var detail = await s.Secretary.GetOk($"/api/v1/appointments/{appt}");
        Assert.Single(detail["labWorks"]!.AsArray());

        // Edición: sin toothCodes se usa toothCode; se puede desvincular el turno.
        var updated = await s.Doctor.PutOk($"/api/v1/lab-works/{work["id"]}", new
        {
            patientId = s.PatientA, laboratoryId = lab, workType = "Puente de zirconio", toothCode = 24, appointmentId = (Guid?)null,
        });
        Assert.Null(updated["appointmentId"]);
        Assert.Equal([24], updated["toothCodes"]!.AsArray().Select(t => t!.GetValue<int>()));
    }

    [Fact]
    public async Task Appointment_of_another_patient_or_clinic_is_rejected()
    {
        var lab = await LabAsync("Laboratorio Sur");
        var other = await s.Doctor.CreatePatientAsync("28777123", "Marta", "Ríos");
        var otherAppt = await BookAsync(other, 6, 10);
        await TestApi.AssertProblem(await s.Doctor.Post("/api/v1/lab-works", new
        {
            patientId = s.PatientA, laboratoryId = lab, workType = "Corona", toothCodes = new[] { 21 }, appointmentId = otherAppt,
        }), HttpStatusCode.BadRequest);

        var ownAppt = await BookAsync(s.PatientA, 6, 11);
        var work = await s.Doctor.PostOk("/api/v1/lab-works", new { patientId = s.PatientA, laboratoryId = lab, workType = "Corona", toothCode = 21, appointmentId = ownAppt });
        await TestApi.AssertProblem(await s.Doctor.Put($"/api/v1/lab-works/{work["id"]}", new
        {
            patientId = s.PatientA, laboratoryId = lab, workType = "Corona", toothCode = 21, appointmentId = otherAppt,
        }), HttpStatusCode.BadRequest);

        // Turno de otro consultorio: no visible por el filtro de tenant.
        var foreign = (await s.OtherClinic.PostOk("/api/v1/appointments", new
        {
            patientId = s.PatientB, professionalUserId = s.OtherClinic.UserId, start = TestApi.LocalSlot(6, 12), durationMinutes = 30,
        }))["id"]!.GetValue<Guid>();
        await TestApi.AssertProblem(await s.Doctor.Post("/api/v1/lab-works", new
        {
            patientId = s.PatientA, laboratoryId = lab, workType = "Corona", toothCode = 21, appointmentId = foreign,
        }), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Invalid_tooth_in_list_is_rejected()
    {
        var lab = await LabAsync("Laboratorio Este");
        await TestApi.AssertProblem(await s.Doctor.Post("/api/v1/lab-works", new
        {
            patientId = s.PatientA, laboratoryId = lab, workType = "Puente", toothCodes = new[] { 14, 19 },
        }), HttpStatusCode.BadRequest);
    }
}
