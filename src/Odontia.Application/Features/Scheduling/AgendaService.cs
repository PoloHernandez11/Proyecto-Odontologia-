using Microsoft.EntityFrameworkCore;
using Odontia.Application.Abstractions;
using Odontia.Application.Common;
using Odontia.Application.Features.Patients;
using Odontia.Domain.Authorization;
using Odontia.Domain.Entities;
using Odontia.Domain.Enums;

namespace Odontia.Application.Features.Scheduling;

public interface IAgendaService
{
    Task<AgendaDto> GetAgendaAsync(AgendaQuery query, CancellationToken ct);
    Task<AgendaMonthDto> GetMonthAsync(int year, int month, Guid? professionalUserId, Guid? gabineteId, CancellationToken ct);
    Task<DaySummaryDto> GetDaySummaryAsync(DateOnly date, Guid? professionalUserId, CancellationToken ct);
    Task<AvailabilityDto> GetAvailabilityAsync(AvailabilityQuery query, CancellationToken ct);
    Task<IReadOnlyList<WorkingHoursDto>> GetWorkingHoursAsync(Guid professionalUserId, CancellationToken ct);
    Task<IReadOnlyList<WorkingHoursDto>> SetWorkingHoursAsync(Guid professionalUserId, SetWorkingHoursRequest request, CancellationToken ct);
    Task<IReadOnlyList<ScheduleBlockDto>> GetBlocksAsync(DateOnly from, DateOnly to, Guid? professionalUserId, CancellationToken ct);
    Task<ScheduleBlockDto> CreateBlockAsync(SaveScheduleBlockRequest request, CancellationToken ct);
    Task<ScheduleBlockDto> CreateNonWorkingDayAsync(NonWorkingDayRequest request, CancellationToken ct);
    Task<ScheduleBlockDto> UpdateBlockAsync(Guid id, SaveScheduleBlockRequest request, CancellationToken ct);
    Task DeleteBlockAsync(Guid id, CancellationToken ct);
}

internal sealed class AgendaService(IApplicationDbContext db, ITenantContext tenant, IConsultorioClock clock, IAvailabilityReader availability) : IAgendaService
{
    public async Task<AgendaDto> GetAgendaAsync(AgendaQuery q, CancellationToken ct)
    {
        tenant.Require(Permissions.AgendaRead);
        if (q.To < q.From) throw new RequestValidationException("to", "La fecha final debe ser posterior a la inicial.");
        if (q.To.DayNumber - q.From.DayNumber > 42) throw new RequestValidationException("to", "El rango máximo es de 42 días.");
        var professional = ResolveProfessionalFilter(q.ProfessionalUserId);
        var tz = await clock.GetTimeZoneAsync(ct);
        var (start, end) = tz.DayRangeUtc(q.From, q.To);

        var appts = await AgendaAppointmentsQuery(start, end, professional, q.GabineteId, q.IncludeCancelled).ToListAsync(ct);
        var names = await UserNames.LoadAsync(db, appts.Select(a => a.ProfessionalUserId), ct);
        var blocks = await BlocksQuery(start, end, professional).ToListAsync(ct);
        var hours = await db.WorkingHours.AsNoTracking()
            .Where(w => professional == null || w.ProfessionalUserId == professional)
            .Where(w => q.GabineteId == null || w.GabineteId == null || w.GabineteId == q.GabineteId)
            .ToListAsync(ct);
        hours = hours.OrderBy(w => w.DayOfWeek).ThenBy(w => w.StartTime).ToList(); // en memoria: portable entre proveedores
        if (!tenant.Has(Permissions.AgendaAllProfessionals)) hours = hours.Where(h => h.ProfessionalUserId == tenant.UserId).ToList();

        return new AgendaDto(q.From, q.To, tz.Id,
            appts.Select(a => new AgendaAppointmentDto(a.Id, a.StartUtc, a.EndUtc, (int)(a.EndUtc - a.StartUtc).TotalMinutes, a.Status, a.PatientId,
                a.PatientName, a.PatientPhone, a.ProfessionalUserId, names.GetValueOrDefault(a.ProfessionalUserId, "?"), a.GabineteId, a.GabineteName,
                a.Reason, a.IsOverbooking)).ToList(),
            blocks.Select(ToDto).ToList(), hours.Select(ToDto).ToList());
    }

    public async Task<AgendaMonthDto> GetMonthAsync(int year, int month, Guid? professionalUserId, Guid? gabineteId, CancellationToken ct)
    {
        tenant.Require(Permissions.AgendaRead);
        if (month is < 1 or > 12 || year is < 2000 or > 2100) throw new RequestValidationException("month", "Mes inválido.");
        var professional = ResolveProfessionalFilter(professionalUserId);
        var tz = await clock.GetTimeZoneAsync(ct);
        var first = new DateOnly(year, month, 1);
        var last = first.AddMonths(1).AddDays(-1);
        var (start, end) = tz.DayRangeUtc(first, last);
        var appts = await AgendaAppointmentsQuery(start, end, professional, gabineteId, false).ToListAsync(ct);
        var blocks = await BlocksQuery(start, end, professional).Where(b => b.Kind == ScheduleBlockKind.NonWorkingDay).ToListAsync(ct);

        var days = new List<MonthDayDto>();
        for (var d = first; d <= last; d = d.AddDays(1))
        {
            var (ds, de) = tz.DayRangeUtc(d, d);
            var dayAppts = appts.Where(a => a.StartUtc >= ds && a.StartUtc < de).OrderBy(a => a.StartUtc).ToList();
            var nonWorking = blocks.Any(b => b.StartUtc <= ds && b.EndUtc >= de);
            days.Add(new MonthDayDto(d, dayAppts.Count,
                dayAppts.Take(3).Select(a => new MonthDayItemDto(a.Id, a.StartUtc, ShortName(a.PatientFirstName, a.PatientLastName), a.Status)).ToList(),
                nonWorking));
        }
        return new AgendaMonthDto(year, month, tz.Id, days);
    }

    public async Task<DaySummaryDto> GetDaySummaryAsync(DateOnly date, Guid? professionalUserId, CancellationToken ct)
    {
        tenant.Require(Permissions.AgendaRead);
        var professional = ResolveProfessionalFilter(professionalUserId);
        var tz = await clock.GetTimeZoneAsync(ct);
        var (start, end) = tz.DayRangeUtc(date, date);
        var appts = await AgendaAppointmentsQuery(start, end, professional, null, true).ToListAsync(ct);
        var active = appts.Where(a => Appointment.BlocksAgenda(a.Status)).ToList();
        var booked = (int)IntervalSet.Normalize(active.Select(a => new TimeRange(a.StartUtc, a.EndUtc))).Sum(r => r.Duration.TotalMinutes);

        var professionals = professional is not null
            ? [professional.Value]
            : active.Select(a => a.ProfessionalUserId).Distinct().ToList();
        var gaps = new List<TimeRange>();
        foreach (var p in professionals)
        {
            var input = await availability.BuildInputAsync(date, p, null, null, ct);
            gaps.AddRange(AvailabilityCalculator.FreeRanges(input).Where(r => r.Duration.TotalMinutes >= 15));
        }
        return new DaySummaryDto(date, active.Count, booked, gaps.Count,
            gaps.Count == 0 ? 0 : (int)gaps.Average(g => g.Duration.TotalMinutes),
            appts.GroupBy(a => a.Status.ToString()).ToDictionary(g => g.Key, g => g.Count()));
    }

    public async Task<AvailabilityDto> GetAvailabilityAsync(AvailabilityQuery q, CancellationToken ct)
    {
        tenant.Require(Permissions.AgendaRead);
        if (!tenant.CanActOnProfessional(q.ProfessionalUserId)) throw new ForbiddenAccessException("Sólo puede consultar su propia agenda.");
        var duration = q.DurationMinutes ?? await availability.DefaultDurationAsync(q.ProfessionalUserId, ct);
        if (duration is < 5 or > 480) throw new RequestValidationException("durationMinutes", "La duración debe estar entre 5 y 480 minutos.");
        var input = await availability.BuildInputAsync(q.Date, q.ProfessionalUserId, q.GabineteId, null, ct);
        var step = await db.Consultorios.Where(c => c.Id == tenant.ConsultorioId).Select(c => c.SlotIntervalMinutes).FirstAsync(ct);
        var slots = AvailabilityCalculator.Slots(input, duration, step, clock.UtcNow);
        return new AvailabilityDto(q.Date, q.ProfessionalUserId, duration,
            slots.Select(s => new AvailabilitySlotDto(s.Start, s.End, input.TimeZone.UtcToLocal(s.Start).ToString("HH:mm"))).ToList(),
            input.Blocks.Select(ToDto).ToList());
    }

    // ---------------- Horarios laborales ----------------

    public async Task<IReadOnlyList<WorkingHoursDto>> GetWorkingHoursAsync(Guid professionalUserId, CancellationToken ct)
    {
        tenant.Require(Permissions.AgendaRead);
        if (!tenant.CanActOnProfessional(professionalUserId)) throw new ForbiddenAccessException("Sólo puede consultar su propia agenda.");
        return (await db.WorkingHours.AsNoTracking().Where(w => w.ProfessionalUserId == professionalUserId)
            .ToListAsync(ct)).OrderBy(w => w.DayOfWeek).ThenBy(w => w.StartTime).Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<WorkingHoursDto>> SetWorkingHoursAsync(Guid professionalUserId, SetWorkingHoursRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.AgendaManage);
        if (!tenant.CanActOnProfessional(professionalUserId)) throw new ForbiddenAccessException("Sólo puede configurar su propia agenda.");
        await EnsureProfessionalAsync(professionalUserId, ct);
        var gabinetes = r.Slots.Where(s => s.GabineteId != null).Select(s => s.GabineteId!.Value).Distinct().ToList();
        if (gabinetes.Count > 0 && await db.Gabinetes.CountAsync(g => gabinetes.Contains(g.Id), ct) != gabinetes.Count)
            throw new RequestValidationException("gabineteId", "El consultorio físico no existe.");

        var created = new List<WorkingHours>();
        foreach (var s in r.Slots)
        {
            if (created.Any(c => c.Overlaps(s.DayOfWeek, s.StartTime, s.EndTime)))
                throw new BusinessRuleException($"Las franjas del {s.DayOfWeek} se superponen.", "overlapping_working_hours");
            created.Add(new WorkingHours(tenant.ConsultorioId, professionalUserId, s.DayOfWeek, s.StartTime, s.EndTime, s.GabineteId));
        }
        var existing = await db.WorkingHours.Where(w => w.ProfessionalUserId == professionalUserId).ToListAsync(ct);
        db.WorkingHours.RemoveRange(existing);
        db.WorkingHours.AddRange(created);
        await db.SaveChangesAsync(ct);
        return created.OrderBy(w => w.DayOfWeek).ThenBy(w => w.StartTime).Select(ToDto).ToList();
    }

    // ---------------- Bloqueos / excepciones / días no laborables ----------------

    public async Task<IReadOnlyList<ScheduleBlockDto>> GetBlocksAsync(DateOnly from, DateOnly to, Guid? professionalUserId, CancellationToken ct)
    {
        tenant.Require(Permissions.AgendaRead);
        var professional = ResolveProfessionalFilter(professionalUserId);
        var tz = await clock.GetTimeZoneAsync(ct);
        var (start, end) = tz.DayRangeUtc(from, to);
        return (await BlocksQuery(start, end, professional).ToListAsync(ct)).Select(ToDto).ToList();
    }

    public async Task<ScheduleBlockDto> CreateBlockAsync(SaveScheduleBlockRequest r, CancellationToken ct)
    {
        await EnsureCanManageBlockAsync(r.ProfessionalUserId, r.GabineteId, ct);
        var block = new ScheduleBlock(tenant.ConsultorioId, r.Kind, r.Start.UtcDateTime, r.End.UtcDateTime, r.ProfessionalUserId, r.GabineteId, r.Reason);
        db.ScheduleBlocks.Add(block);
        await db.SaveChangesAsync(ct);
        return ToDto(block);
    }

    public async Task<ScheduleBlockDto> CreateNonWorkingDayAsync(NonWorkingDayRequest r, CancellationToken ct)
    {
        await EnsureCanManageBlockAsync(r.ProfessionalUserId, null, ct);
        var tz = await clock.GetTimeZoneAsync(ct);
        var (start, end) = tz.DayRangeUtc(r.Date, r.Date);
        var block = new ScheduleBlock(tenant.ConsultorioId, ScheduleBlockKind.NonWorkingDay, start, end, r.ProfessionalUserId, null, r.Reason ?? "Día no laborable");
        db.ScheduleBlocks.Add(block);
        await db.SaveChangesAsync(ct);
        return ToDto(block);
    }

    public async Task<ScheduleBlockDto> UpdateBlockAsync(Guid id, SaveScheduleBlockRequest r, CancellationToken ct)
    {
        var block = await db.ScheduleBlocks.FirstOrDefaultAsync(b => b.Id == id, ct) ?? throw new NotFoundException();
        await EnsureCanManageBlockAsync(block.ProfessionalUserId, block.GabineteId, ct);
        await EnsureCanManageBlockAsync(r.ProfessionalUserId, r.GabineteId, ct);
        block.Update(r.Kind, r.Start.UtcDateTime, r.End.UtcDateTime, r.ProfessionalUserId, r.GabineteId, r.Reason);
        await db.SaveChangesAsync(ct);
        return ToDto(block);
    }

    public async Task DeleteBlockAsync(Guid id, CancellationToken ct)
    {
        var block = await db.ScheduleBlocks.FirstOrDefaultAsync(b => b.Id == id, ct) ?? throw new NotFoundException();
        await EnsureCanManageBlockAsync(block.ProfessionalUserId, block.GabineteId, ct);
        db.ScheduleBlocks.Remove(block);
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureCanManageBlockAsync(Guid? professionalUserId, Guid? gabineteId, CancellationToken ct)
    {
        tenant.Require(Permissions.AgendaManage);
        if (professionalUserId is null)
        {
            // Bloqueos generales del consultorio o de un gabinete: sólo quien gestiona todas las agendas.
            if (!tenant.Has(Permissions.AgendaAllProfessionals)) throw new ForbiddenAccessException("Sólo puede bloquear su propia agenda.");
        }
        else
        {
            if (!tenant.CanActOnProfessional(professionalUserId.Value)) throw new ForbiddenAccessException("Sólo puede bloquear su propia agenda.");
            await EnsureProfessionalAsync(professionalUserId.Value, ct);
        }
        if (gabineteId is { } g && !await db.Gabinetes.AnyAsync(x => x.Id == g, ct))
            throw new RequestValidationException("gabineteId", "El consultorio físico no existe.");
    }

    private async Task EnsureProfessionalAsync(Guid userId, CancellationToken ct)
    {
        if (!await db.Members.AnyAsync(m => m.UserId == userId && m.IsActive && m.IsProfessional, ct))
            throw new RequestValidationException("professionalUserId", "El profesional no existe o no está activo en el consultorio.");
    }

    // ---------------- Consultas ----------------

    /// <summary>Sin permiso sobre todas las agendas, el filtro se fuerza al propio profesional.</summary>
    private Guid? ResolveProfessionalFilter(Guid? requested)
    {
        if (tenant.Has(Permissions.AgendaAllProfessionals)) return requested;
        if (requested is not null && requested != tenant.UserId) throw new ForbiddenAccessException("Sólo puede consultar su propia agenda.");
        return tenant.UserId;
    }

    private sealed record AgendaRow(Guid Id, DateTime StartUtc, DateTime EndUtc, AppointmentStatus Status, Guid PatientId, string PatientName,
        string PatientFirstName, string PatientLastName, string? PatientPhone, Guid ProfessionalUserId, Guid? GabineteId, string? GabineteName,
        string? Reason, bool IsOverbooking);

    private IQueryable<AgendaRow> AgendaAppointmentsQuery(DateTime start, DateTime end, Guid? professional, Guid? gabinete, bool includeCancelled) =>
        db.Appointments.AsNoTracking()
            .Where(a => a.StartUtc < end && a.EndUtc > start)
            .Where(a => professional == null || a.ProfessionalUserId == professional)
            .Where(a => gabinete == null || a.GabineteId == gabinete)
            .Where(a => includeCancelled || (a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.NoShow))
            .OrderBy(a => a.StartUtc)
            .Select(a => new AgendaRow(a.Id, a.StartUtc, a.EndUtc, a.Status, a.PatientId, a.Patient!.FirstName + " " + a.Patient.LastName,
                a.Patient.FirstName, a.Patient.LastName, a.Patient.Phone, a.ProfessionalUserId, a.GabineteId,
                a.Gabinete != null ? a.Gabinete.Name : null, a.Reason, a.IsOverbooking));

    private IQueryable<ScheduleBlock> BlocksQuery(DateTime start, DateTime end, Guid? professional) =>
        db.ScheduleBlocks.AsNoTracking()
            .Where(b => b.StartUtc < end && b.EndUtc > start)
            .Where(b => professional == null || b.ProfessionalUserId == null || b.ProfessionalUserId == professional)
            .OrderBy(b => b.StartUtc);

    private static string ShortName(string first, string last) => $"{(first.Length > 0 ? first[0] : ' ')}. {last}";

    internal static ScheduleBlockDto ToDto(ScheduleBlock b) => new(b.Id, b.Kind, b.StartUtc, b.EndUtc, b.ProfessionalUserId, b.GabineteId, b.Reason);
    private static WorkingHoursDto ToDto(WorkingHours w) => new(w.Id, w.ProfessionalUserId, w.DayOfWeek, w.StartTime, w.EndTime, w.GabineteId);
}

/// <summary>Carga de datos de disponibilidad (compartido entre consulta y validación al reservar).</summary>
public interface IAvailabilityReader
{
    Task<AvailabilityInput> BuildInputAsync(DateOnly date, Guid professionalUserId, Guid? gabineteId, Guid? excludeAppointmentId, CancellationToken ct);
    Task<int> DefaultDurationAsync(Guid professionalUserId, CancellationToken ct);
}

internal sealed class AvailabilityReader(IApplicationDbContext db, IConsultorioClock clock, ITenantContext tenant) : IAvailabilityReader
{
    public async Task<AvailabilityInput> BuildInputAsync(DateOnly date, Guid professionalUserId, Guid? gabineteId, Guid? excludeAppointmentId, CancellationToken ct)
    {
        var tz = await clock.GetTimeZoneAsync(ct);
        var (start, end) = tz.DayRangeUtc(date, date);
        var hours = await db.WorkingHours.AsNoTracking().Where(w => w.ProfessionalUserId == professionalUserId && w.DayOfWeek == date.DayOfWeek).ToListAsync(ct);
        var blocks = await db.ScheduleBlocks.AsNoTracking()
            .Where(b => b.StartUtc < end && b.EndUtc > start)
            .Where(b => b.ProfessionalUserId == null || b.ProfessionalUserId == professionalUserId)
            .ToListAsync(ct);
        var busy = await db.Appointments.AsNoTracking()
            .Where(a => a.StartUtc < end && a.EndUtc > start && a.Id != excludeAppointmentId)
            .Where(a => a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.NoShow)
            .Where(a => a.ProfessionalUserId == professionalUserId || (gabineteId != null && a.GabineteId == gabineteId))
            .Select(a => new { a.StartUtc, a.EndUtc }).ToListAsync(ct);
        return new AvailabilityInput(date, tz, professionalUserId, gabineteId, hours, blocks, busy.Select(b => new TimeRange(b.StartUtc, b.EndUtc)).ToList());
    }

    public async Task<int> DefaultDurationAsync(Guid professionalUserId, CancellationToken ct)
    {
        var member = await db.Members.Where(m => m.UserId == professionalUserId).Select(m => m.DefaultAppointmentMinutes).FirstOrDefaultAsync(ct);
        if (member is not null) return member.Value;
        var cid = tenant.ConsultorioId;
        return await db.Consultorios.Where(c => c.Id == cid).Select(c => c.DefaultAppointmentMinutes).FirstAsync(ct);
    }
}
