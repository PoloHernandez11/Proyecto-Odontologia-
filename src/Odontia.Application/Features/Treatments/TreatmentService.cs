using Microsoft.EntityFrameworkCore;
using Odontia.Application.Abstractions;
using Odontia.Application.Common;
using Odontia.Application.Features.Patients;
using Odontia.Domain.Authorization;
using Odontia.Domain.Entities;
using Odontia.Domain.Enums;

namespace Odontia.Application.Features.Treatments;

public sealed record ProcedureDto(Guid Id, string Code, string Name, string? Description, decimal Price, bool RequiresTooth, int? DefaultDurationMinutes, bool IsActive);

public sealed record SaveProcedureRequest(string Code, string Name, string? Description, decimal Price, bool RequiresTooth, int? DefaultDurationMinutes, bool IsActive = true);

public sealed class ProcedureQuery : PageQuery
{
    public string? Search { get; set; }
    public bool IncludeInactive { get; set; }
}

public sealed record CreateTreatmentPlanRequest(string Title, string? Notes, DateOnly? StartDate, IReadOnlyList<AddPlanItemRequest>? Items);

public sealed record UpdateTreatmentPlanRequest(string Title, string? Notes);

public sealed record AddPlanItemRequest(Guid ProcedureId, int? ToothCode, IReadOnlyList<ToothSurface>? Surfaces, Guid? ProfessionalUserId,
    Guid? DiagnosisId, string? Notes);

public sealed record ChangeItemStatusRequest(TreatmentStatus Status, string? Note);

public sealed record CancelPlanRequest(string Reason);

public sealed record TreatmentPlanItemDto(Guid Id, Guid ProcedureId, string ProcedureName, decimal UnitPrice, int? ToothCode,
    IReadOnlyList<ToothSurface> Surfaces, TreatmentStatus Status, DateOnly? StartDate, DateOnly? EndDate, Guid ProfessionalUserId,
    string ProfessionalName, Guid? DiagnosisId, string? Notes, int SortOrder);

public sealed record TreatmentPlanDto(Guid Id, Guid PatientId, string Title, string? Notes, TreatmentStatus Status, DateOnly? StartDate,
    DateOnly? EndDate, Guid ProfessionalUserId, string ProfessionalName, int ProgressPercent, int CompletedItems, int TotalItems,
    decimal EstimatedTotal, IReadOnlyList<TreatmentPlanItemDto> Items, DateTime CreatedAtUtc);

public sealed record TreatmentChangeDto(DateTime ChangedAtUtc, Guid? ItemId, TreatmentStatus? FromStatus, TreatmentStatus ToStatus, string? Note, string ChangedBy);

public interface ITreatmentService
{
    Task<PagedResult<ProcedureDto>> GetProceduresAsync(ProcedureQuery query, CancellationToken ct);
    Task<ProcedureDto> CreateProcedureAsync(SaveProcedureRequest request, CancellationToken ct);
    Task<ProcedureDto> UpdateProcedureAsync(Guid id, SaveProcedureRequest request, CancellationToken ct);
    Task<IReadOnlyList<TreatmentPlanDto>> GetPlansAsync(Guid patientId, CancellationToken ct);
    Task<TreatmentPlanDto> GetPlanAsync(Guid patientId, Guid planId, CancellationToken ct);
    Task<TreatmentPlanDto> CreatePlanAsync(Guid patientId, CreateTreatmentPlanRequest request, CancellationToken ct);
    Task<TreatmentPlanDto> UpdatePlanAsync(Guid patientId, Guid planId, UpdateTreatmentPlanRequest request, CancellationToken ct);
    Task<TreatmentPlanDto> AddItemAsync(Guid patientId, Guid planId, AddPlanItemRequest request, CancellationToken ct);
    Task<TreatmentPlanDto> ChangeItemStatusAsync(Guid patientId, Guid planId, Guid itemId, ChangeItemStatusRequest request, CancellationToken ct);
    Task<TreatmentPlanDto> CancelPlanAsync(Guid patientId, Guid planId, CancelPlanRequest request, CancellationToken ct);
    Task<IReadOnlyList<TreatmentChangeDto>> GetPlanHistoryAsync(Guid patientId, Guid planId, CancellationToken ct);
}

internal sealed class TreatmentService(IApplicationDbContext db, ITenantContext tenant, IConsultorioClock clock) : ITreatmentService
{
    // ---------------- Catálogo de prestaciones ----------------

    public async Task<PagedResult<ProcedureDto>> GetProceduresAsync(ProcedureQuery q, CancellationToken ct)
    {
        if (!tenant.Has(Permissions.TreatmentsRead) && !tenant.Has(Permissions.BudgetsRead) && !tenant.Has(Permissions.CatalogManage))
            throw new ForbiddenAccessException();
        var query = db.Procedures.AsNoTracking().Where(p => q.IncludeInactive || p.IsActive);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim();
            query = query.Where(p => p.Name.Contains(s) || p.Code.Contains(s.ToUpper()));
        }
        return await query.OrderBy(p => p.Name).Select(p => new ProcedureDto(p.Id, p.Code, p.Name, p.Description, p.Price, p.RequiresTooth, p.DefaultDurationMinutes, p.IsActive))
            .ToPagedAsync(q, ct);
    }

    public async Task<ProcedureDto> CreateProcedureAsync(SaveProcedureRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.CatalogManage);
        var code = r.Code.Trim().ToUpperInvariant();
        if (await db.Procedures.AnyAsync(p => p.Code == code, ct)) throw new ConflictException("Ya existe una prestación con ese código.");
        var p = new ProcedureCatalogItem(tenant.ConsultorioId, code, r.Name, r.Description, r.Price, r.RequiresTooth, r.DefaultDurationMinutes);
        p.SetActive(r.IsActive);
        db.Procedures.Add(p);
        await db.SaveChangesAsync(ct);
        return ToDto(p);
    }

    /// <summary>Actualizar el precio NO altera planes ni presupuestos existentes (guardan su valor histórico).</summary>
    public async Task<ProcedureDto> UpdateProcedureAsync(Guid id, SaveProcedureRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.CatalogManage);
        var p = await db.Procedures.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException();
        p.Update(r.Name, r.Description, r.Price, r.RequiresTooth, r.DefaultDurationMinutes);
        p.SetActive(r.IsActive);
        await db.SaveChangesAsync(ct);
        return ToDto(p);
    }

    // ---------------- Planes ----------------

    public async Task<IReadOnlyList<TreatmentPlanDto>> GetPlansAsync(Guid patientId, CancellationToken ct)
    {
        tenant.Require(Permissions.TreatmentsRead);
        await EnsurePatientAsync(patientId, ct);
        var plans = await db.TreatmentPlans.AsNoTracking().Include(p => p.Items).Where(p => p.PatientId == patientId)
            .OrderByDescending(p => p.CreatedAtUtc).ToListAsync(ct);
        return await ToDtosAsync(plans, ct);
    }

    public async Task<TreatmentPlanDto> GetPlanAsync(Guid patientId, Guid planId, CancellationToken ct)
    {
        tenant.Require(Permissions.TreatmentsRead);
        var plan = await db.TreatmentPlans.AsNoTracking().Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == planId && p.PatientId == patientId, ct)
                   ?? throw new NotFoundException("El plan de tratamiento no existe.");
        return (await ToDtosAsync([plan], ct))[0];
    }

    public async Task<TreatmentPlanDto> CreatePlanAsync(Guid patientId, CreateTreatmentPlanRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.TreatmentsWrite);
        EnsureProfessional();
        await EnsurePatientAsync(patientId, ct);
        var plan = new TreatmentPlan(tenant.ConsultorioId, patientId, tenant.UserId, r.Title, r.Notes, r.StartDate);
        db.TreatmentPlans.Add(plan);
        foreach (var item in r.Items ?? []) await AddItemInternalAsync(plan, patientId, item, ct);
        var patient = await db.Patients.FirstAsync(p => p.Id == patientId, ct);
        if (patient.Status is PatientStatus.New or PatientStatus.Active) patient.SetStatus(PatientStatus.InTreatment);
        await db.SaveChangesAsync(ct);
        return await GetPlanAsync(patientId, plan.Id, ct);
    }

    public async Task<TreatmentPlanDto> UpdatePlanAsync(Guid patientId, Guid planId, UpdateTreatmentPlanRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.TreatmentsWrite);
        var plan = await LoadAsync(patientId, planId, ct);
        plan.Update(r.Title, r.Notes);
        await db.SaveChangesAsync(ct);
        return await GetPlanAsync(patientId, planId, ct);
    }

    public async Task<TreatmentPlanDto> AddItemAsync(Guid patientId, Guid planId, AddPlanItemRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.TreatmentsWrite);
        EnsureProfessional();
        var plan = await LoadAsync(patientId, planId, ct);
        await AddItemInternalAsync(plan, patientId, r, ct);
        await db.SaveChangesAsync(ct);
        return await GetPlanAsync(patientId, planId, ct);
    }

    public async Task<TreatmentPlanDto> ChangeItemStatusAsync(Guid patientId, Guid planId, Guid itemId, ChangeItemStatusRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.TreatmentsWrite);
        EnsureProfessional();
        var plan = await LoadAsync(patientId, planId, ct);
        plan.ChangeItemStatus(itemId, r.Status, r.Note, tenant.UserId, clock.UtcNow);
        await UpdatePatientStatusAsync(patientId, ct, plan);
        await db.SaveChangesAsync(ct);
        return await GetPlanAsync(patientId, planId, ct);
    }

    public async Task<TreatmentPlanDto> CancelPlanAsync(Guid patientId, Guid planId, CancelPlanRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.TreatmentsWrite);
        var plan = await LoadAsync(patientId, planId, ct);
        plan.Cancel(r.Reason, tenant.UserId, clock.UtcNow);
        await UpdatePatientStatusAsync(patientId, ct, plan);
        await db.SaveChangesAsync(ct);
        return await GetPlanAsync(patientId, planId, ct);
    }

    public async Task<IReadOnlyList<TreatmentChangeDto>> GetPlanHistoryAsync(Guid patientId, Guid planId, CancellationToken ct)
    {
        tenant.Require(Permissions.TreatmentsRead);
        if (!await db.TreatmentPlans.AnyAsync(p => p.Id == planId && p.PatientId == patientId, ct)) throw new NotFoundException();
        var changes = await db.TreatmentChanges.AsNoTracking().Where(c => c.TreatmentPlanId == planId).OrderBy(c => c.ChangedAtUtc).ToListAsync(ct);
        var names = await UserNames.LoadAsync(db, changes.Select(c => c.ChangedByUserId), ct);
        return changes.Select(c => new TreatmentChangeDto(c.ChangedAtUtc, c.TreatmentPlanItemId, c.FromStatus, c.ToStatus, c.Note,
            names.GetValueOrDefault(c.ChangedByUserId, "?"))).ToList();
    }

    // ---------------- Utilidades ----------------

    private async Task AddItemInternalAsync(TreatmentPlan plan, Guid patientId, AddPlanItemRequest r, CancellationToken ct)
    {
        var procedure = await db.Procedures.FirstOrDefaultAsync(p => p.Id == r.ProcedureId, ct)
                        ?? throw new RequestValidationException("procedureId", "La prestación no existe en el catálogo.");
        if (r.ProfessionalUserId is { } pid && !await db.Members.AnyAsync(m => m.UserId == pid && m.IsActive && m.IsProfessional, ct))
            throw new RequestValidationException("professionalUserId", "El profesional no pertenece al consultorio.");
        if (r.DiagnosisId is { } did && !await db.Diagnoses.AnyAsync(d => d.Id == did && d.PatientId == patientId, ct))
            throw new RequestValidationException("diagnosisId", "El diagnóstico no corresponde al paciente.");
        plan.AddItem(procedure, r.ToothCode, r.Surfaces ?? [], r.ProfessionalUserId, r.DiagnosisId, r.Notes, tenant.UserId, clock.UtcNow);
    }

    private async Task UpdatePatientStatusAsync(Guid patientId, CancellationToken ct, TreatmentPlan changed)
    {
        var otherOpen = await db.TreatmentPlans.AnyAsync(p => p.PatientId == patientId && p.Id != changed.Id
                                                              && (p.Status == TreatmentStatus.Pending || p.Status == TreatmentStatus.InProgress), ct);
        var open = otherOpen || changed.Status is TreatmentStatus.Pending or TreatmentStatus.InProgress;
        var patient = await db.Patients.FirstAsync(p => p.Id == patientId, ct);
        if (open && patient.Status != PatientStatus.Inactive) patient.SetStatus(PatientStatus.InTreatment);
        else if (!open && patient.Status == PatientStatus.InTreatment) patient.SetStatus(PatientStatus.Active);
    }

    private void EnsureProfessional()
    {
        if (!tenant.IsProfessional) throw new ForbiddenAccessException("Sólo un profesional puede planificar tratamientos.");
    }

    private async Task<TreatmentPlan> LoadAsync(Guid patientId, Guid planId, CancellationToken ct) =>
        await db.TreatmentPlans.Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == planId && p.PatientId == patientId, ct)
        ?? throw new NotFoundException("El plan de tratamiento no existe.");

    private async Task EnsurePatientAsync(Guid id, CancellationToken ct)
    {
        if (!await db.Patients.AnyAsync(p => p.Id == id, ct)) throw new NotFoundException("El paciente no existe.");
    }

    private async Task<List<TreatmentPlanDto>> ToDtosAsync(IReadOnlyList<TreatmentPlan> plans, CancellationToken ct)
    {
        var names = await UserNames.LoadAsync(db, plans.Select(p => p.ProfessionalUserId).Concat(plans.SelectMany(p => p.Items.Select(i => i.ProfessionalUserId))), ct);
        return plans.Select(p => new TreatmentPlanDto(p.Id, p.PatientId, p.Title, p.Notes, p.Status, p.StartDate, p.EndDate, p.ProfessionalUserId,
            names.GetValueOrDefault(p.ProfessionalUserId, "?"), p.ProgressPercent, p.Items.Count(i => i.Status == TreatmentStatus.Completed),
            p.Items.Count(i => i.Status != TreatmentStatus.Cancelled),
            p.Items.Where(i => i.Status != TreatmentStatus.Cancelled).Sum(i => i.UnitPrice),
            p.Items.OrderBy(i => i.SortOrder).Select(i => new TreatmentPlanItemDto(i.Id, i.ProcedureId, i.ProcedureName, i.UnitPrice, i.ToothCode,
                i.DecodeSurfaces(), i.Status, i.StartDate, i.EndDate, i.ProfessionalUserId, names.GetValueOrDefault(i.ProfessionalUserId, "?"),
                i.DiagnosisId, i.Notes, i.SortOrder)).ToList(), p.CreatedAtUtc)).ToList();
    }

    private static ProcedureDto ToDto(ProcedureCatalogItem p) => new(p.Id, p.Code, p.Name, p.Description, p.Price, p.RequiresTooth, p.DefaultDurationMinutes, p.IsActive);
}
