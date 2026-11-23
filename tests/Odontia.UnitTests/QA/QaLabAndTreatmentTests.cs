using Odontia.Domain.Common;
using Odontia.Domain.Entities;
using Odontia.Domain.Enums;

namespace Odontia.UnitTests.QA;

public class QaLabWorkTests
{
    private static readonly DateTime Now = new(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly D = new(2026, 11, 19);
    private static readonly Guid U = Guid.NewGuid();

    private static readonly HashSet<(LabWorkStatus, LabWorkStatus)> Allowed =
    [
        (LabWorkStatus.Pending, LabWorkStatus.Sent), (LabWorkStatus.Pending, LabWorkStatus.Cancelled),
        (LabWorkStatus.Sent, LabWorkStatus.InProduction), (LabWorkStatus.Sent, LabWorkStatus.ReadyForPickup),
        (LabWorkStatus.Sent, LabWorkStatus.Received), (LabWorkStatus.Sent, LabWorkStatus.Cancelled),
        (LabWorkStatus.InProduction, LabWorkStatus.ReadyForPickup), (LabWorkStatus.InProduction, LabWorkStatus.Received),
        (LabWorkStatus.InProduction, LabWorkStatus.Cancelled),
        (LabWorkStatus.ReadyForPickup, LabWorkStatus.Received), (LabWorkStatus.ReadyForPickup, LabWorkStatus.Cancelled),
        (LabWorkStatus.Received, LabWorkStatus.Placed), (LabWorkStatus.Received, LabWorkStatus.Sent),
    ];

    public static IEnumerable<object[]> AllPairs() =>
        from f in Enum.GetValues<LabWorkStatus>()
        from t in Enum.GetValues<LabWorkStatus>()
        where f != t
        select new object[] { f, t };

    [Theory]
    [MemberData(nameof(AllPairs))]
    public void Transition_matrix_matches_specification(LabWorkStatus from, LabWorkStatus to) =>
        Assert.Equal(Allowed.Contains((from, to)), LabWork.CanTransition(from, to));

    private static LabWork New(IReadOnlyCollection<int>? teeth = null, DateOnly? eta = null, decimal? cost = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Corona", teeth ?? [16], null, eta, cost, null, null);

    private static LabWork InState(LabWorkStatus s)
    {
        var w = New();
        LabWorkStatus[] path = s switch
        {
            LabWorkStatus.Pending => [],
            LabWorkStatus.Sent => [LabWorkStatus.Sent],
            LabWorkStatus.InProduction => [LabWorkStatus.Sent, LabWorkStatus.InProduction],
            LabWorkStatus.ReadyForPickup => [LabWorkStatus.Sent, LabWorkStatus.ReadyForPickup],
            LabWorkStatus.Received => [LabWorkStatus.Sent, LabWorkStatus.Received],
            LabWorkStatus.Placed => [LabWorkStatus.Sent, LabWorkStatus.Received, LabWorkStatus.Placed],
            LabWorkStatus.Cancelled => [LabWorkStatus.Cancelled],
            _ => throw new ArgumentOutOfRangeException(nameof(s)),
        };
        foreach (var p in path) w.ChangeStatus(p, D, null, U, Now);
        return w;
    }

    [Theory]
    [MemberData(nameof(AllPairs))]
    public void ChangeStatus_enforces_matrix_and_records_history(LabWorkStatus from, LabWorkStatus to)
    {
        var w = InState(from);
        var before = w.Changes.Count;
        if (Allowed.Contains((from, to)))
        {
            w.ChangeStatus(to, D, "nota", U, Now);
            Assert.Equal(to, w.Status);
            Assert.Equal(before + 1, w.Changes.Count);
            var last = w.Changes.Last();
            Assert.Equal(from, last.FromStatus);
            Assert.Equal(to, last.ToStatus);
        }
        else
        {
            Assert.Equal("invalid_status_transition", Assert.Throws<DomainException>(() => w.ChangeStatus(to, D, null, U, Now)).Code);
            Assert.Equal(before, w.Changes.Count);
        }
    }

    [Fact]
    public void Same_status_does_not_add_history()
    {
        var w = InState(LabWorkStatus.Sent);
        w.ChangeStatus(LabWorkStatus.Sent, D, null, U, Now);
        Assert.Single(w.Changes);
    }

    [Fact]
    public void Resend_after_reception_clears_delivery_date()
    {
        var w = InState(LabWorkStatus.Received);
        Assert.Equal(D, w.ActualDeliveryDate);
        w.ChangeStatus(LabWorkStatus.Sent, D.AddDays(1), "ajuste", U, Now);
        Assert.Null(w.ActualDeliveryDate);
        Assert.Equal(D.AddDays(1), w.SentDate);
    }

    [Fact]
    public void Placement_before_reception_is_rejected_and_same_day_is_allowed()
    {
        var w = InState(LabWorkStatus.Received);
        Assert.Equal("invalid_date", Assert.Throws<DomainException>(() => w.ChangeStatus(LabWorkStatus.Placed, D.AddDays(-1), null, U, Now)).Code);
        w.ChangeStatus(LabWorkStatus.Placed, D, null, U, Now);
        Assert.Equal(D, w.PlacementDate);
        Assert.False(w.IsOpen);
    }

    [Fact]
    public void Closed_work_cannot_be_updated_or_linked()
    {
        foreach (var s in new[] { LabWorkStatus.Placed, LabWorkStatus.Cancelled })
        {
            var w = InState(s);
            Assert.Equal("lab_work_closed", Assert.Throws<DomainException>(() => w.Update("X", [11], null, null, null, null)).Code);
            Assert.Equal("lab_work_closed", Assert.Throws<DomainException>(() => w.LinkAppointment(Guid.NewGuid())).Code);
        }
    }

    [Fact]
    public void Tooth_codes_allow_empty_and_max_32_distinct()
    {
        var none = New(teeth: []);
        Assert.Empty(none.ToothCodes);
        Assert.Null(none.ToothCode);

        var all32 = Odontia.Domain.Dental.Fdi.All.Where(t => t.Dentition == Dentition.Permanent).Select(t => t.Code).ToList();
        var w = New(teeth: all32.Concat(all32).ToList());
        Assert.Equal(32, w.ToothCodes.Count);

        var tooMany = all32.Append(51).ToList();
        Assert.Equal("too_many_teeth", Assert.Throws<DomainException>(() => New(teeth: tooMany)).Code);
    }

    [Fact]
    public void Tooth_order_is_preserved()
    {
        var w = New(teeth: [21, 11, 21, 12]);
        Assert.Equal([21, 11, 12], w.ToothCodes);
        Assert.Equal(21, w.ToothCode);
    }

    [Fact]
    public void Cost_is_validated_as_money()
    {
        Assert.Throws<DomainException>(() => New(cost: -1));
        Assert.Throws<DomainException>(() => New(cost: 1.005m));
        Assert.Equal(0m, New(cost: 0).Cost);
        Assert.Null(New(cost: null).Cost);
    }

    [Fact]
    public void Ready_for_pickup_is_open_but_not_overdue()
    {
        var w = new LabWork(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Placa", [], null, D.AddDays(-5), null, null, null);
        Assert.True(w.IsOverdue(D));
        w.ChangeStatus(LabWorkStatus.Sent, D.AddDays(-10), null, U, Now);
        Assert.True(w.IsOverdue(D));
        w.ChangeStatus(LabWorkStatus.ReadyForPickup, D, null, U, Now);
        Assert.True(w.IsOpen);
        Assert.False(w.IsOverdue(D));
    }

    [Fact]
    public void Not_overdue_on_estimated_day_or_without_estimate()
    {
        Assert.False(New(eta: D).IsOverdue(D));
        Assert.False(New(eta: null).IsOverdue(D));
    }

    [Fact]
    public void Note_over_500_is_rejected_without_changing_status()
    {
        var w = New();
        Assert.Throws<DomainException>(() => w.ChangeStatus(LabWorkStatus.Sent, D, new string('n', 501), U, Now));
        Assert.Equal(LabWorkStatus.Pending, w.Status);
        Assert.Empty(w.Changes);
    }

    [Fact]
    public void Rejected_status_change_does_not_leave_dates_modified()
    {
        var w = New();
        Assert.Throws<DomainException>(() => w.ChangeStatus(LabWorkStatus.Sent, D, new string('n', 501), U, Now));
        Assert.Null(w.SentDate);
    }

    [Fact]
    public void Laboratory_activation_and_update()
    {
        var lab = new Laboratory(Guid.NewGuid(), "Lab");
        Assert.True(lab.IsActive);
        lab.SetActive(false);
        Assert.False(lab.IsActive);
        Assert.Throws<DomainException>(() => lab.Update("", null, null, null, null, null));
    }
}

public class QaTreatmentPlanTests
{
    private static readonly DateTime Now = new(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid U = Guid.NewGuid();
    private static readonly Guid C = Guid.NewGuid();

    private static ProcedureCatalogItem Proc(bool requiresTooth = false, decimal price = 1000m) =>
        new(C, " res-01 ", "Resina", null, price, requiresTooth, 30);

    private static TreatmentPlan Plan() => new(C, Guid.NewGuid(), Guid.NewGuid(), "Plan", null, null);

    [Fact]
    public void Procedure_code_is_uppercased_and_duration_validated()
    {
        Assert.Equal("RES-01", Proc().Code);
        Assert.Throws<DomainException>(() => new ProcedureCatalogItem(C, "X", "X", null, 10, false, 4));
        Assert.Throws<DomainException>(() => new ProcedureCatalogItem(C, "X", "X", null, 10, false, 481));
        Assert.Throws<DomainException>(() => new ProcedureCatalogItem(C, "X", "X", null, -1, false, null));
        Assert.Null(new ProcedureCatalogItem(C, "X", "X", null, 10, false, null).DefaultDurationMinutes);
    }

    [Fact]
    public void Inactive_procedure_cannot_be_added()
    {
        var p = Proc();
        p.SetActive(false);
        Assert.Equal("procedure_inactive", Assert.Throws<DomainException>(() => Plan().AddItem(p, null, [], null, null, null, U, Now)).Code);
    }

    [Fact]
    public void Surfaces_without_tooth_are_rejected()
    {
        Assert.Equal("invalid_surface", Assert.Throws<DomainException>(() =>
            Plan().AddItem(Proc(), null, [ToothSurface.Mesial], null, null, null, U, Now)).Code);
    }

    [Fact]
    public void Surface_invalid_for_tooth_is_rejected()
    {
        Assert.Equal("invalid_surface", Assert.Throws<DomainException>(() =>
            Plan().AddItem(Proc(), 11, [ToothSurface.Occlusal], null, null, null, U, Now)).Code);
        Assert.Equal("invalid_tooth", Assert.Throws<DomainException>(() =>
            Plan().AddItem(Proc(), 19, [], null, null, null, U, Now)).Code);
    }

    [Fact]
    public void Surfaces_are_encoded_sorted_and_deduplicated_and_roundtrip()
    {
        var item = Plan().AddItem(Proc(), 16, [ToothSurface.Distal, ToothSurface.Occlusal, ToothSurface.Mesial, ToothSurface.Distal], null, null, null, U, Now);
        Assert.Equal("OMD", item.Surfaces);
        Assert.Equal([ToothSurface.Occlusal, ToothSurface.Mesial, ToothSurface.Distal], item.DecodeSurfaces());
        Assert.Null(TreatmentPlanItem.EncodeSurfaces([]));
    }

    [Fact]
    public void Item_defaults_to_plan_professional()
    {
        var plan = Plan();
        var item = plan.AddItem(Proc(), null, [], null, null, null, U, Now);
        Assert.Equal(plan.ProfessionalUserId, item.ProfessionalUserId);
        var other = Guid.NewGuid();
        Assert.Equal(other, plan.AddItem(Proc(), null, [], other, null, null, U, Now).ProfessionalUserId);
    }

    [Fact]
    public void Plan_becomes_cancelled_when_all_items_are_cancelled_individually()
    {
        var plan = Plan();
        var a = plan.AddItem(Proc(), null, [], null, null, null, U, Now);
        var b = plan.AddItem(Proc(), null, [], null, null, null, U, Now);
        plan.ChangeItemStatus(a.Id, TreatmentStatus.Cancelled, null, U, Now);
        Assert.Equal(TreatmentStatus.Pending, plan.Status);
        plan.ChangeItemStatus(b.Id, TreatmentStatus.Cancelled, null, U, Now);
        Assert.Equal(TreatmentStatus.Cancelled, plan.Status);
        Assert.Equal(DateOnly.FromDateTime(Now), plan.EndDate);
        Assert.Equal(0, plan.ProgressPercent);
    }

    [Fact]
    public void Completed_plan_rejects_new_items_and_cancel()
    {
        var plan = Plan();
        var a = plan.AddItem(Proc(), null, [], null, null, null, U, Now);
        plan.ChangeItemStatus(a.Id, TreatmentStatus.Completed, null, U, Now);
        Assert.Equal(TreatmentStatus.Completed, plan.Status);
        Assert.Equal(100, plan.ProgressPercent);
        Assert.Equal("plan_closed", Assert.Throws<DomainException>(() => plan.AddItem(Proc(), null, [], null, null, null, U, Now)).Code);
        Assert.Equal("plan_closed", Assert.Throws<DomainException>(() => plan.Cancel("x", U, Now)).Code);
    }

    [Fact]
    public void Plan_completed_directly_has_start_date()
    {
        var plan = Plan();
        var a = plan.AddItem(Proc(), null, [], null, null, null, U, Now);
        plan.ChangeItemStatus(a.Id, TreatmentStatus.Completed, null, U, Now);
        Assert.NotNull(a.StartDate);
        Assert.NotNull(plan.StartDate);
    }

    [Fact]
    public void Cancel_closes_open_items_keeps_completed_and_requires_reason()
    {
        var plan = Plan();
        var done = plan.AddItem(Proc(), null, [], null, null, null, U, Now);
        var open = plan.AddItem(Proc(), null, [], null, null, null, U, Now);
        plan.ChangeItemStatus(done.Id, TreatmentStatus.Completed, null, U, Now);
        Assert.Throws<DomainException>(() => plan.Cancel("  ", U, Now));
        plan.Cancel("Paciente abandonó", U, Now);
        Assert.Equal(TreatmentStatus.Cancelled, plan.Status);
        Assert.Equal(TreatmentStatus.Completed, done.Status);
        Assert.Equal(TreatmentStatus.Cancelled, open.Status);
        Assert.Equal("plan_closed", Assert.Throws<DomainException>(() => plan.ChangeItemStatus(open.Id, TreatmentStatus.Pending, null, U, Now)).Code);
    }

    [Fact]
    public void Unknown_item_is_rejected()
    {
        Assert.Equal("item_not_found", Assert.Throws<DomainException>(() => Plan().ChangeItemStatus(Guid.NewGuid(), TreatmentStatus.Completed, null, U, Now)).Code);
    }

    [Fact]
    public void Progress_ignores_cancelled_items()
    {
        var plan = Plan();
        var items = Enumerable.Range(0, 4).Select(_ => plan.AddItem(Proc(), null, [], null, null, null, U, Now)).ToList();
        plan.ChangeItemStatus(items[0].Id, TreatmentStatus.Completed, null, U, Now);
        plan.ChangeItemStatus(items[1].Id, TreatmentStatus.Cancelled, null, U, Now);
        Assert.Equal(33, plan.ProgressPercent); // 1 de 3 relevantes
        Assert.Equal(TreatmentStatus.InProgress, plan.Status);
    }

    [Fact]
    public void Back_to_pending_reverts_plan_status_and_clears_end_date()
    {
        var plan = Plan();
        var a = plan.AddItem(Proc(), null, [], null, null, null, U, Now);
        plan.ChangeItemStatus(a.Id, TreatmentStatus.InProgress, null, U, Now);
        Assert.Equal(TreatmentStatus.InProgress, plan.Status);
        plan.ChangeItemStatus(a.Id, TreatmentStatus.Pending, null, U, Now);
        Assert.Equal(TreatmentStatus.Pending, plan.Status);
        Assert.Null(plan.EndDate);
    }

    [Fact]
    public void Changing_item_to_same_status_does_not_add_history()
    {
        var plan = Plan();
        var a = plan.AddItem(Proc(), null, [], null, null, null, U, Now);
        var before = plan.Changes.Count;
        plan.ChangeItemStatus(a.Id, TreatmentStatus.Pending, "sin cambios", U, Now);
        Assert.Equal(before, plan.Changes.Count);
    }

    [Fact]
    public void Item_price_is_not_affected_by_later_catalog_changes()
    {
        var p = Proc(price: 500m);
        var item = Plan().AddItem(p, null, [], null, null, null, U, Now);
        p.Update("Resina", null, 900m, false, null);
        Assert.Equal(500m, item.UnitPrice);
    }

    [Fact]
    public void Change_note_is_truncated_to_500()
    {
        var plan = Plan();
        var a = plan.AddItem(Proc(), null, [], null, null, null, U, Now);
        plan.ChangeItemStatus(a.Id, TreatmentStatus.InProgress, new string('n', 800), U, Now);
        Assert.Contains(plan.Changes, c => c.Note?.Length == 500);
    }
}
