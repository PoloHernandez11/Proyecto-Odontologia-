using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Odontia.Application.Abstractions;
using Odontia.Application.Common;
using Odontia.Application.Features.Finance;
using Odontia.Application.Features.Lab;
using Odontia.Application.Features.Notifications;
using Odontia.Domain.Common;
using Odontia.Domain.Entities;
using Odontia.Domain.Enums;
using Odontia.Infrastructure.Persistence;
using Odontia.UnitTests.QA;

namespace Odontia.UnitTests.Application;

/// <summary>
/// Concurrencia optimista de los agregados de dominio (QA API-04, ARQ-05, ARQ-11, vencimiento de presupuestos)
/// reproducida de forma determinista sobre SQLite: la solicitud "A" lee, otra operación confirma un cambio y luego
/// A intenta guardar. Las pruebas HTTP en paralelo contra MySQL están en Odontia.IntegrationTests (DomainConcurrencyTests).
/// </summary>
public sealed class DomainConcurrencyTests : IDisposable
{
    private readonly QaSqliteDb _qa = new();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 11, 19, 15, 0, 0, TimeSpan.Zero));

    public void Dispose() => _qa.Dispose();

    private OdontiaDbContext Db() => _qa.Create(_qa.Tenant(), _time);
    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    private async Task<Guid> LabWorkAsync()
    {
        await using var db = Db();
        var lab = new Laboratory(_qa.ConsultorioId, "Lab Andes");
        db.Laboratories.Add(lab);
        var w = new LabWork(_qa.ConsultorioId, _qa.PatientId, _qa.UserId, lab.Id, "Corona", [16], null, null, null, null, null);
        db.LabWorks.Add(w);
        await db.SaveChangesAsync();
        return w.Id;
    }

    private LabService Lab(OdontiaDbContext db, ITenantContext t, Func<Task>? concurrent = null)
    {
        IConsultorioClock clock = new ConsultorioClock(db, t, _time);
        if (concurrent is not null) clock = new InterleavingClock(clock, concurrent);
        return new LabService(db, t, clock, new NotificationService(db, t, clock, new FakeDispatcher()));
    }

    // ---- API-04: trabajos de laboratorio ----

    [Fact]
    public async Task Lab_status_change_over_a_stale_read_is_rejected_with_conflict_and_records_a_single_transition()
    {
        var id = await LabWorkAsync();
        var t = _qa.Tenant();
        await using var db = _qa.Create(t, _time);
        // Mientras A ya cargó el trabajo (Pendiente), B lo envía.
        var service = Lab(db, t, async () =>
        {
            await using var other = Db();
            await Lab(other, _qa.Tenant()).ChangeStatusAsync(id, new ChangeLabWorkStatusRequest(LabWorkStatus.Sent, null, "B", null), CancellationToken.None);
        });

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.ChangeStatusAsync(id, new ChangeLabWorkStatusRequest(LabWorkStatus.Cancelled, null, "A", null), CancellationToken.None));
        Assert.Equal("concurrency_conflict", ex.Code);

        await using var check = Db();
        var changes = await check.LabWorkStatusChanges.AsNoTracking().Where(c => c.LabWorkId == id).ToListAsync();
        var single = Assert.Single(changes);
        Assert.Equal(LabWorkStatus.Sent, single.ToStatus);
        Assert.Equal(LabWorkStatus.Sent, (await check.LabWorks.AsNoTracking().SingleAsync(w => w.Id == id)).Status);
    }

    [Fact]
    public async Task Lab_edit_over_a_stale_read_does_not_overwrite_the_other_edit()
    {
        var id = await LabWorkAsync();
        await using var a = Db();
        var stale = await a.LabWorks.SingleAsync(w => w.Id == id);

        await using (var b = Db())
        {
            var w = await b.LabWorks.SingleAsync(x => x.Id == id);
            w.Update("Corona B", [16], null, null, null, "nota B");
            await b.SaveChangesAsync();
        }

        stale.Update("Corona A", [16], null, null, null, "nota A");
        var ex = await Assert.ThrowsAsync<ConflictException>(() => a.SaveOrConflictAsync("conflicto", CancellationToken.None));
        Assert.Equal("concurrency_conflict", ex.Code);
        await using var check = Db();
        Assert.Equal("Corona B", (await check.LabWorks.AsNoTracking().SingleAsync(w => w.Id == id)).WorkType);
    }

    [Fact]
    public void Lab_mutations_renew_the_version()
    {
        var w = new LabWork(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Corona", [16], null, null, null, null, null);
        var v0 = w.Version;
        w.ChangeStatus(LabWorkStatus.Sent, new DateOnly(2026, 11, 19), null, Guid.NewGuid(), Now);
        var v1 = w.Version;
        Assert.NotEqual(v0, v1);
        w.ChangeStatus(LabWorkStatus.Sent, new DateOnly(2026, 11, 19), null, Guid.NewGuid(), Now); // sin cambio
        Assert.Equal(v1, w.Version);
        w.Update("Corona", [16], null, null, null, "x");
        Assert.NotEqual(v1, w.Version);
    }

    // ---- ARQ-05: paciente, plan de tratamiento, odontograma y antecedentes ----

    [Fact]
    public async Task Stale_patient_update_fails_with_concurrency_conflict()
    {
        await using var a = Db();
        var stale = await a.Patients.SingleAsync(p => p.Id == _qa.PatientId);
        await using (var b = Db())
        {
            (await b.Patients.SingleAsync(p => p.Id == _qa.PatientId)).SetAdministrativeNotes("B");
            await b.SaveChangesAsync();
        }
        stale.SetAdministrativeNotes("A");
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => a.SaveChangesAsync());
    }

    [Fact]
    public async Task Stale_medical_history_update_fails_with_concurrency_conflict()
    {
        await using (var seed = Db())
        {
            seed.MedicalHistories.Add(new MedicalHistory(_qa.ConsultorioId, _qa.PatientId));
            await seed.SaveChangesAsync();
        }
        await using var a = Db();
        var stale = await a.MedicalHistories.SingleAsync(h => h.PatientId == _qa.PatientId);
        await using (var b = Db())
        {
            var h = await b.MedicalHistories.SingleAsync(x => x.PatientId == _qa.PatientId);
            h.Allergies = "Penicilina";
            h.Validate();
            await b.SaveChangesAsync();
        }
        stale.Allergies = "Ninguna";
        stale.Validate();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => a.SaveChangesAsync());
    }

    [Fact]
    public async Task Stale_treatment_plan_item_change_fails_with_concurrency_conflict()
    {
        Guid planId, itemId;
        await using (var seed = Db())
        {
            var proc = new ProcedureCatalogItem(_qa.ConsultorioId, "RES", "Resina", null, 100m, false, null);
            seed.Procedures.Add(proc);
            var plan = new TreatmentPlan(_qa.ConsultorioId, _qa.PatientId, _qa.UserId, "Plan", null, null);
            itemId = plan.AddItem(proc, null, [], null, null, null, _qa.UserId, Now).Id;
            planId = plan.Id;
            seed.TreatmentPlans.Add(plan);
            await seed.SaveChangesAsync();
        }
        await using var a = Db();
        var stale = await a.TreatmentPlans.Include(p => p.Items).SingleAsync(p => p.Id == planId);
        await using (var b = Db())
        {
            var plan = await b.TreatmentPlans.Include(p => p.Items).SingleAsync(p => p.Id == planId);
            plan.ChangeItemStatus(itemId, TreatmentStatus.Completed, null, _qa.UserId, Now);
            await b.SaveChangesAsync();
        }
        stale.ChangeItemStatus(itemId, TreatmentStatus.Cancelled, null, _qa.UserId, Now);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => a.SaveChangesAsync());

        await using var check = Db();
        Assert.Equal(TreatmentStatus.Completed, (await check.TreatmentPlanItems.AsNoTracking().SingleAsync(i => i.Id == itemId)).Status);
        Assert.Equal(2, await check.TreatmentChanges.CountAsync(c => c.TreatmentPlanItemId == itemId)); // alta + completada
    }

    [Fact]
    public async Task Stale_odontogram_change_fails_with_concurrency_conflict_even_if_only_children_change()
    {
        Guid odontogramId;
        await using (var seed = Db())
        {
            var o = new Odontogram(_qa.ConsultorioId, _qa.PatientId);
            seed.Odontograms.Add(o);
            await seed.SaveChangesAsync();
            odontogramId = o.Id;
        }
        await using var a = Db();
        var stale = await a.Odontograms.Include(o => o.Findings).SingleAsync(o => o.Id == odontogramId);
        await using (var b = Db())
        {
            (await b.Odontograms.Include(o => o.Findings).SingleAsync(o => o.Id == odontogramId)).AddNote(16, "Nota B", _qa.UserId, Now);
            await b.SaveChangesAsync();
        }
        stale.AddNote(16, "Nota A", _qa.UserId, Now);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => a.SaveChangesAsync());
        await using var check = Db();
        Assert.Equal("Nota B", (await check.ToothNotes.AsNoTracking().SingleAsync()).Text);
    }

    // ---- Vencimiento automático de presupuestos ----

    [Fact]
    public async Task Budget_expiry_skips_a_budget_changed_concurrently_and_expires_the_rest()
    {
        Guid first, second;
        await using (var seed = Db())
        {
            Budget Issued(int n)
            {
                var b = new Budget(_qa.ConsultorioId, _qa.PatientId, n, null, new DateOnly(2026, 10, 1), new DateOnly(2026, 11, 1), 0, null);
                b.AddItem(null, "Corona", 16, 1, 1000m, 0, null);
                b.Issue(_qa.UserId, Now.AddDays(-40));
                seed.Budgets.Add(b);
                return b;
            }
            first = Issued(1).Id;
            second = Issued(2).Id;
            await seed.SaveChangesAsync();
        }
        var t = _qa.Tenant();
        await using var db = _qa.Create(t, _time);
        // Después de que el job cargó los vencidos, otra solicitud rechazó el primero (cambió su versión).
        var clock = new InterleavingClock(new ConsultorioClock(db, t, _time), async () =>
        {
            await using var other = Db();
            var b = await other.Budgets.SingleAsync(x => x.Id == first);
            b.Reject("No acepta", _qa.UserId, Now);
            await other.SaveChangesAsync();
        });
        var service = new BudgetService(db, t, clock, new CountingSequence(), new NullAudit());

        var expired = await service.ExpireDueBudgetsAsync(CancellationToken.None);

        Assert.Equal(1, expired);
        await using var check = Db();
        Assert.Equal(BudgetStatus.Rejected, (await check.Budgets.AsNoTracking().SingleAsync(b => b.Id == first)).Status);
        Assert.Equal(BudgetStatus.Expired, (await check.Budgets.AsNoTracking().SingleAsync(b => b.Id == second)).Status);
    }

    // ---- ARQ-11: recordatorios ----

    private async Task<Guid> AppointmentAsync()
    {
        await using var db = Db();
        var patient = await db.Patients.SingleAsync(p => p.Id == _qa.PatientId);
        patient.SetContact("+54 9 11 5555-1234", "ana@test.local", null, null, null, null);
        var start = Now.AddHours(3);
        var a = new Appointment(_qa.ConsultorioId, _qa.PatientId, _qa.UserId, null, start, start.AddMinutes(30), "Control", null, false);
        db.Appointments.Add(a);
        await db.SaveChangesAsync();
        return a.Id;
    }

    [Fact]
    public async Task Reminder_is_not_sent_when_the_appointment_changed_concurrently_and_is_sent_on_the_next_cycle()
    {
        var id = await AppointmentAsync();
        var t = _qa.Tenant();
        var messages = new FakeDispatcher(beforeFirstCheck: async () =>
        {
            // Entre la carga de turnos del job y su marca, el turno se modificó (p. ej. se editó el motivo).
            await using var other = Db();
            (await other.Appointments.SingleAsync(a => a.Id == id)).SetDetails("Control anual", null);
            await other.SaveChangesAsync();
        });
        await using (var db = _qa.Create(t, _time))
            await new NotificationService(db, t, new ConsultorioClock(db, t, _time), messages).ProcessDueRemindersAsync(CancellationToken.None);

        Assert.Empty(messages.Sent);
        await using (var check = Db())
            Assert.Null((await check.Appointments.AsNoTracking().SingleAsync(a => a.Id == id)).ReminderQueuedAtUtc);

        var next = new FakeDispatcher();
        await using (var db = _qa.Create(t, _time))
            await new NotificationService(db, t, new ConsultorioClock(db, t, _time), next).ProcessDueRemindersAsync(CancellationToken.None);
        Assert.Single(next.Sent);
        await using (var check = Db())
            Assert.NotNull((await check.Appointments.AsNoTracking().SingleAsync(a => a.Id == id)).ReminderQueuedAtUtc);

        // Un tercer ciclo no lo reenvía.
        var third = new FakeDispatcher();
        await using (var db = _qa.Create(t, _time))
            await new NotificationService(db, t, new ConsultorioClock(db, t, _time), third).ProcessDueRemindersAsync(CancellationToken.None);
        Assert.Empty(third.Sent);
    }

    [Fact]
    public async Task Reminder_job_sends_one_message_per_due_appointment()
    {
        await AppointmentAsync();
        await AppointmentAsync();
        var t = _qa.Tenant();
        var messages = new FakeDispatcher();
        await using var db = _qa.Create(t, _time);
        var processed = await new NotificationService(db, t, new ConsultorioClock(db, t, _time), messages).ProcessDueRemindersAsync(CancellationToken.None);
        Assert.Equal(2, processed);
        Assert.Equal(2, messages.Sent.Count);
        Assert.All(messages.Sent, m => Assert.Contains("QA", m.Body));
    }

    // ---- ARQ-14: reloj del consultorio ----

    [Fact]
    public async Task Unknown_time_zone_falls_back_to_utc_with_a_warning()
    {
        using var qa = new QaSqliteDb("Mars/Olympus_Mons");
        await using var db = qa.Create(qa.Tenant());
        var logger = new CapturingLogger<ConsultorioClock>();
        var clock = new ConsultorioClock(db, qa.Tenant(), TimeProvider.System, logger);
        Assert.Equal(TimeZoneInfo.Utc, await clock.GetTimeZoneAsync(CancellationToken.None));
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("Mars/Olympus_Mons", entry.Message);
    }

    [Fact]
    public async Task Clock_without_active_tenant_throws_forbidden_directly()
    {
        await using var db = _qa.Create(null);
        var clock = new ConsultorioClock(db, new TenantContext(), TimeProvider.System);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => clock.TodayAsync(CancellationToken.None));
    }

    [Fact]
    public void Consultorio_settings_reject_an_unknown_time_zone()
    {
        var c = new Consultorio("C", "UTC");
        Assert.Equal("invalid_time_zone", Assert.Throws<DomainException>(() => c.UpdateSettings("Mars/Olympus_Mons", 30, 15, 24)).Code);
        Assert.Equal("UTC", c.TimeZoneId);
        c.UpdateSettings("America/Argentina/Buenos_Aires", 30, 15, 24);
        Assert.Equal("America/Argentina/Buenos_Aires", c.TimeZoneId);
    }

    // ---- Dobles de prueba ----

    private sealed class InterleavingClock(IConsultorioClock inner, Func<Task> concurrent) : IConsultorioClock
    {
        private Func<Task>? _pending = concurrent;

        public DateTime UtcNow
        {
            get
            {
                if (Interlocked.Exchange(ref _pending, null) is { } run) run().GetAwaiter().GetResult();
                return inner.UtcNow;
            }
        }

        public Task<TimeZoneInfo> GetTimeZoneAsync(CancellationToken ct) => inner.GetTimeZoneAsync(ct);
        public Task<DateOnly> TodayAsync(CancellationToken ct) => inner.TodayAsync(ct);
    }

    private sealed class FakeDispatcher(Func<Task>? beforeFirstCheck = null) : IMessageDispatcher
    {
        private Func<Task>? _hook = beforeFirstCheck;
        public List<OutboundMessage> Sent { get; } = [];

        public bool IsChannelConfigured(MessageChannel channel)
        {
            if (Interlocked.Exchange(ref _hook, null) is { } run) run().GetAwaiter().GetResult();
            return true;
        }

        public Task<OutboundMessage> SendEmailAsync(Guid? consultorioId, string to, string subject, string html, string text, string purpose,
            bool containsSecret, string? relatedType, Guid? relatedId, CancellationToken ct) =>
            Task.FromResult(Record(new OutboundMessage(consultorioId, MessageChannel.Email, to, subject, text, purpose, relatedType, relatedId, DateTime.UtcNow, containsSecret)));

        public Task<OutboundMessage> SendWhatsAppAsync(Guid? consultorioId, string toPhone, string text, string purpose, string? relatedType,
            Guid? relatedId, CancellationToken ct) =>
            Task.FromResult(Record(new OutboundMessage(consultorioId, MessageChannel.WhatsApp, toPhone, null, text, purpose, relatedType, relatedId, DateTime.UtcNow, false)));

        private OutboundMessage Record(OutboundMessage m)
        {
            m.RecordResult("fake", true, true, "id-1", null, DateTime.UtcNow);
            Sent.Add(m);
            return m;
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}
