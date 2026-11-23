using System.Collections.Concurrent;
using Odontia.Application.Features.Auth;

namespace Odontia.Infrastructure.Security;

/// <summary>Implementación en memoria (instancia única) de <see cref="IOneTimeUseStore"/>, con purga periódica de vencidos.</summary>
internal sealed class InMemoryOneTimeUseStore(TimeProvider clock) : IOneTimeUseStore
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _entries = new(StringComparer.Ordinal);
    private int _operations;

    public bool TryReserve(string key, DateTimeOffset expiresAtUtc)
    {
        var now = clock.GetUtcNow();
        if (Interlocked.Increment(ref _operations) % 256 == 0) Purge(now);
        while (true)
        {
            if (_entries.TryAdd(key, expiresAtUtc)) return true;
            if (!_entries.TryGetValue(key, out var existing)) continue; // liberada entre medio: reintentar
            if (existing > now) return false;
            // Reserva vencida: se reemplaza sólo si nadie la cambió entre medio.
            if (_entries.TryUpdate(key, expiresAtUtc, existing)) return true;
        }
    }

    public void Release(string key) => _entries.TryRemove(key, out _);

    private void Purge(DateTimeOffset now)
    {
        foreach (var (key, expires) in _entries)
            if (expires <= now) _entries.TryRemove(new KeyValuePair<string, DateTimeOffset>(key, expires));
    }
}
