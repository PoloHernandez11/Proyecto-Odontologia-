using Odontia.Application.Common;
using Odontia.Application.Features.Scheduling;
using Odontia.Domain.Common;
using Odontia.Domain.Entities;
using Odontia.Domain.Enums;

namespace Odontia.UnitTests.Domain;

public class AppointmentTests
{
    private static readonly Guid C = Guid.NewGuid();
    private static Appointment NewAppt(DateTime? start = null)
    {
        var s = start ?? DateTime.UtcNow.AddDays(1);
        return new Appointment(C, Guid.NewGuid(), Guid.NewGuid(), null, s, s.AddMinutes(45), "Control", null, false);
    }

    [Fact]
    public void New_appointment_is_pending()
    {
        Assert.Equal(AppointmentStatus.Pending, NewAppt().Status);
    }

    [Fact]
    public void Full_happy_path_transitions_are_allowed()
    {
        var a = NewAppt(DateTime.UtcNow.AddMinutes(-10));
        var now = DateTime.UtcNow;
        a.TransitionTo(AppointmentStatus.Confirmed, now);
        a.TransitionTo(AppointmentStatus.Arrived, now);
        a.TransitionTo(AppointmentStatus.InWaitingRoom, now);
        a.TransitionTo(AppointmentStatus.InProgress, now);
        a.TransitionTo(AppointmentStatus.Attended, now);
        Assert.Equal(AppointmentStatus.Attended, a.Status);
        Assert.NotNull(a.ArrivedAtUtc);
        Assert.NotNull(a.AttendedAtUtc);
    }

    [Theory]
    [InlineData(AppointmentStatus.Attended, AppointmentStatus.Pending)]
    [InlineData(AppointmentStatus.Cancelled, AppointmentStatus.Confirmed)]
    [InlineData(AppointmentStatus.Pending, AppointmentStatus.Attended)]
    [InlineData(AppointmentStatus.NoShow, AppointmentStatus.Arrived)]
    public void Invalid_transitions_are_rejected(AppointmentStatus from, AppointmentStatus to)
    {
        Assert.False(Appointment.CanTransition(from, to));
    }

    [Fact]
    public void Cannot_register_no_show_before_start()
    {
        var a = NewAppt(DateTime.UtcNow.AddHours(2));
        var ex = Assert.Throws<DomainException>(() => a.TransitionTo(AppointmentStatus.NoShow, DateTime.UtcNow));
        Assert.Equal("invalid_status_transition", ex.Code);
    }

    [Fact]
    public void Cancelled_appointment_cannot_be_rescheduled()
    {
        var a = NewAppt();
        a.TransitionTo(AppointmentStatus.Cancelled, DateTime.UtcNow, "Paciente avisó");
        Assert.Throws<DomainException>(() => a.Reschedule(Guid.NewGuid(), null, DateTime.UtcNow.AddDays(2), DateTime.UtcNow.AddDays(2).AddMinutes(30), false));
    }

    [Fact]
    public void Reschedule_resets_to_pending_and_changes_version()
    {
        var a = NewAppt();
        a.TransitionTo(AppointmentStatus.Confirmed, DateTime.UtcNow);
        var v = a.Version;
        var s = DateTime.UtcNow.AddDays(3);
        a.Reschedule(a.ProfessionalUserId, null, s, s.AddMinutes(30), false);
        Assert.Equal(AppointmentStatus.Pending, a.Status);
        Assert.Equal(1, a.RescheduleCount);
        Assert.NotEqual(v, a.Version);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(481)]
    public void Duration_must_be_reasonable(int minutes)
    {
        var s = DateTime.UtcNow.AddDays(1);
        Assert.Throws<DomainException>(() => new Appointment(C, Guid.NewGuid(), Guid.NewGuid(), null, s, s.AddMinutes(minutes), null, null, false));
    }
}

public class IntervalSetTests
{
    private static DateTime T(int h, int m = 0) => new(2026, 10, 19, h, m, 0, DateTimeKind.Utc);

    [Fact]
    public void Normalize_merges_overlapping_ranges()
    {
        var r = IntervalSet.Normalize([new(T(9), T(10)), new(T(9, 30), T(11)), new(T(12), T(13))]);
        Assert.Equal(2, r.Count);
        Assert.Equal(new TimeRange(T(9), T(11)), r[0]);
    }

    [Fact]
    public void Subtract_splits_ranges()
    {
        var r = IntervalSet.Subtract([new(T(8), T(12))], [new(T(9), T(10)), new(T(11), T(11, 30))]);
        Assert.Equal([new TimeRange(T(8), T(9)), new TimeRange(T(10), T(11)), new TimeRange(T(11, 30), T(12))], r);
    }
}

public class AvailabilityCalculatorTests
{
    private static readonly TimeZoneInfo Ar = TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");
    private static readonly Guid C = Guid.NewGuid();
    private static readonly Guid Pro = Guid.NewGuid();
    private static readonly DateOnly Monday = new(2026, 10, 19);

    private static AvailabilityInput Input(IReadOnlyList<ScheduleBlock>? blocks = null, IReadOnlyList<TimeRange>? busy = null) =>
        new(Monday, Ar, Pro, null,
            [new WorkingHours(C, Pro, DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(12, 0), null),
             new WorkingHours(C, Pro, DayOfWeek.Monday, new TimeOnly(14, 0), new TimeOnly(18, 0), null)],
            blocks ?? [], busy ?? []);

    private static DateTime Local(int h, int m = 0) => Ar.LocalToUtc(Monday, new TimeOnly(h, m));

    [Fact]
    public void Working_ranges_are_converted_to_utc()
    {
        var ranges = AvailabilityCalculator.WorkingRanges(Input());
        Assert.Equal(2, ranges.Count);
        Assert.Equal(Local(8), ranges[0].Start);
        Assert.Equal(11, ranges[0].Start.Hour); // UTC-3
    }

    [Fact]
    public void Lunch_block_and_busy_appointments_are_excluded_from_slots()
    {
        var lunch = new ScheduleBlock(C, ScheduleBlockKind.Blocked, Local(11), Local(12), Pro, null, "Almuerzo");
        var busy = new List<TimeRange> { new(Local(9), Local(10)) };
        var slots = AvailabilityCalculator.Slots(Input([lunch], busy), 30, 30, DateTime.MinValue);
        Assert.DoesNotContain(slots, s => s.Start == Local(9));
        Assert.DoesNotContain(slots, s => s.Start == Local(11));
        Assert.Contains(slots, s => s.Start == Local(8));
        Assert.Contains(slots, s => s.Start == Local(10, 30));
        Assert.Contains(slots, s => s.Start == Local(17, 30));
        Assert.DoesNotContain(slots, s => s.Start == Local(12));
    }

    [Fact]
    public void Non_working_day_for_whole_consultorio_blocks_everything()
    {
        var (start, end) = Ar.DayRangeUtc(Monday, Monday);
        var holiday = new ScheduleBlock(C, ScheduleBlockKind.NonWorkingDay, start, end, null, null, "Feriado");
        Assert.Empty(AvailabilityCalculator.Slots(Input([holiday]), 30, 15, DateTime.MinValue));
    }

    [Fact]
    public void Extra_availability_adds_time_outside_working_hours()
    {
        var extra = new ScheduleBlock(C, ScheduleBlockKind.ExtraAvailability, Local(19), Local(20), Pro, null, "Atención extendida");
        Assert.True(AvailabilityCalculator.IsWithinWorkingHours(Input([extra]), new TimeRange(Local(19), Local(19, 45))));
    }

    [Fact]
    public void Request_outside_working_hours_is_rejected()
    {
        Assert.False(AvailabilityCalculator.IsWithinWorkingHours(Input(), new TimeRange(Local(12, 30), Local(13))));
        Assert.False(AvailabilityCalculator.IsWithinWorkingHours(Input(), new TimeRange(Local(11, 30), Local(12, 30))));
        Assert.True(AvailabilityCalculator.IsWithinWorkingHours(Input(), new TimeRange(Local(14), Local(15))));
    }

    [Fact]
    public void Blocks_of_other_professionals_do_not_apply()
    {
        var other = new ScheduleBlock(C, ScheduleBlockKind.Blocked, Local(8), Local(12), Guid.NewGuid(), null, "Otro");
        Assert.True(AvailabilityCalculator.IsWithinWorkingHours(Input([other]), new TimeRange(Local(9), Local(10))));
    }

    [Fact]
    public void Slots_respect_not_before()
    {
        var slots = AvailabilityCalculator.Slots(Input(), 60, 60, Local(16));
        Assert.All(slots, s => Assert.True(s.Start >= Local(16)));
        Assert.Equal(2, slots.Count); // 16-17, 17-18
    }
}

public class TaskItemTests
{
    [Fact]
    public void Completing_sets_completion_data_and_reopening_clears_it()
    {
        var t = new TaskItem(Guid.NewGuid(), "Confirmar turnos", null, TaskPriority.High, TaskCategory.Appointments, null, DateTime.UtcNow.AddHours(-1), null, null);
        Assert.True(t.IsOverdue(DateTime.UtcNow));
        var u = Guid.NewGuid();
        t.SetStatus(TaskItemStatus.Completed, u, DateTime.UtcNow);
        Assert.Equal(u, t.CompletedByUserId);
        Assert.False(t.IsOverdue(DateTime.UtcNow));
        t.SetStatus(TaskItemStatus.Pending, u, DateTime.UtcNow);
        Assert.Null(t.CompletedAtUtc);
    }
}
