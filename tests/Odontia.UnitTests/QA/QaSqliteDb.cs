using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Odontia.Application.Abstractions;
using Odontia.Application.Common;
using Odontia.Domain.Authorization;
using Odontia.Domain.Entities;
using Odontia.Infrastructure.Persistence;

namespace Odontia.UnitTests.QA;

/// <summary>Reloj fijo para pruebas deterministas.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
}

internal sealed class CountingSequence : ISequenceGenerator
{
    private int _n;
    public Task<int> NextAsync(string name, CancellationToken ct) => Task.FromResult(++_n);
}

internal sealed class NullAudit : IAuditWriter
{
    public void Write(Odontia.Domain.Enums.AuditAction action, string entityType, string? entityId, string? details = null, Guid? consultorioId = null, Guid? userId = null) { }
}

/// <summary>Base SQLite en memoria con un consultorio, un usuario miembro y un paciente.</summary>
internal sealed class QaSqliteDb : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public Guid ConsultorioId { get; } = Guid.CreateVersion7();
    public Guid UserId { get; } = Guid.CreateVersion7();
    public Guid PatientId { get; private set; }

    public QaSqliteDb(string timeZoneId = "America/Argentina/Buenos_Aires")
    {
        _connection.Open();
        using var db = Create(null);
        db.Database.EnsureCreated();
        var u = new ApplicationUser { Email = "qa@test.local", UserName = "qa@test.local", NormalizedEmail = "QA@TEST.LOCAL", NormalizedUserName = "QA@TEST.LOCAL", FirstName = "Q", LastName = "A" };
        typeof(ApplicationUser).GetProperty(nameof(ApplicationUser.Id))!.SetValue(u, UserId);
        var c = new Consultorio("QA", timeZoneId);
        typeof(Odontia.Domain.Common.Entity).GetProperty("Id")!.SetValue(c, ConsultorioId);
        db.Users.Add(u);
        db.Consultorios.Add(c);
        db.SaveChanges();
        db.Members.Add(new ConsultorioMember(ConsultorioId, UserId, true));
        var p = new Patient(ConsultorioId, "Ana", "Pérez", "30123456");
        PatientId = p.Id;
        db.Patients.Add(p);
        db.SaveChanges();
    }

    public TenantContext Tenant(params string[] permissions)
    {
        var t = new TenantContext();
        t.InitializeUser(UserId, null);
        t.InitializeTenant(ConsultorioId, Guid.NewGuid(), true, permissions.Length == 0 ? Permissions.All : permissions);
        return t;
    }

    public OdontiaDbContext Create(ITenantContext? tenant, TimeProvider? clock = null)
    {
        var options = new DbContextOptionsBuilder<OdontiaDbContext>().UseSqlite(_connection).Options;
        return new OdontiaDbContext(options, tenant ?? new TenantContext(), clock ?? TimeProvider.System);
    }

    public void Dispose() => _connection.Dispose();
}
