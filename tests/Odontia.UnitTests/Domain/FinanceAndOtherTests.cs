using Odontia.Domain.Common;
using Odontia.Domain.Entities;
using Odontia.Domain.Enums;

namespace Odontia.UnitTests.Domain;

public class BudgetTests
{
    private static readonly Guid C = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 10, 19);
    private static Budget New(decimal discount = 0) => new(C, Guid.NewGuid(), 1, null, Today, Today.AddDays(30), discount, null);

    [Fact]
    public void Totals_apply_line_and_global_discounts_with_rounding()
    {
        var b = New(10);
        b.AddItem(null, "Endodoncia", 36, 1, 150_000m, 0, null);
        b.AddItem(null, "Obturación", 16, 2, 33_333.33m, 5, null); // 66666.66 - 3333.33 = 63333.33
        Assert.Equal(213_333.33m, b.Subtotal);
        Assert.Equal(21_333.33m, b.DiscountAmount);
        Assert.Equal(192_000.00m, b.Total);
    }

    [Fact]
    public void Lifecycle_draft_issue_approve()
    {
        var b = New();
        b.AddItem(null, "Limpieza", null, 1, 20_000m, 0, null);
        var u = Guid.NewGuid();
        b.Issue(u, DateTime.UtcNow);
        Assert.Throws<DomainException>(() => b.AddItem(null, "Otro", null, 1, 1, 0, null));
        b.Approve(u, DateTime.UtcNow, Today);
        Assert.Equal(BudgetStatus.Approved, b.Status);
        Assert.Equal(2, b.StatusChanges.Count);
    }

    [Fact]
    public void Empty_budget_cannot_be_issued_and_expired_cannot_be_approved()
    {
        var u = Guid.NewGuid();
        Assert.Equal("budget_empty", Assert.Throws<DomainException>(() => New().Issue(u, DateTime.UtcNow)).Code);
        var b = New();
        b.AddItem(null, "X", null, 1, 10, 0, null);
        b.Issue(u, DateTime.UtcNow);
        Assert.Equal("budget_expired", Assert.Throws<DomainException>(() => b.Approve(u, DateTime.UtcNow, Today.AddDays(31))).Code);
        Assert.True(b.MarkExpiredIfDue(Today.AddDays(31), DateTime.UtcNow));
        Assert.Equal(BudgetStatus.Expired, b.Status);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100.001)]
    [InlineData(101)]
    public void Invalid_discount_is_rejected(double discount)
    {
        Assert.Throws<DomainException>(() => New((decimal)discount));
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, -5)]
    [InlineData(1001, 10)]
    public void Invalid_lines_are_rejected(int qty, double price)
    {
        Assert.Throws<DomainException>(() => New().AddItem(null, "X", null, qty, (decimal)price, 0, null));
    }
}

public class PaymentAndAccountTests
{
    private static readonly Guid C = Guid.NewGuid();

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(10.005)]
    [InlineData(1_000_000_000)]
    public void Invalid_amounts_are_rejected(double amount)
    {
        Assert.Throws<DomainException>(() => new Payment(C, Guid.NewGuid(), null, (decimal)amount, PaymentMethod.Cash, DateTime.UtcNow, null, null, Guid.NewGuid(), 1));
    }

    [Fact]
    public void Void_is_controlled_and_only_once()
    {
        var p = new Payment(C, Guid.NewGuid(), null, 1000, PaymentMethod.Cash, DateTime.UtcNow, null, null, Guid.NewGuid(), 1);
        Assert.Throws<DomainException>(() => p.Void(" ", Guid.NewGuid(), DateTime.UtcNow));
        p.Void("Error de carga", Guid.NewGuid(), DateTime.UtcNow);
        Assert.Equal(PaymentStatus.Voided, p.Status);
        Assert.Equal("payment_already_voided", Assert.Throws<DomainException>(() => p.Void("otra vez", Guid.NewGuid(), DateTime.UtcNow)).Code);
    }

    [Fact]
    public void Balance_is_debits_minus_credits_including_reversals()
    {
        var patient = Guid.NewGuid();
        var now = DateTime.UtcNow;
        AccountMovement M(MovementType t, MovementDirection d, decimal a) => new(C, patient, t, d, a, now, "x", null, null, null);
        var movements = new[]
        {
            M(MovementType.BudgetCharge, MovementDirection.Debit, 480_000),
            M(MovementType.Payment, MovementDirection.Credit, 150_000),
            M(MovementType.Payment, MovementDirection.Credit, 50_000),
            M(MovementType.PaymentReversal, MovementDirection.Debit, 50_000),
        };
        Assert.Equal(330_000, AccountCalculator.Balance(movements));
    }

    [Fact]
    public void Budget_outstanding_ignores_voided_payments()
    {
        var b = new Budget(C, Guid.NewGuid(), 1, null, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), 0, null);
        b.AddItem(null, "Corona", 21, 1, 300_000, 0, null);
        b.Issue(Guid.NewGuid(), DateTime.UtcNow);
        b.Approve(Guid.NewGuid(), DateTime.UtcNow, new DateOnly(2026, 6, 1));
        var p1 = new Payment(C, b.PatientId, b.Id, 100_000, PaymentMethod.Cash, DateTime.UtcNow, null, null, Guid.NewGuid(), 1);
        var p2 = new Payment(C, b.PatientId, b.Id, 50_000, PaymentMethod.Cash, DateTime.UtcNow, null, null, Guid.NewGuid(), 2);
        p2.Void("duplicado", Guid.NewGuid(), DateTime.UtcNow);
        Assert.Equal(200_000, AccountCalculator.BudgetOutstanding(b, [p1, p2]));
    }
}

public class LabWorkTests
{
    private static LabWork New() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Corona de zirconio", 21, null,
        new DateOnly(2026, 10, 22), 85_000, null, null);

    [Fact]
    public void Delivery_flow_records_dates_and_history()
    {
        var w = New();
        var u = Guid.NewGuid();
        w.ChangeStatus(LabWorkStatus.Sent, new DateOnly(2026, 10, 15), null, u, DateTime.UtcNow);
        w.ChangeStatus(LabWorkStatus.InProduction, new DateOnly(2026, 10, 16), null, u, DateTime.UtcNow);
        w.ChangeStatus(LabWorkStatus.ReadyForPickup, new DateOnly(2026, 10, 18), null, u, DateTime.UtcNow);
        w.ChangeStatus(LabWorkStatus.Received, new DateOnly(2026, 10, 19), null, u, DateTime.UtcNow);
        w.ChangeStatus(LabWorkStatus.Placed, new DateOnly(2026, 10, 20), null, u, DateTime.UtcNow);
        Assert.Equal(new DateOnly(2026, 10, 19), w.ActualDeliveryDate);
        Assert.Equal(new DateOnly(2026, 10, 20), w.PlacementDate);
        Assert.Equal(5, w.Changes.Count);
    }

    [Fact]
    public void Invalid_transitions_and_dates_are_rejected()
    {
        var w = New();
        var u = Guid.NewGuid();
        Assert.Throws<DomainException>(() => w.ChangeStatus(LabWorkStatus.Placed, new DateOnly(2026, 10, 20), null, u, DateTime.UtcNow));
        w.ChangeStatus(LabWorkStatus.Sent, new DateOnly(2026, 10, 15), null, u, DateTime.UtcNow);
        Assert.Throws<DomainException>(() => w.ChangeStatus(LabWorkStatus.Received, new DateOnly(2026, 10, 10), null, u, DateTime.UtcNow));
    }

    [Fact]
    public void Overdue_detection()
    {
        var w = New();
        Assert.True(w.IsOverdue(new DateOnly(2026, 10, 23)));
        Assert.False(w.IsOverdue(new DateOnly(2026, 10, 22)));
    }
}

public class PatientTests
{
    [Fact]
    public void Document_and_phone_are_normalized_for_search_and_uniqueness()
    {
        var p = new Patient(Guid.NewGuid(), "Martín", "Rodríguez", "32.456.789");
        p.SetContact("+54 9 11 5523-8810", "M.Rodriguez@Mail.com", null, null, null, null);
        Assert.Equal("32456789", p.DocumentNumber);
        Assert.Equal("5491155238810", p.PhoneNormalized);
        Assert.Equal("m.rodriguez@mail.com", p.Email);
        Assert.Equal("rodriguez martin", p.SearchName);
    }

    [Fact]
    public void Age_is_computed_from_birthdate()
    {
        var p = new Patient(Guid.NewGuid(), "Ana", "Torres", "36908774");
        p.SetPersonalData(new DateOnly(1984, 3, 2), Sex.Female, null, new DateOnly(2026, 10, 19));
        Assert.Equal(42, p.AgeAt(new DateOnly(2026, 10, 19)));
        Assert.Equal(41, p.AgeAt(new DateOnly(2026, 3, 1)));
    }

    [Fact]
    public void Future_birthdate_and_short_document_are_rejected()
    {
        var p = new Patient(Guid.NewGuid(), "Ana", "Torres", "36908774");
        Assert.Throws<DomainException>(() => p.SetPersonalData(new DateOnly(2030, 1, 1), Sex.Female, null, new DateOnly(2026, 10, 19)));
        Assert.Throws<DomainException>(() => new Patient(Guid.NewGuid(), "A", "B", "123"));
    }

    [Fact]
    public void Derived_alerts_expose_only_labels()
    {
        var h = new MedicalHistory(Guid.NewGuid(), Guid.NewGuid()) { Anticoagulated = true, Allergies = "Penicilina", CurrentMedication = "Losartán 50 mg" };
        var alerts = h.DerivedAlerts().ToList();
        Assert.Contains(alerts, a => a.Title == "Anticoagulado");
        Assert.Contains(alerts, a => a.Title == "Alergia: Penicilina");
        Assert.DoesNotContain(alerts, a => a.Title.Contains("Losartán"));
        var none = new MedicalHistory(Guid.NewGuid(), Guid.NewGuid()) { Allergies = "Ninguna conocida" };
        Assert.Empty(none.DerivedAlerts());
    }
}

public class SecurityEntityTests
{
    [Fact]
    public void Refresh_token_revocation_is_idempotent()
    {
        var t = new RefreshToken(Guid.NewGuid(), Guid.NewGuid(), "hash", DateTime.UtcNow, DateTime.UtcNow.AddDays(7), null, "127.0.0.1", "ua");
        Assert.True(t.IsActive(DateTime.UtcNow));
        var replacement = Guid.NewGuid();
        t.Revoke(DateTime.UtcNow, "rotated", replacement);
        t.Revoke(DateTime.UtcNow, "other");
        Assert.False(t.IsActive(DateTime.UtcNow));
        Assert.Equal("rotated", t.RevokedReason);
        Assert.Equal(replacement, t.ReplacedByTokenId);
    }

    [Fact]
    public void Invitation_can_be_accepted_once_and_not_after_expiry()
    {
        var inv = new Invitation(Guid.NewGuid(), "A@B.com", "h", DateTime.UtcNow.AddDays(1), Guid.NewGuid(), false, [Guid.NewGuid()]);
        Assert.Equal("a@b.com", inv.Email);
        Assert.False(inv.IsUsable(DateTime.UtcNow.AddDays(2)));
        inv.Accept(Guid.NewGuid(), DateTime.UtcNow);
        Assert.Throws<DomainException>(() => inv.Accept(Guid.NewGuid(), DateTime.UtcNow));
    }

    [Fact]
    public void Role_rejects_unknown_permissions()
    {
        var r = new Role(Guid.NewGuid(), "Custom", null, false);
        Assert.Throws<DomainException>(() => r.SetPermissions(["patients.read", "hack.everything"]));
    }

    [Fact]
    public void Consent_distinguishes_system_acceptance_from_signed_document()
    {
        var c = new Consent(Guid.NewGuid(), Guid.NewGuid(), "Endodoncia", "Texto", null, Guid.NewGuid());
        c.RecordSystemAcceptance("Martín Rodríguez", "32456789", null, Guid.NewGuid(), DateTime.UtcNow);
        Assert.Equal(ConsentStatus.AcceptedInSystem, c.Status);
        Assert.Throws<DomainException>(() => c.RecordSystemAcceptance("x", null, null, Guid.NewGuid(), DateTime.UtcNow));
        c.AttachSignedDocument(Guid.NewGuid(), "Martín Rodríguez", Guid.NewGuid(), DateTime.UtcNow);
        Assert.Equal(ConsentStatus.SignedOnPaperAttached, c.Status);
    }
}
