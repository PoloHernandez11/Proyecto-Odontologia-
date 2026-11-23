using Microsoft.EntityFrameworkCore;
using Odontia.Application.Abstractions;
using Odontia.Application.Common;
using Odontia.Application.Features.Finance;
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
        var context = await LoadReminderContextAsync(ct);
        var msg = await SendAndMarkAsync(appt, channel, context, ct)
                  ?? throw new BusinessRuleException("El paciente no tiene un dato de contacto para ese canal.", "missing_contact");
        return ToDto(msg);
    }

    public async Task<SendRemindersResultDto> SendDayRemindersAsync(SendRemindersRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.MessagingSend);
        var context = await LoadReminderContextAsync(ct);
        var (start, end) = context.TimeZone.DayRangeUtc(r.Date, r.Date);
        var all = tenant.Has(Permissions.AgendaAllProfessionals);
        var uid = tenant.UserId;
        var appts = await db.Appointments.Include(a => a.Patient)
            .Where(a => a.StartUtc >= start && a.StartUtc < end && (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed))
            .Where(a => all || a.ProfessionalUserId == uid)
            .ToListAsync(ct);
        int sent = 0, noProvider = 0, failed = 0, skipped = 0;
        foreach (var a in appts)
        {
            var m = await SendAndMarkAsync(a, r.Channel, context, ct);
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

    /// <summary>
    /// Recordatorios automáticos. El consultorio y su zona horaria se cargan una sola vez por ciclo. Los turnos se
    /// "reservan" (marca ReminderQueuedAtUtc) en un único guardado ANTES de enviar: si un turno cambió en simultáneo
    /// (token Version), se omite y no se envía; así un conflicto nunca provoca reenvíos en ciclos posteriores.
    /// </summary>
    public async Task<int> ProcessDueRemindersAsync(CancellationToken ct)
    {
        if (!tenant.IsBackgroundJob) tenant.Require(Permissions.MessagingSend);
        var cid = tenant.ConsultorioId;
        var consultorio = await db.Consultorios.AsNoTracking().Where(c => c.Id == cid)
            .Select(c => new { c.Name, c.Phone, c.ReminderHoursBefore }).FirstAsync(ct);
        if (consultorio.ReminderHoursBefore <= 0) return 0;
        var context = new ReminderContext(await clock.GetTimeZoneAsync(ct), consultorio.Name, consultorio.Phone);
        var now = clock.UtcNow;
        var until = now.AddHours(consultorio.ReminderHoursBefore);
        var due = await db.Appointments.Include(a => a.Patient)
            .Where(a => a.StartUtc > now && a.StartUtc <= until && a.ReminderQueuedAtUtc == null
                        && (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed))
            .OrderBy(a => a.StartUtc).Take(200).ToListAsync(ct);

        // Sólo canales con proveedor configurado (WhatsApp primero, luego correo). Sin contacto: se marca para no
        // reintentar en cada ciclo. Con contacto pero sin proveedor: no se marca (se enviará cuando haya uno).
        var toSend = new List<(Appointment Appointment, List<MessageChannel> Channels)>();
        var claimed = new List<Appointment>();
        foreach (var a in due)
        {
            var withContact = ContactChannels(a.Patient!);
            var configured = withContact.Where(messages.IsChannelConfigured).ToList();
            if (withContact.Count > 0 && configured.Count == 0) continue;
            a.MarkReminderQueued(now);
            claimed.Add(a);
            if (configured.Count > 0) toSend.Add((a, configured));
        }
        var lost = await SaveClaimsAsync(claimed, ct);

        var released = false;
        foreach (var (a, channels) in toSend.Where(x => !lost.Contains(x.Appointment)))
        {
            // Se prueba el siguiente canal tanto si no hay proveedor como si el envío falló (SMTP caído, timeout).
            var delivered = false;
            var failed = false;
            foreach (var channel in channels)
            {
                var msg = await SendAsync(a, channel, context, ct);
                if (msg is null) continue;
                if (msg.Status == OutboundMessageStatus.Failed) { failed = true; continue; }
                if (msg.Status != OutboundMessageStatus.NotSentNoProvider) { delivered = true; break; }
            }
            if (delivered) continue;
            // Ningún canal lo entregó: se libera la marca para reintentar en un ciclo posterior, salvo que ya se agotaron
            // los intentos fallidos (evita reenvíos indefinidos si el proveedor acepta a medias o falla siempre).
            if (failed && await FailedReminderAttemptsAsync(a.Id, ct) >= MaxReminderAttempts * channels.Count) continue;
            a.ClearReminderQueued();
            released = true;
        }
        if (released) await db.TrySaveAsync(ct);
        return due.Count;
    }

    /// <summary>Ciclos automáticos fallidos tolerados por turno antes de darlo por perdido (cada ciclo prueba todos sus canales).</summary>
    internal const int MaxReminderAttempts = 3;

    private const string ReminderPurpose = "appointment_reminder";

    private Task<int> FailedReminderAttemptsAsync(Guid appointmentId, CancellationToken ct) =>
        db.OutboundMessages.CountAsync(m => m.RelatedEntityType == nameof(Appointment) && m.RelatedEntityId == appointmentId
                                            && m.Purpose == ReminderPurpose && m.Status == OutboundMessageStatus.Failed, ct);

    /// <summary>
    /// Guarda las marcas de recordatorio. Ante un conflicto de concurrencia descarta el turno en conflicto (no se le
    /// envía nada en este ciclo) y reintenta con el resto. Devuelve los turnos descartados.
    /// </summary>
    private async Task<HashSet<Appointment>> SaveClaimsAsync(List<Appointment> claimed, CancellationToken ct)
    {
        var lost = new HashSet<Appointment>();
        if (claimed.Count == 0) return lost;
        while (true)
        {
            try
            {
                await db.SaveChangesAsync(ct);
                return lost;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                var conflicting = ex.Entries.Select(e => e.Entity).OfType<Appointment>().ToList();
                if (conflicting.Count == 0) throw;
                lost.UnionWith(conflicting);
                ex.DetachEntitiesAndPendingAudit(conflicting);
            }
        }
    }

    /// <summary>Canales para los que el paciente tiene dato de contacto, en orden de preferencia.</summary>
    private static List<MessageChannel> ContactChannels(Patient p)
    {
        var channels = new List<MessageChannel>(2);
        if (!string.IsNullOrWhiteSpace(p.PhoneNormalized)) channels.Add(MessageChannel.WhatsApp);
        if (!string.IsNullOrWhiteSpace(p.Email)) channels.Add(MessageChannel.Email);
        return channels;
    }

    /// <summary>Datos del consultorio para el texto del recordatorio (cargados una vez por operación).</summary>
    private sealed record ReminderContext(TimeZoneInfo TimeZone, string Name, string? Phone);

    private async Task<ReminderContext> LoadReminderContextAsync(CancellationToken ct)
    {
        var cid = tenant.ConsultorioId;
        var c = await db.Consultorios.AsNoTracking().Where(x => x.Id == cid).Select(x => new { x.Name, x.Phone }).FirstAsync(ct);
        return new ReminderContext(await clock.GetTimeZoneAsync(ct), c.Name, c.Phone);
    }

    /// <summary>Envío manual: envía y, si el mensaje salió, marca el turno (un conflicto de concurrencia no invalida el envío ya hecho).</summary>
    private async Task<OutboundMessage?> SendAndMarkAsync(Appointment a, MessageChannel channel, ReminderContext context, CancellationToken ct)
    {
        var msg = await SendAsync(a, channel, context, ct);
        // Sin proveedor el mensaje no salió: no se marca el turno, para poder reintentar o usar otro canal.
        if (msg is not null && msg.Status != OutboundMessageStatus.NotSentNoProvider)
        {
            a.MarkReminderQueued(clock.UtcNow);
            await db.TrySaveAsync(ct);
        }
        return msg;
    }

    /// <summary>Envía (o intenta enviar) el recordatorio; devuelve null si el paciente no tiene contacto para el canal.</summary>
    private async Task<OutboundMessage?> SendAsync(Appointment a, MessageChannel channel, ReminderContext context, CancellationToken ct)
    {
        var when = context.TimeZone.UtcToLocal(a.StartUtc);
        var text = $"Hola {a.Patient!.FirstName}, le recordamos su turno en {context.Name} el {when:dd/MM/yyyy} a las {when:HH:mm}. " +
                   $"Por favor confirme su asistencia{(context.Phone is null ? "" : $" al {context.Phone}")}.";
        if (channel == MessageChannel.WhatsApp)
        {
            if (string.IsNullOrWhiteSpace(a.Patient.PhoneNormalized)) return null;
            return await messages.SendWhatsAppAsync(a.ConsultorioId, "+" + a.Patient.PhoneNormalized, text, ReminderPurpose, nameof(Appointment), a.Id, ct);
        }
        if (string.IsNullOrWhiteSpace(a.Patient.Email)) return null;
        return await messages.SendEmailAsync(a.ConsultorioId, a.Patient.Email, $"Recordatorio de turno - {context.Name}",
            $"<p>{System.Net.WebUtility.HtmlEncode(text)}</p>", text, ReminderPurpose, false, nameof(Appointment), a.Id, ct);
    }


    private static OutboundMessageDto ToDto(OutboundMessage m) => new(m.Id, m.Channel, m.Channel == MessageChannel.Email ? Mask.Email(m.Recipient) : Mask.Phone(m.Recipient),
        m.Subject, m.Purpose, m.Status, m.Provider, m.LastError, m.CreatedAtUtc, m.SentAtUtc, m.RelatedEntityType, m.RelatedEntityId);
}
