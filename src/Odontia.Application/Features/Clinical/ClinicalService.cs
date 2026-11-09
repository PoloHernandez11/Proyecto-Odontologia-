using Microsoft.EntityFrameworkCore;
using Odontia.Application.Abstractions;
using Odontia.Application.Common;
using Odontia.Application.Features.Patients;
using Odontia.Domain.Authorization;
using Odontia.Domain.Entities;
using Odontia.Domain.Enums;

namespace Odontia.Application.Features.Clinical;

public sealed record CreateClinicalEntryRequest(ClinicalEntryType Type, DateTimeOffset? OccurredAt, string Title, string? Description,
    string? Indications, IReadOnlyList<int>? Teeth, Guid? AppointmentId);

public sealed record AmendClinicalEntryRequest(string Title, string? Description, string? Indications, IReadOnlyList<int>? Teeth,
    string Reason, int Version);

public sealed record VoidClinicalEntryRequest(string Reason);

public sealed record ClinicalEntryDto(Guid Id, ClinicalEntryType Type, DateTime OccurredAtUtc, string Title, string? Description,
    string? Indications, IReadOnlyList<int> Teeth, Guid ProfessionalUserId, string ProfessionalName, Guid? AppointmentId, int Version,
    bool IsVoided, string? VoidReason, DateTime CreatedAtUtc, DateTime? LastAmendedAtUtc, IReadOnlyList<ClinicalDocumentRefDto> Documents);

public sealed record ClinicalDocumentRefDto(Guid Id, string FileName, DocumentCategory Category);

public sealed record ClinicalEntryRevisionDto(int Version, string Title, string? Description, string? Indications, string Teeth,
    string Reason, string EditedBy, DateTime EditedAtUtc);

public sealed record CreateDiagnosisRequest(string Description, string? Code, int? ToothCode, ToothSurface? Surface,
    DateTimeOffset? DiagnosedAt, Guid? ClinicalEntryId, string? Notes);

public sealed record ChangeDiagnosisStatusRequest(DiagnosisStatus Status);

public sealed record DiagnosisDto(Guid Id, string Description, string? Code, int? ToothCode, ToothSurface? Surface, DateTime DiagnosedAtUtc,
    Guid ProfessionalUserId, string ProfessionalName, DiagnosisStatus Status, DateTime? ResolvedAtUtc, Guid? ClinicalEntryId, string? Notes);

public sealed record ClinicalRecordDto(Guid PatientId, string PatientName, int? Age, Guid? RecordId, DateTime? OpenedAtUtc,
    MedicalHistoryDto? MedicalHistory, IReadOnlyList<AlertDto> Alerts, int EntryCount, int ActiveDiagnoses);

public sealed class ClinicalTimelineQuery : PageQuery
{
    public ClinicalEntryType? Type { get; set; }
    public int? ToothCode { get; set; }
    public bool IncludeVoided { get; set; }
}

public interface IClinicalService
{
    Task<ClinicalRecordDto> GetRecordAsync(Guid patientId, CancellationToken ct);
    Task<PagedResult<ClinicalEntryDto>> GetTimelineAsync(Guid patientId, ClinicalTimelineQuery query, CancellationToken ct);
    Task<ClinicalEntryDto> GetEntryAsync(Guid patientId, Guid entryId, CancellationToken ct);
    Task<ClinicalEntryDto> CreateEntryAsync(Guid patientId, CreateClinicalEntryRequest request, CancellationToken ct);
    Task<ClinicalEntryDto> AmendEntryAsync(Guid patientId, Guid entryId, AmendClinicalEntryRequest request, CancellationToken ct);
    Task<ClinicalEntryDto> VoidEntryAsync(Guid patientId, Guid entryId, VoidClinicalEntryRequest request, CancellationToken ct);
    Task<IReadOnlyList<ClinicalEntryRevisionDto>> GetRevisionsAsync(Guid patientId, Guid entryId, CancellationToken ct);
    Task<IReadOnlyList<DiagnosisDto>> GetDiagnosesAsync(Guid patientId, DiagnosisStatus? status, CancellationToken ct);
    Task<DiagnosisDto> CreateDiagnosisAsync(Guid patientId, CreateDiagnosisRequest request, CancellationToken ct);
    Task<DiagnosisDto> ChangeDiagnosisStatusAsync(Guid patientId, Guid diagnosisId, DiagnosisStatus status, CancellationToken ct);
}

internal sealed class ClinicalService(IApplicationDbContext db, ITenantContext tenant, IConsultorioClock clock, IAuditWriter audit,
    IPatientService patients) : IClinicalService
{
    public async Task<ClinicalRecordDto> GetRecordAsync(Guid patientId, CancellationToken ct)
    {
        tenant.Require(Permissions.ClinicalRead);
        var p = await db.Patients.AsNoTracking().FirstOrDefaultAsync(x => x.Id == patientId, ct) ?? throw new NotFoundException("El paciente no existe.");
        var record = await db.ClinicalRecords.AsNoTracking().FirstOrDefaultAsync(r => r.PatientId == patientId, ct);
        var history = tenant.Has(Permissions.PatientsMedicalRead) ? await patients.GetMedicalHistoryAsync(patientId, ct) : null;
        var alerts = await patients.GetAlertsAsync(patientId, ct);
        var count = await db.ClinicalEntries.CountAsync(e => e.PatientId == patientId && !e.IsVoided, ct);
        var dx = await db.Diagnoses.CountAsync(d => d.PatientId == patientId && d.Status == DiagnosisStatus.Active, ct);
        audit.Write(AuditAction.ClinicalRecordViewed, nameof(ClinicalRecord), patientId.ToString());
        return new ClinicalRecordDto(p.Id, $"{p.FirstName} {p.LastName}", p.AgeAt(await clock.TodayAsync(ct)), record?.Id, record?.CreatedAtUtc,
            history, alerts, count, dx);
    }

    public async Task<PagedResult<ClinicalEntryDto>> GetTimelineAsync(Guid patientId, ClinicalTimelineQuery q, CancellationToken ct)
    {
        tenant.Require(Permissions.ClinicalRead);
        await EnsurePatientAsync(patientId, ct);
        var query = db.ClinicalEntries.AsNoTracking().Include(e => e.Teeth).Include(e => e.Revisions).Where(e => e.PatientId == patientId);
        if (!q.IncludeVoided) query = query.Where(e => !e.IsVoided);
        if (q.Type is not null) query = query.Where(e => e.Type == q.Type);
        if (q.ToothCode is not null) query = query.Where(e => e.Teeth.Any(t => t.ToothCode == q.ToothCode));
        var page = await query.OrderByDescending(e => e.OccurredAtUtc).ThenByDescending(e => e.CreatedAtUtc).ToPagedAsync(q, ct);
        audit.Write(AuditAction.ClinicalRecordViewed, nameof(ClinicalRecord), patientId.ToString(), "Evoluciones");
        return new PagedResult<ClinicalEntryDto>(await ToDtosAsync(page.Items, ct), page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<ClinicalEntryDto> GetEntryAsync(Guid patientId, Guid entryId, CancellationToken ct)
    {
        tenant.Require(Permissions.ClinicalRead);
        var e = await db.ClinicalEntries.AsNoTracking().Include(x => x.Teeth).Include(x => x.Revisions)
                    .FirstOrDefaultAsync(x => x.Id == entryId && x.PatientId == patientId, ct) ?? throw new NotFoundException("El registro clínico no existe.");
        return (await ToDtosAsync([e], ct))[0];
    }

    public async Task<ClinicalEntryDto> CreateEntryAsync(Guid patientId, CreateClinicalEntryRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.ClinicalWrite);
        EnsureProfessional();
        await EnsurePatientAsync(patientId, ct);
        if (r.AppointmentId is { } aid && !await db.Appointments.AnyAsync(a => a.Id == aid && a.PatientId == patientId, ct))
            throw new RequestValidationException("appointmentId", "El turno no corresponde al paciente.");

        var record = await GetOrCreateRecordAsync(patientId, ct);
        var now = clock.UtcNow;
        var entry = new ClinicalEntry(tenant.ConsultorioId, record.Id, patientId, tenant.UserId, r.Type, r.OccurredAt?.UtcDateTime ?? now,
            r.Title, r.Description, r.Indications, r.AppointmentId, r.Teeth ?? [], now);
        db.ClinicalEntries.Add(entry);
        await PromotePatientStatusAsync(patientId, ct);
        await db.SaveChangesAsync(ct);
        return await GetEntryAsync(patientId, entry.Id, ct);
    }

    /// <summary>Corrección trazable: sólo el profesional autor; la versión anterior queda en el historial de revisiones.</summary>
    public async Task<ClinicalEntryDto> AmendEntryAsync(Guid patientId, Guid entryId, AmendClinicalEntryRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.ClinicalWrite);
        var e = await LoadEntryAsync(patientId, entryId, ct);
        if (e.ProfessionalUserId != tenant.UserId)
            throw new ForbiddenAccessException("Sólo el profesional responsable puede corregir el registro; agregue una nueva evolución.");
        if (e.Version != r.Version)
            throw new ConflictException("El registro fue corregido por otra sesión. Actualice y reintente.", "concurrency_conflict");
        e.Amend(r.Title, r.Description, r.Indications, r.Teeth ?? [], r.Reason, tenant.UserId, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        audit.Write(AuditAction.ClinicalEntryAmended, nameof(ClinicalEntry), e.Id.ToString(), $"Versión {e.Version}");
        return await GetEntryAsync(patientId, entryId, ct);
    }

    public async Task<ClinicalEntryDto> VoidEntryAsync(Guid patientId, Guid entryId, VoidClinicalEntryRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.ClinicalWrite);
        var e = await LoadEntryAsync(patientId, entryId, ct);
        if (e.ProfessionalUserId != tenant.UserId)
            throw new ForbiddenAccessException("Sólo el profesional responsable puede anular el registro.");
        e.Void(r.Reason, tenant.UserId, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        audit.Write(AuditAction.ClinicalEntryVoided, nameof(ClinicalEntry), e.Id.ToString());
        return await GetEntryAsync(patientId, entryId, ct);
    }

    public async Task<IReadOnlyList<ClinicalEntryRevisionDto>> GetRevisionsAsync(Guid patientId, Guid entryId, CancellationToken ct)
    {
        tenant.Require(Permissions.ClinicalRead);
        if (!await db.ClinicalEntries.AnyAsync(x => x.Id == entryId && x.PatientId == patientId, ct)) throw new NotFoundException();
        var revs = await db.ClinicalEntryRevisions.AsNoTracking().Where(x => x.ClinicalEntryId == entryId).OrderBy(x => x.Version).ToListAsync(ct);
        var names = await UserNames.LoadAsync(db, revs.Select(x => x.EditedByUserId), ct);
        return revs.Select(x => new ClinicalEntryRevisionDto(x.Version, x.Title, x.Description, x.Indications, x.Teeth, x.Reason,
            names.GetValueOrDefault(x.EditedByUserId, "?"), x.EditedAtUtc)).ToList();
    }

    // ---------------- Diagnósticos ----------------

    public async Task<IReadOnlyList<DiagnosisDto>> GetDiagnosesAsync(Guid patientId, DiagnosisStatus? status, CancellationToken ct)
    {
        tenant.Require(Permissions.ClinicalRead);
        await EnsurePatientAsync(patientId, ct);
        var list = await db.Diagnoses.AsNoTracking().Where(d => d.PatientId == patientId && (status == null || d.Status == status))
            .OrderByDescending(d => d.DiagnosedAtUtc).ToListAsync(ct);
        var names = await UserNames.LoadAsync(db, list.Select(d => d.ProfessionalUserId), ct);
        return list.Select(d => ToDto(d, names)).ToList();
    }

    public async Task<DiagnosisDto> CreateDiagnosisAsync(Guid patientId, CreateDiagnosisRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.ClinicalWrite);
        EnsureProfessional();
        await EnsurePatientAsync(patientId, ct);
        if (r.ClinicalEntryId is { } eid && !await db.ClinicalEntries.AnyAsync(e => e.Id == eid && e.PatientId == patientId, ct))
            throw new RequestValidationException("clinicalEntryId", "El registro clínico no corresponde al paciente.");
        var now = clock.UtcNow;
        var when = r.DiagnosedAt?.UtcDateTime ?? now;
        if (when > now.AddMinutes(5)) throw new RequestValidationException("diagnosedAt", "La fecha no puede ser futura.");
        var d = new Diagnosis(tenant.ConsultorioId, patientId, tenant.UserId, r.Description, r.Code, r.ToothCode, r.Surface, when, r.ClinicalEntryId, r.Notes);
        db.Diagnoses.Add(d);
        await GetOrCreateRecordAsync(patientId, ct);
        await db.SaveChangesAsync(ct);
        return ToDto(d, await UserNames.LoadAsync(db, [d.ProfessionalUserId], ct));
    }

    public async Task<DiagnosisDto> ChangeDiagnosisStatusAsync(Guid patientId, Guid diagnosisId, DiagnosisStatus status, CancellationToken ct)
    {
        tenant.Require(Permissions.ClinicalWrite);
        var d = await db.Diagnoses.FirstOrDefaultAsync(x => x.Id == diagnosisId && x.PatientId == patientId, ct) ?? throw new NotFoundException();
        d.SetStatus(status, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return ToDto(d, await UserNames.LoadAsync(db, [d.ProfessionalUserId], ct));
    }

    // ---------------- Utilidades ----------------

    private void EnsureProfessional()
    {
        if (!tenant.IsProfessional) throw new ForbiddenAccessException("Sólo un profesional puede registrar información clínica.");
    }

    private async Task<ClinicalRecord> GetOrCreateRecordAsync(Guid patientId, CancellationToken ct)
    {
        var record = await db.ClinicalRecords.FirstOrDefaultAsync(r => r.PatientId == patientId, ct);
        if (record is not null) return record;
        record = new ClinicalRecord(tenant.ConsultorioId, patientId);
        db.ClinicalRecords.Add(record);
        return record;
    }

    private async Task PromotePatientStatusAsync(Guid patientId, CancellationToken ct)
    {
        var p = await db.Patients.FirstAsync(x => x.Id == patientId, ct);
        if (p.Status == PatientStatus.New) p.SetStatus(PatientStatus.Active);
    }

    private async Task EnsurePatientAsync(Guid id, CancellationToken ct)
    {
        if (!await db.Patients.AnyAsync(p => p.Id == id, ct)) throw new NotFoundException("El paciente no existe.");
    }

    private async Task<ClinicalEntry> LoadEntryAsync(Guid patientId, Guid entryId, CancellationToken ct) =>
        await db.ClinicalEntries.Include(x => x.Teeth).Include(x => x.Revisions).FirstOrDefaultAsync(x => x.Id == entryId && x.PatientId == patientId, ct)
        ?? throw new NotFoundException("El registro clínico no existe.");

    private async Task<List<ClinicalEntryDto>> ToDtosAsync(IReadOnlyList<ClinicalEntry> entries, CancellationToken ct)
    {
        var names = await UserNames.LoadAsync(db, entries.Select(e => e.ProfessionalUserId), ct);
        var ids = entries.Select(e => e.Id).ToList();
        var canDocs = tenant.Has(Permissions.DocumentsClinicalRead);
        var docs = canDocs
            ? await db.Documents.AsNoTracking().Where(d => d.ClinicalEntryId != null && ids.Contains(d.ClinicalEntryId.Value) && !d.IsDeleted)
                .Select(d => new { d.Id, d.ClinicalEntryId, d.FileName, d.Category }).ToListAsync(ct)
            : [];
        return entries.Select(e => new ClinicalEntryDto(e.Id, e.Type, e.OccurredAtUtc, e.Title, e.Description, e.Indications,
            e.Teeth.Select(t => t.ToothCode).Order().ToList(), e.ProfessionalUserId, names.GetValueOrDefault(e.ProfessionalUserId, "?"),
            e.AppointmentId, e.Version, e.IsVoided, e.VoidReason, e.CreatedAtUtc,
            e.Revisions.Count == 0 ? null : e.Revisions.Max(r => r.EditedAtUtc),
            docs.Where(d => d.ClinicalEntryId == e.Id).Select(d => new ClinicalDocumentRefDto(d.Id, d.FileName, d.Category)).ToList())).ToList();
    }

    private static DiagnosisDto ToDto(Diagnosis d, Dictionary<Guid, string> names) => new(d.Id, d.Description, d.Code, d.ToothCode, d.Surface,
        d.DiagnosedAtUtc, d.ProfessionalUserId, names.GetValueOrDefault(d.ProfessionalUserId, "?"), d.Status, d.ResolvedAtUtc, d.ClinicalEntryId, d.Notes);
}
