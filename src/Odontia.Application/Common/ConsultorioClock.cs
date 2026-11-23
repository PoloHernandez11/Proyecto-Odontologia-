using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Odontia.Application.Abstractions;

namespace Odontia.Application.Common;

/// <summary>Hora local del consultorio activo (las fechas se persisten en UTC; la agenda razona en hora local).</summary>
public interface IConsultorioClock
{
    Task<TimeZoneInfo> GetTimeZoneAsync(CancellationToken ct);
    Task<DateOnly> TodayAsync(CancellationToken ct);
    DateTime UtcNow { get; }
}

internal sealed class ConsultorioClock(IApplicationDbContext db, ITenantContext tenant, TimeProvider clock,
    ILogger<ConsultorioClock>? logger = null) : IConsultorioClock
{
    private readonly ILogger _logger = logger ?? NullLogger<ConsultorioClock>.Instance;
    private TimeZoneInfo? _tz;

    public DateTime UtcNow => clock.GetUtcNow().UtcDateTime;

    public async Task<TimeZoneInfo> GetTimeZoneAsync(CancellationToken ct)
    {
        if (_tz is not null) return _tz;
        // Se resuelve fuera de la consulta: sin consultorio activo la ForbiddenAccessException (403) llega tal cual,
        // en lugar de quedar envuelta por EF en una InvalidOperationException (500).
        var cid = tenant.ConsultorioId;
        var id = await db.Consultorios.Where(c => c.Id == cid).Select(c => c.TimeZoneId).FirstAsync(ct);
        if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var tz))
        {
            _tz = tz;
        }
        else
        {
            _logger.LogWarning("Zona horaria '{TimeZoneId}' del consultorio {ConsultorioId} no reconocida; se usa UTC.", id, cid);
            _tz = TimeZoneInfo.Utc;
        }
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
