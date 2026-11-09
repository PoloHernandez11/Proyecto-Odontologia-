using Microsoft.EntityFrameworkCore;
using Odontia.Application.Abstractions;

namespace Odontia.Application.Common;

/// <summary>Hora local del consultorio activo (las fechas se persisten en UTC; la agenda razona en hora local).</summary>
public interface IConsultorioClock
{
    Task<TimeZoneInfo> GetTimeZoneAsync(CancellationToken ct);
    Task<DateOnly> TodayAsync(CancellationToken ct);
    DateTime UtcNow { get; }
}

internal sealed class ConsultorioClock(IApplicationDbContext db, ITenantContext tenant, TimeProvider clock) : IConsultorioClock
{
    private TimeZoneInfo? _tz;

    public DateTime UtcNow => clock.GetUtcNow().UtcDateTime;

    public async Task<TimeZoneInfo> GetTimeZoneAsync(CancellationToken ct)
    {
        if (_tz is not null) return _tz;
        var id = await db.Consultorios.Where(c => c.Id == tenant.ConsultorioId).Select(c => c.TimeZoneId).FirstAsync(ct);
        _tz = TimeZoneInfo.TryFindSystemTimeZoneById(id, out var tz) ? tz : TimeZoneInfo.Utc;
        return _tz;
    }

    public async Task<DateOnly> TodayAsync(CancellationToken ct)
    {
        var tz = await GetTimeZoneAsync(ct);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(UtcNow, tz));
    }
}

public static class TimeZoneExtensions
{
    /// <summary>Convierte una fecha/hora local del consultorio a UTC (resolviendo horas ambiguas/inválidas por DST).</summary>
    public static DateTime LocalToUtc(this TimeZoneInfo tz, DateOnly date, TimeOnly time)
    {
        var local = DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Unspecified);
        if (tz.IsInvalidTime(local)) local = local.AddHours(1);
        return TimeZoneInfo.ConvertTimeToUtc(local, tz);
    }

    public static DateTime UtcToLocal(this TimeZoneInfo tz, DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), tz);

    public static (DateTime StartUtc, DateTime EndUtc) DayRangeUtc(this TimeZoneInfo tz, DateOnly from, DateOnly toInclusive) =>
        (tz.LocalToUtc(from, TimeOnly.MinValue), tz.LocalToUtc(toInclusive.AddDays(1), TimeOnly.MinValue));
}
