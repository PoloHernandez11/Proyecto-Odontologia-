using Odontia.Domain.Common;
using Odontia.Domain.Entities;
using Odontia.Domain.Enums;

namespace Odontia.UnitTests.QA;

public class QaOdontogramTests
{
    private static readonly DateTime Now = new(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Pro = Guid.NewGuid();
    private readonly List<OdontogramCondition> _conditions = OdontogramCondition.SystemSeed.ToList();

    private OdontogramCondition Cond(string code) => _conditions.Single(c => c.Code == code);
    private OdontogramCondition Lookup(Guid id) => _conditions.Single(c => c.Id == id);
    private static Odontogram New() => new(Guid.NewGuid(), Guid.NewGuid());

    private ToothFinding Apply(Odontogram o, int tooth, ToothSurface? s, string code, FindingStatus st = FindingStatus.Existing, string? notes = null) =>
        o.ApplyCondition(tooth, s, Cond(code), st, notes, Pro, Now, Lookup);

    [Fact]
    public void System_ids_are_deterministic_and_unique()
    {
        Assert.Equal(OdontogramCondition.SystemId(7), OdontogramCondition.SystemSeed.Single(c => c.Code == OdontogramCondition.Missing).Id);
        Assert.Equal(_conditions.Count, _conditions.Select(c => c.Id).Distinct().Count());
        Assert.All(_conditions, c => Assert.True(c.IsSystem));
        Assert.Single(_conditions, c => c.MarksToothAbsent);
    }

    [Fact]
    public void Condition_code_is_uppercased_and_color_validated()
    {
        var c = new OdontogramCondition(Guid.NewGuid(), " mancha ", "Mancha", ConditionScope.Both, "#FFAA00", false, false);
        Assert.Equal("MANCHA", c.Code);
        Assert.Throws<DomainException>(() => c.Update("Mancha", ConditionScope.Both, "#FFAA00FF00"));
    }

    [Fact]
    public void Invalid_tooth_is_rejected()
    {
        Assert.Equal("invalid_tooth", Assert.Throws<DomainException>(() => Apply(New(), 19, null, OdontogramCondition.Crown)).Code);
    }

    [Fact]
    public void Both_scope_condition_accepts_tooth_and_surface()
    {
        var o = New();
        Apply(o, 16, null, OdontogramCondition.Caries);
        Apply(o, 16, ToothSurface.Occlusal, OdontogramCondition.Caries);
        Assert.Equal(2, o.CurrentFindings.Count());
    }

    [Fact]
    public void Same_condition_with_different_status_is_not_duplicate()
    {
        var o = New();
        Apply(o, 16, null, OdontogramCondition.Crown, FindingStatus.Planned);
        var existing = Apply(o, 16, null, OdontogramCondition.Crown, FindingStatus.Existing);
        Assert.Single(o.CurrentFindings);
        Assert.Equal(existing.Id, o.CurrentFindings.Single().Id);
        Assert.Contains(o.History, h => h.ChangeType == OdontogramChangeType.Completed);
    }

    [Fact]
    public void Crown_resolves_caries_on_all_surfaces_of_the_tooth_only()
    {
        var o = New();
        Apply(o, 16, ToothSurface.Occlusal, OdontogramCondition.Caries);
        Apply(o, 16, ToothSurface.Mesial, OdontogramCondition.Caries);
        Apply(o, 17, ToothSurface.Occlusal, OdontogramCondition.Caries);
        var crown = Apply(o, 16, null, OdontogramCondition.Crown);
        var current = o.CurrentFindings.ToList();
        Assert.Equal(2, current.Count);
        Assert.Contains(current, f => f.ToothCode == 17);
        Assert.All(o.Findings.Where(f => f.ToothCode == 16 && !f.IsCurrent), f => Assert.Equal(crown.Id, f.SupersededByFindingId));
    }

    [Fact]
    public void Planned_caries_is_not_resolved_by_filling()
    {
        var o = New();
        Apply(o, 16, ToothSurface.Occlusal, OdontogramCondition.Caries, FindingStatus.Planned);
        Apply(o, 16, ToothSurface.Occlusal, OdontogramCondition.Filling);
        Assert.Equal(2, o.CurrentFindings.Count());
    }

    [Fact]
    public void Planned_filling_does_not_resolve_existing_caries()
    {
        var o = New();
        Apply(o, 16, ToothSurface.Occlusal, OdontogramCondition.Caries);
        Apply(o, 16, ToothSurface.Occlusal, OdontogramCondition.Filling, FindingStatus.Planned);
        Assert.Equal(2, o.CurrentFindings.Count());
    }

    [Fact]
    public void Planned_missing_does_not_mark_absent()
    {
        var o = New();
        Apply(o, 36, null, OdontogramCondition.Missing, FindingStatus.Planned);
        Assert.False(o.IsToothAbsent(36, Lookup));
        Apply(o, 36, ToothSurface.Occlusal, OdontogramCondition.Caries);
    }

    [Fact]
    public void Existing_missing_completes_planned_missing()
    {
        var o = New();
        Apply(o, 36, null, OdontogramCondition.Missing, FindingStatus.Planned);
        Apply(o, 36, null, OdontogramCondition.Missing);
        Assert.True(o.IsToothAbsent(36, Lookup));
        Assert.Single(o.CurrentFindings);
        Assert.Contains(o.History, h => h.ChangeType == OdontogramChangeType.Completed);
    }

    [Fact]
    public void Absent_tooth_rejects_new_missing_and_crown()
    {
        var o = New();
        Apply(o, 36, null, OdontogramCondition.Missing);
        Assert.Equal("tooth_absent", Assert.Throws<DomainException>(() => Apply(o, 36, null, OdontogramCondition.Missing)).Code);
        Assert.Equal("tooth_absent", Assert.Throws<DomainException>(() => Apply(o, 36, null, OdontogramCondition.Crown)).Code);
    }

    [Fact]
    public void Implant_on_absent_tooth_is_allowed_planned_or_existing()
    {
        var o = New();
        Apply(o, 36, null, OdontogramCondition.Missing);
        Apply(o, 36, null, OdontogramCondition.Implant, FindingStatus.Planned);
        Apply(o, 36, null, OdontogramCondition.Implant, FindingStatus.Existing);
        Assert.Contains(o.CurrentFindings, f => f.ConditionId == Cond(OdontogramCondition.Implant).Id && f.Status == FindingStatus.Existing);
    }

    [Fact]
    public void Existing_implant_on_present_tooth_with_findings_is_rejected()
    {
        var o = New();
        Apply(o, 36, ToothSurface.Occlusal, OdontogramCondition.Caries);
        Assert.Equal("implant_requires_absence", Assert.Throws<DomainException>(() => Apply(o, 36, null, OdontogramCondition.Implant)).Code);
    }

    [Fact]
    public void Resolving_missing_makes_tooth_present_again()
    {
        var o = New();
        var missing = Apply(o, 36, null, OdontogramCondition.Missing);
        o.ResolveFinding(missing.Id, null, Pro, Now);
        Assert.False(o.IsToothAbsent(36, Lookup));
        Assert.Equal("Resuelto", missing.ClosedReason);
    }

    [Fact]
    public void Closed_findings_cannot_be_resolved_or_removed_again()
    {
        var o = New();
        var f = Apply(o, 16, null, OdontogramCondition.Crown);
        o.RemoveFinding(f.Id, "Error", Pro, Now);
        Assert.Equal("finding_not_current", Assert.Throws<DomainException>(() => o.ResolveFinding(f.Id, null, Pro, Now)).Code);
        Assert.Equal("finding_not_current", Assert.Throws<DomainException>(() => o.RemoveFinding(f.Id, "x", Pro, Now)).Code);
        Assert.Equal("finding_not_current", Assert.Throws<DomainException>(() => o.ResolveFinding(Guid.NewGuid(), null, Pro, Now)).Code);
    }

    [Fact]
    public void Remove_requires_reason()
    {
        var o = New();
        var f = Apply(o, 16, null, OdontogramCondition.Crown);
        Assert.Throws<DomainException>(() => o.RemoveFinding(f.Id, "  ", Pro, Now));
        Assert.True(f.IsCurrent);
    }

    [Fact]
    public void Notes_too_long_fail_before_any_side_effect()
    {
        var o = New();
        Apply(o, 16, ToothSurface.Occlusal, OdontogramCondition.Caries);
        var historyBefore = o.History.Count;
        Assert.Throws<DomainException>(() => Apply(o, 16, ToothSurface.Occlusal, OdontogramCondition.Filling, notes: new string('n', 501)));
        Assert.Single(o.CurrentFindings);
        Assert.Equal(historyBefore, o.History.Count);
    }

    [Fact]
    public void Tooth_note_validates_tooth_and_text_and_is_historized()
    {
        var o = New();
        Assert.Throws<DomainException>(() => o.AddNote(99, "x", Pro, Now));
        Assert.Throws<DomainException>(() => o.AddNote(11, " ", Pro, Now));
        Assert.Throws<DomainException>(() => o.AddNote(11, new string('x', 1001), Pro, Now));
        var n = o.AddNote(11, " Sensibilidad ", Pro, Now);
        Assert.Equal("Sensibilidad", n.Text);
        Assert.Single(o.History, h => h.ChangeType == OdontogramChangeType.NoteAdded);
    }

    [Fact]
    public void History_records_professional_and_treatment_link()
    {
        var o = New();
        var item = Guid.NewGuid();
        var f = o.ApplyCondition(16, null, Cond(OdontogramCondition.Crown), FindingStatus.Planned, null, Pro, Now, Lookup, item);
        Assert.Equal(item, f.TreatmentPlanItemId);
        Assert.All(o.History, h => Assert.Equal(Pro, h.ProfessionalUserId));
    }

    [Fact]
    public void Custom_tenant_condition_can_be_applied()
    {
        var custom = new OdontogramCondition(Guid.NewGuid(), "MANCHA", "Mancha", ConditionScope.Surface, null, false, false);
        _conditions.Add(custom);
        var o = New();
        o.ApplyCondition(11, ToothSurface.Vestibular, custom, FindingStatus.Existing, null, Pro, Now, Lookup);
        Assert.Single(o.CurrentFindings);
    }
}

public class QaClinicalTests
{
    private static readonly DateTime Now = new(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid U = Guid.NewGuid();

    private static ClinicalEntry New(DateTime? occurred = null, IEnumerable<int>? teeth = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), U, ClinicalEntryType.Evolution, occurred ?? Now, "Control", "Desc", null, null,
            teeth ?? [16, 16, 17], Now);

    [Fact]
    public void Five_minute_clock_skew_is_tolerated()
    {
        Assert.Equal(Now.AddMinutes(5), New(Now.AddMinutes(5)).OccurredAtUtc);
        Assert.Equal("future_clinical_entry", Assert.Throws<DomainException>(() => New(Now.AddMinutes(5).AddSeconds(1))).Code);
    }

    [Fact]
    public void Teeth_are_deduplicated()
    {
        Assert.Equal([16, 17], New().Teeth.Select(t => t.ToothCode));
    }

    [Fact]
    public void Amend_snapshots_previous_teeth_sorted_and_bumps_version()
    {
        var e = New(teeth: [17, 16]);
        var rev = e.Amend("Nuevo", null, null, [21], "Corrección", U, Now);
        Assert.Equal("16,17", rev.Teeth);
        Assert.Equal(1, rev.Version);
        Assert.Equal(2, e.Version);
        Assert.Equal("Control", rev.Title);
        Assert.Equal([21], e.Teeth.Select(t => t.ToothCode));
        e.Amend("Otro", null, null, [], "Corrección 2", U, Now);
        Assert.Equal(3, e.Version);
        Assert.Equal(2, e.Revisions.Count);
    }

    [Fact]
    public void Failed_amend_with_invalid_title_leaves_no_revision()
    {
        var e = New();
        Assert.Throws<DomainException>(() => e.Amend(" ", null, null, [16], "Motivo", U, Now));
        Assert.Empty(e.Revisions);
        Assert.Equal(1, e.Version);
    }

    [Fact]
    public void Failed_amend_with_invalid_tooth_does_not_change_content()
    {
        var e = New();
        Assert.Throws<DomainException>(() => e.Amend("Contenido nuevo", "otra", null, [99], "Motivo", U, Now));
        Assert.Equal("Control", e.Title);
        Assert.Equal("Desc", e.Description);
        Assert.Empty(e.Revisions);
    }

    [Fact]
    public void Void_is_idempotent_and_requires_reason()
    {
        var e = New();
        Assert.Throws<DomainException>(() => e.Void(" ", U, Now));
        Assert.False(e.IsVoided);
        e.Void("Paciente equivocado", U, Now);
        e.Void("otro motivo", Guid.NewGuid(), Now.AddDays(1));
        Assert.Equal("Paciente equivocado", e.VoidReason);
        Assert.Equal(Now, e.VoidedAtUtc);
    }

    [Fact]
    public void Content_length_limits()
    {
        Assert.Throws<DomainException>(() => new ClinicalEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), U, ClinicalEntryType.Evolution, Now,
            new string('t', 201), null, null, null, [], Now));
        Assert.Throws<DomainException>(() => new ClinicalEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), U, ClinicalEntryType.Evolution, Now,
            "T", new string('d', 8001), null, null, [], Now));
        Assert.Throws<DomainException>(() => new ClinicalEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), U, ClinicalEntryType.Evolution, Now,
            "T", null, new string('i', 2001), null, [], Now));
    }

    [Fact]
    public void Diagnosis_validates_tooth_and_surface()
    {
        var c = Guid.NewGuid();
        Assert.Throws<DomainException>(() => new Diagnosis(c, Guid.NewGuid(), U, "Caries", null, 99, null, Now, null, null));
        Assert.Equal("invalid_surface", Assert.Throws<DomainException>(() =>
            new Diagnosis(c, Guid.NewGuid(), U, "Caries", null, null, ToothSurface.Mesial, Now, null, null)).Code);
        Assert.Equal("invalid_surface", Assert.Throws<DomainException>(() =>
            new Diagnosis(c, Guid.NewGuid(), U, "Caries", null, 36, ToothSurface.Palatal, Now, null, null)).Code);
        var d = new Diagnosis(c, Guid.NewGuid(), U, " Caries ", "K02.1", 36, ToothSurface.Lingual, DateTime.SpecifyKind(Now, DateTimeKind.Unspecified), null, null);
        Assert.Equal("Caries", d.Description);
        Assert.Equal(DiagnosisStatus.Active, d.Status);
        Assert.Equal(DateTimeKind.Utc, d.DiagnosedAtUtc.Kind);
    }

    [Fact]
    public void Diagnosis_status_sets_and_clears_resolution_date()
    {
        var d = new Diagnosis(Guid.NewGuid(), Guid.NewGuid(), U, "Gingivitis", null, null, null, Now, null, null);
        d.SetStatus(DiagnosisStatus.Resolved, Now);
        Assert.Equal(Now, d.ResolvedAtUtc);
        d.SetStatus(DiagnosisStatus.Active, Now);
        Assert.Null(d.ResolvedAtUtc);
        d.SetStatus(DiagnosisStatus.Discarded, Now);
        Assert.Equal(Now, d.ResolvedAtUtc);
    }
}
