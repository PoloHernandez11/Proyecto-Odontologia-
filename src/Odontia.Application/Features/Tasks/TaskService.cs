using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Odontia.Application.Abstractions;
using Odontia.Application.Common;
using Odontia.Application.Features.Notifications;
using Odontia.Application.Features.Patients;
using Odontia.Domain.Authorization;
using Odontia.Domain.Entities;
using Odontia.Domain.Enums;

namespace Odontia.Application.Features.Tasks;

public sealed record SaveTaskRequest(string Title, string? Description, TaskPriority Priority, TaskCategory Category,
    Guid? AssignedToUserId, DateTimeOffset? DueAt, Guid? PatientId, Guid? AppointmentId);

public sealed record AssignTaskRequest(Guid? AssignedToUserId);

public sealed record ChangeTaskStatusRequest(TaskItemStatus Status);

public sealed record TaskDto(Guid Id, string Title, string? Description, TaskPriority Priority, TaskCategory Category, TaskItemStatus Status,
    Guid? AssignedToUserId, string? AssignedToName, DateTime? DueAtUtc, bool IsOverdue, Guid? PatientId, string? PatientName,
    Guid? AppointmentId, DateTime? CompletedAtUtc, string? CompletedBy, DateTime CreatedAtUtc, string? CreatedBy);

public sealed class TaskQuery : PageQuery
{
    public TaskItemStatus? Status { get; set; }
    /// <summary>open | overdue | today | upcoming | completed</summary>
    public string? View { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public bool Mine { get; set; }
    public TaskPriority? Priority { get; set; }
    public TaskCategory? Category { get; set; }
    public Guid? PatientId { get; set; }
}

public interface ITaskService
{
    Task<PagedResult<TaskDto>> SearchAsync(TaskQuery query, CancellationToken ct);
    Task<TaskDto> GetAsync(Guid id, CancellationToken ct);
    Task<TaskDto> CreateAsync(SaveTaskRequest request, CancellationToken ct);
    Task<TaskDto> UpdateAsync(Guid id, SaveTaskRequest request, CancellationToken ct);
    Task<TaskDto> AssignAsync(Guid id, AssignTaskRequest request, CancellationToken ct);
    Task<TaskDto> ChangeStatusAsync(Guid id, TaskItemStatus status, CancellationToken ct);
    /// <summary>Trabajo en segundo plano: alerta a los responsables de tareas que vencen en la próxima hora o ya vencieron.</summary>
    Task<int> NotifyDueTasksAsync(CancellationToken ct);
}

internal sealed class TaskService(IApplicationDbContext db, ITenantContext tenant, IConsultorioClock clock, INotificationService notifications) : ITaskService
{
    private static readonly IReadOnlyDictionary<string, Expression<Func<TaskItem, object?>>> Sorts = new Dictionary<string, Expression<Func<TaskItem, object?>>>
    {
        ["due"] = t => t.DueAtUtc,
        ["priority"] = t => t.Priority,
        ["created"] = t => t.CreatedAtUtc,
        ["status"] = t => t.Status,
    };

    public async Task<PagedResult<TaskDto>> SearchAsync(TaskQuery q, CancellationToken ct)
    {
        tenant.Require(Permissions.TasksRead);
        var now = clock.UtcNow;
        var tz = await clock.GetTimeZoneAsync(ct);
        var today = await clock.TodayAsync(ct);
        var (dayStart, dayEnd) = tz.DayRangeUtc(today, today);
        var query = db.Tasks.AsNoTracking().AsQueryable();
        if (q.Status is not null) query = query.Where(t => t.Status == q.Status);
        if (q.Priority is not null) query = query.Where(t => t.Priority == q.Priority);
        if (q.Category is not null) query = query.Where(t => t.Category == q.Category);
        if (q.PatientId is not null) query = query.Where(t => t.PatientId == q.PatientId);
        if (q.Mine) { var uid = tenant.UserId; query = query.Where(t => t.AssignedToUserId == uid); }
        else if (q.AssignedToUserId is not null) query = query.Where(t => t.AssignedToUserId == q.AssignedToUserId);

        query = q.View?.ToLowerInvariant() switch
        {
            "open" => query.Where(t => t.Status == TaskItemStatus.Pending || t.Status == TaskItemStatus.InProgress),
            "overdue" => query.Where(t => (t.Status == TaskItemStatus.Pending || t.Status == TaskItemStatus.InProgress) && t.DueAtUtc < now),
            "today" => query.Where(t => (t.Status == TaskItemStatus.Pending || t.Status == TaskItemStatus.InProgress) && t.DueAtUtc >= dayStart && t.DueAtUtc < dayEnd),
            "upcoming" => query.Where(t => (t.Status == TaskItemStatus.Pending || t.Status == TaskItemStatus.InProgress) && (t.DueAtUtc >= dayEnd || t.DueAtUtc == null)),
            "completed" => query.Where(t => t.Status == TaskItemStatus.Completed),
            _ => query,
        };

        var page = await query.ApplySort(q.Sort, Sorts, "due").ToPagedAsync(q, ct);
        return new PagedResult<TaskDto>(await ToDtosAsync(page.Items, ct), page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<TaskDto> GetAsync(Guid id, CancellationToken ct)
    {
        tenant.Require(Permissions.TasksRead);
        return (await ToDtosAsync([await LoadAsync(id, ct)], ct))[0];
    }

    public async Task<TaskDto> CreateAsync(SaveTaskRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.TasksWrite);
        await ValidateReferencesAsync(r, ct);
        var t = new TaskItem(tenant.ConsultorioId, r.Title, r.Description, r.Priority, r.Category, r.AssignedToUserId,
            r.DueAt?.UtcDateTime, r.PatientId, r.AppointmentId);
        db.Tasks.Add(t);
        await db.SaveChangesAsync(ct);
        await NotifyAssigneeAsync(t, ct);
        return await GetAsync(t.Id, ct);
    }

    public async Task<TaskDto> UpdateAsync(Guid id, SaveTaskRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.TasksWrite);
        var t = await LoadAsync(id, ct);
        await ValidateReferencesAsync(r, ct);
        var reassigned = t.AssignedToUserId != r.AssignedToUserId;
        t.Update(r.Title, r.Description, r.Priority, r.Category, r.DueAt?.UtcDateTime, r.PatientId, r.AppointmentId);
        t.Assign(r.AssignedToUserId);
        await db.SaveChangesAsync(ct);
        if (reassigned) await NotifyAssigneeAsync(t, ct);
        return await GetAsync(id, ct);
    }

    public async Task<TaskDto> AssignAsync(Guid id, AssignTaskRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.TasksWrite);
        var t = await LoadAsync(id, ct);
        if (r.AssignedToUserId is { } uid) await EnsureMemberAsync(uid, ct);
        t.Assign(r.AssignedToUserId);
        await db.SaveChangesAsync(ct);
        await NotifyAssigneeAsync(t, ct);
        return await GetAsync(id, ct);
    }

    public async Task<TaskDto> ChangeStatusAsync(Guid id, TaskItemStatus status, CancellationToken ct)
    {
        tenant.Require(Permissions.TasksWrite);
        var t = await LoadAsync(id, ct);
        t.SetStatus(status, tenant.UserId, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<int> NotifyDueTasksAsync(CancellationToken ct)
    {
        if (!tenant.IsBackgroundJob) tenant.Require(Permissions.TasksWrite);
        var limit = clock.UtcNow.AddHours(1);
        var due = await db.Tasks.Where(t => (t.Status == TaskItemStatus.Pending || t.Status == TaskItemStatus.InProgress)
                                           && t.AssignedToUserId != null && t.DueAtUtc != null && t.DueAtUtc <= limit && t.DueNotifiedAtUtc == null)
            .Take(500).ToListAsync(ct);
        foreach (var t in due)
        {
            t.MarkDueNotified(clock.UtcNow);
            await notifications.NotifyAsync(t.AssignedToUserId!.Value, NotificationType.TaskDue,
                t.IsOverdue(clock.UtcNow) ? "Tarea vencida" : "Tarea por vencer", t.Title, nameof(TaskItem), t.Id, ct);
        }
        await db.SaveChangesAsync(ct);
        return due.Count;
    }

    private async Task ValidateReferencesAsync(SaveTaskRequest r, CancellationToken ct)
    {
        if (r.AssignedToUserId is { } uid) await EnsureMemberAsync(uid, ct);
        if (r.PatientId is { } pid && !await db.Patients.AnyAsync(p => p.Id == pid, ct))
            throw new RequestValidationException("patientId", "El paciente no existe.");
        if (r.AppointmentId is { } aid)
        {
            var appt = await db.Appointments.Where(a => a.Id == aid).Select(a => new { a.PatientId }).FirstOrDefaultAsync(ct)
                       ?? throw new RequestValidationException("appointmentId", "El turno no existe.");
            if (r.PatientId is not null && appt.PatientId != r.PatientId)
                throw new RequestValidationException("appointmentId", "El turno no corresponde al paciente indicado.");
        }
    }

    private async Task EnsureMemberAsync(Guid userId, CancellationToken ct)
    {
        if (!await db.Members.AnyAsync(m => m.UserId == userId && m.IsActive, ct))
            throw new RequestValidationException("assignedToUserId", "El responsable no pertenece al consultorio.");
    }

    private async Task NotifyAssigneeAsync(TaskItem t, CancellationToken ct)
    {
        if (t.AssignedToUserId is not { } uid || uid == tenant.UserId) return;
        await notifications.NotifyAsync(uid, NotificationType.TaskAssigned, "Nueva tarea asignada", t.Title, nameof(TaskItem), t.Id, ct);
    }

    private async Task<TaskItem> LoadAsync(Guid id, CancellationToken ct) =>
        await db.Tasks.FirstOrDefaultAsync(t => t.Id == id, ct) ?? throw new NotFoundException("La tarea no existe.");

    private async Task<List<TaskDto>> ToDtosAsync(IReadOnlyList<TaskItem> items, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var userIds = items.SelectMany(t => new[] { t.AssignedToUserId, t.CompletedByUserId, t.CreatedByUserId }).Where(x => x != null).Select(x => x!.Value);
        var names = await UserNames.LoadAsync(db, userIds, ct);
        var patientIds = items.Where(t => t.PatientId != null).Select(t => t.PatientId!.Value).Distinct().ToList();
        var patientNames = await db.Patients.AsNoTracking().Where(p => patientIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.FirstName + " " + p.LastName, ct);
        string? N(Guid? id) => id is null ? null : names.GetValueOrDefault(id.Value);
        return items.Select(t => new TaskDto(t.Id, t.Title, t.Description, t.Priority, t.Category, t.Status, t.AssignedToUserId, N(t.AssignedToUserId),
            t.DueAtUtc, t.IsOverdue(now), t.PatientId, t.PatientId is null ? null : patientNames.GetValueOrDefault(t.PatientId.Value),
            t.AppointmentId, t.CompletedAtUtc, N(t.CompletedByUserId), t.CreatedAtUtc, N(t.CreatedByUserId))).ToList();
    }
}
