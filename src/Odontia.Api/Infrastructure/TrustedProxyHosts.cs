using System.Net;

namespace Odontia.Api.Infrastructure;

/// <summary>
/// Proxys de confianza identificados por nombre de host (p. ej. el contenedor de Caddy "miaula-web-1"),
/// resueltos por DNS y re-resueltos periódicamente o cuando llega una IP desconocida (la IP del contenedor
/// puede cambiar si se recrea). Si la resolución de un host falla se conservan sus últimas IPs conocidas
/// (sólo si nunca se resolvió no se confía en nadie): una caída breve del DNS no debe hacer que todos los
/// clientes compartan la IP del proxy a efectos del rate limiting por IP.
/// </summary>
public sealed class TrustedProxyHosts
{
    private readonly string[] _hosts;
    private readonly TimeSpan _refresh;
    private readonly TimeSpan _retryOnMiss;
    private readonly Func<string, CancellationToken, Task<IPAddress[]>> _resolve;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile IReadOnlySet<IPAddress> _addresses = new HashSet<IPAddress>();
    // Última resolución exitosa por host (sólo se accede bajo _gate).
    private readonly Dictionary<string, IPAddress[]> _lastGood = new(StringComparer.OrdinalIgnoreCase);
    private long _lastResolvedTicks = long.MinValue;

    public TrustedProxyHosts(
        IEnumerable<string> hosts,
        TimeSpan refresh,
        TimeSpan retryOnMiss,
        Func<string, CancellationToken, Task<IPAddress[]>> resolve,
        TimeProvider time,
        ILogger<TrustedProxyHosts> logger)
    {
        _hosts = hosts.Where(h => !string.IsNullOrWhiteSpace(h)).Select(h => h.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        _refresh = refresh;
        _retryOnMiss = retryOnMiss;
        _resolve = resolve;
        _time = time;
        _logger = logger;
    }

    /// <summary>Resolución DNS real con un tiempo máximo por host.</summary>
    public static async Task<IPAddress[]> ResolveWithDnsAsync(string host, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(3));
        return await Dns.GetHostAddressesAsync(host, cts.Token);
    }

    public async ValueTask<bool> IsTrustedAsync(IPAddress? remote, CancellationToken ct = default)
    {
        if (remote is null || _hosts.Length == 0) return false;
        var ip = Normalize(remote);
        var elapsed = Elapsed();
        var known = _addresses.Contains(ip);
        if (elapsed >= _refresh || (!known && elapsed >= _retryOnMiss))
        {
            await RefreshAsync(ct);
            known = _addresses.Contains(ip);
        }
        return known;
    }

    private TimeSpan Elapsed()
    {
        var last = Interlocked.Read(ref _lastResolvedTicks);
        return last == long.MinValue ? TimeSpan.MaxValue : TimeSpan.FromTicks(_time.GetUtcNow().UtcTicks - last);
    }

    private async Task RefreshAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            // Otro request pudo haber resuelto mientras se esperaba el semáforo.
            if (Elapsed() < _retryOnMiss) return;
            var resolved = new HashSet<IPAddress>();
            foreach (var host in _hosts)
            {
                try
                {
                    var addresses = (await _resolve(host, ct)).Select(Normalize).ToArray();
                    _lastGood[host] = addresses;
                    resolved.UnionWith(addresses);
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
                {
                    if (_lastGood.TryGetValue(host, out var previous))
                    {
                        resolved.UnionWith(previous);
                        _logger.LogWarning("No se pudo resolver el proxy de confianza {Host}: {Error}. Se conservan sus últimas IPs conocidas ({Addresses}).",
                            host, ex.GetType().Name, previous.Length == 0 ? "ninguna" : string.Join(",", (IEnumerable<IPAddress>)previous));
                    }
                    else
                    {
                        _logger.LogWarning("No se pudo resolver el proxy de confianza {Host}: {Error}. No se confía en sus cabeceras X-Forwarded-*.", host, ex.GetType().Name);
                    }
                }
            }
            if (!resolved.SetEquals(_addresses))
                _logger.LogInformation("Proxys de confianza ({Hosts}): {Addresses}", string.Join(",", _hosts),
                    resolved.Count == 0 ? "ninguno" : string.Join(",", resolved));
            _addresses = resolved;
            Interlocked.Exchange(ref _lastResolvedTicks, _time.GetUtcNow().UtcTicks);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static IPAddress Normalize(IPAddress ip) => ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip;
}

/// <summary>
/// Va antes de UseForwardedHeaders: si la conexión no viene de un proxy de confianza, descarta las cabeceras
/// X-Forwarded-* para que nadie más en la red compartida pueda falsear la IP del cliente o el esquema.
/// </summary>
public sealed class TrustedProxyHostsMiddleware(RequestDelegate next, TrustedProxyHosts trust)
{
    private static readonly string[] Forwarded = ["X-Forwarded-For", "X-Forwarded-Proto", "X-Forwarded-Host", "X-Forwarded-Prefix"];

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Request.Headers;
        if (Forwarded.Any(headers.ContainsKey) && !await trust.IsTrustedAsync(context.Connection.RemoteIpAddress, context.RequestAborted))
            foreach (var h in Forwarded) headers.Remove(h);
        await next(context);
    }
}
