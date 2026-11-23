using Microsoft.EntityFrameworkCore;
using Odontia.Application.Abstractions;
using Odontia.Application.Common;
using Odontia.Application.Features.Finance;
using Odontia.Domain.Entities;
using Odontia.Domain.Enums;
using Odontia.Infrastructure.Persistence;
using Odontia.UnitTests.QA;

namespace Odontia.UnitTests.Application;

/// <summary>
/// Carreras de QA API-01/02/03 reproducidas de forma determinista sobre SQLite: la solicitud "A" lee el estado,
/// y antes de que escriba se completa una solicitud "B" concurrente. A debe recibir 409 en lugar de duplicar
/// el efecto. (Las pruebas con solicitudes HTTP en paralelo contra MySQL están en Odontia.IntegrationTests.)
/// </summary>
public sealed class FinanceConcurrencyTests : IDisposable
{
    private readonly QaSqliteDb _qa = new();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 11, 19, 15, 0, 0, TimeSpan.Zero));
    private readonly CountingSequence _seq = new();

    public void Dispose() => _qa.Dispose();

    private OdontiaDbContext Db(ITenantContext tenant) => _qa.Create(tenant, _time);

    /// <summary>
    /// Servicio de la solicitud "A". Si se indica <paramref name="concurrent"/>, la solicitud "B" se ejecuta completa
    /// justo después de que A cargó la entidad (primera lectura del reloj, previa a cualquier escritura de A).
    /// </summary>
    private BudgetService Budgets(OdontiaDbContext db, ITenantContext t, Func<Task>? concurrent = null) =>
        new(db, t, Clock(db, t, concurrent), _seq, new NullAudit());

    private PaymentService Payments(OdontiaDbContext db, ITenantContext t, ISequenceGenerator? seq = null, Func<Task>? concurrent = null) =>
        new(db, t, Clock(db, t, concurrent), seq ?? _seq, new NullAudit());

    private IConsultorioClock Clock(OdontiaDbContext db, ITenantContext t, Func<Task>? concurrent)
    {
        var clock = new ConsultorioClock(db, t, _time);
        return concurrent is null ? clock : new InterleavingClock(clock, concurrent);
    }

    private async Task<Guid> BudgetAsync(decimal total, bool approve)
    {
        await using var db = Db(_qa.Tenant());
        var now = _time.GetUtcNow().UtcDateTime;
        var b = new Budget(_qa.ConsultorioId, _qa.PatientId, 1, null, new DateOnly(2026, 11, 1), new DateOnly(2026, 12, 31), 0, null);
        b.AddItem(null, "Corona", 16, 1, total, 0, null);
        b.Issue(_qa.UserId, now);
        if (approve) b.Approve(_qa.UserId, now, new DateOnly(2026, 11, 19));
        db.Budgets.Add(b);
        if (approve)
            db.AccountMovements.Add(new AccountMovement(_qa.ConsultorioId, _qa.PatientId, MovementType.BudgetCharge, MovementDirection.Debit, b.Total,
                now, "Presupuesto #1 aprobado", b.Id, null, null));
        await db.SaveChangesAsync();
        return b.Id;
    }

    private RegisterPaymentRequest Pay(decimal amount, Guid? budget) => new(_qa.PatientId, budget, amount, PaymentMethod.Cash, null, null, null);

    private async Task<List<AccountMovement>> MovementsAsync()
    {
        await using var db = Db(_qa.Tenant());
        return await db.AccountMovements.AsNoTracking().ToListAsync();
    }

    // ---- API-01: pagos simultáneos sobre el mismo presupuesto ----

    [Fact]
    public async Task Payment_validated_against_a_stale_budget_balance_is_rejected_with_conflict()
    {
        var budgetId = await BudgetAsync(1000m, approve: true);
        var t = _qa.Tenant();
        await using var db = Db(t);
        // Entre el control de saldo y el guardado, otra solicitud imputó un pago al mismo presupuesto (cambió su versión).
        var seq = new HookedSequence(_seq, () => db.Budgets.Where(b => b.Id == budgetId)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.Version, Guid.NewGuid())));

        var ex = await Assert.ThrowsAsync<ConflictException>(() => Payments(db, t, seq).RegisterAsync(Pay(600m, budgetId), CancellationToken.None));
        Assert.Equal("concurrency_conflict", ex.Code);

        await using var check = Db(_qa.Tenant());
        Assert.Empty(await check.Payments.ToListAsync());
        Assert.DoesNotContain(await MovementsAsync(), m => m.Type == MovementType.Payment);
    }

    [Fact]
    public async Task Sequential_payments_still_enforce_outstanding_and_bump_the_budget_version()
    {
        var budgetId = await BudgetAsync(1000m, approve: true);
        var t = _qa.Tenant();
        Guid before;
        await using (var db = Db(t)) before = (await db.Budgets.SingleAsync(b => b.Id == budgetId)).Version;

        await using (var db = Db(t)) await Payments(db, t).RegisterAsync(Pay(600m, budgetId), CancellationToken.None);
        await using (var db = Db(t))
        {
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => Payments(db, t).RegisterAsync(Pay(600m, budgetId), CancellationToken.None));
            Assert.Equal("amount_exceeds_outstanding", ex.Code);
            Assert.NotEqual(before, (await db.Budgets.AsNoTracking().SingleAsync(b => b.Id == budgetId)).Version);
        }
        await using (var db = Db(t)) await Payments(db, t).RegisterAsync(Pay(400m, budgetId), CancellationToken.None);
    }

    [Fact]
    public async Task Payment_concurrent_with_budget_cancellation_is_rejected()
    {
        var budgetId = await BudgetAsync(1000m, approve: true);
        var t = _qa.Tenant();
        await using var db = Db(t);
        // La cancelación (que también cambia la versión) se confirma entre el control de saldo y el guardado del pago.
        var seq = new HookedSequence(_seq, () => db.Budgets.Where(b => b.Id == budgetId)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.Version, Guid.NewGuid()).SetProperty(b => b.Status, BudgetStatus.Cancelled)));

        await Assert.ThrowsAsync<ConflictException>(() => Payments(db, t, seq).RegisterAsync(Pay(100m, budgetId), CancellationToken.None));
    }

    // ---- API-02: anulaciones simultáneas del mismo pago ----

    [Fact]
    public async Task Concurrent_void_of_the_same_payment_yields_one_reversal_and_a_conflict()
    {
        var t = _qa.Tenant();
        Guid paymentId;
        await using (var db = Db(t)) paymentId = (await Payments(db, t).RegisterAsync(Pay(100m, null), CancellationToken.None)).Id;

        await using var dbA = Db(t);
        var a = Payments(dbA, t, concurrent: async () =>
        {
            await using var dbB = Db(t);
            await Payments(dbB, t).VoidAsync(paymentId, new VoidPaymentRequest("Error B"), CancellationToken.None);
        });
        var ex = await Assert.ThrowsAsync<ConflictException>(() => a.VoidAsync(paymentId, new VoidPaymentRequest("Error A"), CancellationToken.None));
        Assert.Equal("concurrency_conflict", ex.Code);

        var reversal = Assert.Single(await MovementsAsync(), m => m.Type == MovementType.PaymentReversal);
        Assert.Contains("Error B", reversal.Description);
    }

    [Fact]
    public async Task Database_rejects_a_second_reversal_of_the_same_payment()
    {
        var t = _qa.Tenant();
        Guid paymentId;
        await using (var db = Db(t))
        {
            paymentId = (await Payments(db, t).RegisterAsync(Pay(100m, null), CancellationToken.None)).Id;
            await Payments(db, t).VoidAsync(paymentId, new VoidPaymentRequest("Error"), CancellationToken.None);
        }
        // Defensa en profundidad: aunque se saltee el dominio, los índices únicos impiden el contraasiento duplicado.
        await using var raw = Db(t);
        var original = await raw.AccountMovements.SingleAsync(m => m.PaymentId == paymentId && m.Type == MovementType.Payment);
        raw.AccountMovements.Add(new AccountMovement(_qa.ConsultorioId, _qa.PatientId, MovementType.PaymentReversal, MovementDirection.Debit, 100m,
            _time.GetUtcNow().UtcDateTime, "Duplicado", null, paymentId, original.Id));
        await Assert.ThrowsAsync<DbUpdateException>(() => raw.SaveChangesAsync());
    }

    [Fact]
    public async Task A_budget_keeps_one_charge_one_reversal_and_several_payments()
    {
        // Los pagos repiten (BudgetId, Type): por eso no hay índice único por presupuesto.
        var budgetId = await BudgetAsync(1000m, approve: true);
        var t = _qa.Tenant();
        await using (var db = Db(t)) await Payments(db, t).RegisterAsync(Pay(100m, budgetId), CancellationToken.None);
        await using (var db = Db(t)) await Payments(db, t).RegisterAsync(Pay(200m, budgetId), CancellationToken.None);
        await using (var db = Db(t)) await Budgets(db, t).CancelAsync(budgetId, new BudgetDecisionRequest("Desiste"), CancellationToken.None);

        var movements = await MovementsAsync();
        Assert.Equal(2, movements.Count(m => m.BudgetId == budgetId && m.Type == MovementType.Payment));
        Assert.Single(movements, m => m.Type == MovementType.BudgetCharge);
        Assert.Single(movements, m => m.Type == MovementType.BudgetChargeReversal);
    }

    // ---- API-03: decisiones simultáneas sobre el mismo presupuesto ----

    public static TheoryData<string, string> Decisions => new()
    {
        { "approve", "approve" }, { "approve", "reject" }, { "reject", "cancel" }, { "cancel", "approve" }, { "cancel", "cancel" },
    };

    [Theory]
    [MemberData(nameof(Decisions))]
    public async Task Concurrent_decisions_on_an_issued_budget_apply_only_one(string first, string second)
    {
        var budgetId = await BudgetAsync(1000m, approve: false);
        var t = _qa.Tenant();

        await using var dbA = Db(t);
        var a = Budgets(dbA, t, concurrent: async () =>
        {
            await using var dbB = Db(t);
            await DecideAsync(Budgets(dbB, t), first, budgetId);
        });
        var ex = await Assert.ThrowsAsync<ConflictException>(() => DecideAsync(a, second, budgetId));
        Assert.Equal("concurrency_conflict", ex.Code);

        await using var check = Db(_qa.Tenant());
        Assert.Equal(2, await check.BudgetStatusChanges.CountAsync(c => c.BudgetId == budgetId)); // emisión + una decisión
        var status = (await check.Budgets.AsNoTracking().SingleAsync(b => b.Id == budgetId)).Status;
        Assert.Equal(first switch { "approve" => BudgetStatus.Approved, "reject" => BudgetStatus.Rejected, _ => BudgetStatus.Cancelled }, status);
        Assert.Equal(status == BudgetStatus.Approved ? 1 : 0, (await MovementsAsync()).Count(m => m.Type == MovementType.BudgetCharge));
    }

    [Fact]
    public async Task Concurrent_cancellations_of_an_approved_budget_create_one_reversal()
    {
        var budgetId = await BudgetAsync(1000m, approve: true);
        var t = _qa.Tenant();

        await using var dbA = Db(t);
        var a = Budgets(dbA, t, concurrent: async () =>
        {
            await using var dbB = Db(t);
            await Budgets(dbB, t).CancelAsync(budgetId, new BudgetDecisionRequest("B"), CancellationToken.None);
        });
        var ex = await Assert.ThrowsAsync<ConflictException>(() => a.CancelAsync(budgetId, new BudgetDecisionRequest("A"), CancellationToken.None));
        Assert.Equal("concurrency_conflict", ex.Code);

        Assert.Single(await MovementsAsync(), m => m.Type == MovementType.BudgetChargeReversal);
    }

    // ---- QA-01 ----

    [Fact]
    public async Task Void_reason_longer_than_the_movement_description_is_kept_in_full_on_the_payment()
    {
        var t = _qa.Tenant();
        await using var db = Db(t);
        var p = await Payments(db, t).RegisterAsync(Pay(10m, null), CancellationToken.None);
        var reason = new string('x', 500);

        var voided = await Payments(db, t).VoidAsync(p.Id, new VoidPaymentRequest(reason), CancellationToken.None);

        Assert.Equal(reason, voided.VoidReason);
        var reversal = Assert.Single(await MovementsAsync(), m => m.Type == MovementType.PaymentReversal);
        Assert.Equal(AccountMovement.DescriptionMaxLength, reversal.Description.Length);
        Assert.StartsWith($"Anulación recibo #{p.ReceiptNumber}: xxx", reversal.Description);
        Assert.EndsWith("…", reversal.Description);
    }

    private static Task DecideAsync(BudgetService svc, string decision, Guid id) => decision switch
    {
        "approve" => svc.ApproveAsync(id, CancellationToken.None),
        "reject" => svc.RejectAsync(id, new BudgetDecisionRequest("No acepta"), CancellationToken.None),
        _ => svc.CancelAsync(id, new BudgetDecisionRequest("Desiste"), CancellationToken.None),
    };

    /// <summary>
    /// Ejecuta la solicitud concurrente la primera vez que el servicio consulta la hora, lo que en estas operaciones
    /// ocurre después de cargar la entidad y antes de escribir. Microsoft.Data.Sqlite es síncrono por debajo,
    /// por lo que esperar aquí no bloquea.
    /// </summary>
    private sealed class InterleavingClock(IConsultorioClock inner, Func<Task> concurrent) : IConsultorioClock
    {
        private Func<Task>? _pending = concurrent;

        public DateTime UtcNow
        {
            get
            {
                if (Interlocked.Exchange(ref _pending, null) is { } run) run().GetAwaiter().GetResult();
                return inner.UtcNow;
            }
        }

        public Task<TimeZoneInfo> GetTimeZoneAsync(CancellationToken ct) => inner.GetTimeZoneAsync(ct);
        public Task<DateOnly> TodayAsync(CancellationToken ct) => inner.TodayAsync(ct);
    }

    /// <summary>Simula otra transacción que modifica el presupuesto entre el control de saldo y el guardado.</summary>
    private sealed class HookedSequence(ISequenceGenerator inner, Func<Task<int>> hook) : ISequenceGenerator
    {
        public async Task<int> NextAsync(string name, CancellationToken ct)
        {
            await hook();
            return await inner.NextAsync(name, ct);
        }
    }
}
