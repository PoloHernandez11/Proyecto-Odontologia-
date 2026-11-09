namespace Odontia.Domain.Authorization;

/// <summary>
/// Catálogo de permisos granulares. Los endpoints verifican permisos, nunca nombres de rol.
/// Los roles (de sistema o personalizados por consultorio) son conjuntos de estos permisos.
/// </summary>
public static class Permissions
{
    // Consultorio
    public const string ConsultorioRead = "consultorio.read";
    public const string ConsultorioManage = "consultorio.manage";

    // Usuarios, roles, auditoría
    public const string UsersRead = "users.read";
    public const string UsersManage = "users.manage";
    public const string RolesManage = "roles.manage";
    public const string AuditRead = "audit.read";

    // Pacientes
    public const string PatientsRead = "patients.read";
    public const string PatientsWrite = "patients.write";
    public const string PatientsMedicalRead = "patients.medical.read";
    public const string PatientsMedicalWrite = "patients.medical.write";
    public const string PatientsAlertsRead = "patients.alerts.read";
    public const string InsurancesManage = "insurances.manage";

    // Agenda y turnos
    public const string AgendaRead = "agenda.read";
    public const string AgendaManage = "agenda.manage";
    /// <summary>Permite operar sobre la agenda de todos los profesionales (sin él, sólo la propia).</summary>
    public const string AgendaAllProfessionals = "agenda.all_professionals";
    public const string AppointmentsRead = "appointments.read";
    public const string AppointmentsWrite = "appointments.write";
    public const string AppointmentsOverbook = "appointments.overbook";

    // Tareas
    public const string TasksRead = "tasks.read";
    public const string TasksWrite = "tasks.write";

    // Clínica
    public const string ClinicalRead = "clinical.read";
    public const string ClinicalWrite = "clinical.write";
    public const string OdontogramRead = "odontogram.read";
    public const string OdontogramWrite = "odontogram.write";
    public const string TreatmentsRead = "treatments.read";
    public const string TreatmentsWrite = "treatments.write";
    public const string CatalogManage = "catalog.manage";

    // Finanzas
    public const string BudgetsRead = "budgets.read";
    public const string BudgetsWrite = "budgets.write";
    public const string BudgetsApprove = "budgets.approve";
    public const string FinanceRead = "finance.read";
    public const string PaymentsWrite = "payments.write";
    public const string PaymentsVoid = "payments.void";

    // Laboratorio
    public const string LabRead = "lab.read";
    public const string LabWrite = "lab.write";
    /// <summary>Actualizar logística de trabajos (envío, recepción, retiro) sin acceso a crear/editar el trabajo.</summary>
    public const string LabTrack = "lab.track";
    public const string LaboratoriesManage = "laboratories.manage";

    // Documentos y consentimientos
    public const string DocumentsRead = "documents.read";
    public const string DocumentsClinicalRead = "documents.clinical.read";
    public const string DocumentsWrite = "documents.write";
    public const string DocumentsDelete = "documents.delete";
    public const string ConsentsRead = "consents.read";
    public const string ConsentsWrite = "consents.write";

    // Notificaciones / mensajería
    public const string MessagingSend = "messaging.send";

    // Dashboards
    public const string DashboardReception = "dashboard.reception";
    public const string DashboardClinical = "dashboard.clinical";

    public static readonly IReadOnlyList<string> All =
    [
        ConsultorioRead, ConsultorioManage, UsersRead, UsersManage, RolesManage, AuditRead,
        PatientsRead, PatientsWrite, PatientsMedicalRead, PatientsMedicalWrite, PatientsAlertsRead, InsurancesManage,
        AgendaRead, AgendaManage, AgendaAllProfessionals, AppointmentsRead, AppointmentsWrite, AppointmentsOverbook,
        TasksRead, TasksWrite,
        ClinicalRead, ClinicalWrite, OdontogramRead, OdontogramWrite, TreatmentsRead, TreatmentsWrite, CatalogManage,
        BudgetsRead, BudgetsWrite, BudgetsApprove, FinanceRead, PaymentsWrite, PaymentsVoid,
        LabRead, LabWrite, LabTrack, LaboratoriesManage,
        DocumentsRead, DocumentsClinicalRead, DocumentsWrite, DocumentsDelete, ConsentsRead, ConsentsWrite,
        MessagingSend, DashboardReception, DashboardClinical,
    ];

    public static bool IsValid(string permission) => All.Contains(permission);
}

/// <summary>Roles de sistema predeterminados (sembrados de forma idempotente).</summary>
public static class SystemRoles
{
    public const string Administrador = "Administrador";
    public const string Odontologo = "Odontólogo";
    public const string Secretaria = "Secretaria";

    public static readonly Guid AdministradorId = Guid.Parse("0f6b7a52-1c1e-4f7a-9c11-000000000001");
    public static readonly Guid OdontologoId = Guid.Parse("0f6b7a52-1c1e-4f7a-9c11-000000000002");
    public static readonly Guid SecretariaId = Guid.Parse("0f6b7a52-1c1e-4f7a-9c11-000000000003");

    /// <summary>
    /// Administrador: gestión del consultorio, usuarios y configuración. NO incluye acceso clínico
    /// (mínimo privilegio): un administrador que además atiende debe tener también el rol Odontólogo.
    /// </summary>
    public static readonly IReadOnlyList<string> AdministradorPermissions =
    [
        Permissions.ConsultorioRead, Permissions.ConsultorioManage,
        Permissions.UsersRead, Permissions.UsersManage, Permissions.RolesManage, Permissions.AuditRead,
        Permissions.InsurancesManage, Permissions.CatalogManage, Permissions.LaboratoriesManage,
        Permissions.AgendaRead, Permissions.AgendaManage, Permissions.AgendaAllProfessionals,
        Permissions.PatientsRead, Permissions.TasksRead, Permissions.TasksWrite,
        Permissions.BudgetsRead, Permissions.FinanceRead, Permissions.PaymentsVoid, Permissions.DocumentsRead, Permissions.DocumentsDelete,
        Permissions.DashboardReception, Permissions.MessagingSend,
    ];

    public static readonly IReadOnlyList<string> OdontologoPermissions =
    [
        Permissions.ConsultorioRead, Permissions.UsersRead,
        Permissions.PatientsRead, Permissions.PatientsWrite, Permissions.PatientsMedicalRead,
        Permissions.PatientsMedicalWrite, Permissions.PatientsAlertsRead,
        Permissions.AgendaRead, Permissions.AgendaManage, Permissions.AppointmentsRead, Permissions.AppointmentsWrite,
        Permissions.AppointmentsOverbook,
        Permissions.TasksRead, Permissions.TasksWrite,
        Permissions.ClinicalRead, Permissions.ClinicalWrite, Permissions.OdontogramRead, Permissions.OdontogramWrite,
        Permissions.TreatmentsRead, Permissions.TreatmentsWrite,
        Permissions.BudgetsRead, Permissions.BudgetsWrite, Permissions.BudgetsApprove,
        Permissions.FinanceRead, Permissions.PaymentsWrite,
        Permissions.LabRead, Permissions.LabWrite, Permissions.LabTrack,
        Permissions.DocumentsRead, Permissions.DocumentsClinicalRead, Permissions.DocumentsWrite, Permissions.DocumentsDelete,
        Permissions.ConsentsRead, Permissions.ConsentsWrite,
        Permissions.DashboardClinical,
    ];

    /// <summary>
    /// Secretaria: agenda, turnos, pacientes, tareas y datos administrativos. Sin historia clínica,
    /// odontograma, diagnósticos ni antecedentes completos; sólo alertas médicas relevantes.
    /// </summary>
    public static readonly IReadOnlyList<string> SecretariaPermissions =
    [
        Permissions.ConsultorioRead, Permissions.UsersRead,
        Permissions.PatientsRead, Permissions.PatientsWrite, Permissions.PatientsAlertsRead, Permissions.InsurancesManage,
        Permissions.AgendaRead, Permissions.AgendaManage, Permissions.AgendaAllProfessionals,
        Permissions.AppointmentsRead, Permissions.AppointmentsWrite, Permissions.AppointmentsOverbook,
        Permissions.TasksRead, Permissions.TasksWrite,
        Permissions.BudgetsRead, Permissions.FinanceRead, Permissions.PaymentsWrite,
        Permissions.LabRead, Permissions.LabTrack,
        Permissions.DocumentsRead, Permissions.DocumentsWrite, Permissions.ConsentsRead, Permissions.ConsentsWrite,
        Permissions.MessagingSend, Permissions.DashboardReception,
    ];

    /// <summary>Roles que habilitan a un miembro como profesional que atiende turnos.</summary>
    public static bool IsProfessionalRole(Guid roleId) => roleId == OdontologoId;
}
