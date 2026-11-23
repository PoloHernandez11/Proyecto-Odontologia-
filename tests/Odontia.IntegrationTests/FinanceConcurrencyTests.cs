using System.Net;
using System.Text.Json.Nodes;
using Odontia.IntegrationTests.Infrastructure;

namespace Odontia.IntegrationTests;

/// <summary>
/// Solicitudes simultáneas sobre el mismo presupuesto o pago (QA API-01/02/03). Requieren MySQL real
/// (ODONTIA_TEST_MYSQL): SQLite en memoria comparte una única conexión y no admite concurrencia real.
/// La lógica equivalente se prueba de forma determinista sobre SQLite en Odontia.UnitTests (FinanceConcurrencyTests).
/// </summary>
public sealed class FinanceConcurrencyTests(ClinicScenario s) : IClassFixture<ClinicScenario>
{
    private const int Parallel = 6;
    private static int _dni = 61000000;

    private async Task<TestApi[]> ClientsAsync() => await Task.WhenAll(Enumerable.Range(0, Parallel).Select(async _ =>
    {
        var c = new TestApi(s.Factory.NewClient(), s.Factory);
        await c.LoginAsync(s.Doctor.Email, s.Doctor.Password);
        return c;
    }));

    private async Task<(Guid Patient, Guid Budget)> IssuedBudgetAsync(decimal total)
    {
        var patient = await s.Doctor.CreatePatientAsync(Interlocked.Increment(ref _dni).ToString(), "Paciente", "Concurrente");
        var budget = await s.Doctor.PostOk($"/api/v1/patients/{patient}/budgets", new
        {
            discountPercent = 0,
            items = new object[] { new { description = "Corona", toothCode = 16, quantity = 1, unitPrice = total, discountPercent = 0 } },
        });
        var id = budget["id"]!.GetValue<Guid>();
        await s.Doctor.PostOk($"/api/v1/budgets/{id}/issue");
        return (patient, id);
    }

    private async Task<(Guid Patient, Guid Budget)> ApprovedBudgetAsync(decimal total)
    {
        var (patient, id) = await IssuedBudgetAsync(total);
        await s.Doctor.PostOk($"/api/v1/budgets/{id}/approve");
        return (patient, id);
    }

    private async Task<JsonArray> MovementsAsync(Guid patient) =>
        (await s.Doctor.GetOk($"/api/v1/patients/{patient}/account"))["movements"]!.AsArray();

    private static void AssertOneSuccess(HttpResponseMessage[] results, HttpStatusCode success)
    {
        Assert.Equal(1, results.Count(r => r.StatusCode == success));
        Assert.All(results.Where(r => r.StatusCode != success),
            r => Assert.Contains(r.StatusCode, new[] { HttpStatusCode.Conflict, HttpStatusCode.UnprocessableEntity }));
    }

    [MySqlFact]
    public async Task Simultaneous_payments_cannot_exceed_the_budget_outstanding()
    {
        var (patient, budget) = await ApprovedBudgetAsync(1000m);
        var clients = await ClientsAsync();

        var results = await Task.WhenAll(clients.Select(c =>
            c.Post("/api/v1/payments", new { patientId = patient, budgetId = budget, amount = 600, method = "Cash" })));

        AssertOneSuccess(results, HttpStatusCode.Created);
        var b = await s.Doctor.GetOk($"/api/v1/budgets/{budget}");
        Assert.Equal(600m, b["paid"]!.GetValue<decimal>());
        Assert.Equal(400m, b["outstanding"]!.GetValue<decimal>());
        Assert.Single(await MovementsAsync(patient), m => m!["type"]!.GetValue<string>() == "Payment");

        // Tras el conflicto, un reintento con el saldo actualizado se valida correctamente.
        await TestApi.AssertProblem(await s.Doctor.Post("/api/v1/payments", new { patientId = patient, budgetId = budget, amount = 600, method = "Cash" }),
            HttpStatusCode.UnprocessableEntity, "amount_exceeds_outstanding");
    }

    [MySqlFact]
    public async Task Simultaneous_voids_of_the_same_payment_create_a_single_reversal()
    {
        var (patient, budget) = await ApprovedBudgetAsync(1000m);
        var payment = (await s.Doctor.PostOk("/api/v1/payments", new { patientId = patient, budgetId = budget, amount = 300, method = "Cash" }))["id"]!.GetValue<Guid>();
        var clients = await ClientsAsync();

        var results = await Task.WhenAll(clients.Select(c => c.Post($"/api/v1/payments/{payment}/void", new { reason = "Error de carga" })));

        AssertOneSuccess(results, HttpStatusCode.OK);
        var movements = await MovementsAsync(patient);
        Assert.Single(movements, m => m!["type"]!.GetValue<string>() == "PaymentReversal");
        Assert.Equal(1000m, (await s.Doctor.GetOk($"/api/v1/budgets/{budget}"))["outstanding"]!.GetValue<decimal>());
    }

    [MySqlFact]
    public async Task Simultaneous_decisions_on_an_issued_budget_apply_a_single_transition()
    {
        var (patient, budget) = await IssuedBudgetAsync(1000m);
        var clients = await ClientsAsync();

        var results = await Task.WhenAll(clients.Select((c, i) => (i % 3) switch
        {
            0 => c.Post($"/api/v1/budgets/{budget}/approve"),
            1 => c.Post($"/api/v1/budgets/{budget}/reject", new { reason = "No acepta" }),
            _ => c.Post($"/api/v1/budgets/{budget}/cancel", new { reason = "Desiste" }),
        }));

        AssertOneSuccess(results, HttpStatusCode.OK);
        var history = (await s.Doctor.GetOk($"/api/v1/budgets/{budget}/history")).AsArray();
        Assert.Equal(2, history.Count); // emisión + una única decisión
        var status = (await s.Doctor.GetOk($"/api/v1/budgets/{budget}"))["status"]!.GetValue<string>();
        var charges = (await MovementsAsync(patient)).Count(m => m!["type"]!.GetValue<string>() == "BudgetCharge");
        Assert.Equal(status == "Approved" ? 1 : 0, charges);
    }

    [MySqlFact]
    public async Task Simultaneous_cancellations_of_an_approved_budget_create_a_single_reversal()
    {
        var (patient, budget) = await ApprovedBudgetAsync(1000m);
        var clients = await ClientsAsync();

        var results = await Task.WhenAll(clients.Select(c => c.Post($"/api/v1/budgets/{budget}/cancel", new { reason = "Desiste" })));

        AssertOneSuccess(results, HttpStatusCode.OK);
        var movements = await MovementsAsync(patient);
        Assert.Single(movements, m => m!["type"]!.GetValue<string>() == "BudgetChargeReversal");
        Assert.Equal(3, (await s.Doctor.GetOk($"/api/v1/budgets/{budget}/history")).AsArray().Count);
    }
}
