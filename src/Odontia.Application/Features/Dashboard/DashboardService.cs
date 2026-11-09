using Microsoft.EntityFrameworkCore;
using Odontia.Application.Abstractions;
using Odontia.Application.Common;
using Odontia.Application.Features.Patients;
using Odontia.Domain.Authorization;
using Odontia.Domain.Entities;
using Odontia.Domain.Enums;

namespace Odontia.Application.Features.Dashboard;

public sealed record DashboardAppointmentDto(Guid Id, DateTime StartUtc, DateTime EndUtc, Guid PatientId, string PatientName, string Professional,
    string? Gabinete, string? Reason, AppointmentStatus Status);

public sealed record WaitingRoomItemDto(Guid AppointmentId, Guid PatientId, string PatientName, string Professional, string? Gabinete,
    DateTime? ArrivedAtUtc, int WaitingMinutes, AppointmentStatus Status);

public sealed record DashboardTaskDto(Guid Id, string Title, TaskPriority Priority, TaskCategory Category, DateTime? DueAtUtc, bool IsOverdue, string? AssignedTo);

public sealed record DashboardLabDto(Guid Id, string PatientName, string WorkType, int? ToothCode, string Laboratory, LabWorkStatus Status,
    DateOnly? EstimatedDeliveryDate, bool IsOverdue);

public sealed record ReceptionDashboardDto(DateOnly Date, int AppointmentsToday, int ConfirmedToday, int PendingConfirmationToday,
    int PendingConfirmationNext48h, int InWaitingRoom, int AverageWaitMinutes, IReadOnlyList<WaitingRoomItemDto> WaitingRoom,
    IReadOnlyList<DashboardAppointmentDto> TodayAgenda, int OpenTasks, int TasksDueToday, int OverdueTasks,
    IReadOnlyList<DashboardTaskDto> PendingTasks, IReadOnlyList<DashboardLabDto> UpcomingLabDeliveries);

public sealed record PatientAlertSummaryDto(Guid PatientId, string PatientName, IReadOnlyList<AlertDto> Alerts);

public sealed record ActivityItemDto(DateTime AtUtc, string Kind, string Description, Guid? PatientId, string? PatientName);

public sealed record WeeklyActivityDto(DateOnly Date, int Attended);

public sealed record InRoomPatientDto(Guid AppointmentId, Guid PatientId, string PatientName, int? Age, string? Reason, string? Gabinete,
    DateTime? LastVisitUtc, IReadOnlyList<string> Alerts);

public sealed record ClinicalDashboardDto(DateOnly Date, int PatientsToday, int AttendedToday, int RemainingToday,
    DashboardAppointmentDto? NextAppointment, IReadOnlyList<DashboardAppointmentDto> MyAppointmentsToday, InRoomPatientDto? InRoom,
    int TreatmentsInProgress, int TreatmentsNeedingFollowUp, int OpenLabWorks, int LabWorksReadyForPickup,
    IReadOnlyList<PatientAlertSummaryDto> MedicalAlerts, IReadOnlyList<ActivityItemDto> RecentActivity, IReadOnlyList<WeeklyActivityDto> WeeklyActivity);

public interface IDashboardService
{
    Task<ReceptionDashboardDto> GetReceptionAsync(CancellationToken ct);
    Task<ClinicalDashboardDto> GetClinicalAsync(CancellationToken ct);
}

internal sealed class DashboardService(IApplicationDbContext db, ITenantContext tenant, IConsultorioClock clock, IPatientService patients) : IDashboardService
{
    public async Task<ReceptionDashboardDto> GetReceptionAsync(CancellationToken ct)
    {
        tenant.Require(Permissions.DashboardReception);
        var now = clock.UtcNow;
        var tz = await clock.GetTimeZoneAsync(ct);
        var today = await clock.TodayAsync(ct);
        var (start, end) = tz.DayRangeUtc(today, today);
        var all = tenant.Has(Permissions.AgendaAllProfessionals);
        var uid = tenant.UserId;

        var appts = await db.Appointments.AsNoTracking().Include(a => a.Patient).Include(a => a.Gabinete)
            .Where(a => a.StartUtc >= start && a.StartUtc < end && (all || a.ProfessionalUserId == uid))
            .OrderBy(a => a.StartUtc).ToListAsync(ct);
        var names = await UserNames.LoadAsync(db, appts.Select(a => a.ProfessionalUserId), ct);
        var active = appts.Where(a => a.IsActive).ToList();
        var waiting = active.Where(a => a.Status is AppointmentStatus.Arrived or AppointmentStatus.InWaitingRoom).OrderBy(a => a.ArrivedAtUtc).ToList();
        var waitItems = waiting.Select(a => new WaitingRoomItemDto(a.Id, a.PatientId, FullName(a.Patient), names.GetValueOrDefault(a.ProfessionalUserId, "?"),
            a.Gabinete?.Name, a.ArrivedAtUtc, a.ArrivedAtUtc is null ? 0 : Math.Max(0, (int)(now - a.ArrivedAtUtc.Value).TotalMinutes), a.Status)).ToList();

        var next48 = now.AddHours(48);
        var pending48 = await db.Appointments.CountAsync(a => a.StartUtc >= now && a.StartUtc < next48 && a.Status == AppointmentStatus.Pending
                                                              && (all || a.ProfessionalUserId == uid), ct);

        var openTasks = await db.Tasks.AsNoTracking().Where(t => t.Status == TaskItemStatus.Pending || t.Status == TaskItemStatus.InProgress)
            .OrderBy(t => t.DueAtUtc == null).ThenBy(t => t.DueAtUtc).ToListAsync(ct);
        var taskNames = await UserNames.LoadAsync(db, openTasks.Where(t => t.AssignedToUserId != null).Select(t => t.AssignedToUserId!.Value), ct);

        IReadOnlyList<DashboardLabDto> labs = [];
        if (tenant.Has(Permissions.LabRead))
        {
            var horizon = today.AddDays(7);
            var labRows = await db.LabWorks.AsNoTracking().Include(l => l.Patient).Include(l => l.Laboratory)
                .Where(l => l.Status == LabWorkStatus.ReadyForPickup
                            || ((l.Status == LabWorkStatus.Sent || l.Status == LabWorkStatus.InProduction || l.Status == LabWorkStatus.Pending)
                                && l.EstimatedDeliveryDate <= horizon))
                .OrderBy(l => l.EstimatedDeliveryDate).Take(10).ToListAsync(ct);
            labs = labRows.Select(l => new DashboardLabDto(l.Id, FullName(l.Patient), l.WorkType, l.ToothCode, l.Laboratory?.Name ?? "?", l.Status,
                l.EstimatedDeliveryDate, l.IsOverdue(today))).ToList();
        }

        return new ReceptionDashboardDto(today, active.Count, active.Count(a => a.Status == AppointmentStatus.Confirmed),
            active.Count(a => a.Status == AppointmentStatus.Pending), pending48, waiting.Count,
            waitItems.Count == 0 ? 0 : (int)waitItems.Average(w => w.WaitingMinutes), waitItems,
            appts.Select(a => ToDashboard(a, names)).ToList(),
            openTasks.Count, openTasks.Count(t => t.DueAtUtc >= start && t.DueAtUtc < end), openTasks.Count(t => t.IsOverdue(now)),
            openTasks.Take(8).Select(t => new DashboardTaskDto(t.Id, t.Title, t.Priority, t.Category, t.DueAtUtc, t.IsOverdue(now),
                t.AssignedToUserId is null ? null : taskNames.GetValueOrDefault(t.AssignedToUserId.Value))).ToList(),
            labs);
    }

    public async Task<ClinicalDashboardDto> GetClinicalAsync(CancellationToken ct)
    {
        tenant.Require(Permissions.DashboardClinical);
        var uid = tenant.UserId;
        var now = clock.UtcNow;
        var tz = await clock.GetTimeZoneAsync(ct);
        var today = await clock.TodayAsync(ct);
        var (start, end) = tz.DayRangeUtc(today, today);

        var mine = await db.Appointments.AsNoTracking().Include(a => a.Patient).Include(a => a.Gabinete)
            .Where(a => a.ProfessionalUserId == uid && a.StartUtc >= start && a.StartUtc < end)
            .OrderBy(a => a.StartUtc).ToListAsync(ct);
        var names = await UserNames.LoadAsync(db, [uid], ct);
        var active = mine.Where(a => a.IsActive).ToList();
        var next = await db.Appointments.AsNoTracking().Include(a => a.Patient).Include(a => a.Gabinete)
            .Where(a => a.ProfessionalUserId == uid && a.StartUtc >= now && (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed))
            .OrderBy(a => a.StartUtc).FirstOrDefaultAsync(ct);

        InRoomPatientDto? inRoom = null;
        var room = active.Where(a => a.Status is AppointmentStatus.InProgress or AppointmentStatus.InWaitingRoom or AppointmentStatus.Arrived)
            .OrderByDescending(a => a.Status == AppointmentStatus.InProgress).ThenBy(a => a.ArrivedAtUtc).FirstOrDefault();
        if (room is not null)
        {
            var lastVisit = await db.Appointments.Where(a => a.PatientId == room.PatientId && a.Status == AppointmentStatus.Attended && a.StartUtc < start)
                .MaxAsync(a => (DateTime?)a.StartUtc, ct);
            var roomAlerts = await patients.GetAlertsAsync(room.PatientId, ct);
            inRoom = new InRoomPatientDto(room.Id, room.PatientId, FullName(room.Patient), room.Patient?.AgeAt(today), room.Reason, room.Gabinete?.Name,
                lastVisit, roomAlerts.Select(a => a.Title).ToList());
        }

        var plans = await db.TreatmentPlans.AsNoTracking().Include(p => p.Items)
            .Where(p => p.ProfessionalUserId == uid && p.Status == TreatmentStatus.InProgress).ToListAsync(ct);
        var followUpThreshold = now.AddDays(-21);
        var planIds = plans.Select(p => p.Id).ToList();
        var lastChanges = await db.TreatmentChanges.AsNoTracking().Where(c => planIds.Contains(c.TreatmentPlanId))
            .GroupBy(c => c.TreatmentPlanId).Select(g => new { g.Key, Last = g.Max(c => c.ChangedAtUtc) }).ToListAsync(ct);
        var needFollowUp = lastChanges.Count(c => c.Last < followUpThreshold);

        var openLab = await db.LabWorks.CountAsync(l => l.ProfessionalUserId == uid && l.Status != LabWorkStatus.Placed && l.Status != LabWorkStatus.Cancelled, ct);
        var readyLab = await db.LabWorks.CountAsync(l => l.ProfessionalUserId == uid && (l.Status == LabWorkStatus.ReadyForPickup || l.Status == LabWorkStatus.Received), ct);

        var alerts = new List<PatientAlertSummaryDto>();
        foreach (var p in active.Select(a => a.Patient!).DistinctBy(p => p.Id))
        {
            var list = await patients.GetAlertsAsync(p.Id, ct);
            if (list.Count > 0) alerts.Add(new PatientAlertSummaryDto(p.Id, FullName(p), list));
        }

        var activity = new List<ActivityItemDto>();
        if (tenant.Has(Permissions.ClinicalRead))
        {
            var entries = await db.ClinicalEntries.AsNoTracking().Where(e => e.ProfessionalUserId == uid && !e.IsVoided)
                .OrderByDescending(e => e.CreatedAtUtc).Take(8)
                .Select(e => new { e.CreatedAtUtc, e.Title, e.PatientId, e.Type }).ToListAsync(ct);
            var pIds = entries.Select(e => e.PatientId).Distinct().ToList();
            var pNames = await db.Patients.AsNoTracking().Where(p => pIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.FirstName + " " + p.LastName, ct);
            activity.AddRange(entries.Select(e => new ActivityItemDto(e.CreatedAtUtc, e.Type.ToString(), e.Title, e.PatientId, pNames.GetValueOrDefault(e.PatientId))));
        }

        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var (weekStart, weekEnd) = tz.DayRangeUtc(monday, monday.AddDays(5));
        var attended = await db.Appointments.AsNoTracking()
            .Where(a => a.ProfessionalUserId == uid && a.Status == AppointmentStatus.Attended && a.StartUtc >= weekStart && a.StartUtc < weekEnd)
            .Select(a => a.StartUtc).ToListAsync(ct);
        var weekly = Enumerable.Range(0, 6).Select(i => monday.AddDays(i))
            .Select(d => new WeeklyActivityDto(d, attended.Count(s => DateOnly.FromDateTime(tz.UtcToLocal(s)) == d))).ToList();

        return new ClinicalDashboardDto(today, active.Count, active.Count(a => a.Status == AppointmentStatus.Attended),
            active.Count(a => a.Status != AppointmentStatus.Attended), next is null ? null : ToDashboard(next, names),
            mine.Select(a => ToDashboard(a, names)).ToList(), inRoom, plans.Count, needFollowUp, openLab, readyLab, alerts,
            activity.OrderByDescending(a => a.AtUtc).ToList(), weekly);
    }

    private static string FullName(Patient? p) => p is null ? "?" : $"{p.FirstName} {p.LastName}";

    private static DashboardAppointmentDto ToDashboard(Appointment a, Dictionary<Guid, string> names) => new(a.Id, a.StartUtc, a.EndUtc, a.PatientId,
        FullName(a.Patient), names.GetValueOrDefault(a.ProfessionalUserId, "?"), a.Gabinete?.Name, a.Reason, a.Status);
}
