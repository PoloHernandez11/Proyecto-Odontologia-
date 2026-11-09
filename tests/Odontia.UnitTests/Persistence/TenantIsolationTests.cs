using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Odontia.Application.Common;
using Odontia.Domain.Entities;
using Odontia.Infrastructure.Persistence;

namespace Odontia.UnitTests.Persistence;

/// <summary>
/// Verifica el aislamiento por consultorio a nivel de persistencia: filtro global, imposición del
/// consultorio activo en escrituras y FKs compuestas que impiden referencias cruzadas.
/// </summary>
public sealed class TenantIsolationTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly Guid _a = Guid.CreateVersion7();
    private readonly Guid _b = Guid.CreateVersion7();
    private readonly Guid _userA = Guid.CreateVersion7();
    private readonly Guid _userB = Guid.CreateVersion7();

    public TenantIsolationTests()
    {
        _connection.Open();
        using var db = Create(null);
        db.Database.EnsureCreated();
        db.Users.AddRange(User(_userA, "a@test.local"), User(_userB, "b@test.local"));
        db.Consultorios.AddRange(Consultorio(_a, "A"), Consultorio(_b, "B"));
        db.SaveChanges();
        db.Members.Add(new ConsultorioMember(_a, _userA, true));
        db.Members.Add(new ConsultorioMember(_b, _userB, true));
        db.Patients.Add(new Patient(_a, "Martín", "Rodríguez", "32456789"));
        db.Patients.Add(new Patient(_b, "Sofía", "Herrera", "40112334"));
        db.SaveChanges();
    }

    private static ApplicationUser User(Guid id, string email)
    {
        var u = new ApplicationUser { Email = email, UserName = email, NormalizedEmail = email.ToUpperInvariant(), NormalizedUserName = email.ToUpperInvariant(), FirstName = "U", LastName = "T" };
        typeof(ApplicationUser).GetProperty(nameof(ApplicationUser.Id))!.SetValue(u, id);
        return u;
    }

    private static Consultorio Consultorio(Guid id, string name)
    {
        var c = new Consultorio(name, "America/Argentina/Buenos_Aires");
        typeof(Odontia.Domain.Common.Entity).GetProperty("Id")!.SetValue(c, id);
        return c;
    }

    private OdontiaDbContext Create(Guid? consultorio)
    {
        var tenant = new TenantContext();
        if (consultorio is { } c)
        {
            tenant.InitializeUser(c == _a ? _userA : _userB, null);
            tenant.InitializeTenant(c, Guid.NewGuid(), true, []);
        }
        var options = new DbContextOptionsBuilder<OdontiaDbContext>().UseSqlite(_connection).Options;
        return new OdontiaDbContext(options, tenant, TimeProvider.System);
    }

    [Fact]
    public void Global_filter_only_returns_active_consultorio_data()
    {
        using var dbA = Create(_a);
        Assert.Equal(["32456789"], dbA.Patients.Select(p => p.DocumentNumber).ToList());
        using var dbB = Create(_b);
        Assert.Equal(["40112334"], dbB.Patients.Select(p => p.DocumentNumber).ToList());
    }

    [Fact]
    public void Without_tenant_no_tenant_data_is_visible()
    {
        using var db = Create(null);
        Assert.Empty(db.Patients.ToList());
    }

    [Fact]
    public void Writing_into_another_consultorio_is_rejected()
    {
        using var dbA = Create(_a);
        dbA.Patients.Add(new Patient(_b, "Intruso", "X", "11111111"));
        Assert.Throws<InvalidOperationException>(() => dbA.SaveChanges());
    }

    [Fact]
    public void Modifying_other_consultorio_record_is_rejected()
    {
        Guid idB;
        using (var dbB = Create(_b)) idB = dbB.Patients.Select(p => p.Id).First();
        using var dbA = Create(_a);
        var foreign = dbA.Patients.IgnoreQueryFilters().First(p => p.Id == idB);
        foreign.SetAdministrativeNotes("modificado");
        Assert.Throws<InvalidOperationException>(() => dbA.SaveChanges());
    }

    [Fact]
    public void Composite_foreign_key_prevents_cross_consultorio_reference()
    {
        Guid patientB;
        using (var dbB = Create(_b)) patientB = dbB.Patients.Select(p => p.Id).First();
        using var dbA = Create(_a);
        var start = DateTime.UtcNow.AddDays(1);
        // Turno del consultorio A apuntando a un paciente del consultorio B: la FK (ConsultorioId, PatientId) lo impide.
        dbA.Appointments.Add(new Appointment(_a, patientB, _userA, null, start, start.AddMinutes(30), null, null, false));
        Assert.Throws<DbUpdateException>(() => dbA.SaveChanges());
    }

    [Fact]
    public void Professional_must_be_member_of_same_consultorio()
    {
        Guid patientA;
        using (var dbA0 = Create(_a)) patientA = dbA0.Patients.Select(p => p.Id).First();
        using var dbA = Create(_a);
        var start = DateTime.UtcNow.AddDays(1);
        dbA.Appointments.Add(new Appointment(_a, patientA, _userB, null, start, start.AddMinutes(30), null, null, false));
        Assert.Throws<DbUpdateException>(() => dbA.SaveChanges());
    }

    [Fact]
    public void Document_number_is_unique_per_consultorio_but_reusable_across_consultorios()
    {
        using (var dbB = Create(_b))
        {
            dbB.Patients.Add(new Patient(_b, "Otro", "Paciente", "32.456.789"));
            dbB.SaveChanges(); // mismo DNI que un paciente de A: permitido en B
        }
        using var dbA = Create(_a);
        dbA.Patients.Add(new Patient(_a, "Duplicado", "X", "32456789"));
        Assert.Throws<DbUpdateException>(() => dbA.SaveChanges());
    }

    [Fact]
    public void Changes_generate_audit_entries_with_property_names_only_for_sensitive_data()
    {
        using var dbA = Create(_a);
        var p = dbA.Patients.First();
        dbA.MedicalHistories.Add(new MedicalHistory(_a, p.Id) { Allergies = "Penicilina" });
        dbA.SaveChanges();
        var log = dbA.AuditLogs.Where(l => l.EntityType == nameof(MedicalHistory)).ToList();
        Assert.Single(log);
        Assert.Contains("Allergies", log[0].Details);
        Assert.DoesNotContain("Penicilina", log[0].Details);
        Assert.Equal(_userA, log[0].UserId);
    }

    public void Dispose() => _connection.Dispose();
}
