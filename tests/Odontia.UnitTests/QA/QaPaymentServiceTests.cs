using Microsoft.EntityFrameworkCore;
using Odontia.Application.Common;
using Odontia.Application.Features.Finance;
using Odontia.Domain.Authorization;
using Odontia.Domain.Common;
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

    // ---------------- Cargos manuales ----------------

    private static RegisterAccountChargeRequest Charge(decimal amount, DateTimeOffset? at = null) => new("Consulta de urgencia", amount, at);

    [Fact]
    public async Task Manual_charge_raises_balance_and_payment_reduces_it()
    {
        var (svc, db) = Service();
        await using var _ = db;
        var st = await svc.RegisterChargeAsync(_qa.PatientId, Charge(800m), CancellationToken.None);
        Assert.Equal(800m, st.Balance);
        var row = Assert.Single(st.Movements);
        Assert.Equal(MovementType.ManualCharge, row.Type);
        Assert.Equal(MovementDirection.Debit, row.Direction);
        Assert.Null(row.ReversedByMovementId);
        Assert.Empty(st.Budgets);

        await svc.RegisterAsync(Pay(300m), CancellationToken.None);
        st = await svc.GetStatementAsync(_qa.PatientId, CancellationToken.None);
        Assert.Equal(500m, st.Balance);
        Assert.Equal(800m, st.TotalCharges);
        Assert.Equal(300m, st.TotalCredits);
    }

    [Fact]
    public async Task Voiding_manual_charge_creates_linked_credit_and_cannot_repeat()
    {
        var (svc, db) = Service();
        await using var _ = db;
        var chargeId = (await svc.RegisterChargeAsync(_qa.PatientId, Charge(800m), CancellationToken.None)).Movements.Single().Id;

        var st = await svc.VoidChargeAsync(_qa.PatientId, chargeId, new VoidAccountChargeRequest("Cargado por error"), CancellationToken.None);
        Assert.Equal(0m, st.Balance);
        var reversal = st.Movements.Single(m => m.Type == MovementType.ManualChargeReversal);
        Assert.Equal(MovementDirection.Credit, reversal.Direction);
        Assert.Equal(800m, reversal.Amount);
        Assert.Equal(chargeId, reversal.ReversesMovementId);
        Assert.Equal(reversal.Id, st.Movements.Single(m => m.Id == chargeId).ReversedByMovementId);
        Assert.Contains("Cargado por error", reversal.Description);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            svc.VoidChargeAsync(_qa.PatientId, chargeId, new VoidAccountChargeRequest("x"), CancellationToken.None));
        Assert.Equal("charge_already_voided", ex.Code);
        Assert.Equal(1, await db.AccountMovements.CountAsync(m => m.Type == MovementType.ManualChargeReversal));
    }

    [Fact]
    public async Task Only_manual_charges_of_the_patient_can_be_voided()
    {
        var (svc, db) = Service();
        await using var _ = db;
        await svc.RegisterAsync(Pay(10m), CancellationToken.None);
        var paymentMovement = (await db.AccountMovements.SingleAsync()).Id;
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            svc.VoidChargeAsync(_qa.PatientId, paymentMovement, new VoidAccountChargeRequest("x"), CancellationToken.None));
        Assert.Equal("movement_not_manual_charge", ex.Code);

        var chargeId = (await svc.RegisterChargeAsync(_qa.PatientId, Charge(5m), CancellationToken.None)).Movements.Single(m => m.Type == MovementType.ManualCharge).Id;
        await Assert.ThrowsAsync<NotFoundException>(() =>
            svc.VoidChargeAsync(Guid.NewGuid(), chargeId, new VoidAccountChargeRequest("x"), CancellationToken.None));
    }

    [Fact]
    public async Task Manual_charge_validates_date_patient_and_permissions()
    {
        var (svc, db) = Service();
        await using var _ = db;
        var now = _time.GetUtcNow();
        await svc.RegisterChargeAsync(_qa.PatientId, Charge(10m, now.AddMinutes(5)), CancellationToken.None);
        await Assert.ThrowsAsync<RequestValidationException>(() => svc.RegisterChargeAsync(_qa.PatientId, Charge(10m, now.AddMinutes(6)), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => svc.RegisterChargeAsync(Guid.NewGuid(), Charge(10m), CancellationToken.None));

        var (readOnly, db2) = Service(Permissions.FinanceRead);
        await using var __ = db2;
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => readOnly.RegisterChargeAsync(_qa.PatientId, Charge(10m), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            readOnly.VoidChargeAsync(_qa.PatientId, Guid.NewGuid(), new VoidAccountChargeRequest("x"), CancellationToken.None));
    }

    [Fact]
    public void Charge_validators_enforce_amount_and_description_limits()
    {
        var v = new Odontia.Application.Validation.RegisterAccountChargeRequestValidator();
        Assert.True(v.Validate(new RegisterAccountChargeRequest("Consulta", 0.01m, null)).IsValid);
        Assert.False(v.Validate(new RegisterAccountChargeRequest("Consulta", 0m, null)).IsValid);
        Assert.False(v.Validate(new RegisterAccountChargeRequest("Consulta", 1.005m, null)).IsValid);
        Assert.False(v.Validate(new RegisterAccountChargeRequest("", 10m, null)).IsValid);
        Assert.True(v.Validate(new RegisterAccountChargeRequest(new string('x', AccountMovement.DescriptionMaxLength), 10m, null)).IsValid);
        Assert.False(v.Validate(new RegisterAccountChargeRequest(new string('x', AccountMovement.DescriptionMaxLength + 1), 10m, null)).IsValid);
        var vv = new Odontia.Application.Validation.VoidAccountChargeRequestValidator();
        Assert.False(vv.Validate(new VoidAccountChargeRequest("")).IsValid);
        Assert.False(vv.Validate(new VoidAccountChargeRequest(new string('m', 501))).IsValid);
    }

    [Fact]
    public void Only_manual_charge_movements_can_be_reversed_in_domain()
    {
        var at = _time.GetUtcNow().UtcDateTime;
        var charge = AccountMovement.ManualCharge(_qa.ConsultorioId, _qa.PatientId, 50m, at, "Consulta");
        var reversal = charge.ReverseManualCharge(new string('m', 500), at);
        Assert.Equal(-50m, reversal.SignedAmount);
        Assert.Equal(charge.Id, reversal.ReversesMovementId);
        Assert.True(reversal.Description.Length <= AccountMovement.DescriptionMaxLength);
        var payment = new AccountMovement(_qa.ConsultorioId, _qa.PatientId, MovementType.Payment, MovementDirection.Credit, 50m, at, "Pago", null, null, null);
        Assert.Equal("movement_not_manual_charge", Assert.Throws<DomainException>(() => payment.ReverseManualCharge("x", at)).Code);
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
