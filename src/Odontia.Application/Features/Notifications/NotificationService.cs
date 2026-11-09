using Microsoft.EntityFrameworkCore;
using Odontia.Application.Abstractions;
using Odontia.Application.Common;
using Odontia.Domain.Authorization;
using Odontia.Domain.Entities;
using Odontia.Domain.Enums;

namespace Odontia.Application.Features.Notifications;

public sealed record NotificationDto(Guid Id, NotificationType Type, string Title, string? Message, string? EntityType, Guid? EntityId,
    DateTime CreatedAtUtc, DateTime? ReadAtUtc);

public sealed class NotificationQuery : PageQuery
{
    public bool UnreadOnly { get; set; }
}

public sealed record UnreadCountDto(int Unread);

public sealed record OutboundMessageDto(Guid Id, MessageChannel Channel, string Recipient, string? Subject, string Purpose,
    OutboundMessageStatus Status, string? Provider, string? LastError, DateTime CreatedAtUtc, DateTime? SentAtUtc, string? RelatedEntityType, Guid? RelatedEntityId);

public sealed class OutboundMessageQuery : PageQuery
{
    public OutboundMessageStatus? Status { get; set; }
    public MessageChannel? Channel { get; set; }
}

public sealed record SendRemindersRequest(DateOnly Date, MessageChannel Channel = MessageChannel.WhatsApp);

public sealed record SendRemindersResultDto(int Appointments, int Sent, int NotSentNoProvider, int Failed, int SkippedNoContact);

public interface INotificationService
{
    /// <summary>Crea una notificación interna respetando las preferencias del destinatario.</summary>
    Task NotifyAsync(Guid userId, NotificationType type, string title, string? message, string? entityType, Guid? entityId, CancellationToken ct);
    Task<PagedResult<NotificationDto>> GetMineAsync(NotificationQuery query, CancellationToken ct);
    Task<UnreadCountDto> GetUnreadCountAsync(CancellationToken ct);
    Task MarkReadAsync(Guid id, CancellationToken ct);
    Task<int> MarkAllReadAsync(CancellationToken ct);
    Task<PagedResult<OutboundMessageDto>> GetOutboundAsync(OutboundMessageQuery query, CancellationToken ct);
    Task<OutboundMessageDto> SendAppointmentReminderAsync(Guid appointmentId, MessageChannel channel, CancellationToken ct);
    Task<SendRemindersResultDto> SendDayRemindersAsync(SendRemindersRequest request, CancellationToken ct);
    /// <summary>Trabajo en segundo plano: recordatorios automáticos del consultorio del contexto actual.</summary>
    Task<int> ProcessDueRemindersAsync(CancellationToken ct);
}

internal sealed class NotificationService(IApplicationDbContext db, ITenantContext tenant, IConsultorioClock clock, IMessageDispatcher messages)
    : INotificationService
{
    public async Task NotifyAsync(Guid userId, NotificationType type, string title, string? message, string? entityType, Guid? entityId, CancellationToken ct)
    {
        var settings = await db.NotificationSettings.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == userId, ct);
        if (settings is not null && !Allows(settings, type)) return;
        db.Notifications.Add(new Notification(tenant.ConsultorioId, userId, type, title, message, entityType, entityId, clock.UtcNow));
        await db.SaveChangesAsync(ct);
    }

    private static bool Allows(UserNotificationSettings s, NotificationType type) => s.InAppEnabled && type switch
    {
        NotificationType.TaskAssigned => s.NotifyTaskAssigned,
        NotificationType.TaskDue => s.NotifyTaskDue,
        NotificationType.LabWorkUpdate or NotificationType.LabWorkOverdue => s.NotifyLabUpdates,
        NotificationType.AppointmentChanged => s.NotifyAppointmentChanges,
        _ => true,
    };

    public async Task<PagedResult<NotificationDto>> GetMineAsync(NotificationQuery q, CancellationToken ct)
    {
        var uid = tenant.UserId;
        var query = db.Notifications.AsNoTracking().Where(n => n.UserId == uid);
        if (q.UnreadOnly) query = query.Where(n => n.ReadAtUtc == null);
        return await query.OrderByDescending(n => n.CreatedAtUtc)
            .Select(n => new NotificationDto(n.Id, n.Type, n.Title, n.Message, n.EntityType, n.EntityId, n.CreatedAtUtc, n.ReadAtUtc))
            .ToPagedAsync(q, ct);
    }

    public async Task<UnreadCountDto> GetUnreadCountAsync(CancellationToken ct)
    {
        var uid = tenant.UserId;
        return new UnreadCountDto(await db.Notifications.CountAsync(n => n.UserId == uid && n.ReadAtUtc == null, ct));
    }

    public async Task MarkReadAsync(Guid id, CancellationToken ct)
    {
        var uid = tenant.UserId;
        var n = await db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == uid, ct) ?? throw new NotFoundException();
        n.MarkRead(clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> MarkAllReadAsync(CancellationToken ct)
    {
        var uid = tenant.UserId;
        var list = await db.Notifications.Where(x => x.UserId == uid && x.ReadAtUtc == null).ToListAsync(ct);
        foreach (var n in list) n.MarkRead(clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return list.Count;
    }

    public async Task<PagedResult<OutboundMessageDto>> GetOutboundAsync(OutboundMessageQuery q, CancellationToken ct)
    {
        tenant.Require(Permissions.MessagingSend);
        var cid = tenant.ConsultorioId;
        var query = db.OutboundMessages.AsNoTracking().Where(m => m.ConsultorioId == cid);
        if (q.Status is not null) query = query.Where(m => m.Status == q.Status);
        if (q.Channel is not null) query = query.Where(m => m.Channel == q.Channel);
        var page = await query.OrderByDescending(m => m.CreatedAtUtc).ToPagedAsync(q, ct);
        return new PagedResult<OutboundMessageDto>(page.Items.Select(ToDto).ToList(), page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<OutboundMessageDto> SendAppointmentReminderAsync(Guid appointmentId, MessageChannel channel, CancellationToken ct)
    {
        tenant.Require(Permissions.MessagingSend);
        var appt = await db.Appointments.Include(a => a.Patient).FirstOrDefaultAsync(a => a.Id == appointmentId, ct) ?? throw new NotFoundException("El turno no existe.");
        if (!tenant.CanActOnProfessional(appt.ProfessionalUserId)) throw new NotFoundException("El turno no existe.");
        if (appt.Status is not (AppointmentStatus.Pending or AppointmentStatus.Confirmed))
            throw new BusinessRuleException("Sólo se envían recordatorios de turnos pendientes o confirmados.");
        var msg = await SendReminderInternalAsync(appt, channel, ct)
                  ?? throw new BusinessRuleException("El paciente no tiene un dato de contacto para ese canal.", "missing_contact");
        return ToDto(msg);
    }

    public async Task<SendRemindersResultDto> SendDayRemindersAsync(SendRemindersRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.MessagingSend);
        var tz = await clock.GetTimeZoneAsync(ct);
        var (start, end) = tz.DayRangeUtc(r.Date, r.Date);
        var all = tenant.Has(Permissions.AgendaAllProfessionals);
        var uid = tenant.UserId;
        var appts = await db.Appointments.Include(a => a.Patient)
            .Where(a => a.StartUtc >= start && a.StartUtc < end && (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed))
            .Where(a => all || a.ProfessionalUserId == uid)
            .ToListAsync(ct);
        int sent = 0, noProvider = 0, failed = 0, skipped = 0;
        foreach (var a in appts)
        {
            var m = await SendReminderInternalAsync(a, r.Channel, ct);
            if (m is null) { skipped++; continue; }
            switch (m.Status)
            {
                case OutboundMessageStatus.Sent: sent++; break;
                case OutboundMessageStatus.NotSentNoProvider: noProvider++; break;
                default: failed++; break;
            }
        }
        return new SendRemindersResultDto(appts.Count, sent, noProvider, failed, skipped);
    }

    public async Task<int> ProcessDueRemindersAsync(CancellationToken ct)
    {
        if (!tenant.IsBackgroundJob) tenant.Require(Permissions.MessagingSend);
        var cid = tenant.ConsultorioId;
        var hours = await db.Consultorios.Where(c => c.Id == cid).Select(c => c.ReminderHoursBefore).FirstAsync(ct);
        if (hours <= 0) return 0;
        var now = clock.UtcNow;
        var until = now.AddHours(hours);
        var due = await db.Appointments.Include(a => a.Patient)
            .Where(a => a.StartUtc > now && a.StartUtc <= until && a.ReminderQueuedAtUtc == null
                        && (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed))
            .OrderBy(a => a.StartUtc).Take(200).ToListAsync(ct);
        foreach (var a in due)
        {
            var channel = !string.IsNullOrWhiteSpace(a.Patient!.PhoneNormalized) ? MessageChannel.WhatsApp : MessageChannel.Email;
            if (await SendReminderInternalAsync(a, channel, ct) is null)
            {
                a.MarkReminderQueued(now); // sin datos de contacto: no reintentar en cada ciclo
                await db.SaveChangesAsync(ct);
            }
        }
        return due.Count;
    }

    /// <summary>Envía (o intenta enviar) el recordatorio; devuelve null si el paciente no tiene contacto para el canal.</summary>
    internal async Task<OutboundMessage?> SendReminderInternalAsync(Appointment a, MessageChannel channel, CancellationToken ct)
    {
        var tz = await clock.GetTimeZoneAsync(ct);
        var consultorio = await db.Consultorios.Where(c => c.Id == a.ConsultorioId).Select(c => new { c.Name, c.Phone }).FirstAsync(ct);
        var when = tz.UtcToLocal(a.StartUtc);
        var text = $"Hola {a.Patient!.FirstName}, le recordamos su turno en {consultorio.Name} el {when:dd/MM/yyyy} a las {when:HH:mm}. " +
                   $"Por favor confirme su asistencia{(consultorio.Phone is null ? "" : $" al {consultorio.Phone}")}.";
        OutboundMessage msg;
        if (channel == MessageChannel.WhatsApp)
        {
            if (string.IsNullOrWhiteSpace(a.Patient.PhoneNormalized)) return null;
            msg = await messages.SendWhatsAppAsync(a.ConsultorioId, "+" + a.Patient.PhoneNormalized, text, "appointment_reminder", nameof(Appointment), a.Id, ct);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(a.Patient.Email)) return null;
            msg = await messages.SendEmailAsync(a.ConsultorioId, a.Patient.Email, $"Recordatorio de turno - {consultorio.Name}",
                $"<p>{System.Net.WebUtility.HtmlEncode(text)}</p>", text, "appointment_reminder", false, nameof(Appointment), a.Id, ct);
        }
        a.MarkReminderQueued(clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return msg;
    }

    private static OutboundMessageDto ToDto(OutboundMessage m) => new(m.Id, m.Channel, m.Channel == MessageChannel.Email ? Mask.Email(m.Recipient) : Mask.Phone(m.Recipient),
        m.Subject, m.Purpose, m.Status, m.Provider, m.LastError, m.CreatedAtUtc, m.SentAtUtc, m.RelatedEntityType, m.RelatedEntityId);
}
