using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Odontia.Application.Abstractions;
using Odontia.Application.Common;
using Odontia.Application.Features.Auth;
using Odontia.Domain.Enums;

namespace Odontia.Infrastructure.Messaging;

/// <summary>Correo en la cola con el número de intentos ya realizados.</summary>
internal sealed record QueuedEmail(DeferredEmail Email, int Attempts);

/// <summary>Cola acotada en memoria. Si se llena (proveedor caído), descarta y lo registra sin bloquear la solicitud.</summary>
internal sealed class BackgroundEmailQueue(ILogger<BackgroundEmailQueue> logger) : IEmailQueue
{
    /// <summary>Esperas entre reintentos de un envío fallido (SMTP caído, timeout). Tras agotarlas, el correo se descarta.</summary>
    internal static readonly TimeSpan[] DefaultRetryDelays = [TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2)];

    private readonly Channel<QueuedEmail> _channel = Channel.CreateBounded<QueuedEmail>(
        new BoundedChannelOptions(1000) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });

    public ChannelReader<QueuedEmail> Reader => _channel.Reader;

    internal IReadOnlyList<TimeSpan> RetryDelays { get; set; } = DefaultRetryDelays;

    public void Enqueue(DeferredEmail email) => Requeue(new QueuedEmail(email, 0));

    /// <summary>Vuelve a encolar un correo (primer envío o reintento). Devuelve false si la cola está llena.</summary>
    internal bool Requeue(QueuedEmail item)
    {
        if (_channel.Writer.TryWrite(item)) return true;
        logger.LogError("Cola de correos llena: se descartó el correo {Purpose} a {Recipient} (intentos previos: {Attempts})",
            item.Email.Purpose, Mask.Email(item.Email.To), item.Attempts);
        return false;
    }
}

/// <summary>
/// Envía los correos encolados, cada uno en su propio scope de DI (DbContext y proveedor propios). Un envío fallido
/// (estado Failed) se reintenta con espera creciente sin bloquear al resto de la cola. En el apagado ordenado se
/// drena lo pendiente (incluidos los reintentos programados, que se adelantan) hasta el timeout de apagado del host.
/// </summary>
internal sealed class BackgroundEmailSender(BackgroundEmailQueue queue, IServiceScopeFactory scopes, ILogger<BackgroundEmailSender> logger)
    : BackgroundService
{
    private readonly CancellationTokenSource _abortDrain = new();
    private int _pendingRetries;

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        // El token se cancela cuando vence el timeout de apagado del host: ahí se aborta el drenado.
        await using var reg = cancellationToken.Register(() => _abortDrain.Cancel());
        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        _abortDrain.Dispose();
        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var item in queue.Reader.ReadAllAsync(stoppingToken))
                await ProcessAsync(item, retryUntil: stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // apagado ordenado: se drena lo pendiente
        }
        await DrainAsync();
    }

    private async Task DrainAsync()
    {
        var abort = _abortDrain.Token;
        var sent = 0;
        try
        {
            while (true)
            {
                while (queue.Reader.TryRead(out var item))
                {
                    await ProcessAsync(item, retryUntil: null);
                    sent++;
                }
                // Los reintentos programados se re-encolan apenas se cancela su espera (antes de descontarse).
                if (Volatile.Read(ref _pendingRetries) == 0 && !queue.Reader.TryPeek(out _)) break;
                await Task.Delay(TimeSpan.FromMilliseconds(20), abort);
            }
            if (sent > 0) logger.LogInformation("Apagado: se procesaron {Count} correos pendientes de la cola", sent);
        }
        catch (OperationCanceledException)
        {
            var left = 0;
            while (queue.Reader.TryRead(out var item))
            {
                left++;
                logger.LogError("Apagado: se descartó el correo {Purpose} a {Recipient} (venció el tiempo de apagado)", item.Email.Purpose, Mask.Email(item.Email.To));
            }
            logger.LogError("Apagado: tiempo agotado drenando la cola de correos; descartados {Count}", left + Volatile.Read(ref _pendingRetries));
        }
    }

    /// <param name="retryUntil">Token de detención para programar reintentos; null (drenado del apagado) = último intento.</param>
    private async Task ProcessAsync(QueuedEmail item, CancellationToken? retryUntil)
    {
        // El envío en curso no se corta al iniciar el apagado: sólo al vencer su timeout.
        var ct = _abortDrain.Token;
        var email = item.Email;
        var attempt = item.Attempts + 1;
        OutboundMessageStatus status;
        try
        {
            using var scope = scopes.CreateScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IMessageDispatcher>();
            var msg = await dispatcher.SendEmailAsync(email.ConsultorioId, email.To, email.Subject, email.Html, email.Text, email.Purpose,
                email.ContainsSecret, email.RelatedType, email.RelatedId, ct);
            status = msg.Status;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            logger.LogError("Apagado: se descartó el correo {Purpose} a {Recipient} (envío interrumpido por el tiempo de apagado)", email.Purpose, Mask.Email(email.To));
            throw;
        }
        catch (Exception ex)
        {
            // Error fuera del envío (p. ej. al registrar el mensaje): no se reintenta para no duplicar un correo ya enviado.
            logger.LogError(ex, "No se pudo procesar el correo encolado {Purpose} a {Recipient}; se descarta", email.Purpose, Mask.Email(email.To));
            return;
        }

        if (status != OutboundMessageStatus.Failed) return;

        var delays = queue.RetryDelays;
        if (retryUntil is not { } stoppingToken || item.Attempts >= delays.Count)
        {
            logger.LogError("Se descartó el correo {Purpose} a {Recipient} tras {Attempts} intentos fallidos", email.Purpose, Mask.Email(email.To), attempt);
            return;
        }
        var delay = delays[item.Attempts];
        logger.LogWarning("Falló el envío del correo {Purpose} a {Recipient} (intento {Attempt}); se reintenta en {Delay}",
            email.Purpose, Mask.Email(email.To), attempt, delay);
        ScheduleRetry(item with { Attempts = attempt }, delay, stoppingToken);
    }

    /// <summary>Re-encola tras la espera sin bloquear la lectura. Si el host se apaga, se re-encola de inmediato para el drenado.</summary>
    private void ScheduleRetry(QueuedEmail item, TimeSpan delay, CancellationToken stoppingToken)
    {
        Interlocked.Increment(ref _pendingRetries);
        _ = Task.Run(async () =>
        {
            try
            {
                try { await Task.Delay(delay, stoppingToken); }
                catch (OperationCanceledException) { /* apagado: se adelanta el reintento */ }
                queue.Requeue(item);
            }
            finally
            {
                Interlocked.Decrement(ref _pendingRetries);
            }
        }, CancellationToken.None);
    }
}
