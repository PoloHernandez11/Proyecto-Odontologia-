using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Odontia.Application.Abstractions;
using Odontia.Application.Common;
using Odontia.Application.Features.Notifications;
using Odontia.Application.Features.Patients;
using Odontia.Domain.Authorization;
using Odontia.Domain.Entities;
using Odontia.Domain.Enums;

namespace Odontia.Application.Features.Scheduling;

public interface IAppointmentService
{
    Task<PagedResult<AppointmentDto>> SearchAsync(AppointmentQuery query, CancellationToken ct);
    Task<AppointmentStatusCountsDto> GetStatusCountsAsync(DateTimeOffset from, DateTimeOffset to, Guid? professionalUserId, CancellationToken ct);
    Task<AppointmentDto> GetAsync(Guid id, CancellationToken ct);
    Task<AppointmentDto> CreateAsync(CreateAppointmentRequest request, CancellationToken ct);
    Task<AppointmentDto> UpdateAsync(Guid id, UpdateAppointmentRequest request, CancellationToken ct);
    Task<AppointmentDto> RescheduleAsync(Guid id, RescheduleAppointmentRequest request, CancellationToken ct);
    Task<AppointmentDto> ChangeStatusAsync(Guid id, ChangeAppointmentStatusRequest request, CancellationToken ct);
    Task<IReadOnlyList<AppointmentHistoryDto>> GetHistoryAsync(Guid id, CancellationToken ct);
}

internal sealed class AppointmentService(
    IApplicationDbContext db,
    ITenantContext tenant,
    IConsultorioClock clock,
    IAvailabilityReader availability,
    IAgendaLock agendaLock,
    INotificationService notifications,
    IPatientService patients) : IAppointmentService
{
    private static readonly IReadOnlyDictionary<string, Expression<Func<Appointment, object?>>> Sorts = new Dictionary<string, Expression<Func<Appointment, object?>>>
    {
        ["start"] = a => a.StartUtc,
        ["status"] = a => a.Status,
        ["patient"] = a => a.Patient!.SearchName,
        ["created"] = a => a.CreatedAtUtc,
    };

    public async Task<PagedResult<AppointmentDto>> SearchAsync(AppointmentQuery q, CancellationToken ct)
    {
        tenant.Require(Permissions.AppointmentsRead);
        var query = ScopedQuery(q.ProfessionalUserId);
        if (q.From is not null) query = query.Where(a => a.EndUtc > q.From.Value.UtcDateTime);
        if (q.To is not null) query = query.Where(a => a.StartUtc < q.To.Value.UtcDateTime);
        if (q.Status is not null) query = query.Where(a => a.Status == q.Status);
        if (q.PatientId is not null) query = query.Where(a => a.PatientId == q.PatientId);
        if (q.GabineteId is not null) query = query.Where(a => a.GabineteId == q.GabineteId);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var patientIds = PatientService.ApplySearch(db.Patients, q.Search).Select(p => p.Id);
            query = query.Where(a => patientIds.Contains(a.PatientId));
        }
        var page = await query.Include(a => a.Patient).ThenInclude(p => p!.HealthInsurance).Include(a => a.Gabinete)
            .ApplySort(q.Sort, Sorts, "start").ToPagedAsync(q, ct);
        var dtos = await ToDtosAsync(page.Items, includeAlerts: false, ct);
        return new PagedResult<AppointmentDto>(dtos, page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<AppointmentStatusCountsDto> GetStatusCountsAsync(DateTimeOffset from, DateTimeOffset to, Guid? professionalUserId, CancellationToken ct)
    {
        tenant.Require(Permissions.AppointmentsRead);
        var f = from.UtcDateTime;
        var t = to.UtcDateTime;
        var counts = await ScopedQuery(professionalUserId).Where(a => a.StartUtc >= f && a.StartUtc < t)
            .GroupBy(a => a.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        return new AppointmentStatusCountsDto(counts.Sum(c => c.Count), counts.ToDictionary(c => c.Key.ToString(), c => c.Count));
    }

    public async Task<AppointmentDto> GetAsync(Guid id, CancellationToken ct)
    {
        tenant.Require(Permissions.AppointmentsRead);
        var a = await LoadAsync(id, ct);
        return (await ToDtosAsync([a], includeAlerts: true, ct))[0];
    }

    public async Task<AppointmentDto> CreateAsync(CreateAppointmentRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.AppointmentsWrite);
        EnsureScope(r.ProfessionalUserId);
        if (r.IsOverbooking) tenant.Require(Permissions.AppointmentsOverbook);
        await EnsureReferencesAsync(r.PatientId, r.ProfessionalUserId, r.GabineteId, ct);

        var duration = r.DurationMinutes ?? await availability.DefaultDurationAsync(r.ProfessionalUserId, ct);
        var start = r.Start.UtcDateTime;
        var end = start.AddMinutes(duration);
        if (start < clock.UtcNow.AddMinutes(-5)) throw new BusinessRuleException("No pueden crearse turnos en el pasado.", "appointment_in_past");

        // La disponibilidad se revalida dentro de una transacción con bloqueo para evitar reservas simultáneas.
        await using var tx = await agendaLock.AcquireAsync(r.ProfessionalUserId, r.GabineteId, ct);
        await ValidateSlotAsync(null, r.PatientId, r.ProfessionalUserId, r.GabineteId, start, end, r.IsOverbooking, ct);

        var appt = new Appointment(tenant.ConsultorioId, r.PatientId, r.ProfessionalUserId, r.GabineteId, start, end, r.Reason, r.AdministrativeNotes, r.IsOverbooking);
        if (r.Confirmed) appt.TransitionTo(AppointmentStatus.Confirmed, clock.UtcNow);
        db.Appointments.Add(appt);
        AddHistory(appt, "created", null, appt.Status, $"{start:O} ({duration} min)");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await NotifyProfessionalAsync(appt, "Nuevo turno asignado", ct);
        return await GetAsync(appt.Id, ct);
    }

    public async Task<AppointmentDto> UpdateAsync(Guid id, UpdateAppointmentRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.AppointmentsWrite);
        var a = await LoadAsync(id, ct);
        EnsureVersion(a, r.Version);
        if (r.PatientId != a.PatientId)
        {
            await EnsureReferencesAsync(r.PatientId, a.ProfessionalUserId, null, ct);
            await using var tx = await agendaLock.AcquireAsync(a.ProfessionalUserId, a.GabineteId, ct);
            await EnsureNoPatientConflictAsync(a.Id, r.PatientId, a.StartUtc, a.EndUtc, ct);
            a.ChangePatient(r.PatientId);
            a.SetDetails(r.Reason, r.AdministrativeNotes);
            AddHistory(a, "patient_changed", null, null, null);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        else
        {
            a.SetDetails(r.Reason, r.AdministrativeNotes);
            AddHistory(a, "updated", null, null, "Motivo/observaciones actualizados");
            await SaveAsync(ct);
        }
        return await GetAsync(id, ct);
    }

    public async Task<AppointmentDto> RescheduleAsync(Guid id, RescheduleAppointmentRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.AppointmentsWrite);
        var a = await LoadAsync(id, ct);
        EnsureVersion(a, r.Version);
        var professional = r.ProfessionalUserId ?? a.ProfessionalUserId;
        EnsureScope(professional);
        if (r.IsOverbooking) tenant.Require(Permissions.AppointmentsOverbook);
        if (professional != a.ProfessionalUserId || r.GabineteId != a.GabineteId)
            await EnsureReferencesAsync(a.PatientId, professional, r.GabineteId, ct);

        var duration = r.DurationMinutes ?? a.DurationMinutes;
        var start = r.Start.UtcDateTime;
        var end = start.AddMinutes(duration);
        if (start < clock.UtcNow.AddMinutes(-5)) throw new BusinessRuleException("No puede reprogramarse a un horario pasado.", "appointment_in_past");

        await using var tx = await agendaLock.AcquireAsync(professional, r.GabineteId, ct);
        await ValidateSlotAsync(a.Id, a.PatientId, professional, r.GabineteId, start, end, r.IsOverbooking, ct);
        var before = $"{a.StartUtc:O} / {a.ProfessionalUserId}";
        var fromStatus = a.Status;
        a.Reschedule(professional, r.GabineteId, start, end, r.IsOverbooking);
        AddHistory(a, "rescheduled", fromStatus, a.Status, $"{before} → {start:O} / {professional}{(string.IsNullOrWhiteSpace(r.Reason) ? "" : $". Motivo: {r.Reason}")}");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await NotifyProfessionalAsync(a, "Turno reprogramado", ct);
        return await GetAsync(id, ct);
    }

    public async Task<AppointmentDto> ChangeStatusAsync(Guid id, ChangeAppointmentStatusRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.AppointmentsWrite);
        var a = await LoadAsync(id, ct);
        var from = a.Status;
        a.TransitionTo(r.Status, clock.UtcNow, r.Reason);
        AddHistory(a, "status_changed", from, a.Status, r.Reason);
        await SaveAsync(ct);
        if (r.Status == AppointmentStatus.Cancelled) await NotifyProfessionalAsync(a, "Turno cancelado", ct);
        return await GetAsync(id, ct);
    }

    public async Task<IReadOnlyList<AppointmentHistoryDto>> GetHistoryAsync(Guid id, CancellationToken ct)
    {
        tenant.Require(Permissions.AppointmentsRead);
        await LoadAsync(id, ct);
        var rows = await db.AppointmentHistories.AsNoTracking().Where(h => h.AppointmentId == id).OrderBy(h => h.ChangedAtUtc).ToListAsync(ct);
        var names = await UserNames.LoadAsync(db, rows.Where(h => h.ChangedByUserId != null).Select(h => h.ChangedByUserId!.Value), ct);
        return rows.Select(h => new AppointmentHistoryDto(h.ChangedAtUtc, h.Action, h.FromStatus, h.ToStatus, h.Details,
            h.ChangedByUserId is null ? null : names.GetValueOrDefault(h.ChangedByUserId.Value))).ToList();
    }

    // ---------------- Reglas de reserva ----------------

    /// <summary>
    /// Reglas: dentro del horario disponible del profesional (horario laboral + excepciones − bloqueos),
    /// sin superposición con otros turnos del profesional o del consultorio físico (salvo sobreturno autorizado)
    /// y sin turnos superpuestos del mismo paciente (evita duplicados).
    /// </summary>
    private async Task ValidateSlotAsync(Guid? excludeId, Guid patientId, Guid professional, Guid? gabinete, DateTime start, DateTime end,
        bool overbooking, CancellationToken ct)
    {
        var tz = await clock.GetTimeZoneAsync(ct);
        var localStart = tz.UtcToLocal(start);
        var localEnd = tz.UtcToLocal(end);
        var date = DateOnly.FromDateTime(localStart);
        if (DateOnly.FromDateTime(localEnd.AddTicks(-1)) != date)
            throw new BusinessRuleException("El turno debe comenzar y terminar el mismo día.", "appointment_spans_days");

        var input = await availability.BuildInputAsync(date, professional, gabinete, excludeId, ct);
        var requested = new TimeRange(start, end);
        if (!AvailabilityCalculator.IsWithinWorkingHours(input, requested))
            throw new BusinessRuleException("El horario solicitado está fuera de la disponibilidad del profesional.", "outside_availability");

        if (!overbooking && input.BusyRanges.Any(b => b.Overlaps(requested)))
            throw new ConflictException("El horario ya está ocupado por otro turno.", "slot_taken");

        await EnsureNoPatientConflictAsync(excludeId, patientId, start, end, ct);
    }

    private async Task EnsureNoPatientConflictAsync(Guid? excludeId, Guid patientId, DateTime start, DateTime end, CancellationToken ct)
    {
        var conflict = await db.Appointments.AnyAsync(a => a.PatientId == patientId && a.Id != excludeId
            && a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.NoShow
            && a.StartUtc < end && a.EndUtc > start, ct);
        if (conflict) throw new ConflictException("El paciente ya tiene un turno en ese horario.", "patient_double_booking");
    }

    private async Task EnsureReferencesAsync(Guid patientId, Guid professionalUserId, Guid? gabineteId, CancellationToken ct)
    {
        if (!await db.Patients.AnyAsync(p => p.Id == patientId && p.Status != PatientStatus.Inactive, ct))
            throw new RequestValidationException("patientId", "El paciente no existe o está inactivo.");
        if (!await db.Members.AnyAsync(m => m.UserId == professionalUserId && m.IsActive && m.IsProfessional && m.User!.IsActive, ct))
            throw new RequestValidationException("professionalUserId", "El profesional no existe o no está activo en el consultorio.");
        if (gabineteId is { } g && !await db.Gabinetes.AnyAsync(x => x.Id == g && x.IsActive, ct))
            throw new RequestValidationException("gabineteId", "El consultorio físico no existe o está inactivo.");
    }

    private void EnsureScope(Guid professionalUserId)
    {
        if (!tenant.CanActOnProfessional(professionalUserId))
            throw new ForbiddenAccessException("Sólo puede gestionar turnos de su propia agenda.");
    }

    private static void EnsureVersion(Appointment a, Guid version)
    {
        if (a.Version != version)
            throw new ConflictException("El turno fue modificado por otro usuario. Actualice y reintente.", "concurrency_conflict");
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("El turno fue modificado por otro usuario. Actualice y reintente.", "concurrency_conflict");
        }
    }

    private IQueryable<Appointment> ScopedQuery(Guid? professionalFilter)
    {
        var query = db.Appointments.AsNoTracking().AsQueryable();
        if (!tenant.Has(Permissions.AgendaAllProfessionals))
        {
            if (professionalFilter is not null && professionalFilter != tenant.UserId)
                throw new ForbiddenAccessException("Sólo puede consultar su propia agenda.");
            var uid = tenant.UserId;
            return query.Where(a => a.ProfessionalUserId == uid);
        }
        return professionalFilter is null ? query : query.Where(a => a.ProfessionalUserId == professionalFilter);
    }

    private async Task<Appointment> LoadAsync(Guid id, CancellationToken ct)
    {
        var a = await db.Appointments.Include(x => x.Patient).ThenInclude(p => p!.HealthInsurance).Include(x => x.Gabinete)
                    .FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("El turno no existe.");
        if (!tenant.CanActOnProfessional(a.ProfessionalUserId)) throw new NotFoundException("El turno no existe.");
        return a;
    }

    private void AddHistory(Appointment a, string action, AppointmentStatus? from, AppointmentStatus? to, string? details) =>
        db.AppointmentHistories.Add(new AppointmentHistory(tenant.ConsultorioId, a.Id, action, from, to, details,
            tenant.IsAuthenticated ? tenant.UserId : null, clock.UtcNow));

    private async Task NotifyProfessionalAsync(Appointment a, string title, CancellationToken ct)
    {
        if (!tenant.IsAuthenticated || a.ProfessionalUserId == tenant.UserId) return;
        var tz = await clock.GetTimeZoneAsync(ct);
        await notifications.NotifyAsync(a.ProfessionalUserId, NotificationType.AppointmentChanged, title,
            $"{tz.UtcToLocal(a.StartUtc):dd/MM HH:mm} - {a.Patient?.FirstName} {a.Patient?.LastName}", nameof(Appointment), a.Id, ct);
    }

    private async Task<List<AppointmentDto>> ToDtosAsync(IReadOnlyList<Appointment> items, bool includeAlerts, CancellationToken ct)
    {
        var names = await UserNames.LoadAsync(db, items.Select(a => a.ProfessionalUserId), ct);
        var today = await clock.TodayAsync(ct);
        var result = new List<AppointmentDto>();
        foreach (var a in items)
        {
            IReadOnlyList<string> alerts = includeAlerts && (tenant.Has(Permissions.PatientsAlertsRead) || tenant.Has(Permissions.PatientsMedicalRead))
                ? (await patients.GetAlertsAsync(a.PatientId, ct)).Select(x => x.Title).ToList()
                : [];
            var p = a.Patient!;
            result.Add(new AppointmentDto(a.Id, a.PatientId, $"{p.FirstName} {p.LastName}", p.DocumentNumber, p.Phone, p.AgeAt(today),
                p.HealthInsurance is null ? null : $"{p.HealthInsurance.Name} {p.InsurancePlan}".Trim(),
                a.ProfessionalUserId, names.GetValueOrDefault(a.ProfessionalUserId, "?"), a.GabineteId, a.Gabinete?.Name,
                a.StartUtc, a.EndUtc, a.DurationMinutes, a.Status, a.Reason, a.AdministrativeNotes, a.IsOverbooking, a.ConfirmedAtUtc,
                a.ArrivedAtUtc, a.AttendedAtUtc, a.CancelledAtUtc, a.CancellationReason, a.RescheduleCount, alerts, a.Version, a.CreatedAtUtc));
        }
        return result;
    }
}
