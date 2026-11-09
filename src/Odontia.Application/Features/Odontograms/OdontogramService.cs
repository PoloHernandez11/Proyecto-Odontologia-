using Microsoft.EntityFrameworkCore;
using Odontia.Application.Abstractions;
using Odontia.Application.Common;
using Odontia.Application.Features.Patients;
using Odontia.Domain.Authorization;
using Odontia.Domain.Dental;
using Odontia.Domain.Entities;
using Odontia.Domain.Enums;

namespace Odontia.Application.Features.Odontograms;

public sealed record ToothCatalogDto(int Code, int Quadrant, int Position, Dentition Dentition, ToothType Type, string Name, IReadOnlyList<ToothSurface> Surfaces);

public sealed record SurfaceCatalogDto(ToothSurface Code, string Name, string Abbreviation);

public sealed record ConditionDto(Guid Id, string Code, string Name, ConditionScope Scope, string? Color, bool MarksToothAbsent, bool IsSystem, bool IsActive);

public sealed record OdontogramCatalogDto(IReadOnlyList<ToothCatalogDto> Teeth, IReadOnlyList<SurfaceCatalogDto> Surfaces, IReadOnlyList<ConditionDto> Conditions);

public sealed record SaveConditionRequest(string Code, string Name, ConditionScope Scope, string? Color, bool MarksToothAbsent, bool IsActive = true);

public sealed record ApplyConditionRequest(int ToothCode, ToothSurface? Surface, Guid ConditionId, FindingStatus Status, string? Notes, Guid? TreatmentPlanItemId);

/// <summary>Aplica varias condiciones en una sola operación atómica (p. ej. varias superficies de una pieza).</summary>
public sealed record ApplyConditionsRequest(IReadOnlyList<ApplyConditionRequest> Items);

public sealed record ResolveFindingRequest(string? Reason);

public sealed record RemoveFindingRequest(string Reason);

public sealed record AddToothNoteRequest(string Text);

public sealed record FindingDto(Guid Id, int ToothCode, ToothSurface? Surface, Guid ConditionId, string ConditionCode, string ConditionName,
    string? Color, FindingStatus Status, string? Notes, DateTime RecordedAtUtc, Guid RecordedByUserId, string RecordedBy, Guid? TreatmentPlanItemId);

public sealed record ToothStateDto(int ToothCode, bool IsAbsent, IReadOnlyList<FindingDto> Findings, IReadOnlyList<ToothNoteDto> Notes);

public sealed record ToothNoteDto(Guid Id, string Text, DateTime WrittenAtUtc, string Professional);

public sealed record OdontogramDto(Guid PatientId, Guid? OdontogramId, IReadOnlyList<ToothStateDto> Teeth, DateTime? LastChangeUtc, string? LastChangeBy);

public sealed record OdontogramHistoryDto(Guid Id, DateTime ChangedAtUtc, int ToothCode, ToothSurface? Surface, string? ConditionName,
    OdontogramChangeType ChangeType, FindingStatus? Status, string? Notes, Guid ProfessionalUserId, string Professional);

public sealed class OdontogramHistoryQuery : PageQuery
{
    public int? ToothCode { get; set; }
}

public interface IOdontogramService
{
    Task<OdontogramCatalogDto> GetCatalogAsync(CancellationToken ct);
    Task<ConditionDto> CreateConditionAsync(SaveConditionRequest request, CancellationToken ct);
    Task<ConditionDto> UpdateConditionAsync(Guid id, SaveConditionRequest request, CancellationToken ct);
    Task<OdontogramDto> GetAsync(Guid patientId, CancellationToken ct);
    Task<OdontogramDto> ApplyAsync(Guid patientId, ApplyConditionsRequest request, CancellationToken ct);
    Task<OdontogramDto> ResolveAsync(Guid patientId, Guid findingId, ResolveFindingRequest request, CancellationToken ct);
    Task<OdontogramDto> RemoveAsync(Guid patientId, Guid findingId, RemoveFindingRequest request, CancellationToken ct);
    Task<ToothNoteDto> AddNoteAsync(Guid patientId, int toothCode, AddToothNoteRequest request, CancellationToken ct);
    Task<PagedResult<OdontogramHistoryDto>> GetHistoryAsync(Guid patientId, OdontogramHistoryQuery query, CancellationToken ct);
}

internal sealed class OdontogramService(IApplicationDbContext db, ITenantContext tenant, IConsultorioClock clock) : IOdontogramService
{
    public async Task<OdontogramCatalogDto> GetCatalogAsync(CancellationToken ct)
    {
        tenant.Require(Permissions.OdontogramRead);
        var surfaces = await db.DentalSurfaces.AsNoTracking().OrderBy(s => s.Code).Select(s => new SurfaceCatalogDto(s.Code, s.Name, s.Abbreviation)).ToListAsync(ct);
        var conditions = (await ConditionsQuery().ToListAsync(ct)).Select(ToDto).ToList();
        var teeth = Fdi.All.OrderBy(t => t.Dentition).ThenBy(t => t.Code)
            .Select(t => new ToothCatalogDto(t.Code, t.Quadrant, t.Position, t.Dentition, t.Type, t.Name, t.ValidSurfaces)).ToList();
        return new OdontogramCatalogDto(teeth, surfaces, conditions);
    }

    public async Task<ConditionDto> CreateConditionAsync(SaveConditionRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.CatalogManage);
        var cid = tenant.ConsultorioId;
        var code = r.Code.Trim().ToUpperInvariant();
        if (await db.OdontogramConditions.AnyAsync(c => (c.ConsultorioId == null || c.ConsultorioId == cid) && c.Code == code, ct))
            throw new ConflictException("Ya existe una condición con ese código.");
        var c = new OdontogramCondition(cid, code, r.Name, r.Scope, r.Color, r.MarksToothAbsent, false);
        c.SetActive(r.IsActive);
        db.OdontogramConditions.Add(c);
        await db.SaveChangesAsync(ct);
        return ToDto(c);
    }

    public async Task<ConditionDto> UpdateConditionAsync(Guid id, SaveConditionRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.CatalogManage);
        var cid = tenant.ConsultorioId;
        var c = await db.OdontogramConditions.FirstOrDefaultAsync(x => x.Id == id && x.ConsultorioId == cid, ct)
                ?? throw new NotFoundException("La condición no existe o es de sistema (no editable).");
        c.Update(r.Name, r.Scope, r.Color);
        c.SetActive(r.IsActive);
        await db.SaveChangesAsync(ct);
        return ToDto(c);
    }

    public async Task<OdontogramDto> GetAsync(Guid patientId, CancellationToken ct)
    {
        tenant.Require(Permissions.OdontogramRead);
        await EnsurePatientAsync(patientId, ct);
        var odontogram = await db.Odontograms.AsNoTracking().FirstOrDefaultAsync(o => o.PatientId == patientId, ct);
        if (odontogram is null) return new OdontogramDto(patientId, null, [], null, null);

        var findings = await db.ToothFindings.AsNoTracking().Include(f => f.Condition)
            .Where(f => f.OdontogramId == odontogram.Id && f.IsCurrent).OrderBy(f => f.ToothCode).ThenBy(f => f.Surface).ToListAsync(ct);
        var notes = await db.ToothNotes.AsNoTracking().Where(n => n.OdontogramId == odontogram.Id).OrderByDescending(n => n.WrittenAtUtc).ToListAsync(ct);
        var last = await db.OdontogramHistory.AsNoTracking().Where(h => h.OdontogramId == odontogram.Id).OrderByDescending(h => h.ChangedAtUtc)
            .Select(h => new { h.ChangedAtUtc, h.ProfessionalUserId }).FirstOrDefaultAsync(ct);
        var names = await UserNames.LoadAsync(db, findings.Select(f => f.RecordedByUserId).Concat(notes.Select(n => n.ProfessionalUserId))
            .Concat(last is null ? [] : [last.ProfessionalUserId]), ct);

        var teeth = findings.Select(f => f.ToothCode).Concat(notes.Select(n => n.ToothCode)).Distinct().Order()
            .Select(code => new ToothStateDto(code,
                findings.Any(f => f.ToothCode == code && f.Status == FindingStatus.Existing && f.Condition!.MarksToothAbsent),
                findings.Where(f => f.ToothCode == code).Select(f => ToDto(f, names)).ToList(),
                notes.Where(n => n.ToothCode == code).Select(n => new ToothNoteDto(n.Id, n.Text, n.WrittenAtUtc, names.GetValueOrDefault(n.ProfessionalUserId, "?"))).ToList()))
            .ToList();
        return new OdontogramDto(patientId, odontogram.Id, teeth, last?.ChangedAtUtc, last is null ? null : names.GetValueOrDefault(last.ProfessionalUserId));
    }

    public async Task<OdontogramDto> ApplyAsync(Guid patientId, ApplyConditionsRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.OdontogramWrite);
        EnsureProfessional();
        if (r.Items.Count is 0 or > 64) throw new RequestValidationException("items", "Debe indicar entre 1 y 64 condiciones.");
        var odontogram = await LoadOrCreateAsync(patientId, ct);
        var conditions = await ConditionsQuery().ToDictionaryAsync(c => c.Id, ct);
        OdontogramCondition Lookup(Guid id) => conditions.TryGetValue(id, out var c) ? c : throw new RequestValidationException("conditionId", "La condición no existe.");

        foreach (var item in r.Items)
        {
            if (item.TreatmentPlanItemId is { } tpi && !await db.TreatmentPlanItems.AnyAsync(i => i.Id == tpi && db.TreatmentPlans.Any(p => p.Id == i.TreatmentPlanId && p.PatientId == patientId), ct))
                throw new RequestValidationException("treatmentPlanItemId", "La prestación no corresponde al paciente.");
            odontogram.ApplyCondition(item.ToothCode, item.Surface, Lookup(item.ConditionId), item.Status, item.Notes, tenant.UserId, clock.UtcNow, Lookup, item.TreatmentPlanItemId);
        }
        await db.SaveChangesAsync(ct);
        return await GetAsync(patientId, ct);
    }

    public async Task<OdontogramDto> ResolveAsync(Guid patientId, Guid findingId, ResolveFindingRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.OdontogramWrite);
        EnsureProfessional();
        var o = await LoadExistingAsync(patientId, ct);
        o.ResolveFinding(findingId, r.Reason, tenant.UserId, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return await GetAsync(patientId, ct);
    }

    public async Task<OdontogramDto> RemoveAsync(Guid patientId, Guid findingId, RemoveFindingRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.OdontogramWrite);
        EnsureProfessional();
        var o = await LoadExistingAsync(patientId, ct);
        o.RemoveFinding(findingId, r.Reason, tenant.UserId, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return await GetAsync(patientId, ct);
    }

    public async Task<ToothNoteDto> AddNoteAsync(Guid patientId, int toothCode, AddToothNoteRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.OdontogramWrite);
        EnsureProfessional();
        var o = await LoadOrCreateAsync(patientId, ct);
        var note = o.AddNote(toothCode, r.Text, tenant.UserId, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        var names = await UserNames.LoadAsync(db, [tenant.UserId], ct);
        return new ToothNoteDto(note.Id, note.Text, note.WrittenAtUtc, names.GetValueOrDefault(tenant.UserId, "?"));
    }

    public async Task<PagedResult<OdontogramHistoryDto>> GetHistoryAsync(Guid patientId, OdontogramHistoryQuery q, CancellationToken ct)
    {
        tenant.Require(Permissions.OdontogramRead);
        await EnsurePatientAsync(patientId, ct);
        var query = db.OdontogramHistory.AsNoTracking().Where(h => h.PatientId == patientId);
        if (q.ToothCode is not null) query = query.Where(h => h.ToothCode == q.ToothCode);
        var page = await query.OrderByDescending(h => h.ChangedAtUtc).ToPagedAsync(q, ct);
        var conditionIds = page.Items.Where(h => h.ConditionId != null).Select(h => h.ConditionId!.Value).Distinct().ToList();
        var conditionNames = await db.OdontogramConditions.AsNoTracking().Where(c => conditionIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        var names = await UserNames.LoadAsync(db, page.Items.Select(h => h.ProfessionalUserId), ct);
        return new PagedResult<OdontogramHistoryDto>(page.Items.Select(h => new OdontogramHistoryDto(h.Id, h.ChangedAtUtc, h.ToothCode, h.Surface,
            h.ConditionId is null ? null : conditionNames.GetValueOrDefault(h.ConditionId.Value), h.ChangeType, h.Status, h.Notes,
            h.ProfessionalUserId, names.GetValueOrDefault(h.ProfessionalUserId, "?"))).ToList(), page.Page, page.PageSize, page.TotalCount);
    }

    // ---------------- Utilidades ----------------

    private void EnsureProfessional()
    {
        if (!tenant.IsProfessional) throw new ForbiddenAccessException("Sólo un profesional puede modificar el odontograma.");
    }

    private IQueryable<OdontogramCondition> ConditionsQuery()
    {
        var cid = tenant.ConsultorioId;
        return db.OdontogramConditions.AsNoTracking().Where(c => c.ConsultorioId == null || c.ConsultorioId == cid).OrderBy(c => c.Name);
    }

    private async Task<Odontogram> LoadOrCreateAsync(Guid patientId, CancellationToken ct)
    {
        await EnsurePatientAsync(patientId, ct);
        var o = await db.Odontograms.Include(x => x.Findings.Where(f => f.IsCurrent)).FirstOrDefaultAsync(x => x.PatientId == patientId, ct);
        if (o is not null) return o;
        o = new Odontogram(tenant.ConsultorioId, patientId);
        db.Odontograms.Add(o);
        return o;
    }

    private async Task<Odontogram> LoadExistingAsync(Guid patientId, CancellationToken ct) =>
        await db.Odontograms.Include(x => x.Findings.Where(f => f.IsCurrent)).FirstOrDefaultAsync(x => x.PatientId == patientId, ct)
        ?? throw new NotFoundException("El paciente no tiene odontograma.");

    private async Task EnsurePatientAsync(Guid id, CancellationToken ct)
    {
        if (!await db.Patients.AnyAsync(p => p.Id == id, ct)) throw new NotFoundException("El paciente no existe.");
    }

    private static ConditionDto ToDto(OdontogramCondition c) => new(c.Id, c.Code, c.Name, c.Scope, c.Color, c.MarksToothAbsent, c.IsSystem, c.IsActive);

    private static FindingDto ToDto(ToothFinding f, Dictionary<Guid, string> names) => new(f.Id, f.ToothCode, f.Surface, f.ConditionId,
        f.Condition?.Code ?? "", f.Condition?.Name ?? "", f.Condition?.Color, f.Status, f.Notes, f.RecordedAtUtc, f.RecordedByUserId,
        names.GetValueOrDefault(f.RecordedByUserId, "?"), f.TreatmentPlanItemId);
}
