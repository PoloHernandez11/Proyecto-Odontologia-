using System.Net;
using Odontia.Domain.Authorization;
using Odontia.IntegrationTests.Infrastructure;

namespace Odontia.IntegrationTests;

/// <summary>
/// Solicitudes simultáneas sobre trabajos de laboratorio (QA API-04) y sobre la regla "al menos un administrador"
/// (QA ARQ-05). Requieren MySQL real (ODONTIA_TEST_MYSQL): SQLite en memoria comparte una única conexión.
/// Las variantes deterministas sobre SQLite están en Odontia.UnitTests (DomainConcurrencyTests).
/// </summary>
public sealed class DomainConcurrencyTests(ClinicScenario s) : IClassFixture<ClinicScenario>
{
    private const int Parallel = 6;

    private async Task<TestApi[]> ClientsAsync() => await Task.WhenAll(Enumerable.Range(0, Parallel).Select(async _ =>
    {
        var c = new TestApi(s.Factory.NewClient(), s.Factory);
        await c.LoginAsync(s.Doctor.Email, s.Doctor.Password);
        return c;
    }));

    private async Task<Guid> LabWorkAsync()
    {
        var lab = (await s.Doctor.PostOk("/api/v1/laboratories", new { name = $"Lab {Guid.NewGuid():N}"[..20] }))["id"]!.GetValue<Guid>();
        return (await s.Doctor.PostOk("/api/v1/lab-works", new { patientId = s.PatientA, laboratoryId = lab, workType = "Corona", toothCode = 16 }))["id"]!.GetValue<Guid>();
    }

    [MySqlFact]
    public async Task Simultaneous_status_changes_of_a_lab_work_record_a_single_transition()
    {
        var work = await LabWorkAsync();
        var clients = await ClientsAsync();

        // Mitad "Enviado", mitad "Cancelado": antes del arreglo todas respondían 200 (historial Sent, Sent, Cancelled...).
        var results = await Task.WhenAll(clients.Select((c, i) =>
            c.Post($"/api/v1/lab-works/{work}/status", new { status = i % 2 == 0 ? "Sent" : "Cancelled", note = $"cliente {i}" })));

        Assert.All(results, r => Assert.Contains(r.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict, HttpStatusCode.UnprocessableEntity }));
        Assert.Contains(results, r => r.StatusCode == HttpStatusCode.OK);
        var history = (await s.Doctor.GetOk($"/api/v1/lab-works/{work}/history")).AsArray();
        // Cada transición registrada parte del estado que dejó la anterior (sin dos transiciones desde el mismo estado).
        Assert.Equal(history.Count, history.Select(h => h!["fromStatus"]!.GetValue<string>()).Distinct().Count());
        Assert.Equal("Pending", history[0]!["fromStatus"]!.GetValue<string>());
        for (var i = 1; i < history.Count; i++)
            Assert.Equal(history[i - 1]!["toStatus"]!.GetValue<string>(), history[i]!["fromStatus"]!.GetValue<string>());
        var status = (await s.Doctor.GetOk($"/api/v1/lab-works/{work}"))["status"]!.GetValue<string>();
        Assert.Equal(history[^1]!["toStatus"]!.GetValue<string>(), status);
    }

    [MySqlFact]
    public async Task Simultaneous_identical_status_changes_create_one_history_entry()
    {
        var work = await LabWorkAsync();
        var clients = await ClientsAsync();

        var results = await Task.WhenAll(clients.Select(c => c.Post($"/api/v1/lab-works/{work}/status", new { status = "Sent" })));

        // Las que leyeron el estado vencido reciben 409; las que llegaron después ven "Enviado" y no cambian nada (200).
        Assert.All(results, r => Assert.Contains(r.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }));
        Assert.Single((await s.Doctor.GetOk($"/api/v1/lab-works/{work}/history")).AsArray());
    }

    [MySqlFact]
    public async Task Simultaneous_edits_of_a_lab_work_never_mix_or_fail_with_500()
    {
        var work = await LabWorkAsync();
        var current = await s.Doctor.GetOk($"/api/v1/lab-works/{work}");
        var lab = current["laboratoryId"]!.GetValue<Guid>();
        var clients = await ClientsAsync();

        var results = await Task.WhenAll(clients.Select((c, i) => c.Put($"/api/v1/lab-works/{work}", new
        {
            patientId = s.PatientA, laboratoryId = lab, workType = $"Corona {i}", toothCode = 11 + i, notes = $"nota {i}",
        })));

        Assert.All(results, r => Assert.Contains(r.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }));
        Assert.Contains(results, r => r.StatusCode == HttpStatusCode.OK);
        var saved = await s.Doctor.GetOk($"/api/v1/lab-works/{work}");
        var winner = int.Parse(saved["workType"]!.GetValue<string>()["Corona ".Length..]);
        Assert.Equal($"nota {winner}", saved["notes"]!.GetValue<string>());
        Assert.Equal(11 + winner, saved["toothCode"]!.GetValue<int>());
        // La edición guardada corresponde a una solicitud que respondió 200 (ninguna 409 quedó aplicada).
        Assert.Equal(HttpStatusCode.OK, results[winner].StatusCode);
    }

    [MySqlFact]
    public async Task Two_administrators_demoting_each_other_simultaneously_cannot_leave_the_clinic_without_administrator()
    {
        // Varios consultorios en paralelo, cada uno con dos administradores que se degradan mutuamente a la vez.
        var rounds = await Task.WhenAll(Enumerable.Range(0, 10).Select(async i =>
        {
            var tag = Guid.NewGuid().ToString("N")[..8];
            var owner = await TestApi.RegisterOwnerAsync(s.Factory, $"admin-a-{tag}@odontia.test", $"Consultorio {tag}");
            var second = await owner.InviteAndLoginAsync($"admin-b-{tag}@odontia.test", SystemRoles.AdministradorId, "Bruno", "Admin");
            var members = (await owner.GetOk("/api/v1/users")).AsArray();
            Guid MemberOf(Guid userId) => members.First(m => m!["userId"]!.GetValue<Guid>() == userId)!["memberId"]!.GetValue<Guid>();
            var ownerMember = MemberOf(owner.UserId);
            var secondMember = MemberOf(second.UserId);
            // Mismos roles que el dueño (Administrador + Odontólogo): si no, la degradación del dueño se rechaza siempre con
            // 403 (no puede quitar permisos que no posee) y la prueba no ejercitaría la carrera.
            var ownerRoles = members.First(m => m!["userId"]!.GetValue<Guid>() == owner.UserId)!["roles"]!.AsArray()
                .Select(r => r!["id"]!.GetValue<Guid>()).ToArray();
            await owner.PutOk($"/api/v1/users/{secondMember}/roles", new { roleIds = ownerRoles });

            var results = await Task.WhenAll(
                owner.Put($"/api/v1/users/{secondMember}/roles", new { roleIds = new[] { SystemRoles.SecretariaId } }),
                second.Put($"/api/v1/users/{ownerMember}/roles", new { roleIds = new[] { SystemRoles.SecretariaId } }));
            return (owner, second, results);
        }));

        foreach (var (owner, second, results) in rounds)
        {
            Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.OK));
            // El perdedor recibe 422 last_administrator (esperó el bloqueo y vio la degradación confirmada) o 403 si su
            // solicitud llegó cuando ya había perdido el rol.
            Assert.All(results.Where(r => r.StatusCode != HttpStatusCode.OK),
                r => Assert.Contains(r.StatusCode, new[] { HttpStatusCode.UnprocessableEntity, HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized }));
            // Quien logró degradar al otro sigue siendo el único administrador activo.
            var winner = results[0].StatusCode == HttpStatusCode.OK ? owner : second;
            var users = (await winner.GetOk("/api/v1/users")).AsArray();
            var admins = users.Where(m => m!["isActive"]!.GetValue<bool>()
                                          && m["roles"]!.AsArray().Any(r => r!["id"]!.GetValue<Guid>() == SystemRoles.AdministradorId)).ToList();
            Assert.Equal(winner.UserId, Assert.Single(admins)!["userId"]!.GetValue<Guid>());
        }
    }
}
