using Odontia.Domain.Common;
using Odontia.Domain.Dental;
using Odontia.Domain.Enums;

namespace Odontia.Domain.Entities;

/// <summary>Tabla de referencia de piezas dentales FDI (sembrada).</summary>
public class DentalPiece
{
    private DentalPiece() { }

    public DentalPiece(FdiTooth tooth)
    {
        Code = tooth.Code;
        Quadrant = tooth.Quadrant;
        Position = tooth.Position;
        Dentition = tooth.Dentition;
        Type = tooth.Type;
        Name = tooth.Name;
    }

    public int Code { get; private set; }
    public int Quadrant { get; private set; }
    public int Position { get; private set; }
    public Dentition Dentition { get; private set; }
    public ToothType Type { get; private set; }
    public string Name { get; private set; } = default!;
}

/// <summary>Tabla de referencia de superficies dentarias (sembrada).</summary>
public class DentalSurface
{
    private DentalSurface() { }

    public DentalSurface(ToothSurface surface, string name, string abbreviation)
    {
        Code = surface;
        Name = name;
        Abbreviation = abbreviation;
    }

    public ToothSurface Code { get; private set; }
    public string Name { get; private set; } = default!;
    public string Abbreviation { get; private set; } = default!;

    public static IReadOnlyList<DentalSurface> Seed =>
    [
        new(ToothSurface.Occlusal, "Oclusal", "O"),
        new(ToothSurface.Incisal, "Incisal", "I"),
        new(ToothSurface.Mesial, "Mesial", "M"),
        new(ToothSurface.Distal, "Distal", "D"),
        new(ToothSurface.Vestibular, "Vestibular", "V"),
        new(ToothSurface.Lingual, "Lingual", "L"),
        new(ToothSurface.Palatal, "Palatina", "P"),
    ];
}

/// <summary>Condición registrable en el odontograma. ConsultorioId nulo = condición de sistema.</summary>
public class OdontogramCondition : AuditableEntity
{
    public const string Caries = "CARIES";
    public const string Filling = "FILLING";
    public const string Sealant = "SEALANT";
    public const string Crown = "CROWN";
    public const string RootCanal = "ROOT_CANAL";
    public const string Extraction = "EXTRACTION";
    public const string Missing = "MISSING";
    public const string Implant = "IMPLANT";
    public const string Fracture = "FRACTURE";
    public const string Prosthesis = "PROSTHESIS";
    public const string RootRemnant = "ROOT_REMNANT";

    private OdontogramCondition() { }

    public OdontogramCondition(Guid? consultorioId, string code, string name, ConditionScope scope, string? color,
        bool marksToothAbsent, bool isSystem, Guid? id = null)
    {
        if (id.HasValue) Id = id.Value;
        ConsultorioId = consultorioId;
        Code = Guard.Required(code, "Código", 30).ToUpperInvariant();
        IsSystem = isSystem;
        MarksToothAbsent = marksToothAbsent;
        Update(name, scope, color);
        IsActive = true;
    }

    public Guid? ConsultorioId { get; private set; }
    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public ConditionScope Scope { get; private set; }
    public string? Color { get; private set; }
    /// <summary>Si es existente, la pieza se considera ausente (p. ej. extraída / ausente).</summary>
    public bool MarksToothAbsent { get; private set; }
    public bool IsSystem { get; private set; }
    public bool IsActive { get; private set; }

    public void Update(string name, ConditionScope scope, string? color)
    {
        Name = Guard.Required(name, "Nombre", 80);
        Scope = scope;
        Color = Guard.Optional(color, "Color", 9);
    }

    public void SetActive(bool active) => IsActive = active;

    public static Guid SystemId(int n) => Guid.Parse($"6c1d3e0a-7b2f-4e11-8a55-{n:D12}");

    public static IReadOnlyList<OdontogramCondition> SystemSeed =>
    [
        new(null, Caries, "Caries", ConditionScope.Both, "#E5484D", false, true, SystemId(1)),
        new(null, Filling, "Obturación", ConditionScope.Surface, "#3E63DD", false, true, SystemId(2)),
        new(null, Sealant, "Sellante", ConditionScope.Surface, "#12A594", false, true, SystemId(3)),
        new(null, Crown, "Corona", ConditionScope.Tooth, "#F5A524", false, true, SystemId(4)),
        new(null, RootCanal, "Endodoncia", ConditionScope.Tooth, "#8E4EC6", false, true, SystemId(5)),
        new(null, Extraction, "Extracción", ConditionScope.Tooth, "#E5484D", true, true, SystemId(6)),
        new(null, Missing, "Ausente", ConditionScope.Tooth, "#8B8D98", true, true, SystemId(7)),
        new(null, Implant, "Implante", ConditionScope.Tooth, "#0090FF", false, true, SystemId(8)),
        new(null, Fracture, "Fractura", ConditionScope.Both, "#D6409F", false, true, SystemId(9)),
        new(null, Prosthesis, "Prótesis", ConditionScope.Tooth, "#AD7F58", false, true, SystemId(10)),
        new(null, RootRemnant, "Resto radicular", ConditionScope.Tooth, "#BD4B00", false, true, SystemId(11)),
    ];
}

/// <summary>
/// Odontograma del paciente (agregado). El estado actual son los hallazgos vigentes;
/// todo cambio genera una entrada inmutable de historial con profesional y fecha.
/// </summary>
public class Odontogram : TenantEntity, ISensitiveData
{
    private readonly List<ToothFinding> _findings = [];
    private readonly List<OdontogramHistoryEntry> _history = [];
    private readonly List<ToothNote> _notes = [];

    private Odontogram() { }

    public Odontogram(Guid consultorioId, Guid patientId)
    {
        ConsultorioId = consultorioId;
        PatientId = patientId;
    }

    public Guid PatientId { get; private set; }
    public IReadOnlyCollection<ToothFinding> Findings => _findings;
    public IReadOnlyCollection<OdontogramHistoryEntry> History => _history;
    public IReadOnlyCollection<ToothNote> Notes => _notes;

    public IEnumerable<ToothFinding> CurrentFindings => _findings.Where(f => f.IsCurrent);

    public bool IsToothAbsent(int toothCode, Func<Guid, OdontogramCondition> conditionLookup) =>
        CurrentFindings.Any(f => f.ToothCode == toothCode && f.Status == FindingStatus.Existing
                                 && conditionLookup(f.ConditionId).MarksToothAbsent);

    /// <summary>Registra una condición sobre una pieza o superficie aplicando las reglas clínicas del odontograma.</summary>
    public ToothFinding ApplyCondition(int toothCode, ToothSurface? surface, OdontogramCondition condition, FindingStatus status,
        string? notes, Guid professionalUserId, DateTime nowUtc, Func<Guid, OdontogramCondition> conditionLookup,
        Guid? treatmentPlanItemId = null)
    {
        var tooth = Fdi.Get(toothCode);
        if (!condition.IsActive) throw new DomainException("La condición seleccionada está inactiva.", "condition_inactive");

        if (surface is not null)
        {
            if (condition.Scope == ConditionScope.Tooth)
                throw new DomainException($"La condición '{condition.Name}' se registra sobre la pieza completa, no por superficie.", "invalid_scope");
            if (!tooth.ValidSurfaces.Contains(surface.Value))
                throw new DomainException($"La superficie {surface} no es válida para la pieza {toothCode}.", "invalid_surface");
        }
        else if (condition.Scope == ConditionScope.Surface)
        {
            throw new DomainException($"La condición '{condition.Name}' requiere indicar la superficie.", "surface_required");
        }

        var absent = IsToothAbsent(toothCode, conditionLookup);
        if (absent && condition.Code is not (OdontogramCondition.Implant or OdontogramCondition.Prosthesis))
            throw new DomainException($"La pieza {toothCode} está registrada como ausente.", "tooth_absent");
        if (condition.Code == OdontogramCondition.Implant && status == FindingStatus.Existing && !absent
            && CurrentFindings.Any(f => f.ToothCode == toothCode && f.Status == FindingStatus.Existing))
            throw new DomainException("Para registrar un implante existente la pieza debe figurar como ausente.", "implant_requires_absence");

        var duplicate = CurrentFindings.FirstOrDefault(f => f.ToothCode == toothCode && f.Surface == surface
                                                           && f.ConditionId == condition.Id && f.Status == status);
        if (duplicate is not null)
            throw new DomainException("La condición ya está registrada en esa pieza/superficie.", "duplicate_finding");

        var finding = new ToothFinding(ConsultorioId, Id, PatientId, toothCode, surface, condition.Id, status, notes,
            professionalUserId, nowUtc, treatmentPlanItemId);

        if (status == FindingStatus.Existing)
        {
            // Un tratamiento planificado de la misma condición queda completado.
            foreach (var planned in CurrentFindings.Where(f => f.ToothCode == toothCode && f.Surface == surface
                                                              && f.ConditionId == condition.Id && f.Status == FindingStatus.Planned).ToList())
            {
                planned.Close(professionalUserId, nowUtc, "Realizado", finding.Id);
                AddHistory(planned, OdontogramChangeType.Completed, professionalUserId, nowUtc, "Tratamiento planificado realizado");
            }

            // Obturación/sellante/corona existente resuelve caries de la superficie (o de la pieza, para corona).
            if (condition.Code is OdontogramCondition.Filling or OdontogramCondition.Sealant or OdontogramCondition.Crown)
            {
                var cariesFindings = CurrentFindings.Where(f => f.ToothCode == toothCode
                                                                && conditionLookup(f.ConditionId).Code == OdontogramCondition.Caries
                                                                && f.Status == FindingStatus.Existing
                                                                && (condition.Code == OdontogramCondition.Crown || f.Surface == surface)).ToList();
                foreach (var c in cariesFindings)
                {
                    c.Close(professionalUserId, nowUtc, $"Tratada con {condition.Name}", finding.Id);
                    AddHistory(c, OdontogramChangeType.Resolved, professionalUserId, nowUtc, $"Resuelta por {condition.Name}");
                }
            }

            // Pieza ausente/extraída: se cierran los demás hallazgos vigentes de la pieza.
            if (condition.MarksToothAbsent)
            {
                foreach (var f in CurrentFindings.Where(f => f.ToothCode == toothCode).ToList())
                {
                    var closingCompleted = f.ConditionId == condition.Id && f.Status == FindingStatus.Planned;
                    f.Close(professionalUserId, nowUtc, closingCompleted ? "Realizado" : "Pieza ausente", finding.Id);
                    AddHistory(f, closingCompleted ? OdontogramChangeType.Completed : OdontogramChangeType.Resolved,
                        professionalUserId, nowUtc, "Pieza registrada como ausente");
                }
            }
        }

        _findings.Add(finding);
        AddHistory(finding, OdontogramChangeType.Added, professionalUserId, nowUtc, notes);
        return finding;
    }

    /// <summary>Marca un hallazgo como resuelto (sigue en el historial).</summary>
    public void ResolveFinding(Guid findingId, string? reason, Guid professionalUserId, DateTime nowUtc)
    {
        var f = GetCurrent(findingId);
        f.Close(professionalUserId, nowUtc, Guard.Optional(reason, "Motivo", 250) ?? "Resuelto", null);
        AddHistory(f, OdontogramChangeType.Resolved, professionalUserId, nowUtc, reason);
    }

    /// <summary>Retira un hallazgo cargado por error. No se elimina físicamente: queda trazado.</summary>
    public void RemoveFinding(Guid findingId, string reason, Guid professionalUserId, DateTime nowUtc)
    {
        var f = GetCurrent(findingId);
        var r = Guard.Required(reason, "Motivo", 250);
        f.Close(professionalUserId, nowUtc, $"Retirado: {r}", null);
        AddHistory(f, OdontogramChangeType.Removed, professionalUserId, nowUtc, r);
    }

    public ToothNote AddNote(int toothCode, string text, Guid professionalUserId, DateTime nowUtc)
    {
        Fdi.Get(toothCode);
        var note = new ToothNote(ConsultorioId, Id, toothCode, text, professionalUserId, nowUtc);
        _notes.Add(note);
        _history.Add(new OdontogramHistoryEntry(ConsultorioId, Id, PatientId, toothCode, null, null, null,
            OdontogramChangeType.NoteAdded, null, note.Text, professionalUserId, nowUtc));
        return note;
    }

    private ToothFinding GetCurrent(Guid findingId) =>
        _findings.FirstOrDefault(x => x.Id == findingId && x.IsCurrent)
        ?? throw new DomainException("El hallazgo no existe o ya no está vigente.", "finding_not_current");

    private void AddHistory(ToothFinding f, OdontogramChangeType type, Guid userId, DateTime nowUtc, string? notes) =>
        _history.Add(new OdontogramHistoryEntry(ConsultorioId, Id, PatientId, f.ToothCode, f.Surface, f.ConditionId, f.Id,
            type, f.Status, notes, userId, nowUtc));
}

/// <summary>Hallazgo (condición) sobre una pieza o superficie. Vigente mientras IsCurrent.</summary>
public class ToothFinding : TenantEntity, ISensitiveData
{
    private ToothFinding() { }

    internal ToothFinding(Guid consultorioId, Guid odontogramId, Guid patientId, int toothCode, ToothSurface? surface,
        Guid conditionId, FindingStatus status, string? notes, Guid userId, DateTime nowUtc, Guid? treatmentPlanItemId)
    {
        ConsultorioId = consultorioId;
        OdontogramId = odontogramId;
        PatientId = patientId;
        ToothCode = toothCode;
        Surface = surface;
        ConditionId = conditionId;
        Status = status;
        Notes = Guard.Optional(notes, "Notas", 500);
        RecordedByUserId = userId;
        RecordedAtUtc = nowUtc;
        TreatmentPlanItemId = treatmentPlanItemId;
        IsCurrent = true;
    }

    public Guid OdontogramId { get; private set; }
    public Guid PatientId { get; private set; }
    public int ToothCode { get; private set; }
    public ToothSurface? Surface { get; private set; }
    public Guid ConditionId { get; private set; }
    public FindingStatus Status { get; private set; }
    public string? Notes { get; private set; }
    public bool IsCurrent { get; private set; }
    public Guid RecordedByUserId { get; private set; }
    public DateTime RecordedAtUtc { get; private set; }
    public DateTime? ClosedAtUtc { get; private set; }
    public Guid? ClosedByUserId { get; private set; }
    public string? ClosedReason { get; private set; }
    public Guid? SupersededByFindingId { get; private set; }
    public Guid? TreatmentPlanItemId { get; private set; }

    public OdontogramCondition? Condition { get; private set; }

    internal void Close(Guid userId, DateTime nowUtc, string reason, Guid? supersededBy)
    {
        IsCurrent = false;
        ClosedAtUtc = nowUtc;
        ClosedByUserId = userId;
        ClosedReason = reason.Length > 250 ? reason[..250] : reason;
        SupersededByFindingId = supersededBy;
    }
}

/// <summary>Historial inmutable del odontograma.</summary>
public class OdontogramHistoryEntry : TenantEntity, ISensitiveData
{
    private OdontogramHistoryEntry() { }

    internal OdontogramHistoryEntry(Guid consultorioId, Guid odontogramId, Guid patientId, int toothCode, ToothSurface? surface,
        Guid? conditionId, Guid? findingId, OdontogramChangeType changeType, FindingStatus? status, string? notes,
        Guid professionalUserId, DateTime atUtc)
    {
        ConsultorioId = consultorioId;
        OdontogramId = odontogramId;
        PatientId = patientId;
        ToothCode = toothCode;
        Surface = surface;
        ConditionId = conditionId;
        FindingId = findingId;
        ChangeType = changeType;
        Status = status;
        Notes = notes?.Length > 500 ? notes[..500] : notes;
        ProfessionalUserId = professionalUserId;
        ChangedAtUtc = atUtc;
    }

    public Guid OdontogramId { get; private set; }
    public Guid PatientId { get; private set; }
    public int ToothCode { get; private set; }
    public ToothSurface? Surface { get; private set; }
    public Guid? ConditionId { get; private set; }
    public Guid? FindingId { get; private set; }
    public OdontogramChangeType ChangeType { get; private set; }
    public FindingStatus? Status { get; private set; }
    public string? Notes { get; private set; }
    public Guid ProfessionalUserId { get; private set; }
    public DateTime ChangedAtUtc { get; private set; }
}

/// <summary>Nota clínica sobre una pieza.</summary>
public class ToothNote : TenantEntity, ISensitiveData
{
    private ToothNote() { }

    internal ToothNote(Guid consultorioId, Guid odontogramId, int toothCode, string text, Guid userId, DateTime nowUtc)
    {
        ConsultorioId = consultorioId;
        OdontogramId = odontogramId;
        ToothCode = toothCode;
        Text = Guard.Required(text, "Nota", 1000);
        ProfessionalUserId = userId;
        WrittenAtUtc = nowUtc;
    }

    public Guid OdontogramId { get; private set; }
    public int ToothCode { get; private set; }
    public string Text { get; private set; } = default!;
    public Guid ProfessionalUserId { get; private set; }
    public DateTime WrittenAtUtc { get; private set; }
}
