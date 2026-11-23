using Odontia.Application.Common;
using Odontia.Application.Features.Scheduling;
using Odontia.Domain.Common;
using Odontia.Domain.Entities;
using Odontia.Domain.Enums;

namespace Odontia.UnitTests.QA;

public class QaAppointmentStateMachineTests
{
    private static readonly DateTime Now = new(2026, 11, 19, 15, 0, 0, DateTimeKind.Utc);

    /// <summary>Especificación de transiciones permitidas (máquina de estados del turno).</summary>
    private static readonly HashSet<(AppointmentStatus, AppointmentStatus)> Allowed =
    [
        (AppointmentStatus.Pending, AppointmentStatus.Confirmed), (AppointmentStatus.Pending, AppointmentStatus.Arrived),
        (AppointmentStatus.Pending, AppointmentStatus.InWaitingRoom), (AppointmentStatus.Pending, AppointmentStatus.Cancelled),
        (AppointmentStatus.Pending, AppointmentStatus.NoShow),
        (AppointmentStatus.Confirmed, AppointmentStatus.Pending), (AppointmentStatus.Confirmed, AppointmentStatus.Arrived),
        (AppointmentStatus.Confirmed, AppointmentStatus.InWaitingRoom), (AppointmentStatus.Confirmed, AppointmentStatus.Cancelled),
        (AppointmentStatus.Confirmed, AppointmentStatus.NoShow),
        (AppointmentStatus.Arrived, AppointmentStatus.InWaitingRoom), (AppointmentStatus.Arrived, AppointmentStatus.InProgress),
        (AppointmentStatus.Arrived, AppointmentStatus.Attended), (AppointmentStatus.Arrived, AppointmentStatus.Cancelled),
        (AppointmentStatus.InWaitingRoom, AppointmentStatus.InProgress), (AppointmentStatus.InWaitingRoom, AppointmentStatus.Attended),
        (AppointmentStatus.InWaitingRoom, AppointmentStatus.Cancelled),
        (AppointmentStatus.InProgress, AppointmentStatus.Attended),
    ];

    public static IEnumerable<object[]> AllPairs() =>
        from f in Enum.GetValues<AppointmentStatus>()
        from t in Enum.GetValues<AppointmentStatus>()
        where f != t
        select new object[] { f, t };

    [Theory]
    [MemberData(nameof(AllPairs))]
    public void Transition_matrix_matches_specification(AppointmentStatus from, AppointmentStatus to) =>
        Assert.Equal(Allowed.Contains((from, to)), Appointment.CanTransition(from, to));

    [Fact]
    public void Undefined_status_cannot_transition()
    {
        Assert.False(Appointment.CanTransition((AppointmentStatus)99, AppointmentStatus.Confirmed));
        Assert.False(Appointment.CanTransition(AppointmentStatus.Pending, (AppointmentStatus)99));
    }

    private static Appointment New(DateTime? start = null, int minutes = 30) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, start ?? Now.AddHours(-1), (start ?? Now.AddHours(-1)).AddMinutes(minutes), "Control", null, false);

    private static Appointment InState(AppointmentStatus s)
    {
        var a = New();
        var path = s switch
        {
            AppointmentStatus.Pending => Array.Empty<AppointmentStatus>(),
            AppointmentStatus.Confirmed => [AppointmentStatus.Confirmed],
            AppointmentStatus.Arrived => [AppointmentStatus.Arrived],
            AppointmentStatus.InWaitingRoom => [AppointmentStatus.InWaitingRoom],
            AppointmentStatus.InProgress => [AppointmentStatus.Arrived, AppointmentStatus.InProgress],
            AppointmentStatus.Attended => [AppointmentStatus.Arrived, AppointmentStatus.InProgress, AppointmentStatus.Attended],
            AppointmentStatus.Cancelled => [AppointmentStatus.Cancelled],
            AppointmentStatus.NoShow => [AppointmentStatus.NoShow],
            _ => throw new ArgumentOutOfRangeException(nameof(s)),
        };
        foreach (var p in path) a.TransitionTo(p, Now);
        return a;
    }

    [Theory]
    [MemberData(nameof(AllPairs))]
    public void TransitionTo_enforces_the_matrix(AppointmentStatus from, AppointmentStatus to)
    {
        var a = InState(from);
        if (Allowed.Contains((from, to)))
        {
            a.TransitionTo(to, Now);
            Assert.Equal(to, a.Status);
        }
        else
        {
            var ex = Assert.Throws<DomainException>(() => a.TransitionTo(to, Now));
            Assert.Equal("invalid_status_transition", ex.Code);
            Assert.Equal(from, a.Status);
        }
    }

    [Fact]
    public void Same_status_transition_is_a_no_op_and_keeps_version()
    {
        var a = InState(AppointmentStatus.Attended);
        var v = a.Version;
        a.TransitionTo(AppointmentStatus.Attended, Now);
        Assert.Equal(v, a.Version);
    }

    [Fact]
    public void Timestamps_are_recorded_per_transition()
    {
        var a = New();
        a.TransitionTo(AppointmentStatus.Confirmed, Now);
        Assert.Equal(Now, a.ConfirmedAtUtc);
        a.TransitionTo(AppointmentStatus.Pending, Now.AddMinutes(1));
        Assert.Null(a.ConfirmedAtUtc);
        a.TransitionTo(AppointmentStatus.InWaitingRoom, Now.AddMinutes(2));
        Assert.Equal(Now.AddMinutes(2), a.ArrivedAtUtc);
        a.TransitionTo(AppointmentStatus.InProgress, Now.AddMinutes(3));
        Assert.Equal(Now.AddMinutes(2), a.ArrivedAtUtc); // no se sobrescribe la llegada
        Assert.Equal(Now.AddMinutes(3), a.StartedAtUtc);
        a.TransitionTo(AppointmentStatus.Attended, Now.AddMinutes(30));
        Assert.Equal(Now.AddMinutes(30), a.AttendedAtUtc);
        Assert.True(a.IsActive);
        Assert.False(a.IsEditable);
    }

    [Fact]
    public void Cancellation_records_reason_and_frees_agenda()
    {
        var a = New();
        a.TransitionTo(AppointmentStatus.Cancelled, Now, "  Paciente avisó  ");
        Assert.Equal("Paciente avisó", a.CancellationReason);
        Assert.Equal(Now, a.CancelledAtUtc);
        Assert.False(a.IsActive);
        Assert.False(Appointment.BlocksAgenda(a.Status));
        Assert.False(Appointment.BlocksAgenda(AppointmentStatus.NoShow));
        Assert.True(Appointment.BlocksAgenda(AppointmentStatus.Attended));
    }

    [Fact]
    public void Failed_cancellation_does_not_leave_partial_state()
    {
        var a = New();
        Assert.Throws<DomainException>(() => a.TransitionTo(AppointmentStatus.Cancelled, Now, new string('x', 251)));
        Assert.Equal(AppointmentStatus.Pending, a.Status);
        Assert.Null(a.CancelledAtUtc);
    }

    [Fact]
    public void No_show_is_allowed_exactly_at_start_time()
    {
        var a = New(start: Now);
        a.TransitionTo(AppointmentStatus.NoShow, Now);
        Assert.Equal(AppointmentStatus.NoShow, a.Status);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(481)]
    [InlineData(0)]
    [InlineData(-30)]
    public void Duration_out_of_range_is_rejected(int minutes)
    {
        Assert.Throws<DomainException>(() => New(minutes: minutes));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(480)]
    public void Duration_boundaries_are_accepted(int minutes) => Assert.Equal(minutes, New(minutes: minutes).DurationMinutes);

    [Fact]
    public void Times_are_stored_as_utc_kind()
    {
        var start = DateTime.SpecifyKind(Now, DateTimeKind.Unspecified);
        var a = new Appointment(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, start, start.AddMinutes(30), null, null, false);
        Assert.Equal(DateTimeKind.Utc, a.StartUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, a.EndUtc.Kind);
    }

    [Fact]
    public void Overlap_is_half_open()
    {
        var a = New(start: Now, minutes: 30);
        Assert.False(a.OverlapsWith(Now.AddMinutes(30), Now.AddMinutes(60)));
        Assert.False(a.OverlapsWith(Now.AddMinutes(-30), Now));
        Assert.True(a.OverlapsWith(Now.AddMinutes(29), Now.AddMinutes(60)));
        Assert.True(a.OverlapsWith(Now.AddMinutes(-10), Now.AddMinutes(100)));
    }

    [Fact]
    public void Details_are_validated_and_change_version()
    {
        var a = New();
        var v = a.Version;
        a.SetDetails("  ", " obs ");
        Assert.Null(a.Reason);
        Assert.Equal("obs", a.AdministrativeNotes);
        Assert.NotEqual(v, a.Version);
        Assert.Throws<DomainException>(() => a.SetDetails(new string('r', 251), null));
    }

    [Fact]
    public void Reschedule_increments_counter_clears_reminder_and_confirmation()
    {
        var a = New(start: Now.AddDays(1));
        a.TransitionTo(AppointmentStatus.Confirmed, Now);
        a.MarkReminderQueued(Now);
        a.Reschedule(a.ProfessionalUserId, null, Now.AddDays(2), Now.AddDays(2).AddMinutes(45), true);
        Assert.Equal(1, a.RescheduleCount);
        Assert.Equal(AppointmentStatus.Pending, a.Status);
        Assert.Null(a.ConfirmedAtUtc);
        Assert.Null(a.ReminderQueuedAtUtc);
        Assert.True(a.IsOverbooking);
        Assert.Equal(45, a.DurationMinutes);
    }

    [Theory]
    [InlineData(AppointmentStatus.Arrived)]
    [InlineData(AppointmentStatus.InProgress)]
    [InlineData(AppointmentStatus.Attended)]
    [InlineData(AppointmentStatus.NoShow)]
    public void Non_editable_appointments_cannot_change_patient_or_be_reassigned(AppointmentStatus s)
    {
        var a = InState(s);
        Assert.Equal("appointment_not_editable", Assert.Throws<DomainException>(() => a.ChangePatient(Guid.NewGuid())).Code);
        Assert.Throws<DomainException>(() => a.Reassign(Guid.NewGuid(), null, Now, Now.AddMinutes(30), false));
    }

    [Fact]
    public void Invalid_reschedule_range_does_not_change_status()
    {
        var a = New(start: Now.AddDays(1));
        a.TransitionTo(AppointmentStatus.Confirmed, Now);
        Assert.Throws<DomainException>(() => a.Reschedule(a.ProfessionalUserId, null, Now.AddDays(2), Now.AddDays(2), false));
        Assert.Equal(AppointmentStatus.Confirmed, a.Status);
        Assert.Equal(0, a.RescheduleCount);
    }

    [Fact]
    public void History_details_are_truncated_to_1000()
    {
        var h = new AppointmentHistory(Guid.NewGuid(), Guid.NewGuid(), "status", AppointmentStatus.Pending, AppointmentStatus.Confirmed,
            new string('d', 1500), null, Now);
        Assert.Equal(1000, h.Details!.Length);
    }
}

public class QaScheduleEntitiesTests
{
    private static readonly Guid C = Guid.NewGuid();
    private static readonly DateTime T0 = new(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Working_hours_require_end_after_start()
    {
        Assert.Throws<DomainException>(() => new WorkingHours(C, Guid.NewGuid(), DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(9, 0), null));
        Assert.Throws<DomainException>(() => new WorkingHours(C, Guid.NewGuid(), DayOfWeek.Monday, new TimeOnly(10, 0), new TimeOnly(9, 0), null));
    }

    [Fact]
    public void Working_hours_overlap_is_half_open_and_per_day()
    {
        var w = new WorkingHours(C, Guid.NewGuid(), DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0), null);
        Assert.True(w.Overlaps(DayOfWeek.Monday, new TimeOnly(11, 0), new TimeOnly(13, 0)));
        Assert.False(w.Overlaps(DayOfWeek.Monday, new TimeOnly(12, 0), new TimeOnly(13, 0)));
        Assert.False(w.Overlaps(DayOfWeek.Tuesday, new TimeOnly(9, 0), new TimeOnly(12, 0)));
    }

    [Fact]
    public void Schedule_block_validates_range_and_duration()
    {
        Assert.Throws<DomainException>(() => new ScheduleBlock(C, ScheduleBlockKind.Blocked, T0, T0, null, null, null));
        Assert.Throws<DomainException>(() => new ScheduleBlock(C, ScheduleBlockKind.Blocked, T0, T0.AddDays(367), null, null, null));
        var ok = new ScheduleBlock(C, ScheduleBlockKind.Blocked, T0, T0.AddDays(366), null, null, " Vacaciones ");
        Assert.Equal("Vacaciones", ok.Reason);
    }

    [Fact]
    public void Extra_availability_requires_professional()
    {
        Assert.Throws<DomainException>(() => new ScheduleBlock(C, ScheduleBlockKind.ExtraAvailability, T0, T0.AddHours(2), null, null, null));
    }

    [Fact]
    public void Block_scope_rules()
    {
        var pro = Guid.NewGuid();
        var other = Guid.NewGuid();
        var gab = Guid.NewGuid();
        var whole = new ScheduleBlock(C, ScheduleBlockKind.NonWorkingDay, T0, T0.AddDays(1), null, null, null);
        Assert.True(whole.AppliesTo(pro, null));
        Assert.True(whole.AppliesTo(pro, gab));

        var proBlock = new ScheduleBlock(C, ScheduleBlockKind.Blocked, T0, T0.AddHours(1), pro, null, null);
        Assert.True(proBlock.AppliesTo(pro, gab));
        Assert.False(proBlock.AppliesTo(other, null));

        var gabBlock = new ScheduleBlock(C, ScheduleBlockKind.Blocked, T0, T0.AddHours(1), null, gab, null);
        Assert.True(gabBlock.AppliesTo(other, gab));
        Assert.False(gabBlock.AppliesTo(other, Guid.NewGuid()));
        Assert.False(gabBlock.AppliesTo(other, null));
    }
}

public class QaTaskItemTests
{
    private static readonly DateTime Now = new(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);

    private static TaskItem New(DateTime? due = null) =>
        new(Guid.NewGuid(), "  Llamar al laboratorio ", null, TaskPriority.High, TaskCategory.Laboratory, null, due, null, null);

    [Fact]
    public void New_task_is_pending_and_trimmed()
    {
        var t = New();
        Assert.Equal("Llamar al laboratorio", t.Title);
        Assert.Equal(TaskItemStatus.Pending, t.Status);
        Assert.True(t.IsOpen);
    }

    [Fact]
    public void Title_is_required_and_limited()
    {
        Assert.Throws<DomainException>(() => new TaskItem(Guid.NewGuid(), "", null, TaskPriority.Low, TaskCategory.General, null, null, null, null));
        Assert.Throws<DomainException>(() => new TaskItem(Guid.NewGuid(), new string('t', 201), null, TaskPriority.Low, TaskCategory.General, null, null, null, null));
    }

    [Fact]
    public void Overdue_only_for_open_tasks_with_past_due_date()
    {
        Assert.False(New().IsOverdue(Now));
        Assert.False(New(Now).IsOverdue(Now)); // vence justo ahora: aún no está vencida
        var t = New(Now.AddMinutes(-1));
        Assert.True(t.IsOverdue(Now));
        t.SetStatus(TaskItemStatus.Completed, Guid.NewGuid(), Now);
        Assert.False(t.IsOverdue(Now));
    }

    [Fact]
    public void Cancelled_task_can_only_be_reopened_to_pending()
    {
        var t = New();
        t.SetStatus(TaskItemStatus.Cancelled, Guid.NewGuid(), Now);
        Assert.False(t.IsOpen);
        Assert.Throws<DomainException>(() => t.SetStatus(TaskItemStatus.Completed, Guid.NewGuid(), Now));
        Assert.Throws<DomainException>(() => t.SetStatus(TaskItemStatus.InProgress, Guid.NewGuid(), Now));
        t.SetStatus(TaskItemStatus.Pending, Guid.NewGuid(), Now);
        Assert.True(t.IsOpen);
    }

    [Fact]
    public void Update_normalizes_due_kind_and_resets_due_notification()
    {
        var t = New(Now);
        t.MarkDueNotified(Now);
        t.Update("Otro", null, TaskPriority.Low, TaskCategory.General, DateTime.SpecifyKind(Now.AddDays(1), DateTimeKind.Unspecified), null, null);
        Assert.Null(t.DueNotifiedAtUtc);
        Assert.Equal(DateTimeKind.Utc, t.DueAtUtc!.Value.Kind);
    }

    [Fact]
    public void Same_status_is_a_no_op_and_keeps_completion_data()
    {
        var t = New();
        var user = Guid.NewGuid();
        t.SetStatus(TaskItemStatus.Completed, user, Now);
        t.SetStatus(TaskItemStatus.Completed, Guid.NewGuid(), Now.AddHours(1));
        Assert.Equal(user, t.CompletedByUserId);
        Assert.Equal(Now, t.CompletedAtUtc);
    }

    [Fact]
    public void Assign_can_clear_assignee()
    {
        var t = New();
        t.Assign(Guid.NewGuid());
        t.Assign(null);
        Assert.Null(t.AssignedToUserId);
    }
}

public class QaAvailabilityTests
{
    private static readonly Guid C = Guid.NewGuid();
    private static readonly Guid Pro = Guid.NewGuid();
    private static readonly DateOnly Monday = new(2026, 10, 19);

    private static AvailabilityInput Input(TimeZoneInfo tz, IReadOnlyList<TimeRange>? busy = null, IReadOnlyList<ScheduleBlock>? blocks = null,
        Guid? gabinete = null, IReadOnlyList<WorkingHours>? hours = null) =>
        new(Monday, tz, Pro, gabinete,
            hours ?? [new WorkingHours(C, Pro, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0), null)],
            blocks ?? [], busy ?? []);

    private static DateTime U(int h, int m = 0, int s = 0) => new(2026, 10, 19, h, m, s, DateTimeKind.Utc);

    [Fact]
    public void Slots_are_aligned_to_step_and_fit_duration()
    {
        var slots = AvailabilityCalculator.Slots(Input(TimeZoneInfo.Utc), 30, 15, DateTime.MinValue);
        Assert.Equal(U(9), slots[0].Start);
        Assert.Equal(U(11, 30), slots[^1].Start);
        Assert.All(slots, s => Assert.Equal(TimeSpan.FromMinutes(30), s.Duration));
        Assert.Equal(11, slots.Count);
    }

    [Fact]
    public void Slots_after_misaligned_busy_range_start_at_next_step()
    {
        var slots = AvailabilityCalculator.Slots(Input(TimeZoneInfo.Utc, busy: [new TimeRange(U(9), U(9, 20))]), 30, 15, DateTime.MinValue);
        Assert.Equal(U(9, 30), slots[0].Start);
    }

    [Fact]
    public void Slots_never_start_before_free_range_when_busy_end_has_seconds()
    {
        var input = Input(TimeZoneInfo.Utc, busy: [new TimeRange(U(9), U(9, 30, 30))]);
        var free = AvailabilityCalculator.FreeRanges(input);
        var slots = AvailabilityCalculator.Slots(input, 30, 15, DateTime.MinValue);
        Assert.All(slots, s => Assert.Contains(free, f => f.Contains(s)));
    }

    [Fact]
    public void Gabinete_specific_hours_only_apply_to_that_gabinete()
    {
        var g1 = Guid.NewGuid();
        var g2 = Guid.NewGuid();
        var hours = new List<WorkingHours> { new(C, Pro, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0), g1) };
        Assert.Single(AvailabilityCalculator.WorkingRanges(Input(TimeZoneInfo.Utc, gabinete: g1, hours: hours)));
        Assert.Empty(AvailabilityCalculator.WorkingRanges(Input(TimeZoneInfo.Utc, gabinete: g2, hours: hours)));
        Assert.Single(AvailabilityCalculator.WorkingRanges(Input(TimeZoneInfo.Utc, gabinete: null, hours: hours)));
    }

    [Fact]
    public void Hours_of_other_days_and_professionals_are_ignored()
    {
        var hours = new List<WorkingHours>
        {
            new(C, Pro, DayOfWeek.Tuesday, new TimeOnly(9, 0), new TimeOnly(12, 0), null),
            new(C, Guid.NewGuid(), DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0), null),
        };
        Assert.Empty(AvailabilityCalculator.WorkingRanges(Input(TimeZoneInfo.Utc, hours: hours)));
    }

    [Fact]
    public void Extra_availability_is_clipped_to_the_day()
    {
        var block = new ScheduleBlock(C, ScheduleBlockKind.ExtraAvailability, U(20), U(20).AddHours(10), Pro, null, null);
        var ranges = AvailabilityCalculator.WorkingRanges(Input(TimeZoneInfo.Utc, blocks: [block]));
        Assert.Equal(U(0).AddDays(1), ranges[^1].End);
    }

    [Fact]
    public void Overlapping_working_hours_are_merged()
    {
        var hours = new List<WorkingHours>
        {
            new(C, Pro, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0), null),
            new(C, Pro, DayOfWeek.Monday, new TimeOnly(11, 0), new TimeOnly(14, 0), null),
        };
        var ranges = AvailabilityCalculator.WorkingRanges(Input(TimeZoneInfo.Utc, hours: hours));
        Assert.Equal([new TimeRange(U(9), U(14))], ranges);
    }

    [Fact]
    public void Request_partially_outside_hours_is_not_within()
    {
        var input = Input(TimeZoneInfo.Utc);
        Assert.True(AvailabilityCalculator.IsWithinWorkingHours(input, new TimeRange(U(9), U(12))));
        Assert.False(AvailabilityCalculator.IsWithinWorkingHours(input, new TimeRange(U(11, 45), U(12, 15))));
    }

    [Fact]
    public void Interval_subtract_edge_cases()
    {
        var src = new[] { new TimeRange(U(9), U(12)) };
        Assert.Empty(IntervalSet.Subtract(src, [new TimeRange(U(8), U(13))]));
        Assert.Equal(src, IntervalSet.Subtract(src, [new TimeRange(U(12), U(13))]));
        Assert.Equal(src, IntervalSet.Subtract(src, []));
        Assert.Empty(IntervalSet.Normalize([new TimeRange(U(10), U(9)), new TimeRange(U(9), U(9))]));
        Assert.Equal([new TimeRange(U(9), U(11))], IntervalSet.Normalize([new TimeRange(U(10), U(11)), new TimeRange(U(9), U(10))]));
    }

    [Fact]
    public void Time_range_contains_and_overlaps()
    {
        var r = new TimeRange(U(9), U(10));
        Assert.True(r.Contains(r));
        Assert.False(r.Overlaps(new TimeRange(U(10), U(11))));
        Assert.Equal(TimeSpan.FromHours(1), r.Duration);
    }
}

public class QaTimeZoneExtensionsTests
{
    [Fact]
    public void Buenos_aires_local_to_utc_and_back()
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");
        var utc = tz.LocalToUtc(new DateOnly(2026, 11, 19), new TimeOnly(21, 30));
        Assert.Equal(new DateTime(2026, 11, 20, 0, 30, 0, DateTimeKind.Utc), utc);
        Assert.Equal(new DateTime(2026, 11, 19, 21, 30, 0), tz.UtcToLocal(utc));
    }

    [Fact]
    public void Day_range_covers_whole_local_days()
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");
        var (s, e) = tz.DayRangeUtc(new DateOnly(2026, 11, 19), new DateOnly(2026, 11, 20));
        Assert.Equal(new DateTime(2026, 11, 19, 3, 0, 0, DateTimeKind.Utc), s);
        Assert.Equal(new DateTime(2026, 11, 21, 3, 0, 0, DateTimeKind.Utc), e);
    }

    [Fact]
    public void Invalid_dst_local_time_is_shifted_forward()
    {
        // 2026-03-08 02:30 no existe en Nueva York (adelanto de hora).
        var tz = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        var utc = tz.LocalToUtc(new DateOnly(2026, 3, 8), new TimeOnly(2, 30));
        Assert.Equal(new DateTime(2026, 3, 8, 7, 30, 0, DateTimeKind.Utc), utc);
    }

    [Fact]
    public void Utc_to_local_treats_unspecified_as_utc()
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");
        Assert.Equal(new DateTime(2026, 1, 1, 9, 0, 0), tz.UtcToLocal(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Unspecified)));
    }
}
