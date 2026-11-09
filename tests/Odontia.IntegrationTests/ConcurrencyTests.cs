using System.Net;
using Odontia.IntegrationTests.Infrastructure;

namespace Odontia.IntegrationTests;

/// <summary>Sólo se ejecuta contra MySQL real (ODONTIA_TEST_MYSQL): SQLite en memoria no admite concurrencia real.</summary>
public sealed class MySqlFactAttribute : FactAttribute
{
    public MySqlFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("ODONTIA_TEST_MYSQL") is null)
            Skip = "Requiere ODONTIA_TEST_MYSQL (MySQL real) para probar concurrencia.";
    }
}

public sealed class ConcurrencyTests(ClinicScenario s) : IClassFixture<ClinicScenario>
{
    [MySqlFact]
    public async Task Simultaneous_bookings_of_the_same_slot_produce_exactly_one_appointment()
    {
        var patients = new List<Guid>();
        for (var i = 0; i < 6; i++) patients.Add(await s.Doctor.CreatePatientAsync($"5000000{i}", "Paciente", $"Concurrente{i}"));
        var start = TestApi.LocalSlot(10, 11);

        // Seis recepcionistas/clientes intentan reservar el mismo horario al mismo tiempo.
        var clients = await Task.WhenAll(Enumerable.Range(0, patients.Count).Select(async _ =>
        {
            var c = new TestApi(s.Factory.NewClient(), s.Factory);
            await c.LoginAsync(s.Secretary.Email, s.Secretary.Password);
            return c;
        }));
        var results = await Task.WhenAll(clients.Select((c, i) =>
            c.Post("/api/v1/appointments", new { patientId = patients[i], professionalUserId = s.Doctor.UserId, start, durationMinutes = 30 })));

        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.All(results.Where(r => r.StatusCode != HttpStatusCode.Created), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));

        var from = Uri.EscapeDataString(DateTimeOffset.Parse(start).AddMinutes(-1).ToString("O"));
        var to = Uri.EscapeDataString(DateTimeOffset.Parse(start).AddMinutes(31).ToString("O"));
        var list = await s.Doctor.GetOk($"/api/v1/appointments?from={from}&to={to}&professionalUserId={s.Doctor.UserId}");
        Assert.Equal(1, list["totalCount"]!.GetValue<int>());
    }
}
