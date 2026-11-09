using Odontia.Domain.Common;
using Odontia.Domain.Enums;

namespace Odontia.Domain.Entities;

/// <summary>Franja de horario laboral semanal de un profesional (hora local del consultorio).</summary>
public class WorkingHours : TenantEntity
{
    private WorkingHours() { }

    public WorkingHours(Guid consultorioId, Guid professionalUserId, DayOfWeek day, TimeOnly start, TimeOnly end, Guid? gabineteId)
    {
        ConsultorioId = consultorioId;
        ProfessionalUserId = professionalUserId;
        Set(day, start, end, gabineteId);
    }

    public Guid ProfessionalUserId { get; private set; }
    public DayOfWeek DayOfWeek { get; private set; }
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }
    public Guid? GabineteId { get; private set; }

    public void Set(DayOfWeek day, TimeOnly start, TimeOnly end, Guid? gabineteId)
    {
        if (end <= start) throw new DomainException("La hora de fin debe ser posterior a la de inicio.", "invalid_range");
        DayOfWeek = day;
        StartTime = start;
        EndTime = end;
        GabineteId = gabineteId;
    }

    public bool Overlaps(DayOfWeek day, TimeOnly start, TimeOnly end) =>
        DayOfWeek == day && start < EndTime && StartTime < end;
}

/// <summary>Bloqueo de horario, día no laborable o excepción de disponibilidad.</summary>
public class ScheduleBlock : TenantEntity
{
    private ScheduleBlock() { }

    public ScheduleBlock(Guid consultorioId, ScheduleBlockKind kind, DateTime startUtc, DateTime endUtc,
        Guid? professionalUserId, Guid? gabineteId, string? reason)
    {
        ConsultorioId = consultorioId;
        Update(kind, startUtc, endUtc, professionalUserId, gabineteId, reason);
    }

    public ScheduleBlockKind Kind { get; private set; }
    public DateTime StartUtc { get; private set; }
    public DateTime EndUtc { get; private set; }
    /// <summary>Nulo = aplica a todo el consultorio (p. ej. feriado).</summary>
    public Guid? ProfessionalUserId { get; private set; }
    public Guid? GabineteId { get; private set; }
    public string? Reason { get; private set; }

    public void Update(ScheduleBlockKind kind, DateTime startUtc, DateTime endUtc, Guid? professionalUserId, Guid? gabineteId, string? reason)
    {
        if (endUtc <= startUtc) throw new DomainException("El fin debe ser posterior al inicio.", "invalid_range");
        if ((endUtc - startUtc).TotalDays > 366) throw new DomainException("El bloqueo no puede superar un año.", "invalid_range");
        if (kind == ScheduleBlockKind.ExtraAvailability && professionalUserId is null)
            throw new DomainException("Una excepción de disponibilidad debe indicar el profesional.");
        Kind = kind;
        StartUtc = DateTime.SpecifyKind(startUtc, DateTimeKind.Utc);
        EndUtc = DateTime.SpecifyKind(endUtc, DateTimeKind.Utc);
        ProfessionalUserId = professionalUserId;
        GabineteId = gabineteId;
        Reason = Guard.Optional(reason, "Motivo", 200);
    }

    public bool AppliesTo(Guid professionalUserId, Guid? gabineteId) =>
        (ProfessionalUserId is null && GabineteId is null)
        || ProfessionalUserId == professionalUserId
        || (GabineteId is not null && GabineteId == gabineteId && ProfessionalUserId is null);
}

/// <summary>Turno. Implementa una máquina de estados controlada.</summary>
public class Appointment : TenantEntity
{
    private static readonly Dictionary<AppointmentStatus, AppointmentStatus[]> Transitions = new()
    {
        [AppointmentStatus.Pending] = [AppointmentStatus.Confirmed, AppointmentStatus.Arrived, AppointmentStatus.InWaitingRoom, AppointmentStatus.Cancelled, AppointmentStatus.NoShow],
        [AppointmentStatus.Confirmed] = [AppointmentStatus.Pending, AppointmentStatus.Arrived, AppointmentStatus.InWaitingRoom, AppointmentStatus.Cancelled, AppointmentStatus.NoShow],
        [AppointmentStatus.Arrived] = [AppointmentStatus.InWaitingRoom, AppointmentStatus.InProgress, AppointmentStatus.Attended, AppointmentStatus.Cancelled],
        [AppointmentStatus.InWaitingRoom] = [AppointmentStatus.InProgress, AppointmentStatus.Attended, AppointmentStatus.Cancelled],
        [AppointmentStatus.InProgress] = [AppointmentStatus.Attended],
        [AppointmentStatus.Attended] = [],
        [AppointmentStatus.Cancelled] = [],
        [AppointmentStatus.NoShow] = [],
    };

    private Appointment() { }

    public Appointment(Guid consultorioId, Guid patientId, Guid professionalUserId, Guid? gabineteId,
        DateTime startUtc, DateTime endUtc, string? reason, string? notes, bool isOverbooking)
    {
        ConsultorioId = consultorioId;
        PatientId = patientId;
        ProfessionalUserId = professionalUserId;
        GabineteId = gabineteId;
        SetTime(startUtc, endUtc);
        SetDetails(reason, notes);
        IsOverbooking = isOverbooking;
        Status = AppointmentStatus.Pending;
    }

    public Guid PatientId { get; private set; }
    public Guid ProfessionalUserId { get; private set; }
    public Guid? GabineteId { get; private set; }
    public DateTime StartUtc { get; private set; }
    public DateTime EndUtc { get; private set; }
    public AppointmentStatus Status { get; private set; }
    /// <summary>Motivo de consulta.</summary>
    public string? Reason { get; private set; }
    public string? AdministrativeNotes { get; private set; }
    /// <summary>Sobreturno / urgencia: puede superponerse con otros turnos (requiere permiso).</summary>
    public bool IsOverbooking { get; private set; }
    public DateTime? ConfirmedAtUtc { get; private set; }
    public DateTime? ArrivedAtUtc { get; private set; }
    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? AttendedAtUtc { get; private set; }
    public DateTime? CancelledAtUtc { get; private set; }
    public string? CancellationReason { get; private set; }
    public DateTime? ReminderQueuedAtUtc { get; private set; }
    /// <summary>Token de concurrencia optimista.</summary>
    public Guid Version { get; private set; } = Guid.NewGuid();

    public Patient? Patient { get; private set; }
    public Gabinete? Gabinete { get; private set; }

    public int DurationMinutes => (int)(EndUtc - StartUtc).TotalMinutes;
    public bool IsActive => Status is not (AppointmentStatus.Cancelled or AppointmentStatus.NoShow);
    public bool IsEditable => Status is AppointmentStatus.Pending or AppointmentStatus.Confirmed;

    public static bool BlocksAgenda(AppointmentStatus status) =>
        status is not (AppointmentStatus.Cancelled or AppointmentStatus.NoShow);

    public static bool CanTransition(AppointmentStatus from, AppointmentStatus to) =>
        Transitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

    public bool OverlapsWith(DateTime startUtc, DateTime endUtc) => startUtc < EndUtc && StartUtc < endUtc;

    private void SetTime(DateTime startUtc, DateTime endUtc)
    {
        if (endUtc <= startUtc) throw new DomainException("El fin del turno debe ser posterior al inicio.", "invalid_range");
        var minutes = (endUtc - startUtc).TotalMinutes;
        if (minutes is < 5 or > 480) throw new DomainException("La duración del turno debe estar entre 5 y 480 minutos.", "invalid_duration");
        StartUtc = DateTime.SpecifyKind(startUtc, DateTimeKind.Utc);
        EndUtc = DateTime.SpecifyKind(endUtc, DateTimeKind.Utc);
    }

    public void SetDetails(string? reason, string? notes)
    {
        Reason = Guard.Optional(reason, "Motivo", 250);
        AdministrativeNotes = Guard.Optional(notes, "Observaciones", 1000);
        Touch();
    }

    public void Reassign(Guid professionalUserId, Guid? gabineteId, DateTime startUtc, DateTime endUtc, bool isOverbooking)
    {
        EnsureEditable();
        ProfessionalUserId = professionalUserId;
        GabineteId = gabineteId;
        SetTime(startUtc, endUtc);
        IsOverbooking = isOverbooking;
        ReminderQueuedAtUtc = null;
        Touch();
    }

    public void ChangePatient(Guid patientId)
    {
        EnsureEditable();
        PatientId = patientId;
        Touch();
    }

    public void TransitionTo(AppointmentStatus target, DateTime nowUtc, string? reason = null)
    {
        if (Status == target) return;
        if (!CanTransition(Status, target))
            throw new DomainException($"No se puede pasar un turno de '{Status}' a '{target}'.", "invalid_status_transition");

        switch (target)
        {
            case AppointmentStatus.Confirmed: ConfirmedAtUtc = nowUtc; break;
            case AppointmentStatus.Pending: ConfirmedAtUtc = null; break;
            case AppointmentStatus.Arrived:
            case AppointmentStatus.InWaitingRoom: ArrivedAtUtc ??= nowUtc; break;
            case AppointmentStatus.InProgress: StartedAtUtc = nowUtc; ArrivedAtUtc ??= nowUtc; break;
            case AppointmentStatus.Attended: AttendedAtUtc = nowUtc; break;
            case AppointmentStatus.Cancelled:
                CancelledAtUtc = nowUtc;
                CancellationReason = Guard.Optional(reason, "Motivo de cancelación", 250);
                break;
            case AppointmentStatus.NoShow:
                if (StartUtc > nowUtc)
                    throw new DomainException("No puede registrarse ausencia antes del horario del turno.", "invalid_status_transition");
                break;
        }
        Status = target;
        Touch();
    }

    /// <summary>
    /// Reprograma el turno a un nuevo horario (mismo registro, trazado en el historial). El turno vuelve
    /// a pendiente de confirmación y se reinicia el recordatorio.
    /// </summary>
    public void Reschedule(Guid professionalUserId, Guid? gabineteId, DateTime startUtc, DateTime endUtc, bool isOverbooking)
    {
        Reassign(professionalUserId, gabineteId, startUtc, endUtc, isOverbooking);
        Status = AppointmentStatus.Pending;
        ConfirmedAtUtc = null;
        RescheduleCount++;
    }

    public int RescheduleCount { get; private set; }

    public void MarkReminderQueued(DateTime nowUtc) => ReminderQueuedAtUtc = nowUtc;

    private void EnsureEditable()
    {
        if (!IsEditable)
            throw new DomainException("Sólo pueden modificarse turnos pendientes o confirmados.", "appointment_not_editable");
    }

    private void Touch() => Version = Guid.NewGuid();
}

/// <summary>Historial inmutable de cambios de un turno.</summary>
public class AppointmentHistory : TenantEntity
{
    private AppointmentHistory() { }

    public AppointmentHistory(Guid consultorioId, Guid appointmentId, string action, AppointmentStatus? fromStatus,
        AppointmentStatus? toStatus, string? details, Guid? userId, DateTime atUtc)
    {
        ConsultorioId = consultorioId;
        AppointmentId = appointmentId;
        Action = action;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        Details = details?.Length > 1000 ? details[..1000] : details;
        ChangedByUserId = userId;
        ChangedAtUtc = atUtc;
    }

    public Guid AppointmentId { get; private set; }
    public string Action { get; private set; } = default!;
    public AppointmentStatus? FromStatus { get; private set; }
    public AppointmentStatus? ToStatus { get; private set; }
    public string? Details { get; private set; }
    public Guid? ChangedByUserId { get; private set; }
    public DateTime ChangedAtUtc { get; private set; }
}

/// <summary>Tarea interna del consultorio.</summary>
public class TaskItem : TenantEntity
{
    private TaskItem() { }

    public TaskItem(Guid consultorioId, string title, string? description, TaskPriority priority, TaskCategory category,
        Guid? assignedToUserId, DateTime? dueAtUtc, Guid? patientId, Guid? appointmentId)
    {
        ConsultorioId = consultorioId;
        Update(title, description, priority, category, dueAtUtc, patientId, appointmentId);
        AssignedToUserId = assignedToUserId;
        Status = TaskItemStatus.Pending;
    }

    public string Title { get; private set; } = default!;
    public string? Description { get; private set; }
    public TaskPriority Priority { get; private set; }
    public TaskCategory Category { get; private set; }
    public TaskItemStatus Status { get; private set; }
    public Guid? AssignedToUserId { get; private set; }
    public DateTime? DueAtUtc { get; private set; }
    public Guid? PatientId { get; private set; }
    public Guid? AppointmentId { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public Guid? CompletedByUserId { get; private set; }
    public DateTime? DueNotifiedAtUtc { get; private set; }

    public bool IsOpen => Status is TaskItemStatus.Pending or TaskItemStatus.InProgress;
    public bool IsOverdue(DateTime nowUtc) => IsOpen && DueAtUtc is not null && DueAtUtc < nowUtc;

    public void Update(string title, string? description, TaskPriority priority, TaskCategory category, DateTime? dueAtUtc,
        Guid? patientId, Guid? appointmentId)
    {
        Title = Guard.Required(title, "Título", 200);
        Description = Guard.Optional(description, "Descripción", 2000);
        Priority = priority;
        Category = category;
        DueAtUtc = dueAtUtc is null ? null : DateTime.SpecifyKind(dueAtUtc.Value, DateTimeKind.Utc);
        PatientId = patientId;
        AppointmentId = appointmentId;
        DueNotifiedAtUtc = null;
    }

    public void Assign(Guid? userId) => AssignedToUserId = userId;

    public void SetStatus(TaskItemStatus status, Guid userId, DateTime nowUtc)
    {
        if (Status == status) return;
        if (Status is TaskItemStatus.Cancelled && status != TaskItemStatus.Pending)
            throw new DomainException("Una tarea cancelada sólo puede reabrirse.", "invalid_status_transition");
        Status = status;
        if (status == TaskItemStatus.Completed)
        {
            CompletedAtUtc = nowUtc;
            CompletedByUserId = userId;
        }
        else
        {
            CompletedAtUtc = null;
            CompletedByUserId = null;
        }
    }

    public void MarkDueNotified(DateTime nowUtc) => DueNotifiedAtUtc = nowUtc;
}
