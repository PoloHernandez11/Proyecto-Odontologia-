using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Odontia.Domain.Entities;
using Odontia.Domain.Enums;

namespace Odontia.Application.Abstractions;

/// <summary>
/// Puerto de persistencia. Las consultas sobre entidades de consultorio quedan filtradas
/// automáticamente por el consultorio activo (filtro global), garantizado en el servidor.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<ApplicationUser> Users { get; }
    DbSet<Consultorio> Consultorios { get; }
    DbSet<Gabinete> Gabinetes { get; }
    DbSet<ConsultorioMember> Members { get; }
    DbSet<Role> AppRoles { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<MemberRole> MemberRoles { get; }
    DbSet<Invitation> Invitations { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<UserNotificationSettings> NotificationSettings { get; }
    DbSet<HealthInsurance> HealthInsurances { get; }
    DbSet<Patient> Patients { get; }
    DbSet<MedicalHistory> MedicalHistories { get; }
    DbSet<PatientAlert> PatientAlerts { get; }
    DbSet<WorkingHours> WorkingHours { get; }
    DbSet<ScheduleBlock> ScheduleBlocks { get; }
    DbSet<Appointment> Appointments { get; }
    DbSet<AppointmentHistory> AppointmentHistories { get; }
    DbSet<TaskItem> Tasks { get; }
    DbSet<ClinicalRecord> ClinicalRecords { get; }
    DbSet<ClinicalEntry> ClinicalEntries { get; }
    DbSet<ClinicalEntryRevision> ClinicalEntryRevisions { get; }
    DbSet<Diagnosis> Diagnoses { get; }
    DbSet<DentalPiece> DentalPieces { get; }
    DbSet<DentalSurface> DentalSurfaces { get; }
    DbSet<OdontogramCondition> OdontogramConditions { get; }
    DbSet<Odontogram> Odontograms { get; }
    DbSet<ToothFinding> ToothFindings { get; }
    DbSet<OdontogramHistoryEntry> OdontogramHistory { get; }
    DbSet<ToothNote> ToothNotes { get; }
    DbSet<ProcedureCatalogItem> Procedures { get; }
    DbSet<TreatmentPlan> TreatmentPlans { get; }
    DbSet<TreatmentPlanItem> TreatmentPlanItems { get; }
    DbSet<TreatmentChange> TreatmentChanges { get; }
    DbSet<Budget> Budgets { get; }
    DbSet<BudgetItem> BudgetItems { get; }
    DbSet<BudgetStatusChange> BudgetStatusChanges { get; }
    DbSet<Payment> Payments { get; }
    DbSet<AccountMovement> AccountMovements { get; }
    DbSet<Laboratory> Laboratories { get; }
    DbSet<LabWork> LabWorks { get; }
    DbSet<LabWorkStatusChange> LabWorkStatusChanges { get; }
    DbSet<StoredDocument> Documents { get; }
    DbSet<Consent> Consents { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<OutboundMessage> OutboundMessages { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<ConsultorioSequence> Sequences { get; }

    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Contexto de la solicitud: usuario autenticado y consultorio activo validado en el servidor.</summary>
public interface ITenantContext
{
    bool IsAuthenticated { get; }
    bool HasTenant { get; }
    Guid UserId { get; }
    /// <summary>Consultorio activo. Lanza 403 si no hay un consultorio validado.</summary>
    Guid ConsultorioId { get; }
    Guid? ConsultorioIdOrNull { get; }
    Guid MemberId { get; }
    bool IsProfessional { get; }
    IReadOnlySet<string> Permissions { get; }
    string? IpAddress { get; }
    bool IsBackgroundJob { get; }

    bool Has(string permission);
    void Require(string permission);
    /// <summary>¿Puede operar sobre la agenda/turnos del profesional indicado?</summary>
    bool CanActOnProfessional(Guid professionalUserId);
}

/// <summary>Inicialización del contexto (middleware HTTP o trabajos en segundo plano).</summary>
public interface ITenantContextInitializer
{
    void InitializeUser(Guid userId, string? ipAddress);
    void InitializeTenant(Guid consultorioId, Guid memberId, bool isProfessional, IEnumerable<string> permissions);
    /// <summary>Contexto de sistema para trabajos en segundo plano, acotado a un consultorio.</summary>
    void InitializeBackground(Guid consultorioId);
}

/// <summary>
/// Serializa reservas concurrentes de un mismo profesional/gabinete: abre una transacción y toma un
/// bloqueo de fila exclusivo, de modo que la validación de disponibilidad y la inserción sean atómicas.
/// </summary>
public interface IAgendaLock
{
    Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> AcquireAsync(Guid professionalUserId, Guid? gabineteId, CancellationToken ct);
}

/// <summary>Numeración correlativa atómica por consultorio (presupuestos, recibos).</summary>
public interface ISequenceGenerator
{
    Task<int> NextAsync(string name, CancellationToken ct);
}

public sealed record StoredFile(string StorageKey, long SizeBytes, string Sha256);

/// <summary>Almacenamiento privado de archivos (nunca público). Reemplazable por S3/Azure Blob/etc.</summary>
public interface IFileStorage
{
    Task<StoredFile> SaveAsync(Guid consultorioId, Stream content, string extension, CancellationToken ct);
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct);
    Task DeleteAsync(string storageKey, CancellationToken ct);
}

public sealed record OutgoingEmail(string To, string Subject, string HtmlBody, string TextBody);
public sealed record OutgoingWhatsApp(string ToPhoneE164, string Text);

/// <summary>Resultado de envío. Accepted sólo es true cuando el proveedor confirmó la aceptación.</summary>
public sealed record SendResult(bool ProviderConfigured, bool Accepted, string Provider, string? ProviderMessageId, string? Error)
{
    public static SendResult NoProvider(string provider) => new(false, false, provider, null, null);
}

public interface IEmailSender
{
    Task<SendResult> SendAsync(OutgoingEmail email, CancellationToken ct);
}

public interface IWhatsAppSender
{
    Task<SendResult> SendAsync(OutgoingWhatsApp message, CancellationToken ct);
}

/// <summary>Registro y despacho de mensajes salientes con trazabilidad del resultado real.</summary>
public interface IMessageDispatcher
{
    Task<OutboundMessage> SendEmailAsync(Guid? consultorioId, string to, string subject, string html, string text, string purpose,
        bool containsSecret, string? relatedType, Guid? relatedId, CancellationToken ct);

    Task<OutboundMessage> SendWhatsAppAsync(Guid? consultorioId, string toPhone, string text, string purpose,
        string? relatedType, Guid? relatedId, CancellationToken ct);
}

/// <summary>Registro explícito de eventos de seguridad/operaciones importantes.</summary>
public interface IAuditWriter
{
    void Write(AuditAction action, string entityType, string? entityId, string? details = null, Guid? consultorioId = null, Guid? userId = null);
}

public sealed record AccessTokenResult(string Token, DateTime ExpiresAtUtc);

public interface IJwtTokenService
{
    AccessTokenResult CreateAccessToken(ApplicationUser user, Guid? consultorioId, Guid sessionId);
    /// <summary>Token corto (5 min) para completar el segundo factor.</summary>
    string CreateTwoFactorChallenge(Guid userId);
    Guid? ValidateTwoFactorChallenge(string token);
}

/// <summary>Configuración de URLs públicas del frontend (enlaces en correos).</summary>
public sealed class FrontendOptions
{
    public const string Section = "Frontend";
    public string BaseUrl { get; set; } = "http://localhost:5173";
    public string ResetPasswordPath { get; set; } = "/restablecer-contrasena";
    public string ConfirmEmailPath { get; set; } = "/confirmar-correo";
    public string AcceptInvitationPath { get; set; } = "/aceptar-invitacion";
}
