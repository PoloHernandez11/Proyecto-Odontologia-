using Microsoft.EntityFrameworkCore;
using Odontia.Application.Common;
using Odontia.Application.Features.Finance;
using Odontia.Domain.Authorization;
using Odontia.Domain.Entities;
using Odontia.Domain.Enums;
using Odontia.Infrastructure.Persistence;

namespace Odontia.UnitTests.QA;

/// <summary>
/// Servicio de pagos contra SQLite en memoria: imputación a presupuestos, contraasientos y saldos.
/// </summary>
public sealed class QaPaymentServiceTests : IDisposable
{
    private readonly QaSqliteDb _qa = new();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 11, 19, 15, 0, 0, TimeSpan.Zero));
    private readonly CountingSequence _seq = new();

    public void Dispose() => _qa.Dispose();

    private (PaymentService Service, OdontiaDbContext Db) Service(params string[] permissions)
    {
        var tenant = _qa.Tenant(permissions);
        var db = _qa.Create(tenant, _time);
        return (new PaymentService(db, tenant, new ConsultorioClock(db, tenant, _time), _seq, new NullAudit()), db);
    }

    private async Task<Guid> ApprovedBudgetAsync(decimal amount)
    {
        await using var db = _qa.Create(_qa.Tenant(), _time);
        var b = new Budget(_qa.ConsultorioId, _qa.PatientId, 1, null, new DateOnly(2026, 11, 1), new DateOnly(2026, 12, 31), 0, null);
        b.AddItem(null, "Corona", 16, 1, amount, 0, null);
        b.Issue(_qa.UserId, _time.GetUtcNow().UtcDateTime);
        b.Approve(_qa.UserId, _time.GetUtcNow().UtcDateTime, new DateOnly(2026, 11, 19));
        db.Budgets.Add(b);
        db.AccountMovements.Add(new AccountMovement(_qa.ConsultorioId, _qa.PatientId, MovementType.BudgetCharge, MovementDirection.Debit, b.Total,
            _time.GetUtcNow().UtcDateTime.AddMinutes(-1), "Presupuesto #1 aprobado", b.Id, null, null));
        await db.SaveChangesAsync();
        return b.Id;
    }

    private RegisterPaymentRequest Pay(decimal amount, Guid? budget = null, DateTimeOffset? paidAt = null) =>
        new(_qa.PatientId, budget, amount, PaymentMethod.Cash, paidAt, null, null);

    [Fact]
    public async Task Register_creates_receipt_and_credit_movement()
    {
        var (svc, db) = Service();
        await using var _ = db;
        var dto = await svc.RegisterAsync(Pay(1500.50m), CancellationToken.None);
        Assert.Equal(1, dto.ReceiptNumber);
        var mov = await db.AccountMovements.SingleAsync();
        Assert.Equal(MovementDirection.Credit, mov.Direction);
        Assert.Equal(1500.50m, mov.Amount);
        Assert.Equal(dto.Id, mov.PaymentId);
    }

    [Fact]
    public async Task Register_requires_permission()
    {
        var (svc, db) = Service(Permissions.FinanceRead);
        await using var _ = db;
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => svc.RegisterAsync(Pay(10), CancellationToken.None));
    }

    [Fact]
    public async Task Future_payment_date_beyond_tolerance_is_rejected()
    {
        var (svc, db) = Service();
        await using var _ = db;
        var now = _time.GetUtcNow();
        await svc.RegisterAsync(Pay(10, paidAt: now.AddMinutes(5)), CancellationToken.None);
        await Assert.ThrowsAsync<RequestValidationException>(() => svc.RegisterAsync(Pay(10, paidAt: now.AddMinutes(6)), CancellationToken.None));
    }

    [Fact]
    public async Task Unknown_patient_is_rejected()
    {
        var (svc, db) = Service();
        await using var _ = db;
        await Assert.ThrowsAsync<RequestValidationException>(() =>
            svc.RegisterAsync(new RegisterPaymentRequest(Guid.NewGuid(), null, 10, PaymentMethod.Cash, null, null, null), CancellationToken.None));
    }

    [Fact]
    public async Task Payment_cannot_exceed_budget_outstanding_and_exact_amount_is_allowed()
    {
        var budget = await ApprovedBudgetAsync(1000m);
        var (svc, db) = Service();
        await using var _ = db;
        await svc.RegisterAsync(Pay(600m, budget), CancellationToken.None);
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => svc.RegisterAsync(Pay(400.01m, budget), CancellationToken.None));
        Assert.Equal("amount_exceeds_outstanding", ex.Code);
        await svc.RegisterAsync(Pay(400m, budget), CancellationToken.None);
        var st = await svc.GetStatementAsync(_qa.PatientId, CancellationToken.None);
        Assert.Equal(0m, st.Budgets.Single().Outstanding);
    }

    [Fact]
    public async Task Voided_payment_frees_budget_outstanding_and_reverses_balance()
    {
        var budget = await ApprovedBudgetAsync(1000m);
        var (svc, db) = Service();
        await using var _ = db;
        var p = await svc.RegisterAsync(Pay(1000m, budget), CancellationToken.None);
        await svc.VoidAsync(p.Id, new VoidPaymentRequest("Error de carga"), CancellationToken.None);
        var st = await svc.GetStatementAsync(_qa.PatientId, CancellationToken.None);
        Assert.Equal(1000m, st.Balance);
        Assert.Equal(1000m, st.Budgets.Single().Outstanding);
        Assert.Equal(3, st.Movements.Count);
        Assert.Equal(1000m, st.Movements[^1].RunningBalance);
        var reversal = await db.AccountMovements.SingleAsync(m => m.Type == MovementType.PaymentReversal);
        Assert.NotNull(reversal.ReversesMovementId);
    }

    [Fact]
    public async Task Void_twice_is_rejected()
    {
        var (svc, db) = Service();
        await using var _ = db;
        var p = await svc.RegisterAsync(Pay(10m), CancellationToken.None);
        await svc.VoidAsync(p.Id, new VoidPaymentRequest("x"), CancellationToken.None);
        await Assert.ThrowsAnyAsync<Exception>(() => svc.VoidAsync(p.Id, new VoidPaymentRequest("x"), CancellationToken.None));
        Assert.Equal(1, await db.AccountMovements.CountAsync(m => m.Type == MovementType.PaymentReversal));
    }

    [Fact]
    public async Task Payment_to_unapproved_or_foreign_budget_is_rejected()
    {
        await using (var seed = _qa.Create(_qa.Tenant(), _time))
        {
            var draft = new Budget(_qa.ConsultorioId, _qa.PatientId, 2, null, new DateOnly(2026, 11, 1), new DateOnly(2026, 12, 31), 0, null);
            seed.Budgets.Add(draft);
            await seed.SaveChangesAsync();
            var (svc, db) = Service();
            await using var _ = db;
            Assert.Equal("budget_not_approved", (await Assert.ThrowsAsync<BusinessRuleException>(() => svc.RegisterAsync(Pay(10, draft.Id), CancellationToken.None))).Code);
            await Assert.ThrowsAsync<RequestValidationException>(() => svc.RegisterAsync(Pay(10, Guid.NewGuid()), CancellationToken.None));
        }
    }

    [Fact]
    public async Task Statement_running_balance_and_totals()
    {
        await ApprovedBudgetAsync(250.75m);
        var (svc, db) = Service();
        await using var _ = db;
        await svc.RegisterAsync(Pay(100.25m), CancellationToken.None);
        var st = await svc.GetStatementAsync(_qa.PatientId, CancellationToken.None);
        Assert.Equal(250.75m, st.TotalCharges);
        Assert.Equal(100.25m, st.TotalCredits);
        Assert.Equal(150.50m, st.Balance);
        Assert.Equal([250.75m, 150.50m], st.Movements.Select(m => m.RunningBalance));
    }

    [Fact] // QA-01 (corregido): la descripción del contraasiento se ajusta a 250; el motivo completo queda en Payment.VoidReason.
    public async Task Void_with_long_but_valid_reason_succeeds()
    {
        var (svc, db) = Service();
        await using var _ = db;
        var p = await svc.RegisterAsync(Pay(10m), CancellationToken.None);
        var reason = new string('m', 300);
        Assert.True(new Odontia.Application.Validation.VoidPaymentRequestValidator().Validate(new VoidPaymentRequest(reason)).IsValid);
        var voided = await svc.VoidAsync(p.Id, new VoidPaymentRequest(reason), CancellationToken.None);
        Assert.Equal(PaymentStatus.Voided, voided.Status);
    }
}
