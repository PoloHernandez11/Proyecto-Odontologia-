using Odontia.Domain.Common;
using Odontia.Domain.Dental;
using Odontia.Domain.Entities;
using Odontia.Domain.Enums;

namespace Odontia.UnitTests.Domain;

public class FdiTests
{
    [Fact]
    public void Catalog_contains_32_permanent_and_20_temporary_teeth()
    {
        Assert.Equal(32, Fdi.All.Count(t => t.Dentition == Dentition.Permanent));
        Assert.Equal(20, Fdi.All.Count(t => t.Dentition == Dentition.Temporary));
    }

    [Theory]
    [InlineData(11, true)]
    [InlineData(48, true)]
    [InlineData(55, true)]
    [InlineData(85, true)]
    [InlineData(19, false)]
    [InlineData(56, false)]
    [InlineData(90, false)]
    [InlineData(0, false)]
    public void Validates_fdi_codes(int code, bool valid) => Assert.Equal(valid, Fdi.IsValid(code));

    [Fact]
    public void Surfaces_depend_on_tooth_type_and_arch()
    {
        Assert.Contains(ToothSurface.Incisal, Fdi.Get(11).ValidSurfaces);
        Assert.DoesNotContain(ToothSurface.Occlusal, Fdi.Get(11).ValidSurfaces);
        Assert.Contains(ToothSurface.Palatal, Fdi.Get(16).ValidSurfaces);
        Assert.Contains(ToothSurface.Lingual, Fdi.Get(36).ValidSurfaces);
        Assert.Contains(ToothSurface.Occlusal, Fdi.Get(36).ValidSurfaces);
        Assert.False(Fdi.IsValidSurface(36, ToothSurface.Palatal));
    }
}

public class OdontogramTests
{
    private static readonly Guid C = Guid.NewGuid();
    private static readonly Guid Pro = Guid.NewGuid();
    private static readonly Dictionary<string, OdontogramCondition> Conditions = OdontogramCondition.SystemSeed.ToDictionary(c => c.Code);
    private static readonly Dictionary<Guid, OdontogramCondition> ById = OdontogramCondition.SystemSeed.ToDictionary(c => c.Id);
    private static OdontogramCondition Lookup(Guid id) => ById[id];
    private static Odontogram New() => new(C, Guid.NewGuid());

    private static ToothFinding Apply(Odontogram o, int tooth, ToothSurface? s, string code, FindingStatus st = FindingStatus.Existing) =>
        o.ApplyCondition(tooth, s, Conditions[code], st, null, Pro, DateTime.UtcNow, Lookup);

    [Fact]
    public void Records_caries_on_surface_with_history_and_professional()
    {
        var o = New();
        var f = Apply(o, 36, ToothSurface.Occlusal, OdontogramCondition.Caries);
        Assert.True(f.IsCurrent);
        Assert.Equal(Pro, f.RecordedByUserId);
        Assert.Single(o.History);
        Assert.Equal(OdontogramChangeType.Added, o.History.First().ChangeType);
    }

    [Fact]
    public void Existing_filling_resolves_caries_on_same_surface_only()
    {
        var o = New();
        var occlusal = Apply(o, 36, ToothSurface.Occlusal, OdontogramCondition.Caries);
        var mesial = Apply(o, 36, ToothSurface.Mesial, OdontogramCondition.Caries);
        Apply(o, 36, ToothSurface.Occlusal, OdontogramCondition.Filling);
        Assert.False(occlusal.IsCurrent);
        Assert.True(mesial.IsCurrent);
        Assert.Contains(o.History, h => h.FindingId == occlusal.Id && h.ChangeType == OdontogramChangeType.Resolved);
    }

    [Fact]
    public void Planned_treatment_is_completed_when_recorded_as_existing()
    {
        var o = New();
        var planned = Apply(o, 36, null, OdontogramCondition.RootCanal, FindingStatus.Planned);
        Apply(o, 36, null, OdontogramCondition.RootCanal, FindingStatus.Existing);
        Assert.False(planned.IsCurrent);
        Assert.Equal("Realizado", planned.ClosedReason);
    }

    [Fact]
    public void Extraction_marks_tooth_absent_and_closes_other_findings()
    {
        var o = New();
        var caries = Apply(o, 48, ToothSurface.Occlusal, OdontogramCondition.Caries);
        Apply(o, 48, null, OdontogramCondition.Extraction, FindingStatus.Existing);
        Assert.False(caries.IsCurrent);
        Assert.True(o.IsToothAbsent(48, Lookup));
    }

    [Fact]
    public void Absent_tooth_rejects_surface_conditions_but_allows_implant()
    {
        var o = New();
        Apply(o, 46, null, OdontogramCondition.Missing);
        var ex = Assert.Throws<DomainException>(() => Apply(o, 46, ToothSurface.Occlusal, OdontogramCondition.Caries));
        Assert.Equal("tooth_absent", ex.Code);
        var implant = Apply(o, 46, null, OdontogramCondition.Implant);
        Assert.True(implant.IsCurrent);
    }

    [Fact]
    public void Invalid_surface_for_tooth_is_rejected()
    {
        var ex = Assert.Throws<DomainException>(() => Apply(New(), 11, ToothSurface.Occlusal, OdontogramCondition.Caries));
        Assert.Equal("invalid_surface", ex.Code);
    }

    [Fact]
    public void Surface_only_condition_requires_surface_and_tooth_condition_rejects_surface()
    {
        Assert.Equal("surface_required", Assert.Throws<DomainException>(() => Apply(New(), 16, null, OdontogramCondition.Filling)).Code);
        Assert.Equal("invalid_scope", Assert.Throws<DomainException>(() => Apply(New(), 16, ToothSurface.Mesial, OdontogramCondition.Crown)).Code);
    }

    [Fact]
    public void Duplicate_condition_is_rejected()
    {
        var o = New();
        Apply(o, 16, ToothSurface.Mesial, OdontogramCondition.Sealant);
        Assert.Equal("duplicate_finding", Assert.Throws<DomainException>(() => Apply(o, 16, ToothSurface.Mesial, OdontogramCondition.Sealant)).Code);
    }

    [Fact]
    public void Removing_a_finding_keeps_it_in_history()
    {
        var o = New();
        var f = Apply(o, 21, null, OdontogramCondition.Crown);
        o.RemoveFinding(f.Id, "Cargado por error", Pro, DateTime.UtcNow);
        Assert.False(f.IsCurrent);
        Assert.Contains(o.Findings, x => x.Id == f.Id);
        Assert.Contains(o.History, h => h.ChangeType == OdontogramChangeType.Removed && h.FindingId == f.Id);
    }

    [Fact]
    public void Temporary_teeth_are_supported()
    {
        var o = New();
        var f = Apply(o, 75, ToothSurface.Occlusal, OdontogramCondition.Caries);
        Assert.Equal(75, f.ToothCode);
    }
}

public class ClinicalEntryTests
{
    private static ClinicalEntry New(Guid? pro = null) => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), pro ?? Guid.NewGuid(),
        ClinicalEntryType.Evolution, DateTime.UtcNow, "Endodoncia 2.ª sesión", "Conductometría", null, null, [36], DateTime.UtcNow);

    [Fact]
    public void Amend_preserves_previous_version()
    {
        var e = New();
        var rev = e.Amend("Endodoncia 2.ª sesión", "Conductometría corregida", null, [36, 37], "Error de tipeo", Guid.NewGuid(), DateTime.UtcNow);
        Assert.Equal(2, e.Version);
        Assert.Equal(1, rev.Version);
        Assert.Equal("Conductometría", rev.Description);
        Assert.Equal("36", rev.Teeth);
        Assert.Equal(2, e.Teeth.Count);
    }

    [Fact]
    public void Amend_requires_reason()
    {
        Assert.Throws<DomainException>(() => New().Amend("x", null, null, [], " ", Guid.NewGuid(), DateTime.UtcNow));
    }

    [Fact]
    public void Voided_entry_cannot_be_amended()
    {
        var e = New();
        e.Void("Paciente equivocado", Guid.NewGuid(), DateTime.UtcNow);
        Assert.Throws<DomainException>(() => e.Amend("x", null, null, [], "motivo", Guid.NewGuid(), DateTime.UtcNow));
    }

    [Fact]
    public void Future_entries_are_rejected()
    {
        Assert.Throws<DomainException>(() => new ClinicalEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ClinicalEntryType.Evolution,
            DateTime.UtcNow.AddDays(1), "x", null, null, null, [], DateTime.UtcNow));
    }

    [Fact]
    public void Invalid_tooth_is_rejected()
    {
        Assert.Throws<DomainException>(() => new ClinicalEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ClinicalEntryType.Evolution,
            DateTime.UtcNow, "x", null, null, null, [99], DateTime.UtcNow));
    }
}

public class TreatmentPlanTests
{
    private static readonly Guid C = Guid.NewGuid();
    private static ProcedureCatalogItem Proc(decimal price, bool tooth = true) => new(C, "P" + Guid.NewGuid().ToString("N")[..5], "Prestación", null, price, tooth, null);

    [Fact]
    public void Progress_and_status_are_recalculated_from_items()
    {
        var u = Guid.NewGuid();
        var plan = new TreatmentPlan(C, Guid.NewGuid(), u, "Plan", null, null);
        var a = plan.AddItem(Proc(100), 36, [], null, null, null, u, DateTime.UtcNow);
        var b = plan.AddItem(Proc(200), 16, [ToothSurface.Occlusal, ToothSurface.Mesial], null, null, null, u, DateTime.UtcNow);
        Assert.Equal(TreatmentStatus.Pending, plan.Status);

        plan.ChangeItemStatus(a.Id, TreatmentStatus.InProgress, null, u, DateTime.UtcNow);
        Assert.Equal(TreatmentStatus.InProgress, plan.Status);
        Assert.NotNull(plan.StartDate);

        plan.ChangeItemStatus(a.Id, TreatmentStatus.Completed, null, u, DateTime.UtcNow);
        Assert.Equal(50, plan.ProgressPercent);

        plan.ChangeItemStatus(b.Id, TreatmentStatus.Cancelled, null, u, DateTime.UtcNow);
        Assert.Equal(TreatmentStatus.Completed, plan.Status);
        Assert.Equal(100, plan.ProgressPercent);
        Assert.Equal("OM", b.Surfaces); // notación del diseño: "Obturación OM"
        Assert.True(plan.Changes.Count >= 4);
    }

    [Fact]
    public void Item_price_is_snapshot_of_catalog()
    {
        var u = Guid.NewGuid();
        var proc = Proc(1000);
        var plan = new TreatmentPlan(C, Guid.NewGuid(), u, "Plan", null, null);
        var item = plan.AddItem(proc, 11, [], null, null, null, u, DateTime.UtcNow);
        proc.Update("Prestación", null, 5000, true, null);
        Assert.Equal(1000, item.UnitPrice);
    }

    [Fact]
    public void Procedure_requiring_tooth_rejects_missing_tooth()
    {
        var u = Guid.NewGuid();
        var plan = new TreatmentPlan(C, Guid.NewGuid(), u, "Plan", null, null);
        Assert.Equal("tooth_required", Assert.Throws<DomainException>(() => plan.AddItem(Proc(1), null, [], null, null, null, u, DateTime.UtcNow)).Code);
    }

    [Fact]
    public void Closed_item_cannot_change_again()
    {
        var u = Guid.NewGuid();
        var plan = new TreatmentPlan(C, Guid.NewGuid(), u, "Plan", null, null);
        var a = plan.AddItem(Proc(1), 11, [], null, null, null, u, DateTime.UtcNow);
        plan.AddItem(Proc(1), 12, [], null, null, null, u, DateTime.UtcNow);
        plan.ChangeItemStatus(a.Id, TreatmentStatus.Completed, null, u, DateTime.UtcNow);
        Assert.Throws<DomainException>(() => plan.ChangeItemStatus(a.Id, TreatmentStatus.Pending, null, u, DateTime.UtcNow));
    }
}
