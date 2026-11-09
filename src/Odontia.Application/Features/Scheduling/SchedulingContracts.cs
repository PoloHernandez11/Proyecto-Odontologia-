using Odontia.Application.Common;
using Odontia.Domain.Enums;

namespace Odontia.Application.Features.Scheduling;

public sealed record WorkingHoursDto(Guid Id, Guid ProfessionalUserId, DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime, Guid? GabineteId);

public sealed record WorkingHoursSlotRequest(DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime, Guid? GabineteId);

/// <summary>Reemplaza el horario semanal completo del profesional.</summary>
public sealed record SetWorkingHoursRequest(IReadOnlyList<WorkingHoursSlotRequest> Slots);

public sealed record ScheduleBlockDto(Guid Id, ScheduleBlockKind Kind, DateTime StartUtc, DateTime EndUtc, Guid? ProfessionalUserId,
    Guid? GabineteId, string? Reason);

public sealed record SaveScheduleBlockRequest(ScheduleBlockKind Kind, DateTimeOffset Start, DateTimeOffset End, Guid? ProfessionalUserId,
    Guid? GabineteId, string? Reason);

/// <summary>Día no laborable completo (en la zona horaria del consultorio).</summary>
public sealed record NonWorkingDayRequest(DateOnly Date, Guid? ProfessionalUserId, string? Reason);

public sealed class AgendaQuery
{
    /// <summary>Fecha local inicial (incluida).</summary>
    public DateOnly From { get; set; }
    /// <summary>Fecha local final (incluida). Máximo 42 días.</summary>
    public DateOnly To { get; set; }
    public Guid? ProfessionalUserId { get; set; }
    public Guid? GabineteId { get; set; }
    public bool IncludeCancelled { get; set; }
}

public sealed record AgendaAppointmentDto(Guid Id, DateTime StartUtc, DateTime EndUtc, int DurationMinutes, AppointmentStatus Status,
    Guid PatientId, string PatientName, string? PatientPhone, Guid ProfessionalUserId, string ProfessionalName, Guid? GabineteId,
    string? GabineteName, string? Reason, bool IsOverbooking);

public sealed record AgendaDto(DateOnly From, DateOnly To, string TimeZoneId, IReadOnlyList<AgendaAppointmentDto> Appointments,
    IReadOnlyList<ScheduleBlockDto> Blocks, IReadOnlyList<WorkingHoursDto> WorkingHours);

public sealed record MonthDayDto(DateOnly Date, int AppointmentCount, IReadOnlyList<MonthDayItemDto> FirstAppointments, bool IsNonWorkingDay);

public sealed record MonthDayItemDto(Guid Id, DateTime StartUtc, string PatientShortName, AppointmentStatus Status);

public sealed record AgendaMonthDto(int Year, int Month, string TimeZoneId, IReadOnlyList<MonthDayDto> Days);

public sealed record DaySummaryDto(DateOnly Date, int Appointments, int BookedMinutes, int FreeGaps, int AverageGapMinutes,
    IReadOnlyDictionary<string, int> ByStatus);

public sealed class AvailabilityQuery
{
    public DateOnly Date { get; set; }
    public Guid ProfessionalUserId { get; set; }
    public Guid? GabineteId { get; set; }
    /// <summary>Duración deseada; por defecto la del profesional o consultorio.</summary>
    public int? DurationMinutes { get; set; }
}

public sealed record AvailabilitySlotDto(DateTime StartUtc, DateTime EndUtc, string LocalTime);

public sealed record AvailabilityDto(DateOnly Date, Guid ProfessionalUserId, int DurationMinutes, IReadOnlyList<AvailabilitySlotDto> Slots,
    IReadOnlyList<ScheduleBlockDto> Blocks);

// ---------- Turnos ----------

public sealed record CreateAppointmentRequest(Guid PatientId, Guid ProfessionalUserId, Guid? GabineteId, DateTimeOffset Start,
    int? DurationMinutes, string? Reason, string? AdministrativeNotes, bool IsOverbooking = false, bool Confirmed = false);

public sealed record UpdateAppointmentRequest(Guid PatientId, string? Reason, string? AdministrativeNotes, Guid Version);

public sealed record RescheduleAppointmentRequest(DateTimeOffset Start, int? DurationMinutes, Guid? ProfessionalUserId, Guid? GabineteId,
    bool IsOverbooking, string? Reason, Guid Version);

public sealed record ChangeAppointmentStatusRequest(AppointmentStatus Status, string? Reason);

public sealed record CancelAppointmentRequest(string? Reason);

public sealed record AppointmentDto(Guid Id, Guid PatientId, string PatientName, string PatientDocument, string? PatientPhone, int? PatientAge,
    string? PatientInsurance, Guid ProfessionalUserId, string ProfessionalName, Guid? GabineteId, string? GabineteName,
    DateTime StartUtc, DateTime EndUtc, int DurationMinutes, AppointmentStatus Status, string? Reason, string? AdministrativeNotes,
    bool IsOverbooking, DateTime? ConfirmedAtUtc, DateTime? ArrivedAtUtc, DateTime? AttendedAtUtc, DateTime? CancelledAtUtc,
    string? CancellationReason, int RescheduleCount, IReadOnlyList<string> Alerts, Guid Version, DateTime CreatedAtUtc);

public sealed record AppointmentHistoryDto(DateTime ChangedAtUtc, string Action, AppointmentStatus? FromStatus, AppointmentStatus? ToStatus,
    string? Details, string? ChangedBy);

public sealed class AppointmentQuery : PageQuery
{
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public AppointmentStatus? Status { get; set; }
    public Guid? ProfessionalUserId { get; set; }
    public Guid? PatientId { get; set; }
    public Guid? GabineteId { get; set; }
    public string? Search { get; set; }
}

public sealed record AppointmentStatusCountsDto(int Total, IReadOnlyDictionary<string, int> ByStatus);
