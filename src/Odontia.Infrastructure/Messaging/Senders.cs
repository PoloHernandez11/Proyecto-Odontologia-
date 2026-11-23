using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Odontia.Application.Abstractions;
using Odontia.Application.Common;

namespace Odontia.Infrastructure.Messaging;

public sealed class EmailOptions
{
    public const string Section = "Email";
    /// <summary>"Smtp" para envío real; "DevelopmentFile" (sólo desarrollo) guarda .eml locales sin enviar; "None" no envía.</summary>
    public string Provider { get; set; } = "None";
    public string FromAddress { get; set; } = "no-reply@odontia.local";
    public string FromName { get; set; } = "Odentix";
    public SmtpOptions Smtp { get; set; } = new();
    /// <summary>Carpeta privada para el modo DevelopmentFile.</summary>
    public string DevelopmentOutboxPath { get; set; } = "";
}

public sealed class SmtpOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string? UserName { get; set; }
    public string? Password { get; set; }
    /// <summary>Tiempo máximo por envío (conexión + entrega). Evita que un SMTP lento retenga recursos 100 s.</summary>
    public int TimeoutSeconds { get; set; } = 15;
}

public sealed class WhatsAppOptions
{
    public const string Section = "WhatsApp";
    /// <summary>"None" (por defecto) o "DevelopmentLog". La integración con un proveedor real requiere implementar <see cref="IWhatsAppSender"/>.</summary>
    public string Provider { get; set; } = "None";
}

/// <summary>Envío SMTP real. Sólo informa aceptación cuando el servidor SMTP aceptó el mensaje.</summary>
internal sealed class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.Smtp.Host);

    public async Task<SendResult> SendAsync(OutgoingEmail email, CancellationToken ct)
    {
        var o = options.Value;
        if (string.IsNullOrWhiteSpace(o.Smtp.Host)) return SendResult.NoProvider("smtp");
        var timeout = TimeSpan.FromSeconds(Math.Clamp(o.Smtp.TimeoutSeconds, 1, 120));
        using var client = new SmtpClient(o.Smtp.Host, o.Smtp.Port)
        {
            EnableSsl = o.Smtp.EnableSsl, DeliveryMethod = SmtpDeliveryMethod.Network, Timeout = (int)timeout.TotalMilliseconds,
        };
        if (!string.IsNullOrWhiteSpace(o.Smtp.UserName)) client.Credentials = new NetworkCredential(o.Smtp.UserName, o.Smtp.Password);
        using var message = new MailMessage
        {
            From = new MailAddress(o.FromAddress, o.FromName),
            Subject = email.Subject,
            Body = email.HtmlBody,
            IsBodyHtml = true,
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8,
        };
        message.To.Add(email.To);
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(email.TextBody, Encoding.UTF8, "text/plain"));
        // El envío asincrónico se acota además con un token propio (SmtpClient.Timeout no cubre todas las fases).
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            await client.SendMailAsync(message, timeoutCts.Token);
            return new SendResult(true, true, "smtp", message.Headers["Message-ID"], null);
        }
        catch (Exception ex) when (ex is (OperationCanceledException or SmtpException) && !ct.IsCancellationRequested
                                   && (timeoutCts.IsCancellationRequested || System.Diagnostics.Stopwatch.GetElapsedTime(started) >= timeout * 0.9))
        {
            // Vencido el plazo, SmtpClient informa OperationCanceledException o SmtpException(GeneralFailure) según la fase.
            return new SendResult(true, false, "smtp", null, "timeout");
        }
        catch (SmtpException ex)
        {
            return new SendResult(true, false, "smtp", null, ex.StatusCode.ToString());
        }
    }
}

/// <summary>
/// SÓLO DESARROLLO: guarda el correo como .eml en una carpeta privada local para poder probar los flujos
/// (confirmación, recuperación, invitaciones). No envía nada y lo informa como "no enviado".
/// </summary>
internal sealed class DevelopmentFileEmailSender(IOptions<EmailOptions> options, IHostEnvironment env, ILogger<DevelopmentFileEmailSender> logger) : IEmailSender
{
    /// <summary>No entrega correos: los envíos automáticos (recordatorios) no deben considerarlo un proveedor.</summary>
    public bool IsConfigured => false;

    public async Task<SendResult> SendAsync(OutgoingEmail email, CancellationToken ct)
    {
        if (!env.IsDevelopment()) return SendResult.NoProvider("development-file");
        var dir = string.IsNullOrWhiteSpace(options.Value.DevelopmentOutboxPath)
            ? Path.Combine(env.ContentRootPath, "..", "..", "storage", "dev-outbox")
            : options.Value.DevelopmentOutboxPath;
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.eml");
        var content = $"To: {email.To}\r\nSubject: {email.Subject}\r\nContent-Type: text/plain; charset=utf-8\r\n\r\n{email.TextBody}\r\n";
        await File.WriteAllTextAsync(file, content, Encoding.UTF8, ct);
        logger.LogInformation("[DEV] Correo no enviado (sin proveedor). Guardado en outbox local para {Recipient}", Mask.Email(email.To));
        return SendResult.NoProvider("development-file");
    }
}

internal sealed class NoEmailSender(ILogger<NoEmailSender> logger) : IEmailSender
{
    public bool IsConfigured => false;

    public Task<SendResult> SendAsync(OutgoingEmail email, CancellationToken ct)
    {
        logger.LogWarning("No hay proveedor de correo configurado: el mensaje a {Recipient} no fue enviado.", Mask.Email(email.To));
        return Task.FromResult(SendResult.NoProvider("none"));
    }
}

/// <summary>Sin proveedor de WhatsApp configurado: registra el intento sin enviar ni afirmar envío.</summary>
internal sealed class NoWhatsAppSender(IOptions<WhatsAppOptions> options, ILogger<NoWhatsAppSender> logger) : IWhatsAppSender
{
    public bool IsConfigured => false;

    public Task<SendResult> SendAsync(OutgoingWhatsApp message, CancellationToken ct)
    {
        var provider = options.Value.Provider.Equals("DevelopmentLog", StringComparison.OrdinalIgnoreCase) ? "development-log" : "none";
        logger.LogInformation("WhatsApp no enviado (proveedor no configurado) a {Recipient}", Mask.Phone(message.ToPhoneE164));
        return Task.FromResult(SendResult.NoProvider(provider));
    }
}
