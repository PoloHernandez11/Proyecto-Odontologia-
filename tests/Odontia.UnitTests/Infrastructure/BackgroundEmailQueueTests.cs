using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Odontia.Application.Abstractions;
using Odontia.Application.Features.Auth;
using Odontia.Domain.Entities;
using Odontia.Domain.Enums;
using Odontia.Infrastructure.Messaging;

namespace Odontia.UnitTests.Infrastructure;

/// <summary>REV-04: la cola de correos reintenta los envíos fallidos y drena lo pendiente en el apagado.</summary>
public sealed class BackgroundEmailQueueTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private static DeferredEmail Mail(string to = "dest@test.local") => new(null, to, "Asunto", "<p>x</p>", "x", "test", false, null, null);

    private static (BackgroundEmailQueue Queue, BackgroundEmailSender Sender) Create(FakeDispatcher dispatcher, params TimeSpan[] delays)
    {
        var services = new ServiceCollection();
        services.AddScoped<IMessageDispatcher>(_ => dispatcher);
        var sp = services.BuildServiceProvider();
        var queue = new BackgroundEmailQueue(NullLogger<BackgroundEmailQueue>.Instance) { RetryDelays = delays };
        var sender = new BackgroundEmailSender(queue, sp.GetRequiredService<IServiceScopeFactory>(), NullLogger<BackgroundEmailSender>.Instance);
        return (queue, sender);
    }

    [Fact]
    public async Task Failed_send_is_retried_until_it_succeeds()
    {
        var dispatcher = new FakeDispatcher(failuresBeforeSuccess: 2);
        var (queue, sender) = Create(dispatcher, TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(10));
        await sender.StartAsync(CancellationToken.None);
        queue.Enqueue(Mail());

        await dispatcher.WaitForAsync(OutboundMessageStatus.Sent, Timeout);
        Assert.Equal([OutboundMessageStatus.Failed, OutboundMessageStatus.Failed, OutboundMessageStatus.Sent], dispatcher.Results);
        await sender.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Retries_are_bounded_and_the_email_is_dropped_after_the_last_attempt()
    {
        var dispatcher = new FakeDispatcher(failuresBeforeSuccess: int.MaxValue);
        var (queue, sender) = Create(dispatcher, TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(10));
        await sender.StartAsync(CancellationToken.None);
        queue.Enqueue(Mail());

        await dispatcher.WaitForCountAsync(3, Timeout); // 1 intento + 2 reintentos
        await Task.Delay(200);
        Assert.Equal(3, dispatcher.Results.Count);
        await sender.StopAsync(CancellationToken.None);
        Assert.Equal(3, dispatcher.Results.Count);
    }

    [Fact]
    public async Task Without_provider_the_email_is_not_retried()
    {
        var dispatcher = new FakeDispatcher(failuresBeforeSuccess: 0, configured: false);
        var (queue, sender) = Create(dispatcher, TimeSpan.FromMilliseconds(10));
        await sender.StartAsync(CancellationToken.None);
        queue.Enqueue(Mail());

        await dispatcher.WaitForCountAsync(1, Timeout);
        await Task.Delay(150);
        Assert.Equal([OutboundMessageStatus.NotSentNoProvider], dispatcher.Results);
        await sender.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Graceful_shutdown_drains_pending_emails_and_brings_scheduled_retries_forward()
    {
        // Primer envío falla y queda un reintento a 1 hora; además hay correos aún sin procesar.
        using var gate = new SemaphoreSlim(0);
        var dispatcher = new FakeDispatcher(failuresBeforeSuccess: 1) { Gate = gate };
        var (queue, sender) = Create(dispatcher, TimeSpan.FromHours(1));
        await sender.StartAsync(CancellationToken.None);
        queue.Enqueue(Mail("a@test.local"));
        queue.Enqueue(Mail("b@test.local"));
        queue.Enqueue(Mail("c@test.local"));
        gate.Release(); // sólo procesa el primero (falla → reintento programado)
        await dispatcher.WaitForCountAsync(1, Timeout);

        gate.Release(10);
        var stop = sender.StopAsync(new CancellationTokenSource(Timeout).Token);
        await stop.WaitAsync(Timeout);

        // b y c se enviaron en el drenado y el reintento de a se adelantó (último intento).
        Assert.Equal(4, dispatcher.Results.Count);
        Assert.Equal(3, dispatcher.Results.Count(s => s == OutboundMessageStatus.Sent));
        Assert.Contains("a@test.local", dispatcher.SentTo);
    }

    [Fact]
    public async Task Shutdown_timeout_aborts_the_drain()
    {
        using var gate = new SemaphoreSlim(0);
        var dispatcher = new FakeDispatcher(failuresBeforeSuccess: 0) { Gate = gate };
        var (queue, sender) = Create(dispatcher, TimeSpan.FromMilliseconds(10));
        await sender.StartAsync(CancellationToken.None);
        queue.Enqueue(Mail());
        queue.Enqueue(Mail());

        // El proveedor nunca responde: al vencer el timeout de apagado se aborta sin colgar el host.
        using var shutdown = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await sender.StopAsync(shutdown.Token).WaitAsync(Timeout);
        Assert.Empty(dispatcher.Results);
    }

    private sealed class FakeDispatcher(int failuresBeforeSuccess, bool configured = true) : IMessageDispatcher
    {
        private int _calls;
        private readonly ConcurrentQueue<(OutboundMessageStatus Status, string To)> _results = new();
        public SemaphoreSlim? Gate { get; set; }

        public List<OutboundMessageStatus> Results => _results.Select(r => r.Status).ToList();
        public List<string> SentTo => _results.Where(r => r.Status == OutboundMessageStatus.Sent).Select(r => r.To).ToList();

        public bool IsChannelConfigured(MessageChannel channel) => configured;

        public async Task<OutboundMessage> SendEmailAsync(Guid? consultorioId, string to, string subject, string html, string text, string purpose,
            bool containsSecret, string? relatedType, Guid? relatedId, CancellationToken ct)
        {
            if (Gate is { } gate) await gate.WaitAsync(ct);
            var m = new OutboundMessage(consultorioId, MessageChannel.Email, to, subject, text, purpose, relatedType, relatedId, DateTime.UtcNow, containsSecret);
            var fail = Interlocked.Increment(ref _calls) <= failuresBeforeSuccess;
            m.RecordResult("fake", !fail, configured, fail ? null : "id", fail ? "SMTP caído" : null, DateTime.UtcNow);
            _results.Enqueue((m.Status, to));
            return m;
        }

        public Task<OutboundMessage> SendWhatsAppAsync(Guid? consultorioId, string toPhone, string text, string purpose, string? relatedType,
            Guid? relatedId, CancellationToken ct) => throw new NotSupportedException();

        public async Task WaitForCountAsync(int count, TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource(timeout);
            while (_results.Count < count) await Task.Delay(10, cts.Token);
        }

        public async Task WaitForAsync(OutboundMessageStatus status, TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource(timeout);
            while (!_results.Any(r => r.Status == status)) await Task.Delay(10, cts.Token);
        }
    }
}
