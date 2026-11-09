using Odontia.Application.Common;
using Odontia.Application.Features.Documents;
using Odontia.Application.Features.Finance;
using Odontia.Application.Features.Organization;
using Odontia.Application.Validation;
using Odontia.Domain.Authorization;
using Odontia.Domain.Enums;

namespace Odontia.UnitTests.Application;

public class PermissionTests
{
    private static TenantContext Ctx(IEnumerable<string> perms, bool professional = false, Guid? user = null)
    {
        var t = new TenantContext();
        t.InitializeUser(user ?? Guid.NewGuid(), "127.0.0.1");
        t.InitializeTenant(Guid.NewGuid(), Guid.NewGuid(), professional, perms);
        return t;
    }

    [Fact]
    public void Secretaria_role_has_no_clinical_permissions()
    {
        var clinical = new[] { Permissions.ClinicalRead, Permissions.ClinicalWrite, Permissions.OdontogramRead, Permissions.OdontogramWrite,
            Permissions.PatientsMedicalRead, Permissions.PatientsMedicalWrite, Permissions.TreatmentsRead, Permissions.DocumentsClinicalRead };
        Assert.Empty(SystemRoles.SecretariaPermissions.Intersect(clinical));
        Assert.Contains(Permissions.PatientsAlertsRead, SystemRoles.SecretariaPermissions);
    }

    [Fact]
    public void Administrator_role_does_not_include_clinical_access_by_default()
    {
        Assert.DoesNotContain(Permissions.ClinicalRead, SystemRoles.AdministradorPermissions);
        Assert.DoesNotContain(Permissions.PatientsMedicalRead, SystemRoles.AdministradorPermissions);
    }

    [Fact]
    public void All_system_role_permissions_are_valid()
    {
        foreach (var p in SystemRoles.AdministradorPermissions.Concat(SystemRoles.OdontologoPermissions).Concat(SystemRoles.SecretariaPermissions))
            Assert.True(Permissions.IsValid(p), p);
    }

    [Fact]
    public void Require_throws_forbidden_without_permission()
    {
        var t = Ctx(SystemRoles.SecretariaPermissions);
        Assert.Throws<ForbiddenAccessException>(() => t.Require(Permissions.ClinicalRead));
        t.Require(Permissions.AppointmentsWrite);
    }

    [Fact]
    public void Without_tenant_everything_is_forbidden()
    {
        var t = new TenantContext();
        t.InitializeUser(Guid.NewGuid(), null);
        Assert.Throws<ForbiddenAccessException>(() => t.Require(Permissions.PatientsRead));
        Assert.Throws<ForbiddenAccessException>(() => _ = t.ConsultorioId);
    }

    [Fact]
    public void Professional_without_all_agendas_can_only_act_on_own_agenda()
    {
        var me = Guid.NewGuid();
        var t = Ctx(SystemRoles.OdontologoPermissions, professional: true, user: me);
        Assert.True(t.CanActOnProfessional(me));
        Assert.False(t.CanActOnProfessional(Guid.NewGuid()));
        var reception = Ctx(SystemRoles.SecretariaPermissions);
        Assert.True(reception.CanActOnProfessional(Guid.NewGuid()));
    }

    [Fact]
    public void Cannot_grant_permissions_not_owned()
    {
        var t = Ctx(SystemRoles.SecretariaPermissions);
        var ex = Assert.Throws<ForbiddenAccessException>(() => PermissionPolicy.EnsureCanGrant(t, SystemRoles.AdministradorPermissions));
        Assert.Contains(Permissions.UsersManage, ex.Message);
        PermissionPolicy.EnsureCanGrant(t, [Permissions.TasksRead]);
    }

    [Fact]
    public void Background_context_is_scoped_to_one_consultorio()
    {
        var t = new TenantContext();
        var id = Guid.NewGuid();
        t.InitializeBackground(id);
        Assert.Equal(id, t.ConsultorioId);
        Assert.True(t.IsBackgroundJob);
        Assert.False(t.IsAuthenticated);
    }
}

public class FileValidationTests
{
    private static readonly byte[] Pdf = "%PDF-1.7\n"u8.ToArray();
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0];
    private static readonly byte[] Jpg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0];

    [Fact]
    public void Accepts_matching_signatures()
    {
        Assert.Equal(("application/pdf"), FileSignatureValidator.Validate("rx.pdf", Pdf).ContentType);
        Assert.Equal(("image/png"), FileSignatureValidator.Validate("foto.PNG", Png).ContentType);
        Assert.Equal((".jpg", "image/jpeg"), FileSignatureValidator.Validate("foto.jpeg", Jpg));
    }

    [Fact]
    public void Rejects_spoofed_content_and_forbidden_extensions()
    {
        Assert.Throws<RequestValidationException>(() => FileSignatureValidator.Validate("malware.pdf", "MZ\x90\x00"u8.ToArray()));
        Assert.Throws<RequestValidationException>(() => FileSignatureValidator.Validate("script.exe", Pdf));
        Assert.Throws<RequestValidationException>(() => FileSignatureValidator.Validate("pagina.html", Pdf));
        Assert.Throws<RequestValidationException>(() => FileSignatureValidator.Validate("imagen.png", Jpg));
    }

    [Theory]
    [InlineData("../../etc/passwd.pdf", "passwd.pdf")]
    [InlineData("C:\\Windows\\system32\\rx.pdf", "rx.pdf")]
    [InlineData("rx<script>.pdf", "rxscript.pdf")]
    [InlineData("...", "documento")]
    public void Sanitizes_file_names(string input, string expected) => Assert.Equal(expected, FileSignatureValidator.SafeFileName(input));
}

public class ValidatorTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(10.123)]
    public void Payment_validator_rejects_invalid_amounts(double amount)
    {
        var r = new RegisterPaymentRequestValidator().Validate(new RegisterPaymentRequest(Guid.NewGuid(), null, (decimal)amount, PaymentMethod.Cash, null, null, null));
        Assert.False(r.IsValid);
    }

    [Fact]
    public void Payment_validator_rejects_unknown_method_and_missing_patient()
    {
        var r = new RegisterPaymentRequestValidator().Validate(new RegisterPaymentRequest(Guid.Empty, null, 100, (PaymentMethod)42, null, null, null));
        Assert.Contains(r.Errors, e => e.PropertyName == "PatientId");
        Assert.Contains(r.Errors, e => e.PropertyName == "Method");
    }

    [Fact]
    public void Role_validator_rejects_unknown_permission()
    {
        var r = new SaveRoleRequestValidator().Validate(new SaveRoleRequest("Rol", null, ["patients.read", "root"]));
        Assert.False(r.IsValid);
    }

    [Fact]
    public void Budget_validator_requires_lines_and_valid_teeth()
    {
        Assert.False(new SaveBudgetRequestValidator().Validate(new SaveBudgetRequest(null, null, null, 0, null, [])).IsValid);
        var bad = new SaveBudgetRequest(null, null, null, 0, null, [new BudgetItemRequest(null, "Corona", 99, 1, 100, 0, null)]);
        Assert.False(new SaveBudgetRequestValidator().Validate(bad).IsValid);
    }

    [Fact]
    public void Secure_tokens_are_random_and_hash_is_stable()
    {
        var a = SecureTokens.Create();
        Assert.NotEqual(a, SecureTokens.Create());
        Assert.Equal(SecureTokens.Hash(a), SecureTokens.Hash(a));
        Assert.Equal(64, SecureTokens.Hash(a).Length);
    }
}
