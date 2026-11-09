using Odontia.Domain.Common;
using Odontia.Domain.Dental;
using Odontia.Domain.Enums;

namespace Odontia.Domain.Entities;

/// <summary>Prestación del catálogo del consultorio, con su valor vigente.</summary>
public class ProcedureCatalogItem : TenantEntity
{
    private ProcedureCatalogItem() { }

    public ProcedureCatalogItem(Guid consultorioId, string code, string name, string? description, decimal price,
        bool requiresTooth, int? defaultDurationMinutes)
    {
        ConsultorioId = consultorioId;
        Code = Guard.Required(code, "Código", 30).ToUpperInvariant();
        Update(name, description, price, requiresTooth, defaultDurationMinutes);
        IsActive = true;
    }

    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public decimal Price { get; private set; }
    public bool RequiresTooth { get; private set; }
    public int? DefaultDurationMinutes { get; private set; }
    public bool IsActive { get; private set; }

    public void Update(string name, string? description, decimal price, bool requiresTooth, int? defaultDurationMinutes)
    {
        Name = Guard.Required(name, "Nombre", 150);
        Description = Guard.Optional(description, "Descripción", 500);
        Price = Guard.Money(price, "Precio");
        RequiresTooth = requiresTooth;
        if (defaultDurationMinutes is not null and (< 5 or > 480))
            throw new DomainException("La duración debe estar entre 5 y 480 minutos.");
        DefaultDurationMinutes = defaultDurationMinutes;
    }

    public void SetActive(bool active) => IsActive = active;
}

/// <summary>Plan de tratamiento del paciente.</summary>
public class TreatmentPlan : TenantEntity, ISensitiveData
{
    private readonly List<TreatmentPlanItem> _items = [];
    private readonly List<TreatmentChange> _changes = [];

    private TreatmentPlan() { }

    public TreatmentPlan(Guid consultorioId, Guid patientId, Guid professionalUserId, string title, string? notes, DateOnly? startDate)
    {
        ConsultorioId = consultorioId;
        PatientId = patientId;
        ProfessionalUserId = professionalUserId;
        Update(title, notes);
        StartDate = startDate;
        Status = TreatmentStatus.Pending;
    }

    public Guid PatientId { get; private set; }
    public Guid ProfessionalUserId { get; private set; }
    public string Title { get; private set; } = default!;
    public string? Notes { get; private set; }
    public TreatmentStatus Status { get; private set; }
    public DateOnly? StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public IReadOnlyCollection<TreatmentPlanItem> Items => _items;
    public IReadOnlyCollection<TreatmentChange> Changes => _changes;

    /// <summary>Progreso 0–100 sobre las prestaciones no canceladas.</summary>
    public int ProgressPercent
    {
        get
        {
            var relevant = _items.Where(i => i.Status != TreatmentStatus.Cancelled).ToList();
            if (relevant.Count == 0) return 0;
            return (int)Math.Round(100m * relevant.Count(i => i.Status == TreatmentStatus.Completed) / relevant.Count);
        }
    }

    public void Update(string title, string? notes)
    {
        Title = Guard.Required(title, "Título", 150);
        Notes = Guard.Optional(notes, "Observaciones", 2000);
    }

    public TreatmentPlanItem AddItem(ProcedureCatalogItem procedure, int? toothCode, IReadOnlyCollection<ToothSurface> surfaces,
        Guid? professionalUserId, Guid? diagnosisId, string? notes, Guid userId, DateTime nowUtc)
    {
        EnsureOpen();
        if (!procedure.IsActive) throw new DomainException("La prestación está inactiva.", "procedure_inactive");
        if (procedure.RequiresTooth && toothCode is null)
            throw new DomainException($"La prestación '{procedure.Name}' requiere indicar la pieza dental.", "tooth_required");
        if (toothCode is not null)
        {
            var tooth = Fdi.Get(toothCode.Value);
            foreach (var s in surfaces)
                if (!tooth.ValidSurfaces.Contains(s))
                    throw new DomainException($"La superficie {s} no es válida para la pieza {toothCode}.", "invalid_surface");
        }
        else if (surfaces.Count > 0)
        {
            throw new DomainException("No pueden indicarse superficies sin pieza dental.", "invalid_surface");
        }

        var item = new TreatmentPlanItem(ConsultorioId, Id, procedure.Id, procedure.Name, procedure.Price, toothCode,
            surfaces, professionalUserId ?? ProfessionalUserId, diagnosisId, notes, _items.Count + 1);
        _items.Add(item);
        _changes.Add(new TreatmentChange(ConsultorioId, Id, item.Id, null, TreatmentStatus.Pending, $"Prestación agregada: {procedure.Name}", userId, nowUtc));
        RecalculateStatus(userId, nowUtc);
        return item;
    }

    public void ChangeItemStatus(Guid itemId, TreatmentStatus status, string? note, Guid userId, DateTime nowUtc)
    {
        var item = _items.FirstOrDefault(i => i.Id == itemId) ?? throw new DomainException("La prestación no pertenece al plan.", "item_not_found");
        if (Status == TreatmentStatus.Cancelled) throw new DomainException("El plan está cancelado.", "plan_closed");
        var from = item.Status;
        item.ChangeStatus(status, DateOnly.FromDateTime(nowUtc));
        _changes.Add(new TreatmentChange(ConsultorioId, Id, item.Id, from, status, note, userId, nowUtc));
        RecalculateStatus(userId, nowUtc);
    }

    public void Cancel(string reason, Guid userId, DateTime nowUtc)
    {
        if (Status is TreatmentStatus.Completed or TreatmentStatus.Cancelled)
            throw new DomainException("El plan ya está cerrado.", "plan_closed");
        var r = Guard.Required(reason, "Motivo", 500);
        foreach (var i in _items.Where(i => i.Status is TreatmentStatus.Pending or TreatmentStatus.InProgress))
        {
            var from = i.Status;
            i.ChangeStatus(TreatmentStatus.Cancelled, DateOnly.FromDateTime(nowUtc));
            _changes.Add(new TreatmentChange(ConsultorioId, Id, i.Id, from, TreatmentStatus.Cancelled, r, userId, nowUtc));
        }
        var prev = Status;
        Status = TreatmentStatus.Cancelled;
        EndDate = DateOnly.FromDateTime(nowUtc);
        _changes.Add(new TreatmentChange(ConsultorioId, Id, null, prev, Status, r, userId, nowUtc));
    }

    private void EnsureOpen()
    {
        if (Status is TreatmentStatus.Completed or TreatmentStatus.Cancelled)
            throw new DomainException("No pueden agregarse prestaciones a un plan cerrado.", "plan_closed");
    }

    private void RecalculateStatus(Guid userId, DateTime nowUtc)
    {
        var prev = Status;
        var relevant = _items.Where(i => i.Status != TreatmentStatus.Cancelled).ToList();
        TreatmentStatus next;
        if (relevant.Count == 0) next = _items.Count > 0 ? TreatmentStatus.Cancelled : TreatmentStatus.Pending;
        else if (relevant.All(i => i.Status == TreatmentStatus.Completed)) next = TreatmentStatus.Completed;
        else if (relevant.Any(i => i.Status is TreatmentStatus.InProgress or TreatmentStatus.Completed)) next = TreatmentStatus.InProgress;
        else next = TreatmentStatus.Pending;

        if (next == prev) return;
        Status = next;
        var today = DateOnly.FromDateTime(nowUtc);
        if (next == TreatmentStatus.InProgress) StartDate ??= today;
        if (next is TreatmentStatus.Completed or TreatmentStatus.Cancelled) EndDate = today;
        else EndDate = null;
        _changes.Add(new TreatmentChange(ConsultorioId, Id, null, prev, next, "Estado del plan recalculado", userId, nowUtc));
    }
}

/// <summary>Prestación dentro de un plan. Conserva el precio vigente al momento de planificar.</summary>
public class TreatmentPlanItem : TenantEntity, ISensitiveData
{
    private TreatmentPlanItem() { }

    internal TreatmentPlanItem(Guid consultorioId, Guid planId, Guid procedureId, string procedureName, decimal unitPrice,
        int? toothCode, IReadOnlyCollection<ToothSurface> surfaces, Guid professionalUserId, Guid? diagnosisId, string? notes, int order)
    {
        ConsultorioId = consultorioId;
        TreatmentPlanId = planId;
        ProcedureId = procedureId;
        ProcedureName = procedureName;
        UnitPrice = unitPrice;
        ToothCode = toothCode;
        Surfaces = EncodeSurfaces(surfaces);
        ProfessionalUserId = professionalUserId;
        DiagnosisId = diagnosisId;
        Notes = Guard.Optional(notes, "Observaciones", 1000);
        SortOrder = order;
        Status = TreatmentStatus.Pending;
    }

    public Guid TreatmentPlanId { get; private set; }
    public Guid ProcedureId { get; private set; }
    public string ProcedureName { get; private set; } = default!;
    public decimal UnitPrice { get; private set; }
    public int? ToothCode { get; private set; }
    /// <summary>Superficies codificadas como abreviaturas ordenadas (p. ej. "MO"); siempre validadas contra FDI.</summary>
    public string? Surfaces { get; private set; }
    public Guid ProfessionalUserId { get; private set; }
    public Guid? DiagnosisId { get; private set; }
    public string? Notes { get; private set; }
    public TreatmentStatus Status { get; private set; }
    public DateOnly? StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public int SortOrder { get; private set; }

    public ProcedureCatalogItem? Procedure { get; private set; }

    private static readonly Dictionary<ToothSurface, char> Abbrev = new()
    {
        [ToothSurface.Occlusal] = 'O', [ToothSurface.Incisal] = 'I', [ToothSurface.Mesial] = 'M', [ToothSurface.Distal] = 'D',
        [ToothSurface.Vestibular] = 'V', [ToothSurface.Lingual] = 'L', [ToothSurface.Palatal] = 'P',
    };

    public static string? EncodeSurfaces(IEnumerable<ToothSurface> surfaces)
    {
        var s = new string(surfaces.Distinct().OrderBy(x => x).Select(x => Abbrev[x]).ToArray());
        return s.Length == 0 ? null : s;
    }

    public IReadOnlyList<ToothSurface> DecodeSurfaces() =>
        Surfaces is null ? [] : Surfaces.Select(c => Abbrev.First(kv => kv.Value == c).Key).ToList();

    internal void ChangeStatus(TreatmentStatus status, DateOnly today)
    {
        if (Status == status) return;
        if (Status is TreatmentStatus.Completed or TreatmentStatus.Cancelled)
            throw new DomainException("La prestación ya está cerrada y no puede cambiar de estado.", "item_closed");
        Status = status;
        if (status == TreatmentStatus.InProgress) StartDate ??= today;
        if (status == TreatmentStatus.Completed) { StartDate ??= today; EndDate = today; }
        if (status == TreatmentStatus.Cancelled) EndDate = today;
    }
}

/// <summary>Historial de cambios de un plan o sus prestaciones.</summary>
public class TreatmentChange : TenantEntity, ISensitiveData
{
    private TreatmentChange() { }

    internal TreatmentChange(Guid consultorioId, Guid planId, Guid? itemId, TreatmentStatus? from, TreatmentStatus to,
        string? note, Guid userId, DateTime atUtc)
    {
        ConsultorioId = consultorioId;
        TreatmentPlanId = planId;
        TreatmentPlanItemId = itemId;
        FromStatus = from;
        ToStatus = to;
        Note = note?.Length > 500 ? note[..500] : note;
        ChangedByUserId = userId;
        ChangedAtUtc = atUtc;
    }

    public Guid TreatmentPlanId { get; private set; }
    public Guid? TreatmentPlanItemId { get; private set; }
    public TreatmentStatus? FromStatus { get; private set; }
    public TreatmentStatus ToStatus { get; private set; }
    public string? Note { get; private set; }
    public Guid ChangedByUserId { get; private set; }
    public DateTime ChangedAtUtc { get; private set; }
}
