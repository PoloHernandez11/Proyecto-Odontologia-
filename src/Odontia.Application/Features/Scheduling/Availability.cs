using Odontia.Application.Common;
using Odontia.Domain.Entities;
using Odontia.Domain.Enums;

namespace Odontia.Application.Features.Scheduling;

/// <summary>Intervalo semiabierto [Start, End) en UTC.</summary>
public readonly record struct TimeRange(DateTime Start, DateTime End)
{
    public bool Overlaps(TimeRange o) => Start < o.End && o.Start < End;
    public bool Contains(TimeRange o) => Start <= o.Start && o.End <= End;
    public TimeSpan Duration => End - Start;
}

/// <summary>Operaciones de conjuntos de intervalos (unión normalizada y diferencia).</summary>
public static class IntervalSet
{
    public static List<TimeRange> Normalize(IEnumerable<TimeRange> ranges)
    {
        var sorted = ranges.Where(r => r.End > r.Start).OrderBy(r => r.Start).ToList();
        var result = new List<TimeRange>();
        foreach (var r in sorted)
        {
            if (result.Count > 0 && r.Start <= result[^1].End)
                result[^1] = new TimeRange(result[^1].Start, r.End > result[^1].End ? r.End : result[^1].End);
            else
                result.Add(r);
        }
        return result;
    }

    public static List<TimeRange> Subtract(IEnumerable<TimeRange> source, IEnumerable<TimeRange> remove)
    {
        var current = Normalize(source);
        foreach (var cut in Normalize(remove))
        {
            var next = new List<TimeRange>();
            foreach (var r in current)
            {
                if (!r.Overlaps(cut)) { next.Add(r); continue; }
                if (cut.Start > r.Start) next.Add(new TimeRange(r.Start, cut.Start));
                if (cut.End < r.End) next.Add(new TimeRange(cut.End, r.End));
            }
            current = next;
        }
        return current;
    }
}

/// <summary>Datos necesarios para calcular la disponibilidad de un profesional en un día.</summary>
public sealed record AvailabilityInput(
    DateOnly Date,
    TimeZoneInfo TimeZone,
    Guid ProfessionalUserId,
    Guid? GabineteId,
    IReadOnlyList<WorkingHours> WorkingHours,
    IReadOnlyList<ScheduleBlock> Blocks,
    IReadOnlyList<TimeRange> BusyRanges);

/// <summary>Cálculo puro de disponibilidad (testeable sin base de datos).</summary>
public static class AvailabilityCalculator
{
    /// <summary>Franjas en que el profesional atiende ese día (horario laboral + excepciones − bloqueos).</summary>
    public static List<TimeRange> WorkingRanges(AvailabilityInput input)
    {
        var tz = input.TimeZone;
        var day = input.Date.DayOfWeek;
        var ranges = input.WorkingHours
            .Where(w => w.ProfessionalUserId == input.ProfessionalUserId && w.DayOfWeek == day
                        && (input.GabineteId is null || w.GabineteId is null || w.GabineteId == input.GabineteId))
            .Select(w => new TimeRange(tz.LocalToUtc(input.Date, w.StartTime), tz.LocalToUtc(input.Date, w.EndTime)))
            .ToList();

        var (dayStart, dayEnd) = tz.DayRangeUtc(input.Date, input.Date);
        var dayRange = new TimeRange(dayStart, dayEnd);

        ranges.AddRange(input.Blocks
            .Where(b => b.Kind == ScheduleBlockKind.ExtraAvailability && b.ProfessionalUserId == input.ProfessionalUserId)
            .Select(b => Clip(new TimeRange(b.StartUtc, b.EndUtc), dayRange))
            .Where(r => r.End > r.Start));

        var blocked = input.Blocks
            .Where(b => b.Kind is ScheduleBlockKind.Blocked or ScheduleBlockKind.NonWorkingDay && b.AppliesTo(input.ProfessionalUserId, input.GabineteId))
            .Select(b => new TimeRange(b.StartUtc, b.EndUtc));

        return IntervalSet.Subtract(ranges, blocked);
    }

    /// <summary>Franjas libres (horario disponible − turnos ocupados).</summary>
    public static List<TimeRange> FreeRanges(AvailabilityInput input) =>
        IntervalSet.Subtract(WorkingRanges(input), input.BusyRanges);

    /// <summary>Genera turnos posibles de <paramref name="durationMinutes"/> alineados al intervalo de agenda.</summary>
    public static List<TimeRange> Slots(AvailabilityInput input, int durationMinutes, int stepMinutes, DateTime notBeforeUtc)
    {
        var duration = TimeSpan.FromMinutes(durationMinutes);
        var step = TimeSpan.FromMinutes(stepMinutes);
        var slots = new List<TimeRange>();
        foreach (var free in FreeRanges(input))
        {
            var local = input.TimeZone.UtcToLocal(free.Start);
            // Alinear al múltiplo del intervalo en hora local (p. ej. 09:00, 09:15...).
            var minutes = local.Hour * 60 + local.Minute;
            var aligned = (int)Math.Ceiling(minutes / (double)stepMinutes) * stepMinutes;
            var start = free.Start.AddMinutes(aligned - minutes).AddSeconds(-local.Second);
            for (var s = start; s + duration <= free.End; s += step)
                if (s >= notBeforeUtc) slots.Add(new TimeRange(s, s + duration));
        }
        return slots;
    }

    /// <summary>¿El rango solicitado cae íntegramente dentro del horario disponible del profesional?</summary>
    public static bool IsWithinWorkingHours(AvailabilityInput input, TimeRange requested) =>
        WorkingRanges(input).Any(r => r.Contains(requested));

    private static TimeRange Clip(TimeRange r, TimeRange bounds) =>
        new(r.Start < bounds.Start ? bounds.Start : r.Start, r.End > bounds.End ? bounds.End : r.End);
}
