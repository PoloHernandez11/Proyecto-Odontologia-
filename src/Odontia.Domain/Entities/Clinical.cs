using Odontia.Domain.Common;
using Odontia.Domain.Enums;
using Odontia.Domain.Dental;

namespace Odontia.Domain.Entities;

/// <summary>Historia clínica del paciente (1:1). Contenedor histórico y trazable de registros clínicos.</summary>
public class ClinicalRecord : TenantEntity, ISensitiveData
{
    private ClinicalRecord() { }

    public ClinicalRecord(Guid consultorioId, Guid patientId)
    {
        ConsultorioId = consultorioId;
        PatientId = patientId;
    }

    public Guid PatientId { get; private set; }
}

/// <summary>
/// Registro clínico (evolución, consulta, observación, procedimiento, receta, estudio).
/// Nunca se sobrescribe silenciosamente: cada corrección guarda la versión anterior en
/// <see cref="ClinicalEntryRevision"/> y queda auditada. No se elimina: se anula con motivo.
/// </summary>
public class ClinicalEntry : TenantEntity, ISensitiveData
{
    private readonly List<ClinicalEntryTooth> _teeth = [];
    private readonly List<ClinicalEntryRevision> _revisions = [];

    private ClinicalEntry() { }

    public ClinicalEntry(Guid consultorioId, Guid clinicalRecordId, Guid patientId, Guid professionalUserId,
        ClinicalEntryType type, DateTime occurredAtUtc, string title, string? description, string? indications,
        Guid? appointmentId, IEnumerable<int> teeth, DateTime nowUtc)
    {
        ConsultorioId = consultorioId;
        ClinicalRecordId = clinicalRecordId;
        PatientId = patientId;
        ProfessionalUserId = professionalUserId;
        Type = type;
        AppointmentId = appointmentId;
        if (occurredAtUtc > nowUtc.AddMinutes(5))
            throw new DomainException("No pueden registrarse evoluciones con fecha futura.", "future_clinical_entry");
        OccurredAtUtc = DateTime.SpecifyKind(occurredAtUtc, DateTimeKind.Utc);
        SetContent(title, description, indications);
        SetTeeth(teeth);
        Version = 1;
    }

    public Guid ClinicalRecordId { get; private set; }
    public Guid PatientId { get; private set; }
    public Guid ProfessionalUserId { get; private set; }
    public ClinicalEntryType Type { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public string Title { get; private set; } = default!;
    public string? Description { get; private set; }
    public string? Indications { get; private set; }
    public Guid? AppointmentId { get; private set; }
    public int Version { get; private set; }
    public bool IsVoided { get; private set; }
    public string? VoidReason { get; private set; }
    public DateTime? VoidedAtUtc { get; private set; }
    public Guid? VoidedByUserId { get; private set; }

    public IReadOnlyCollection<ClinicalEntryTooth> Teeth => _teeth;
    public IReadOnlyCollection<ClinicalEntryRevision> Revisions => _revisions;

    private void SetContent(string title, string? description, string? indications)
    {
        Title = Guard.Required(title, "Procedimiento / título", 200);
        Description = Guard.Optional(description, "Descripción", 8000);
        Indications = Guard.Optional(indications, "Indicaciones", 2000);
    }

    private void SetTeeth(IEnumerable<int> teeth)
    {
        var codes = teeth.Distinct().ToList();
        foreach (var c in codes) Fdi.Get(c);
        _teeth.Clear();
        _teeth.AddRange(codes.Select(c => new ClinicalEntryTooth(Id, c)));
    }

    /// <summary>Corrige el registro preservando la versión previa. Requiere motivo.</summary>
    public ClinicalEntryRevision Amend(string title, string? description, string? indications, IEnumerable<int> teeth,
        string reason, Guid userId, DateTime nowUtc)
    {
        if (IsVoided) throw new DomainException("No puede corregirse un registro anulado.", "clinical_entry_voided");
        var r = Guard.Required(reason, "Motivo de corrección", 500);
        var revision = new ClinicalEntryRevision(ConsultorioId, Id, Version, Title, Description, Indications,
            string.Join(',', _teeth.Select(t => t.ToothCode).Order()), r, userId, nowUtc);
        _revisions.Add(revision);
        SetContent(title, description, indications);
        SetTeeth(teeth);
        Version++;
        return revision;
    }

    public void Void(string reason, Guid userId, DateTime nowUtc)
    {
        if (IsVoided) return;
        VoidReason = Guard.Required(reason, "Motivo de anulación", 500);
        IsVoided = true;
        VoidedAtUtc = nowUtc;
        VoidedByUserId = userId;
    }
}

public class ClinicalEntryTooth
{
    private ClinicalEntryTooth() { }
    public ClinicalEntryTooth(Guid clinicalEntryId, int toothCode) { ClinicalEntryId = clinicalEntryId; ToothCode = toothCode; }
    public Guid ClinicalEntryId { get; private set; }
    public int ToothCode { get; private set; }
}

/// <summary>Versión anterior de un registro clínico (inmutable).</summary>
public class ClinicalEntryRevision : TenantEntity, ISensitiveData
{
    private ClinicalEntryRevision() { }

    public ClinicalEntryRevision(Guid consultorioId, Guid clinicalEntryId, int version, string title, string? description,
        string? indications, string teeth, string reason, Guid editedByUserId, DateTime editedAtUtc)
    {
        ConsultorioId = consultorioId;
        ClinicalEntryId = clinicalEntryId;
        Version = version;
        Title = title;
        Description = description;
        Indications = indications;
        Teeth = teeth;
        Reason = reason;
        EditedByUserId = editedByUserId;
        EditedAtUtc = editedAtUtc;
    }

    public Guid ClinicalEntryId { get; private set; }
    public int Version { get; private set; }
    public string Title { get; private set; } = default!;
    public string? Description { get; private set; }
    public string? Indications { get; private set; }
    public string Teeth { get; private set; } = default!;
    public string Reason { get; private set; } = default!;
    public Guid EditedByUserId { get; private set; }
    public DateTime EditedAtUtc { get; private set; }
}

/// <summary>Diagnóstico clínico.</summary>
public class Diagnosis : TenantEntity, ISensitiveData
{
    private Diagnosis() { }

    public Diagnosis(Guid consultorioId, Guid patientId, Guid professionalUserId, string description, string? code,
        int? toothCode, ToothSurface? surface, DateTime diagnosedAtUtc, Guid? clinicalEntryId, string? notes)
    {
        ConsultorioId = consultorioId;
        PatientId = patientId;
        ProfessionalUserId = professionalUserId;
        Description = Guard.Required(description, "Diagnóstico", 300);
        Code = Guard.Optional(code, "Código", 20);
        if (toothCode is not null) Fdi.Get(toothCode.Value);
        if (surface is not null && (toothCode is null || !Fdi.IsValidSurface(toothCode.Value, surface.Value)))
            throw new DomainException("La superficie no es válida para la pieza indicada.", "invalid_surface");
        ToothCode = toothCode;
        Surface = surface;
        DiagnosedAtUtc = DateTime.SpecifyKind(diagnosedAtUtc, DateTimeKind.Utc);
        ClinicalEntryId = clinicalEntryId;
        Notes = Guard.Optional(notes, "Notas", 2000);
        Status = DiagnosisStatus.Active;
    }

    public Guid PatientId { get; private set; }
    public Guid ProfessionalUserId { get; private set; }
    public string Description { get; private set; } = default!;
    /// <summary>Código opcional (p. ej. CIE-10 K04.0).</summary>
    public string? Code { get; private set; }
    public int? ToothCode { get; private set; }
    public ToothSurface? Surface { get; private set; }
    public DateTime DiagnosedAtUtc { get; private set; }
    public Guid? ClinicalEntryId { get; private set; }
    public string? Notes { get; private set; }
    public DiagnosisStatus Status { get; private set; }
    public DateTime? ResolvedAtUtc { get; private set; }

    public void SetStatus(DiagnosisStatus status, DateTime nowUtc)
    {
        Status = status;
        ResolvedAtUtc = status == DiagnosisStatus.Active ? null : nowUtc;
    }
}
