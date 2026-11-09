using Microsoft.EntityFrameworkCore;
using Odontia.Application.Abstractions;
using Odontia.Application.Common;
using Odontia.Application.Features.Notifications;
using Odontia.Application.Features.Patients;
using Odontia.Domain.Authorization;
using Odontia.Domain.Entities;
using Odontia.Domain.Enums;

namespace Odontia.Application.Features.Lab;

public sealed record LaboratoryDto(Guid Id, string Name, string? ContactName, string? Phone, string? Email, string? AddressLine, string? Notes, bool IsActive);

public sealed record SaveLaboratoryRequest(string Name, string? ContactName, string? Phone, string? Email, string? AddressLine, string? Notes, bool IsActive = true);

public sealed record SaveLabWorkRequest(Guid PatientId, Guid LaboratoryId, Guid? ProfessionalUserId, string WorkType, int? ToothCode,
    string? Description, DateOnly? EstimatedDeliveryDate, decimal? Cost, string? Notes, Guid? TreatmentPlanItemId);

public sealed record ChangeLabWorkStatusRequest(LabWorkStatus Status, DateOnly? Date, string? Note, DateOnly? NewEstimatedDeliveryDate);

public sealed record LabWorkDto(Guid Id, Guid PatientId, string PatientName, Guid LaboratoryId, string LaboratoryName, Guid ProfessionalUserId,
    string ProfessionalName, string WorkType, int? ToothCode, string? Description, LabWorkStatus Status, DateOnly? SentDate,
    DateOnly? EstimatedDeliveryDate, DateOnly? ActualDeliveryDate, DateOnly? PlacementDate, decimal? Cost, string? Notes, bool IsOverdue,
    Guid? TreatmentPlanItemId, DateTime CreatedAtUtc);

public sealed record LabWorkChangeDto(DateTime ChangedAtUtc, LabWorkStatus FromStatus, LabWorkStatus ToStatus, string? Note, string ChangedBy);

public sealed class LabWorkQuery : PageQuery
{
    public LabWorkStatus? Status { get; set; }
    public Guid? LaboratoryId { get; set; }
    public Guid? PatientId { get; set; }
    /// <summary>open | overdue | this_week | closed</summary>
    public string? View { get; set; }
}

public sealed record LabBoardDto(IReadOnlyDictionary<string, int> CountsByStatus, int Overdue, int DueThisWeek, int Total);

public interface ILabService
{
    Task<IReadOnlyList<LaboratoryDto>> GetLaboratoriesAsync(bool includeInactive, CancellationToken ct);
    Task<LaboratoryDto> CreateLaboratoryAsync(SaveLaboratoryRequest request, CancellationToken ct);
    Task<LaboratoryDto> UpdateLaboratoryAsync(Guid id, SaveLaboratoryRequest request, CancellationToken ct);
    Task<PagedResult<LabWorkDto>> SearchAsync(LabWorkQuery query, CancellationToken ct);
    Task<LabBoardDto> GetBoardAsync(CancellationToken ct);
    Task<LabWorkDto> GetAsync(Guid id, CancellationToken ct);
    Task<LabWorkDto> CreateAsync(SaveLabWorkRequest request, CancellationToken ct);
    Task<LabWorkDto> UpdateAsync(Guid id, SaveLabWorkRequest request, CancellationToken ct);
    Task<LabWorkDto> ChangeStatusAsync(Guid id, ChangeLabWorkStatusRequest request, CancellationToken ct);
    Task<IReadOnlyList<LabWorkChangeDto>> GetHistoryAsync(Guid id, CancellationToken ct);
    /// <summary>Trabajo en segundo plano: avisa al profesional (una vez por día) de trabajos con entrega atrasada.</summary>
    Task<int> NotifyOverdueAsync(CancellationToken ct);
}

internal sealed class LabService(IApplicationDbContext db, ITenantContext tenant, IConsultorioClock clock, INotificationService notifications) : ILabService
{
    public async Task<IReadOnlyList<LaboratoryDto>> GetLaboratoriesAsync(bool includeInactive, CancellationToken ct)
    {
        if (!tenant.Has(Permissions.LabRead) && !tenant.Has(Permissions.LaboratoriesManage)) throw new ForbiddenAccessException();
        return await db.Laboratories.AsNoTracking().Where(l => includeInactive || l.IsActive).OrderBy(l => l.Name)
            .Select(l => new LaboratoryDto(l.Id, l.Name, l.ContactName, l.Phone, l.Email, l.AddressLine, l.Notes, l.IsActive)).ToListAsync(ct);
    }

    public async Task<LaboratoryDto> CreateLaboratoryAsync(SaveLaboratoryRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.LaboratoriesManage);
        if (await db.Laboratories.AnyAsync(l => l.Name == r.Name.Trim(), ct)) throw new ConflictException("Ya existe un laboratorio con ese nombre.");
        var l = new Domain.Entities.Laboratory(tenant.ConsultorioId, r.Name);
        l.Update(r.Name, r.ContactName, r.Phone, r.Email, r.AddressLine, r.Notes);
        l.SetActive(r.IsActive);
        db.Laboratories.Add(l);
        await db.SaveChangesAsync(ct);
        return ToDto(l);
    }

    public async Task<LaboratoryDto> UpdateLaboratoryAsync(Guid id, SaveLaboratoryRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.LaboratoriesManage);
        var l = await db.Laboratories.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException();
        if (await db.Laboratories.AnyAsync(x => x.Id != id && x.Name == r.Name.Trim(), ct)) throw new ConflictException("Ya existe un laboratorio con ese nombre.");
        l.Update(r.Name, r.ContactName, r.Phone, r.Email, r.AddressLine, r.Notes);
        l.SetActive(r.IsActive);
        await db.SaveChangesAsync(ct);
        return ToDto(l);
    }

    public async Task<PagedResult<LabWorkDto>> SearchAsync(LabWorkQuery q, CancellationToken ct)
    {
        tenant.Require(Permissions.LabRead);
        var today = await clock.TodayAsync(ct);
        var weekEnd = today.AddDays(7);
        var query = db.LabWorks.AsNoTracking().Include(w => w.Patient).Include(w => w.Laboratory).AsQueryable();
        if (q.Status is not null) query = query.Where(w => w.Status == q.Status);
        if (q.LaboratoryId is not null) query = query.Where(w => w.LaboratoryId == q.LaboratoryId);
        if (q.PatientId is not null) query = query.Where(w => w.PatientId == q.PatientId);
        query = q.View?.ToLowerInvariant() switch
        {
            "open" => query.Where(w => w.Status != LabWorkStatus.Placed && w.Status != LabWorkStatus.Cancelled),
            "overdue" => query.Where(w => (w.Status == LabWorkStatus.Pending || w.Status == LabWorkStatus.Sent || w.Status == LabWorkStatus.InProduction)
                                          && w.EstimatedDeliveryDate < today),
            "this_week" => query.Where(w => w.Status != LabWorkStatus.Placed && w.Status != LabWorkStatus.Cancelled
                                            && w.EstimatedDeliveryDate >= today && w.EstimatedDeliveryDate <= weekEnd),
            "closed" => query.Where(w => w.Status == LabWorkStatus.Placed || w.Status == LabWorkStatus.Cancelled),
            _ => query,
        };
        var page = await query.OrderBy(w => w.EstimatedDeliveryDate == null).ThenBy(w => w.EstimatedDeliveryDate).ThenByDescending(w => w.CreatedAtUtc).ToPagedAsync(q, ct);
        return new PagedResult<LabWorkDto>(await ToDtosAsync(page.Items, today, ct), page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<LabBoardDto> GetBoardAsync(CancellationToken ct)
    {
        tenant.Require(Permissions.LabRead);
        var today = await clock.TodayAsync(ct);
        var weekEnd = today.AddDays(7);
        var rows = await db.LabWorks.AsNoTracking().Where(w => w.Status != LabWorkStatus.Cancelled)
            .Select(w => new { w.Status, w.EstimatedDeliveryDate }).ToListAsync(ct);
        return new LabBoardDto(rows.GroupBy(r => r.Status.ToString()).ToDictionary(g => g.Key, g => g.Count()),
            rows.Count(r => r.Status is LabWorkStatus.Pending or LabWorkStatus.Sent or LabWorkStatus.InProduction && r.EstimatedDeliveryDate < today),
            rows.Count(r => r.Status is not LabWorkStatus.Placed && r.EstimatedDeliveryDate >= today && r.EstimatedDeliveryDate <= weekEnd),
            rows.Count);
    }

    public async Task<LabWorkDto> GetAsync(Guid id, CancellationToken ct)
    {
        tenant.Require(Permissions.LabRead);
        var w = await db.LabWorks.AsNoTracking().Include(x => x.Patient).Include(x => x.Laboratory).FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("El trabajo de laboratorio no existe.");
        return (await ToDtosAsync([w], await clock.TodayAsync(ct), ct))[0];
    }

    public async Task<LabWorkDto> CreateAsync(SaveLabWorkRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.LabWrite);
        var professional = r.ProfessionalUserId ?? tenant.UserId;
        await ValidateAsync(r, professional, ct);
        var w = new LabWork(tenant.ConsultorioId, r.PatientId, professional, r.LaboratoryId, r.WorkType, r.ToothCode, r.Description,
            r.EstimatedDeliveryDate, r.Cost, r.Notes, r.TreatmentPlanItemId);
        db.LabWorks.Add(w);
        await db.SaveChangesAsync(ct);
        return await GetAsync(w.Id, ct);
    }

    public async Task<LabWorkDto> UpdateAsync(Guid id, SaveLabWorkRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.LabWrite);
        var w = await db.LabWorks.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException();
        if (r.PatientId != w.PatientId) throw new RequestValidationException("patientId", "No puede cambiarse el paciente de un trabajo.");
        await ValidateAsync(r, w.ProfessionalUserId, ct);
        if (r.LaboratoryId != w.LaboratoryId) throw new RequestValidationException("laboratoryId", "Para cambiar de laboratorio cancele y cree un nuevo trabajo.");
        w.Update(r.WorkType, r.ToothCode, r.Description, r.EstimatedDeliveryDate, r.Cost, r.Notes);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>Seguimiento logístico (envío, recepción, retiro, colocación). Requiere lab.track o lab.write.</summary>
    public async Task<LabWorkDto> ChangeStatusAsync(Guid id, ChangeLabWorkStatusRequest r, CancellationToken ct)
    {
        if (!tenant.Has(Permissions.LabTrack) && !tenant.Has(Permissions.LabWrite)) throw new ForbiddenAccessException();
        if (r.Status is LabWorkStatus.Placed or LabWorkStatus.Cancelled) tenant.Require(Permissions.LabWrite);
        var w = await db.LabWorks.Include(x => x.Patient).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException();
        var today = await clock.TodayAsync(ct);
        var date = r.Date ?? today;
        if (date > today) throw new RequestValidationException("date", "La fecha no puede ser futura.");
        w.ChangeStatus(r.Status, date, r.Note, tenant.UserId, clock.UtcNow);
        if (r.NewEstimatedDeliveryDate is not null)
            w.Update(w.WorkType, w.ToothCode, w.Description, r.NewEstimatedDeliveryDate, w.Cost, w.Notes);
        await db.SaveChangesAsync(ct);
        if (r.Status is LabWorkStatus.ReadyForPickup or LabWorkStatus.Received && w.ProfessionalUserId != tenant.UserId)
            await notifications.NotifyAsync(w.ProfessionalUserId, NotificationType.LabWorkUpdate,
                r.Status == LabWorkStatus.Received ? "Trabajo de laboratorio recibido" : "Trabajo listo para retirar",
                $"{w.WorkType} - {w.Patient?.FirstName} {w.Patient?.LastName}", nameof(LabWork), w.Id, ct);
        return await GetAsync(id, ct);
    }

    public async Task<IReadOnlyList<LabWorkChangeDto>> GetHistoryAsync(Guid id, CancellationToken ct)
    {
        tenant.Require(Permissions.LabRead);
        if (!await db.LabWorks.AnyAsync(w => w.Id == id, ct)) throw new NotFoundException();
        var rows = await db.LabWorkStatusChanges.AsNoTracking().Where(c => c.LabWorkId == id).OrderBy(c => c.ChangedAtUtc).ToListAsync(ct);
        var names = await UserNames.LoadAsync(db, rows.Select(c => c.ChangedByUserId), ct);
        return rows.Select(c => new LabWorkChangeDto(c.ChangedAtUtc, c.FromStatus, c.ToStatus, c.Note, names.GetValueOrDefault(c.ChangedByUserId, "?"))).ToList();
    }

    public async Task<int> NotifyOverdueAsync(CancellationToken ct)
    {
        if (!tenant.IsBackgroundJob) tenant.Require(Permissions.LabWrite);
        var today = await clock.TodayAsync(ct);
        var since = clock.UtcNow.AddHours(-24);
        var overdue = await db.LabWorks.Include(w => w.Patient)
            .Where(w => (w.Status == LabWorkStatus.Pending || w.Status == LabWorkStatus.Sent || w.Status == LabWorkStatus.InProduction)
                        && w.EstimatedDeliveryDate < today)
            .Where(w => !db.Notifications.Any(n => n.EntityId == w.Id && n.Type == NotificationType.LabWorkOverdue && n.CreatedAtUtc >= since))
            .Take(200).ToListAsync(ct);
        foreach (var w in overdue)
            await notifications.NotifyAsync(w.ProfessionalUserId, NotificationType.LabWorkOverdue, "Trabajo de laboratorio atrasado",
                $"{w.WorkType} - {w.Patient?.FirstName} {w.Patient?.LastName} (entrega estimada {w.EstimatedDeliveryDate:dd/MM})", nameof(LabWork), w.Id, ct);
        return overdue.Count;
    }

    private async Task ValidateAsync(SaveLabWorkRequest r, Guid professional, CancellationToken ct)
    {
        if (!await db.Patients.AnyAsync(p => p.Id == r.PatientId, ct)) throw new RequestValidationException("patientId", "El paciente no existe.");
        if (!await db.Laboratories.AnyAsync(l => l.Id == r.LaboratoryId && l.IsActive, ct)) throw new RequestValidationException("laboratoryId", "El laboratorio no existe o está inactivo.");
        if (!await db.Members.AnyAsync(m => m.UserId == professional && m.IsActive && m.IsProfessional, ct))
            throw new RequestValidationException("professionalUserId", "El profesional no pertenece al consultorio.");
        if (r.TreatmentPlanItemId is { } tpi && !await db.TreatmentPlanItems.AnyAsync(i => i.Id == tpi && db.TreatmentPlans.Any(p => p.Id == i.TreatmentPlanId && p.PatientId == r.PatientId), ct))
            throw new RequestValidationException("treatmentPlanItemId", "La prestación no corresponde al paciente.");
    }

    private async Task<List<LabWorkDto>> ToDtosAsync(IReadOnlyList<LabWork> items, DateOnly today, CancellationToken ct)
    {
        var names = await UserNames.LoadAsync(db, items.Select(w => w.ProfessionalUserId), ct);
        return items.Select(w => new LabWorkDto(w.Id, w.PatientId, $"{w.Patient?.FirstName} {w.Patient?.LastName}", w.LaboratoryId, w.Laboratory?.Name ?? "?",
            w.ProfessionalUserId, names.GetValueOrDefault(w.ProfessionalUserId, "?"), w.WorkType, w.ToothCode, w.Description, w.Status, w.SentDate,
            w.EstimatedDeliveryDate, w.ActualDeliveryDate, w.PlacementDate, w.Cost, w.Notes, w.IsOverdue(today), w.TreatmentPlanItemId, w.CreatedAtUtc)).ToList();
    }

    private static LaboratoryDto ToDto(Domain.Entities.Laboratory l) => new(l.Id, l.Name, l.ContactName, l.Phone, l.Email, l.AddressLine, l.Notes, l.IsActive);
}
