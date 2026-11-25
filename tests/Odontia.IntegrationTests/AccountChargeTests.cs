using System.Net;
using System.Text.Json.Nodes;
using Odontia.Domain.Authorization;
using Odontia.IntegrationTests.Infrastructure;

namespace Odontia.IntegrationTests;

/// <summary>Cargos manuales en la cuenta corriente: alta, reducción por pagos, anulación con contraasiento, validación y permisos.</summary>
public sealed class AccountChargeTests(ClinicScenario s) : IClassFixture<ClinicScenario>
{
    private static int _dni = 62000000;

    private Task<Guid> NewPatientAsync() => s.Doctor.CreatePatientAsync(Interlocked.Increment(ref _dni).ToString(), "Paciente", "Cargo");

    private static string Charges(Guid patient) => $"/api/v1/patients/{patient}/account/charges";

    private static JsonNode ManualCharge(JsonNode statement) =>
        statement["movements"]!.AsArray().Single(m => m!["type"]!.GetValue<string>() == "ManualCharge")!;

    [Fact]
    public async Task Charge_raises_balance_payment_lowers_it_and_void_reverses_it()
    {
        var patient = await NewPatientAsync();

        var created = await s.Secretary.Post(Charges(patient), new { description = "Consulta de urgencia", amount = 15000.50m });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var statement = await TestApi.Ok(created);
        Assert.Equal(15000.50m, statement["balance"]!.GetValue<decimal>());
        var charge = ManualCharge(statement);
        Assert.Equal("Debit", charge["direction"]!.GetValue<string>());
        Assert.Equal("Consulta de urgencia", charge["description"]!.GetValue<string>());
        Assert.Null(charge["reversedByMovementId"]?.GetValue<Guid?>());

        await s.Secretary.PostOk("/api/v1/payments", new { patientId = patient, amount = 5000, method = "Cash" });
        Assert.Equal(10000.50m, (await s.Secretary.GetOk($"/api/v1/patients/{patient}/account"))["balance"]!.GetValue<decimal>());

        var chargeId = charge["id"]!.GetValue<Guid>();
        // La secretaria registra cargos pero no anula (payments.void), igual que con los pagos.
        await TestApi.AssertProblem(await s.Secretary.Post($"{Charges(patient)}/{chargeId}/void", new { reason = "Error" }), HttpStatusCode.Forbidden);

        var after = await s.Doctor.PostOk($"{Charges(patient)}/{chargeId}/void", new { reason = "Cargado por error" });
        Assert.Equal(-5000m, after["balance"]!.GetValue<decimal>());
        var reversal = after["movements"]!.AsArray().Single(m => m!["type"]!.GetValue<string>() == "ManualChargeReversal")!;
        Assert.Equal("Credit", reversal["direction"]!.GetValue<string>());
        Assert.Equal(chargeId, reversal["reversesMovementId"]!.GetValue<Guid>());
        Assert.Equal(reversal["id"]!.GetValue<Guid>(), ManualCharge(after)["reversedByMovementId"]!.GetValue<Guid>());

        await TestApi.AssertProblem(await s.Doctor.Post($"{Charges(patient)}/{chargeId}/void", new { reason = "Otra vez" }),
            HttpStatusCode.UnprocessableEntity, "charge_already_voided");
    }

    [Fact]
    public async Task Invalid_charges_are_rejected()
    {
        var patient = await NewPatientAsync();
        await TestApi.AssertProblem(await s.Secretary.Post(Charges(patient), new { description = "Consulta", amount = 0 }), HttpStatusCode.BadRequest, "validation_error");
        await TestApi.AssertProblem(await s.Secretary.Post(Charges(patient), new { description = "Consulta", amount = -10 }), HttpStatusCode.BadRequest, "validation_error");
        await TestApi.AssertProblem(await s.Secretary.Post(Charges(patient), new { description = "Consulta", amount = 10.555 }), HttpStatusCode.BadRequest, "validation_error");
        await TestApi.AssertProblem(await s.Secretary.Post(Charges(patient), new { description = " ", amount = 100 }), HttpStatusCode.BadRequest, "validation_error");
        await TestApi.AssertProblem(await s.Secretary.Post(Charges(patient), new { amount = 100 }), HttpStatusCode.BadRequest);
        await TestApi.AssertProblem(await s.Secretary.Post(Charges(patient), new { description = new string('x', 251), amount = 100 }), HttpStatusCode.BadRequest, "validation_error");
        await TestApi.AssertProblem(await s.Secretary.Post(Charges(patient), new { description = "Consulta", amount = 100, occurredAt = DateTimeOffset.UtcNow.AddDays(2) }),
            HttpStatusCode.BadRequest);
        await TestApi.AssertProblem(await s.Secretary.Post(Charges(Guid.NewGuid()), new { description = "Consulta", amount = 100 }), HttpStatusCode.NotFound);

        var past = DateTimeOffset.UtcNow.AddDays(-3);
        var st = await s.Secretary.PostOk(Charges(patient), new { description = "Consulta", amount = 100, occurredAt = past });
        Assert.Equal(past.UtcDateTime.Date, ManualCharge(st)["occurredAtUtc"]!.GetValue<DateTime>().ToUniversalTime().Date);
    }

    [Fact]
    public async Task Only_manual_charges_can_be_voided_and_reason_is_required()
    {
        var patient = await NewPatientAsync();
        await s.Doctor.PostOk("/api/v1/payments", new { patientId = patient, amount = 100, method = "Cash" });
        var st = await s.Doctor.PostOk(Charges(patient), new { description = "Blanqueamiento", amount = 300 });
        var payment = st["movements"]!.AsArray().Single(m => m!["type"]!.GetValue<string>() == "Payment")!["id"]!.GetValue<Guid>();
        var charge = ManualCharge(st)["id"]!.GetValue<Guid>();

        await TestApi.AssertProblem(await s.Doctor.Post($"{Charges(patient)}/{payment}/void", new { reason = "x" }),
            HttpStatusCode.UnprocessableEntity, "movement_not_manual_charge");
        await TestApi.AssertProblem(await s.Doctor.Post($"{Charges(patient)}/{charge}/void", new { reason = "" }), HttpStatusCode.BadRequest, "validation_error");
        await TestApi.AssertProblem(await s.Doctor.Post($"{Charges(patient)}/{Guid.NewGuid()}/void", new { reason = "x" }), HttpStatusCode.NotFound);
        // El movimiento debe pertenecer al paciente de la ruta.
        var other = await NewPatientAsync();
        await TestApi.AssertProblem(await s.Doctor.Post($"{Charges(other)}/{charge}/void", new { reason = "x" }), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Charges_require_permission_and_are_isolated_per_clinic()
    {
        var patient = await NewPatientAsync();
        var admin = await s.Doctor.InviteAndLoginAsync("admin.cargos@odontia.test", SystemRoles.AdministradorId, "Ana", "Admin");
        // Administrador: finance.read y payments.void, pero no payments.write.
        await TestApi.AssertProblem(await admin.Post(Charges(patient), new { description = "Consulta", amount = 100 }), HttpStatusCode.Forbidden);

        var st = await s.Doctor.PostOk(Charges(patient), new { description = "Consulta", amount = 100 });
        var charge = ManualCharge(st)["id"]!.GetValue<Guid>();
        await TestApi.AssertProblem(await s.OtherClinic.Post(Charges(patient), new { description = "Consulta", amount = 100 }), HttpStatusCode.NotFound);
        await TestApi.AssertProblem(await s.OtherClinic.Post($"{Charges(patient)}/{charge}/void", new { reason = "x" }), HttpStatusCode.NotFound);
        Assert.Equal(100m, (await s.Doctor.GetOk($"/api/v1/patients/{patient}/account"))["balance"]!.GetValue<decimal>());
    }

    [MySqlFact]
    public async Task Simultaneous_voids_of_the_same_charge_create_a_single_reversal()
    {
        var patient = await NewPatientAsync();
        var charge = ManualCharge(await s.Doctor.PostOk(Charges(patient), new { description = "Consulta", amount = 100 }))["id"]!.GetValue<Guid>();
        var clients = await Task.WhenAll(Enumerable.Range(0, 6).Select(async _ =>
        {
            var c = new TestApi(s.Factory.NewClient(), s.Factory);
            await c.LoginAsync(s.Doctor.Email, s.Doctor.Password);
            return c;
        }));

        var results = await Task.WhenAll(clients.Select(c => c.Post($"{Charges(patient)}/{charge}/void", new { reason = "Error de carga" })));

        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.All(results.Where(r => r.StatusCode != HttpStatusCode.OK),
            r => Assert.Contains(r.StatusCode, new[] { HttpStatusCode.Conflict, HttpStatusCode.UnprocessableEntity }));
        var movements = (await s.Doctor.GetOk($"/api/v1/patients/{patient}/account"))["movements"]!.AsArray();
        Assert.Single(movements, m => m!["type"]!.GetValue<string>() == "ManualChargeReversal");
    }
}
