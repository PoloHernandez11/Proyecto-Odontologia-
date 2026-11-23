using Odontia.Domain.Common;
using Odontia.Domain.Entities;
using Odontia.Domain.Enums;

namespace Odontia.UnitTests.QA;

public class QaBudgetTests
{
    private static readonly DateTime Now = new(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 11, 19);
    private static readonly Guid U = Guid.NewGuid();

    private static Budget New(decimal discount = 0, DateOnly? exp = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), 7, null, Today, exp ?? Today.AddDays(30), discount, null);

    [Theory]
    [InlineData("-0.01")]
    [InlineData("100.01")]
    [InlineData("10.555")]
    public void Invalid_global_discount_is_rejected(string raw)
    {
        var ex = Assert.Throws<DomainException>(() => New(decimal.Parse(raw, System.Globalization.CultureInfo.InvariantCulture)));
        Assert.Equal("invalid_discount", ex.Code);
    }

    [Fact]
    public void Full_discount_gives_zero_total()
    {
        var b = New(100);
        b.AddItem(null, "Limpieza", null, 2, 1500m, 0, null);
        Assert.Equal(3000m, b.Subtotal);
        Assert.Equal(3000m, b.DiscountAmount);
        Assert.Equal(0m, b.Total);
    }

    [Fact]
    public void Line_and_global_discount_rounding_is_away_from_zero()
    {
        var b = New(10);
        // 3 x 33.35 = 100.05; 15% => 15.0075 => 15.01; línea = 85.04
        var line = b.AddItem(null, "Resina", 16, 3, 33.35m, 15, null);
        Assert.Equal(85.04m, line.LineTotal);
        // 10% de 85.04 = 8.504 => 8.50
        Assert.Equal(8.50m, b.DiscountAmount);
        Assert.Equal(76.54m, b.Total);
        // 0.05 x 10% = 0.005 => 0.01 (alejándose de cero)
        var small = New(10);
        small.AddItem(null, "Item", null, 1, 0.05m, 0, null);
        Assert.Equal(0.01m, small.DiscountAmount);
        Assert.Equal(0.04m, small.Total);
    }

    [Fact]
    public void Totals_are_recalculated_when_items_removed_or_discount_changes()
    {
        var b = New();
        var a = b.AddItem(null, "A", null, 1, 100m, 0, null);
        b.AddItem(null, "B", null, 1, 50m, 0, null);
        Assert.Equal(150m, b.Total);
        b.RemoveItem(a.Id);
        Assert.Equal(50m, b.Total);
        b.SetDiscount(50);
        Assert.Equal(25m, b.Total);
        Assert.Equal("item_not_found", Assert.Throws<DomainException>(() => b.RemoveItem(Guid.NewGuid())).Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1001)]
    [InlineData(-1)]
    public void Invalid_quantity_is_rejected(int qty)
    {
        Assert.Equal("invalid_quantity", Assert.Throws<DomainException>(() => New().AddItem(null, "X", null, qty, 10m, 0, null)).Code);
    }

    [Fact]
    public void Item_validates_tooth_description_and_discount()
    {
        var b = New();
        Assert.Equal("invalid_tooth", Assert.Throws<DomainException>(() => b.AddItem(null, "X", 99, 1, 10m, 0, null)).Code);
        Assert.Equal("required", Assert.Throws<DomainException>(() => b.AddItem(null, " ", null, 1, 10m, 0, null)).Code);
        Assert.Equal("invalid_discount", Assert.Throws<DomainException>(() => b.AddItem(null, "X", null, 1, 10m, 101, null)).Code);
        Assert.Equal("invalid_amount", Assert.Throws<DomainException>(() => b.AddItem(null, "X", null, 1, -10m, 0, null)).Code);
        Assert.Empty(b.Items);
        Assert.Equal(0m, b.Total);
    }

    [Fact]
    public void Item_sort_order_is_sequential()
    {
        var b = New();
        Assert.Equal(1, b.AddItem(null, "A", null, 1, 1m, 0, null).SortOrder);
        Assert.Equal(2, b.AddItem(null, "B", null, 1, 1m, 0, null).SortOrder);
    }

    [Fact]
    public void Expiration_before_issue_is_rejected_same_day_allowed()
    {
        Assert.Equal("invalid_range", Assert.Throws<DomainException>(() => New(exp: Today.AddDays(-1))).Code);
        Assert.Equal(Today, New(exp: Today).ExpirationDate);
    }

    private static Budget Issued(DateOnly? exp = null)
    {
        var b = New(exp: exp);
        b.AddItem(null, "A", null, 1, 100m, 0, null);
        b.Issue(U, Now);
        return b;
    }

    [Fact]
    public void Issued_budget_is_not_editable()
    {
        var b = Issued();
        Assert.False(b.IsEditable);
        Assert.Equal("budget_not_editable", Assert.Throws<DomainException>(() => b.AddItem(null, "B", null, 1, 1m, 0, null)).Code);
        Assert.Equal("budget_not_editable", Assert.Throws<DomainException>(() => b.SetDiscount(5)).Code);
        Assert.Equal("budget_not_editable", Assert.Throws<DomainException>(() => b.SetNotes("x")).Code);
        Assert.Equal("budget_not_editable", Assert.Throws<DomainException>(() => b.SetDates(Today, Today)).Code);
        Assert.Equal("budget_not_editable", Assert.Throws<DomainException>(() => b.Issue(U, Now)).Code);
    }

    [Fact]
    public void Approval_on_expiration_day_is_allowed()
    {
        var b = Issued(exp: Today);
        b.Approve(U, Now, Today);
        Assert.Equal(BudgetStatus.Approved, b.Status);
        Assert.Equal(U, b.ApprovedByUserId);
    }

    [Fact]
    public void Only_issued_can_be_approved_or_rejected()
    {
        var draft = New();
        Assert.Equal("invalid_status_transition", Assert.Throws<DomainException>(() => draft.Approve(U, Now, Today)).Code);
        Assert.Equal("invalid_status_transition", Assert.Throws<DomainException>(() => draft.Reject(null, U, Now)).Code);
        var approved = Issued();
        approved.Approve(U, Now, Today);
        Assert.Throws<DomainException>(() => approved.Approve(U, Now, Today));
        Assert.Throws<DomainException>(() => approved.Reject("x", U, Now));
    }

    [Fact]
    public void Reject_records_reason_and_history()
    {
        var b = Issued();
        b.Reject("  Muy caro ", U, Now);
        Assert.Equal(BudgetStatus.Rejected, b.Status);
        Assert.Equal("Muy caro", b.StatusReason);
        Assert.Equal(Now, b.RejectedAtUtc);
        Assert.Equal([BudgetStatus.Issued, BudgetStatus.Rejected], b.StatusChanges.Select(c => c.ToStatus));
    }

    [Theory]
    [InlineData(BudgetStatus.Draft)]
    [InlineData(BudgetStatus.Issued)]
    [InlineData(BudgetStatus.Approved)]
    public void Open_budgets_can_be_cancelled_with_reason(BudgetStatus s)
    {
        var b = New();
        b.AddItem(null, "A", null, 1, 100m, 0, null);
        if (s >= BudgetStatus.Issued) b.Issue(U, Now);
        if (s == BudgetStatus.Approved) b.Approve(U, Now, Today);
        Assert.Equal("required", Assert.Throws<DomainException>(() => b.Cancel("", U, Now)).Code);
        Assert.Equal(s, b.Status);
        b.Cancel("Error de carga", U, Now);
        Assert.Equal(BudgetStatus.Cancelled, b.Status);
        Assert.Equal(s, b.StatusChanges.Last().FromStatus);
    }

    [Fact]
    public void Closed_budgets_cannot_be_cancelled()
    {
        var rejected = Issued();
        rejected.Reject(null, U, Now);
        Assert.Throws<DomainException>(() => rejected.Cancel("x", U, Now));
        var expired = Issued(exp: Today);
        Assert.True(expired.MarkExpiredIfDue(Today.AddDays(1), Now));
        Assert.Throws<DomainException>(() => expired.Cancel("x", U, Now));
        var cancelled = Issued();
        cancelled.Cancel("x", U, Now);
        Assert.Throws<DomainException>(() => cancelled.Cancel("x", U, Now));
    }

    [Fact]
    public void Mark_expired_only_applies_to_issued_past_due()
    {
        var b = Issued(exp: Today);
        Assert.False(b.MarkExpiredIfDue(Today, Now)); // vence hoy: aún vigente
        Assert.True(b.MarkExpiredIfDue(Today.AddDays(1), Now));
        Assert.Equal(BudgetStatus.Expired, b.Status);
        Assert.Null(b.StatusChanges.Last().ChangedByUserId);
        Assert.False(b.MarkExpiredIfDue(Today.AddDays(2), Now)); // idempotente

        var draft = New(exp: Today);
        Assert.False(draft.MarkExpiredIfDue(Today.AddDays(5), Now));
        Assert.Equal(BudgetStatus.Draft, draft.Status);
    }

    [Fact]
    public void Expired_budget_cannot_be_approved()
    {
        var b = Issued(exp: Today);
        Assert.Equal("budget_expired", Assert.Throws<DomainException>(() => b.Approve(U, Now, Today.AddDays(1))).Code);
        Assert.Equal(BudgetStatus.Issued, b.Status);
    }
}

public class QaPaymentAndAccountTests
{
    private static readonly Guid C = Guid.NewGuid();
    private static readonly Guid P = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);

    private static AccountMovement M(MovementType t, MovementDirection d, decimal amount) =>
        new(C, P, t, d, amount, Now, "mov", null, null, null);

    [Fact]
    public void Signed_amount_depends_on_direction()
    {
        Assert.Equal(10m, M(MovementType.BudgetCharge, MovementDirection.Debit, 10m).SignedAmount);
        Assert.Equal(-10m, M(MovementType.Payment, MovementDirection.Credit, 10m).SignedAmount);
    }

    [Fact]
    public void Balance_of_empty_ledger_is_zero_and_credit_balance_is_negative()
    {
        Assert.Equal(0m, AccountCalculator.Balance([]));
        Assert.Equal(-50.5m, AccountCalculator.Balance([
            M(MovementType.BudgetCharge, MovementDirection.Debit, 100m),
            M(MovementType.Payment, MovementDirection.Credit, 150.5m)]));
    }

    [Fact]
    public void Cent_precision_is_preserved_in_balance()
    {
        var moves = Enumerable.Range(0, 10).Select(_ => M(MovementType.Payment, MovementDirection.Credit, 0.1m))
            .Append(M(MovementType.BudgetCharge, MovementDirection.Debit, 1m));
        Assert.Equal(0m, AccountCalculator.Balance(moves));
    }

    [Fact]
    public void Movement_validates_amount_and_description()
    {
        Assert.Throws<DomainException>(() => M(MovementType.Adjustment, MovementDirection.Debit, 0m));
        Assert.Throws<DomainException>(() => new AccountMovement(C, P, MovementType.Adjustment, MovementDirection.Debit, 1m, Now, " ", null, null, null));
        Assert.Throws<DomainException>(() => new AccountMovement(C, P, MovementType.Adjustment, MovementDirection.Debit, 1m, Now, new string('d', 251), null, null, null));
    }

    private static Budget ApprovedBudget(decimal price)
    {
        var b = new Budget(C, P, 1, null, new DateOnly(2026, 11, 1), new DateOnly(2026, 12, 31), 0, null);
        b.AddItem(null, "A", null, 1, price, 0, null);
        b.Issue(Guid.NewGuid(), Now);
        b.Approve(Guid.NewGuid(), Now, new DateOnly(2026, 11, 19));
        return b;
    }

    private static Payment Pay(Guid? budgetId, decimal amount) =>
        new(C, P, budgetId, amount, PaymentMethod.Cash, Now, null, null, Guid.NewGuid(), 1);

    [Fact]
    public void Outstanding_is_zero_for_non_approved_budget()
    {
        var b = new Budget(C, P, 1, null, new DateOnly(2026, 11, 1), new DateOnly(2026, 12, 31), 0, null);
        b.AddItem(null, "A", null, 1, 100m, 0, null);
        Assert.Equal(0m, AccountCalculator.BudgetOutstanding(b, []));
    }

    [Fact]
    public void Outstanding_ignores_payments_of_other_budgets_and_unassigned()
    {
        var b = ApprovedBudget(1000m);
        var outstanding = AccountCalculator.BudgetOutstanding(b, [Pay(b.Id, 300m), Pay(Guid.NewGuid(), 500m), Pay(null, 200m)]);
        Assert.Equal(700m, outstanding);
    }

    [Fact]
    public void Payment_fields_are_normalized()
    {
        var p = new Payment(C, P, null, 10m, PaymentMethod.BankTransfer, DateTime.SpecifyKind(Now, DateTimeKind.Unspecified), "  TRX-1 ", " ", Guid.NewGuid(), 42);
        Assert.Equal("TRX-1", p.Reference);
        Assert.Null(p.Notes);
        Assert.Equal(DateTimeKind.Utc, p.PaidAtUtc.Kind);
        Assert.Equal(42, p.ReceiptNumber);
        Assert.Equal(PaymentStatus.Registered, p.Status);
    }

    [Fact]
    public void Payment_void_requires_reason_and_keeps_status_when_invalid()
    {
        var p = Pay(null, 10m);
        Assert.Throws<DomainException>(() => p.Void(" ", Guid.NewGuid(), Now));
        Assert.Equal(PaymentStatus.Registered, p.Status);
        Assert.Null(p.VoidedAtUtc);
        Assert.Throws<DomainException>(() => p.Void(new string('r', 501), Guid.NewGuid(), Now));
        p.Void(new string('r', 500), Guid.NewGuid(), Now);
        Assert.Equal(PaymentStatus.Voided, p.Status);
    }
}
