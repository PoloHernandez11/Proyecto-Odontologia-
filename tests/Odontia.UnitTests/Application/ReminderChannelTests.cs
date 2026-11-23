using Microsoft.EntityFrameworkCore;
using Odontia.Application.Abstractions;
using Odontia.Application.Common;
using Odontia.Application.Features.Notifications;
using Odontia.Domain.Entities;
using Odontia.Domain.Enums;
using Odontia.UnitTests.QA;

namespace Odontia.UnitTests.Application;

/// <summary>QA ARQ-02: el recordatorio automático elige el canal según los proveedores configurados.</summary>
public sealed class ReminderChannelTests : IDisposable
{
    private readonly QaSqliteDb _qa = new();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 11, 19, 15, 0, 0, TimeSpan.Zero));

    public void Dispose() => _qa.Dispose();

    private async Task<Guid> AppointmentAsync(string? phone, string? email)
    {
        await using var db = _qa.Create(_qa.Tenant(), _time);
        var patient = await db.Patients.SingleAsync(p => p.Id == _qa.PatientId);
        patient.SetContact(phone, email, null, null, null, null);
        var start = _time.GetUtcNow().UtcDateTime.AddHours(3);
        var a = new Appointment(_qa.ConsultorioId, _qa.PatientId, _qa.UserId, null, start, start.AddMinutes(30), "Control", null, false);
        db.Appointments.Add(a);
        await db.SaveChangesAsync();
        return a.Id;
    }

    private async Task<FakeDispatcher> RunJobAsync(FakeDispatcher messages)
    {
        var t = _qa.Tenant();
        await using var db = _qa.Create(t, _time);
        await new NotificationService(db, t, new ConsultorioClock(db, t, _time), messages).ProcessDueRemindersAsync(CancellationToken.None);
        return messages;
    }

    private async Task<DateTime?> ReminderQueuedAsync(Guid id)
    {
        await using var db = _qa.Create(_qa.Tenant(), _time);
        return (await db.Appointments.AsNoTracking().SingleAsync(a => a.Id == id)).ReminderQueuedAtUtc;
    }

    [Fact]
    public async Task Patient_with_phone_gets_email_when_whatsapp_has_no_provider()
    {
        var id = await AppointmentAsync("+54 9 11 5555-1234", "ana@test.local");
        var sent = await RunJobAsync(new FakeDispatcher(email: true, whatsApp: false));

        var msg = Assert.Single(sent.Sent);
        Assert.Equal(MessageChannel.Email, msg.Channel);
        Assert.Equal("ana@test.local", msg.Recipient);
        Assert.NotNull(await ReminderQueuedAsync(id));
    }

    [Fact]
    public async Task Whatsapp_is_preferred_when_it_has_a_provider()
    {
        var id = await AppointmentAsync("+54 9 11 5555-1234", "ana@test.local");
        var sent = await RunJobAsync(new FakeDispatcher(email: true, whatsApp: true));

        Assert.Equal(MessageChannel.WhatsApp, Assert.Single(sent.Sent).Channel);
        Assert.NotNull(await ReminderQueuedAsync(id));
    }

    [Fact]
    public async Task Without_a_configured_channel_the_appointment_is_not_marked_and_retries_later()
    {
        var id = await AppointmentAsync("+54 9 11 5555-1234", null);
        var sent = await RunJobAsync(new FakeDispatcher(email: true, whatsApp: false));

        Assert.Empty(sent.Sent);
        Assert.Null(await ReminderQueuedAsync(id));

        // Cuando aparece un proveedor, el siguiente ciclo lo envía.
        var later = await RunJobAsync(new FakeDispatcher(email: true, whatsApp: true));
        Assert.Equal(MessageChannel.WhatsApp, Assert.Single(later.Sent).Channel);
        Assert.NotNull(await ReminderQueuedAsync(id));
    }

    [Fact]
    public async Task No_provider_at_all_leaves_the_appointment_unmarked()
    {
        var id = await AppointmentAsync("+54 9 11 5555-1234", "ana@test.local");
        var sent = await RunJobAsync(new FakeDispatcher(email: false, whatsApp: false));

        Assert.Empty(sent.Sent);
        Assert.Null(await ReminderQueuedAsync(id));
    }

    [Fact]
    public async Task Result_without_provider_falls_back_to_next_channel_and_is_not_marked_if_none_delivers()
    {
        var id = await AppointmentAsync("+54 9 11 5555-1234", "ana@test.local");
        // Ambos canales dicen estar configurados pero el proveedor informa NotSentNoProvider.
        var sent = await RunJobAsync(new FakeDispatcher(email: true, whatsApp: true, deliver: false));

        Assert.Equal([MessageChannel.WhatsApp, MessageChannel.Email], sent.Sent.Select(m => m.Channel));
        Assert.All(sent.Sent, m => Assert.Equal(OutboundMessageStatus.NotSentNoProvider, m.Status));
        Assert.Null(await ReminderQueuedAsync(id));
    }

    [Fact]
    public async Task Patient_without_contact_data_is_marked_to_avoid_retrying_every_cycle()
    {
        var id = await AppointmentAsync(null, null);
        var sent = await RunJobAsync(new FakeDispatcher(email: true, whatsApp: true));

        Assert.Empty(sent.Sent);
        Assert.NotNull(await ReminderQueuedAsync(id));
    }

    [Fact]
    public async Task Failed_whatsapp_send_falls_back_to_email()
    {
        var id = await AppointmentAsync("+54 9 11 5555-1234", "ana@test.local");
        var sent = await RunJobAsync(new FakeDispatcher(email: true, whatsApp: true, failing: [MessageChannel.WhatsApp], store: _qa));

        Assert.Equal([MessageChannel.WhatsApp, MessageChannel.Email], sent.Sent.Select(m => m.Channel));
        Assert.Equal([OutboundMessageStatus.Failed, OutboundMessageStatus.Sent], sent.Sent.Select(m => m.Status));
        Assert.NotNull(await ReminderQueuedAsync(id));
    }

    [Fact]
    public async Task When_every_channel_fails_the_reminder_is_retried_in_later_cycles_up_to_the_cap()
    {
        var id = await AppointmentAsync("+54 9 11 5555-1234", "ana@test.local");
        for (var cycle = 1; cycle <= NotificationService.MaxReminderAttempts; cycle++)
        {
            var sent = await RunJobAsync(new FakeDispatcher(email: true, whatsApp: true,
                failing: [MessageChannel.WhatsApp, MessageChannel.Email], store: _qa));
            Assert.Equal([MessageChannel.WhatsApp, MessageChannel.Email], sent.Sent.Select(m => m.Channel));
            Assert.All(sent.Sent, m => Assert.Equal(OutboundMessageStatus.Failed, m.Status));
            // Se libera la marca para reintentar, salvo en el último intento permitido (se da por perdido).
            if (cycle < NotificationService.MaxReminderAttempts) Assert.Null(await ReminderQueuedAsync(id));
            else Assert.NotNull(await ReminderQueuedAsync(id));
        }

        // Agotados los intentos, los ciclos siguientes ya no reenvían.
        var after = await RunJobAsync(new FakeDispatcher(email: true, whatsApp: true, store: _qa));
        Assert.Empty(after.Sent);
    }

    [Fact]
    public async Task A_failed_cycle_followed_by_a_successful_one_delivers_and_marks()
    {
        var id = await AppointmentAsync(null, "ana@test.local");
        await RunJobAsync(new FakeDispatcher(email: true, whatsApp: true, failing: [MessageChannel.Email], store: _qa));
        Assert.Null(await ReminderQueuedAsync(id));

        var later = await RunJobAsync(new FakeDispatcher(email: true, whatsApp: true, store: _qa));
        Assert.Equal(OutboundMessageStatus.Sent, Assert.Single(later.Sent).Status);
        Assert.NotNull(await ReminderQueuedAsync(id));
    }

    /// <param name="failing">Canales cuyo proveedor está configurado pero el envío falla (estado Failed).</param>
    /// <param name="store">Si se indica, persiste los mensajes como el despachador real (los intentos se cuentan en la base).</param>
    private sealed class FakeDispatcher(bool email, bool whatsApp, bool deliver = true, MessageChannel[]? failing = null, QaSqliteDb? store = null)
        : IMessageDispatcher
    {
        public List<OutboundMessage> Sent { get; } = [];

        public bool IsChannelConfigured(MessageChannel channel) => channel == MessageChannel.Email ? email : whatsApp;

        public Task<OutboundMessage> SendEmailAsync(Guid? consultorioId, string to, string subject, string html, string text, string purpose,
            bool containsSecret, string? relatedType, Guid? relatedId, CancellationToken ct) =>
            RecordAsync(new OutboundMessage(consultorioId, MessageChannel.Email, to, subject, text, purpose, relatedType, relatedId, DateTime.UtcNow, containsSecret));

        public Task<OutboundMessage> SendWhatsAppAsync(Guid? consultorioId, string toPhone, string text, string purpose, string? relatedType,
            Guid? relatedId, CancellationToken ct) =>
            RecordAsync(new OutboundMessage(consultorioId, MessageChannel.WhatsApp, toPhone, null, text, purpose, relatedType, relatedId, DateTime.UtcNow, false));

        private async Task<OutboundMessage> RecordAsync(OutboundMessage m)
        {
            if (failing?.Contains(m.Channel) == true) m.RecordResult("fake", false, true, null, "SMTP caído", DateTime.UtcNow);
            else m.RecordResult("fake", deliver, deliver, deliver ? "id-1" : null, null, DateTime.UtcNow);
            Sent.Add(m);
            if (store is not null)
            {
                await using var db = store.Create(store.Tenant());
                db.OutboundMessages.Add(m);
                await db.SaveChangesAsync();
            }
            return m;
        }
    }
}
