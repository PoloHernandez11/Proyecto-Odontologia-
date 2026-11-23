using Odontia.Application.Common;
using Odontia.Domain.Authorization;

namespace Odontia.UnitTests.QA;

public class QaPermissionMatrixTests
{
    public static IEnumerable<object[]> Roles() =>
    [
        [SystemRoles.Administrador, SystemRoles.AdministradorPermissions],
        [SystemRoles.Odontologo, SystemRoles.OdontologoPermissions],
        [SystemRoles.Secretaria, SystemRoles.SecretariaPermissions],
    ];

    /// <summary>Permisos que implican tener el permiso de lectura correspondiente para que la UI/API sea usable.</summary>
    private static readonly (string Requires, string Implied)[] Implications =
    [
        (Permissions.ConsultorioManage, Permissions.ConsultorioRead),
        (Permissions.UsersManage, Permissions.UsersRead),
        (Permissions.PatientsWrite, Permissions.PatientsRead),
        (Permissions.PatientsMedicalWrite, Permissions.PatientsMedicalRead),
        (Permissions.PatientsMedicalRead, Permissions.PatientsRead),
        (Permissions.AgendaManage, Permissions.AgendaRead),
        (Permissions.AppointmentsWrite, Permissions.AppointmentsRead),
        (Permissions.AppointmentsOverbook, Permissions.AppointmentsWrite),
        (Permissions.TasksWrite, Permissions.TasksRead),
        (Permissions.ClinicalWrite, Permissions.ClinicalRead),
        (Permissions.OdontogramWrite, Permissions.OdontogramRead),
        (Permissions.TreatmentsWrite, Permissions.TreatmentsRead),
        (Permissions.BudgetsWrite, Permissions.BudgetsRead),
        (Permissions.BudgetsApprove, Permissions.BudgetsRead),
        (Permissions.PaymentsWrite, Permissions.FinanceRead),
        (Permissions.PaymentsVoid, Permissions.FinanceRead),
        (Permissions.LabWrite, Permissions.LabRead),
        (Permissions.LabTrack, Permissions.LabRead),
        (Permissions.DocumentsWrite, Permissions.DocumentsRead),
        (Permissions.DocumentsDelete, Permissions.DocumentsRead),
        (Permissions.DocumentsClinicalRead, Permissions.DocumentsRead),
        (Permissions.ConsentsWrite, Permissions.ConsentsRead),
    ];

    [Fact]
    public void Catalog_has_no_duplicates_and_follows_naming()
    {
        Assert.Equal(Permissions.All.Count, Permissions.All.Distinct().Count());
        Assert.All(Permissions.All, p => Assert.Matches("^[a-z_]+(\\.[a-z_]+)+$", p));
    }

    [Fact]
    public void Every_public_permission_constant_is_in_catalog()
    {
        var constants = typeof(Permissions).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string)).Select(f => (string)f.GetRawConstantValue()!).ToList();
        Assert.Equal(constants.OrderBy(x => x), Permissions.All.OrderBy(x => x));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" patients.read")]
    [InlineData("PATIENTS.READ")]
    [InlineData("patients.*")]
    public void IsValid_is_exact(string p) => Assert.False(Permissions.IsValid(p));

    [Theory]
    [MemberData(nameof(Roles))]
    public void Role_permissions_are_valid_and_unique(string role, IReadOnlyList<string> perms)
    {
        Assert.NotEmpty(perms);
        Assert.True(perms.All(Permissions.IsValid), role);
        Assert.Equal(perms.Count, perms.Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(Roles))]
    public void Role_permissions_are_internally_consistent(string role, IReadOnlyList<string> perms)
    {
        var missing = Implications.Where(i => perms.Contains(i.Requires) && !perms.Contains(i.Implied))
            .Select(i => $"{role}: {i.Requires} sin {i.Implied}").ToList();
        Assert.Empty(missing);
    }

    [Fact]
    public void Only_admin_manages_users_roles_consultorio_and_audit()
    {
        string[] adminOnly = [Permissions.ConsultorioManage, Permissions.UsersManage, Permissions.RolesManage, Permissions.AuditRead];
        Assert.All(adminOnly, p => Assert.Contains(p, SystemRoles.AdministradorPermissions));
        Assert.All(adminOnly, p => Assert.DoesNotContain(p, SystemRoles.OdontologoPermissions));
        Assert.All(adminOnly, p => Assert.DoesNotContain(p, SystemRoles.SecretariaPermissions));
    }

    [Fact]
    public void Clinical_permissions_belong_only_to_odontologo()
    {
        string[] clinical =
        [
            Permissions.PatientsMedicalRead, Permissions.PatientsMedicalWrite, Permissions.ClinicalRead, Permissions.ClinicalWrite,
            Permissions.OdontogramRead, Permissions.OdontogramWrite, Permissions.TreatmentsRead, Permissions.TreatmentsWrite,
            Permissions.DocumentsClinicalRead, Permissions.DashboardClinical,
        ];
        Assert.All(clinical, p => Assert.Contains(p, SystemRoles.OdontologoPermissions));
        Assert.Empty(clinical.Intersect(SystemRoles.AdministradorPermissions));
        Assert.Empty(clinical.Intersect(SystemRoles.SecretariaPermissions));
    }

    [Fact]
    public void Odontologo_operates_only_own_agenda_by_default()
    {
        Assert.DoesNotContain(Permissions.AgendaAllProfessionals, SystemRoles.OdontologoPermissions);
        Assert.Contains(Permissions.AgendaAllProfessionals, SystemRoles.SecretariaPermissions);
    }

    [Fact]
    public void Secretaria_cannot_void_payments_or_approve_budgets_or_delete_documents()
    {
        Assert.DoesNotContain(Permissions.PaymentsVoid, SystemRoles.SecretariaPermissions);
        Assert.DoesNotContain(Permissions.BudgetsApprove, SystemRoles.SecretariaPermissions);
        Assert.DoesNotContain(Permissions.DocumentsDelete, SystemRoles.SecretariaPermissions);
        Assert.DoesNotContain(Permissions.LabWrite, SystemRoles.SecretariaPermissions);
    }

    [Fact]
    public void Every_permission_is_granted_by_some_system_role()
    {
        var granted = SystemRoles.AdministradorPermissions.Concat(SystemRoles.OdontologoPermissions).Concat(SystemRoles.SecretariaPermissions).ToHashSet();
        Assert.Empty(Permissions.All.Where(p => !granted.Contains(p)));
    }

    [Fact]
    public void System_role_ids_are_distinct_and_only_odontologo_is_professional()
    {
        Assert.Equal(3, new[] { SystemRoles.AdministradorId, SystemRoles.OdontologoId, SystemRoles.SecretariaId }.Distinct().Count());
        Assert.True(SystemRoles.IsProfessionalRole(SystemRoles.OdontologoId));
        Assert.False(SystemRoles.IsProfessionalRole(SystemRoles.AdministradorId));
        Assert.False(SystemRoles.IsProfessionalRole(SystemRoles.SecretariaId));
        Assert.False(SystemRoles.IsProfessionalRole(Guid.Empty));
    }
}

public class QaTenantContextTests
{
    [Fact]
    public void Unauthenticated_context_throws_unauthorized_on_user_id()
    {
        var t = new TenantContext();
        Assert.False(t.IsAuthenticated);
        Assert.Equal(401, Assert.Throws<UnauthorizedException>(() => t.UserId).StatusCode);
        Assert.Throws<ForbiddenAccessException>(() => t.ConsultorioId);
        Assert.Throws<ForbiddenAccessException>(() => t.MemberId);
        Assert.Null(t.ConsultorioIdOrNull);
    }

    [Fact]
    public void Permissions_are_case_sensitive()
    {
        var t = new TenantContext();
        t.InitializeUser(Guid.NewGuid(), "1.2.3.4");
        t.InitializeTenant(Guid.NewGuid(), Guid.NewGuid(), false, [Permissions.PatientsRead]);
        Assert.True(t.Has(Permissions.PatientsRead));
        Assert.False(t.Has("PATIENTS.READ"));
        t.Require(Permissions.PatientsRead);
        Assert.Equal(403, Assert.Throws<ForbiddenAccessException>(() => t.Require(Permissions.PatientsWrite)).StatusCode);
        Assert.Equal("1.2.3.4", t.IpAddress);
    }

    [Fact]
    public void Non_professional_cannot_act_even_on_own_agenda()
    {
        var user = Guid.NewGuid();
        var t = new TenantContext();
        t.InitializeUser(user, null);
        t.InitializeTenant(Guid.NewGuid(), Guid.NewGuid(), isProfessional: false, [Permissions.AgendaManage]);
        Assert.False(t.CanActOnProfessional(user));
    }

    [Fact]
    public void Re_initializing_tenant_replaces_permissions()
    {
        var t = new TenantContext();
        t.InitializeUser(Guid.NewGuid(), null);
        t.InitializeTenant(Guid.NewGuid(), Guid.NewGuid(), false, [Permissions.PatientsRead]);
        var second = Guid.NewGuid();
        t.InitializeTenant(second, Guid.NewGuid(), false, [Permissions.AgendaRead]);
        Assert.False(t.Has(Permissions.PatientsRead));
        Assert.Equal(second, t.ConsultorioId);
    }

    [Fact]
    public void Background_job_has_every_permission_and_any_agenda()
    {
        var t = new TenantContext();
        t.InitializeBackground(Guid.NewGuid());
        Assert.All(Permissions.All, p => Assert.True(t.Has(p)));
        Assert.True(t.CanActOnProfessional(Guid.NewGuid()));
        Assert.False(t.IsAuthenticated);
    }
}
