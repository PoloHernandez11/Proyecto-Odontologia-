using System.Net;
using Odontia.IntegrationTests.Infrastructure;

namespace Odontia.IntegrationTests;

public sealed class ExpenseTests(ClinicScenario s) : IClassFixture<ClinicScenario>
{
    [Fact]
    public async Task Invalid_expenses_are_rejected()
    {
        await TestApi.AssertProblem(await s.Secretary.Post("/api/v1/expenses", new { description = "", category = "Supplies", amount = 100, method = "Cash" }), HttpStatusCode.BadRequest, "validation_error");
        await TestApi.AssertProblem(await s.Secretary.Post("/api/v1/expenses", new { description = "Guantes", category = "Supplies", amount = 0, method = "Cash" }), HttpStatusCode.BadRequest, "validation_error");
        await TestApi.AssertProblem(await s.Secretary.Post("/api/v1/expenses", new { description = "Guantes", category = "Supplies", amount = 10.555, method = "Cash" }), HttpStatusCode.BadRequest, "validation_error");
        await TestApi.AssertProblem(await s.Secretary.Post("/api/v1/expenses", new { description = "Guantes", category = "Caprichos", amount = 100, method = "Cash" }), HttpStatusCode.BadRequest);
        await TestApi.AssertProblem(await s.Secretary.Post("/api/v1/expenses", new { description = "Guantes", category = "Supplies", amount = 100, method = "Cash", paidAt = DateTimeOffset.UtcNow.AddDays(2) }), HttpStatusCode.BadRequest);
        await TestApi.AssertProblem(await s.Secretary.Post("/api/v1/expenses", new { description = "Corona", category = "Laboratory", amount = 100, method = "Cash", laboratoryId = Guid.NewGuid() }), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Expense_register_list_void_and_summary()
    {
        var from = DateTimeOffset.UtcNow.AddDays(-1).ToString("o");
        var to = DateTimeOffset.UtcNow.AddDays(1).ToString("o");
        var range = $"from={Uri.EscapeDataString(from)}&to={Uri.EscapeDataString(to)}";
        var before = await s.Doctor.GetOk($"/api/v1/finance/summary?{range}");
        var expensesBefore = before["expenses"]!.GetValue<decimal>();
        var netBefore = before["net"]!.GetValue<decimal>();

        var gloves = await s.Secretary.PostOk("/api/v1/expenses", new { description = "Guantes y barbijos", category = "Supplies", amount = 38500.50m, method = "DebitCard", supplier = "Insumos Dentales SA" });
        var rent = await s.Secretary.PostOk("/api/v1/expenses", new { description = "Alquiler del consultorio", category = "Rent", amount = 250000, method = "BankTransfer" });
        Assert.Equal("Registered", gloves["status"]!.GetValue<string>());
        Assert.Equal(38500.50m, gloves["amount"]!.GetValue<decimal>());

        var list = await s.Doctor.GetOk($"/api/v1/expenses?{range}&category=Supplies");
        Assert.Contains(list["items"]!.AsArray(), e => e!["id"]!.GetValue<Guid>() == gloves["id"]!.GetValue<Guid>());
        Assert.DoesNotContain(list["items"]!.AsArray(), e => e!["id"]!.GetValue<Guid>() == rent["id"]!.GetValue<Guid>());

        var summary = await s.Doctor.GetOk($"/api/v1/finance/summary?{range}");
        Assert.Equal(expensesBefore + 288500.50m, summary["expenses"]!.GetValue<decimal>());
        Assert.Equal(netBefore - 288500.50m, summary["net"]!.GetValue<decimal>());
        Assert.Contains(summary["expensesByCategory"]!.AsArray(), c => c!["category"]!.GetValue<string>() == "Rent");

        // La secretaria no puede anular; quien tiene payments.void sí, con motivo. El egreso no se borra.
        await TestApi.AssertProblem(await s.Secretary.Post($"/api/v1/expenses/{rent["id"]}/void", new { reason = "Error" }), HttpStatusCode.Forbidden);
        await TestApi.AssertProblem(await s.Doctor.Post($"/api/v1/expenses/{rent["id"]}/void", new { reason = "" }), HttpStatusCode.BadRequest, "validation_error");
        var voided = await s.Doctor.PostOk($"/api/v1/expenses/{rent["id"]}/void", new { reason = "Cargado dos veces" });
        Assert.Equal("Voided", voided["status"]!.GetValue<string>());
        await TestApi.AssertProblem(await s.Doctor.Post($"/api/v1/expenses/{rent["id"]}/void", new { reason = "Otra vez" }), HttpStatusCode.UnprocessableEntity, "expense_already_voided");

        var after = await s.Doctor.GetOk($"/api/v1/finance/summary?{range}");
        Assert.Equal(expensesBefore + 38500.50m, after["expenses"]!.GetValue<decimal>());
        Assert.Equal("Voided", (await s.Doctor.GetOk($"/api/v1/expenses/{rent["id"]}"))["status"]!.GetValue<string>());
    }
}
