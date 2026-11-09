using Odontia.Domain.Common;
using Odontia.Domain.Dental;
using Odontia.Domain.Enums;

namespace Odontia.Domain.Entities;

/// <summary>Laboratorio dental externo.</summary>
public class Laboratory : TenantEntity
{
    private Laboratory() { }

    public Laboratory(Guid consultorioId, string name)
    {
        ConsultorioId = consultorioId;
        Name = Guard.Required(name, "Nombre", 120);
        IsActive = true;
    }

    public string Name { get; private set; } = default!;
    public string? ContactName { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? AddressLine { get; private set; }
    public string? Notes { get; private set; }
    public bool IsActive { get; private set; }

    public void Update(string name, string? contactName, string? phone, string? email, string? addressLine, string? notes)
    {
        Name = Guard.Required(name, "Nombre", 120);
        ContactName = Guard.Optional(contactName, "Contacto", 120);
        Phone = Guard.Optional(phone, "Teléfono", 40);
        Email = Guard.Optional(email, "Correo", 256);
        AddressLine = Guard.Optional(addressLine, "Dirección", 250);
        Notes = Guard.Optional(notes, "Notas", 1000);
    }

    public void SetActive(bool active) => IsActive = active;
}

/// <summary>Trabajo de laboratorio (prótesis, coronas, placas, etc.) con seguimiento de entregas.</summary>
public class LabWork : TenantEntity
{
    private static readonly Dictionary<LabWorkStatus, LabWorkStatus[]> Transitions = new()
    {
        [LabWorkStatus.Pending] = [LabWorkStatus.Sent, LabWorkStatus.Cancelled],
        [LabWorkStatus.Sent] = [LabWorkStatus.InProduction, LabWorkStatus.ReadyForPickup, LabWorkStatus.Received, LabWorkStatus.Cancelled],
        [LabWorkStatus.InProduction] = [LabWorkStatus.ReadyForPickup, LabWorkStatus.Received, LabWorkStatus.Cancelled],
        [LabWorkStatus.ReadyForPickup] = [LabWorkStatus.Received, LabWorkStatus.Cancelled],
        [LabWorkStatus.Received] = [LabWorkStatus.Placed, LabWorkStatus.Sent],
        [LabWorkStatus.Placed] = [],
        [LabWorkStatus.Cancelled] = [],
    };

    private readonly List<LabWorkStatusChange> _changes = [];

    private LabWork() { }

    public LabWork(Guid consultorioId, Guid patientId, Guid professionalUserId, Guid laboratoryId, string workType, int? toothCode,
        string? description, DateOnly? estimatedDeliveryDate, decimal? cost, string? notes, Guid? treatmentPlanItemId)
    {
        ConsultorioId = consultorioId;
        PatientId = patientId;
        ProfessionalUserId = professionalUserId;
        LaboratoryId = laboratoryId;
        Status = LabWorkStatus.Pending;
        Update(workType, toothCode, description, estimatedDeliveryDate, cost, notes);
        TreatmentPlanItemId = treatmentPlanItemId;
    }

    public Guid PatientId { get; private set; }
    public Guid ProfessionalUserId { get; private set; }
    public Guid LaboratoryId { get; private set; }
    public string WorkType { get; private set; } = default!;
    public int? ToothCode { get; private set; }
    public string? Description { get; private set; }
    public LabWorkStatus Status { get; private set; }
    public DateOnly? SentDate { get; private set; }
    public DateOnly? EstimatedDeliveryDate { get; private set; }
    public DateOnly? ActualDeliveryDate { get; private set; }
    public DateOnly? PlacementDate { get; private set; }
    public decimal? Cost { get; private set; }
    public string? Notes { get; private set; }
    public Guid? TreatmentPlanItemId { get; private set; }
    public IReadOnlyCollection<LabWorkStatusChange> Changes => _changes;

    public Patient? Patient { get; private set; }
    public Laboratory? Laboratory { get; private set; }

    public bool IsOpen => Status is LabWorkStatus.Pending or LabWorkStatus.Sent or LabWorkStatus.InProduction or LabWorkStatus.ReadyForPickup;
    public bool IsOverdue(DateOnly today) =>
        Status is LabWorkStatus.Pending or LabWorkStatus.Sent or LabWorkStatus.InProduction
        && EstimatedDeliveryDate is not null && EstimatedDeliveryDate < today;

    public static bool CanTransition(LabWorkStatus from, LabWorkStatus to) =>
        Transitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

    public void Update(string workType, int? toothCode, string? description, DateOnly? estimatedDeliveryDate, decimal? cost, string? notes)
    {
        if (Status is LabWorkStatus.Placed or LabWorkStatus.Cancelled)
            throw new DomainException("El trabajo está cerrado.", "lab_work_closed");
        WorkType = Guard.Required(workType, "Tipo de trabajo", 120);
        if (toothCode is not null) Fdi.Get(toothCode.Value);
        ToothCode = toothCode;
        Description = Guard.Optional(description, "Descripción", 1000);
        EstimatedDeliveryDate = estimatedDeliveryDate;
        Cost = cost is null ? null : Guard.Money(cost.Value, "Costo");
        Notes = Guard.Optional(notes, "Observaciones", 1000);
    }

    public void ChangeStatus(LabWorkStatus target, DateOnly date, string? note, Guid userId, DateTime nowUtc)
    {
        if (Status == target) return;
        if (!CanTransition(Status, target))
            throw new DomainException($"No se puede pasar un trabajo de '{Status}' a '{target}'.", "invalid_status_transition");
        switch (target)
        {
            case LabWorkStatus.Sent:
                SentDate = date;
                if (Status == LabWorkStatus.Received) ActualDeliveryDate = null; // reenvío por ajustes
                break;
            case LabWorkStatus.Received:
                if (SentDate is not null && date < SentDate) throw new DomainException("La fecha de recepción no puede ser anterior al envío.", "invalid_date");
                ActualDeliveryDate = date;
                break;
            case LabWorkStatus.Placed:
                if (ActualDeliveryDate is not null && date < ActualDeliveryDate) throw new DomainException("La fecha de colocación no puede ser anterior a la recepción.", "invalid_date");
                PlacementDate = date;
                break;
        }
        _changes.Add(new LabWorkStatusChange(ConsultorioId, Id, Status, target, Guard.Optional(note, "Nota", 500), userId, nowUtc));
        Status = target;
    }
}

public class LabWorkStatusChange : TenantEntity
{
    private LabWorkStatusChange() { }

    internal LabWorkStatusChange(Guid consultorioId, Guid labWorkId, LabWorkStatus from, LabWorkStatus to, string? note, Guid userId, DateTime atUtc)
    {
        ConsultorioId = consultorioId;
        LabWorkId = labWorkId;
        FromStatus = from;
        ToStatus = to;
        Note = note;
        ChangedByUserId = userId;
        ChangedAtUtc = atUtc;
    }

    public Guid LabWorkId { get; private set; }
    public LabWorkStatus FromStatus { get; private set; }
    public LabWorkStatus ToStatus { get; private set; }
    public string? Note { get; private set; }
    public Guid ChangedByUserId { get; private set; }
    public DateTime ChangedAtUtc { get; private set; }
}

/// <summary>Metadatos de un documento almacenado en el almacenamiento privado (el archivo NO vive en MySQL).</summary>
public class StoredDocument : TenantEntity
{
    private StoredDocument() { }

    public StoredDocument(Guid consultorioId, Guid patientId, DocumentCategory category, string fileName, string storageKey,
        string contentType, long sizeBytes, string sha256, string? description, Guid? clinicalEntryId, Guid? treatmentPlanId,
        Guid uploadedByUserId, DateTime uploadedAtUtc)
    {
        ConsultorioId = consultorioId;
        PatientId = patientId;
        Category = category;
        FileName = Guard.Required(fileName, "Nombre de archivo", 200);
        StorageKey = storageKey;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        Sha256 = sha256;
        Description = Guard.Optional(description, "Descripción", 500);
        ClinicalEntryId = clinicalEntryId;
        TreatmentPlanId = treatmentPlanId;
        UploadedByUserId = uploadedByUserId;
        UploadedAtUtc = uploadedAtUtc;
    }

    public Guid PatientId { get; private set; }
    public DocumentCategory Category { get; private set; }
    public string FileName { get; private set; } = default!;
    public string StorageKey { get; private set; } = default!;
    public string ContentType { get; private set; } = default!;
    public long SizeBytes { get; private set; }
    public string Sha256 { get; private set; } = default!;
    public string? Description { get; private set; }
    public Guid? ClinicalEntryId { get; private set; }
    public Guid? TreatmentPlanId { get; private set; }
    public Guid UploadedByUserId { get; private set; }
    public DateTime UploadedAtUtc { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }
    public Guid? DeletedByUserId { get; private set; }
    public string? DeletionReason { get; private set; }

    /// <summary>Documentos clínicos requieren permiso clínico para su lectura.</summary>
    public bool IsClinical => IsClinicalCategory(Category) || ClinicalEntryId is not null || TreatmentPlanId is not null;

    public static bool IsClinicalCategory(DocumentCategory c) =>
        c is DocumentCategory.Radiograph or DocumentCategory.Study or DocumentCategory.Prescription or DocumentCategory.ClinicalPhoto;

    /// <summary>Baja lógica (el archivo se conserva según la política de retención).</summary>
    public void SoftDelete(string reason, Guid userId, DateTime nowUtc)
    {
        if (IsDeleted) return;
        DeletionReason = Guard.Required(reason, "Motivo", 250);
        IsDeleted = true;
        DeletedAtUtc = nowUtc;
        DeletedByUserId = userId;
    }
}

/// <summary>
/// Consentimiento informado. Distingue explícitamente la aceptación registrada en el sistema
/// y el documento firmado en papel adjunto; Odontia NO emite firmas digitales certificadas.
/// </summary>
public class Consent : TenantEntity
{
    private Consent() { }

    public Consent(Guid consultorioId, Guid patientId, string title, string content, Guid? treatmentPlanId, Guid createdByUserId)
    {
        ConsultorioId = consultorioId;
        PatientId = patientId;
        Title = Guard.Required(title, "Título", 200);
        Content = Guard.Required(content, "Texto del consentimiento", 20000);
        TreatmentPlanId = treatmentPlanId;
        ResponsibleUserId = createdByUserId;
        Status = ConsentStatus.Pending;
    }

    public Guid PatientId { get; private set; }
    public string Title { get; private set; } = default!;
    public string Content { get; private set; } = default!;
    public Guid? TreatmentPlanId { get; private set; }
    public ConsentStatus Status { get; private set; }
    /// <summary>Profesional/usuario responsable del registro.</summary>
    public Guid ResponsibleUserId { get; private set; }
    /// <summary>Nombre de quien acepta (paciente o representante legal).</summary>
    public string? AcceptedByName { get; private set; }
    public string? AcceptedByDocument { get; private set; }
    public string? AcceptedByRelationship { get; private set; }
    public DateTime? DecisionAtUtc { get; private set; }
    public Guid? DecisionRecordedByUserId { get; private set; }
    public Guid? SignedDocumentId { get; private set; }
    public string? Notes { get; private set; }

    public void RecordSystemAcceptance(string acceptedByName, string? acceptedByDocument, string? relationship, Guid userId, DateTime nowUtc)
    {
        EnsurePending();
        AcceptedByName = Guard.Required(acceptedByName, "Nombre de quien acepta", 150);
        AcceptedByDocument = Guard.Optional(acceptedByDocument, "Documento", 20);
        AcceptedByRelationship = Guard.Optional(relationship, "Vínculo", 60);
        Decide(ConsentStatus.AcceptedInSystem, userId, nowUtc);
    }

    public void AttachSignedDocument(Guid documentId, string acceptedByName, Guid userId, DateTime nowUtc)
    {
        if (Status is ConsentStatus.Rejected or ConsentStatus.Revoked)
            throw new DomainException("El consentimiento fue rechazado o revocado.", "consent_closed");
        AcceptedByName = Guard.Required(acceptedByName, "Nombre de quien firma", 150);
        SignedDocumentId = documentId;
        Decide(ConsentStatus.SignedOnPaperAttached, userId, nowUtc);
    }

    public void Reject(string? notes, Guid userId, DateTime nowUtc)
    {
        EnsurePending();
        Notes = Guard.Optional(notes, "Notas", 1000);
        Decide(ConsentStatus.Rejected, userId, nowUtc);
    }

    public void Revoke(string notes, Guid userId, DateTime nowUtc)
    {
        if (Status is not (ConsentStatus.AcceptedInSystem or ConsentStatus.SignedOnPaperAttached))
            throw new DomainException("Sólo puede revocarse un consentimiento aceptado.", "invalid_status_transition");
        Notes = Guard.Required(notes, "Motivo", 1000);
        Decide(ConsentStatus.Revoked, userId, nowUtc);
    }

    private void EnsurePending()
    {
        if (Status != ConsentStatus.Pending) throw new DomainException("El consentimiento ya tiene una decisión registrada.", "consent_closed");
    }

    private void Decide(ConsentStatus status, Guid userId, DateTime nowUtc)
    {
        Status = status;
        DecisionAtUtc = nowUtc;
        DecisionRecordedByUserId = userId;
    }
}

/// <summary>Notificación interna para un usuario dentro de un consultorio.</summary>
public class Notification : TenantEntity
{
    private Notification() { }

    public Notification(Guid consultorioId, Guid userId, NotificationType type, string title, string? message,
        string? entityType, Guid? entityId, DateTime nowUtc)
    {
        ConsultorioId = consultorioId;
        UserId = userId;
        Type = type;
        Title = Guard.Required(title, "Título", 200);
        Message = Guard.Optional(message, "Mensaje", 1000);
        EntityType = entityType;
        EntityId = entityId;
        CreatedAtUtc = nowUtc;
    }

    public Guid UserId { get; private set; }
    public NotificationType Type { get; private set; }
    public string Title { get; private set; } = default!;
    public string? Message { get; private set; }
    public string? EntityType { get; private set; }
    public Guid? EntityId { get; private set; }
    public DateTime? ReadAtUtc { get; private set; }

    public void MarkRead(DateTime nowUtc) => ReadAtUtc ??= nowUtc;
}

/// <summary>
/// Mensaje saliente (correo/WhatsApp). Sólo se marca <see cref="OutboundMessageStatus.Sent"/>
/// cuando el proveedor confirma la aceptación; sin proveedor queda como NotSentNoProvider.
/// </summary>
public class OutboundMessage : Entity
{
    private OutboundMessage() { }

    public OutboundMessage(Guid? consultorioId, MessageChannel channel, string recipient, string? subject, string body,
        string purpose, string? relatedEntityType, Guid? relatedEntityId, DateTime nowUtc, bool containsSecret)
    {
        ConsultorioId = consultorioId;
        Channel = channel;
        Recipient = Guard.Required(recipient, "Destinatario", 256);
        Subject = Guard.Optional(subject, "Asunto", 250);
        // Los mensajes con secretos (enlaces de restablecimiento, invitaciones) no se persisten.
        Body = containsSecret ? "[contenido con enlace de seguridad no almacenado]" : Guard.Required(body, "Mensaje", 4000);
        Purpose = purpose;
        RelatedEntityType = relatedEntityType;
        RelatedEntityId = relatedEntityId;
        CreatedAtUtc = nowUtc;
        Status = OutboundMessageStatus.Pending;
    }

    public Guid? ConsultorioId { get; private set; }
    public MessageChannel Channel { get; private set; }
    public string Recipient { get; private set; } = default!;
    public string? Subject { get; private set; }
    public string Body { get; private set; } = default!;
    public string Purpose { get; private set; } = default!;
    public string? RelatedEntityType { get; private set; }
    public Guid? RelatedEntityId { get; private set; }
    public OutboundMessageStatus Status { get; private set; }
    public string? Provider { get; private set; }
    public string? ProviderMessageId { get; private set; }
    public int Attempts { get; private set; }
    public string? LastError { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? SentAtUtc { get; private set; }

    public void RecordResult(string provider, bool accepted, bool providerConfigured, string? providerMessageId, string? error, DateTime nowUtc)
    {
        Attempts++;
        Provider = provider;
        if (!providerConfigured)
        {
            Status = OutboundMessageStatus.NotSentNoProvider;
            LastError = "No hay proveedor configurado; el mensaje no fue enviado.";
            return;
        }
        if (accepted)
        {
            Status = OutboundMessageStatus.Sent;
            ProviderMessageId = providerMessageId;
            SentAtUtc = nowUtc;
            LastError = null;
        }
        else
        {
            Status = OutboundMessageStatus.Failed;
            LastError = error?.Length > 500 ? error[..500] : error;
        }
    }
}

/// <summary>Registro de auditoría (inmutable). No contiene contraseñas, tokens ni contenido clínico.</summary>
public class AuditLog
{
    private AuditLog() { }

    public AuditLog(Guid? consultorioId, Guid? userId, AuditAction action, string entityType, string? entityId,
        string? details, string? ipAddress, DateTime atUtc)
    {
        Id = Guid.CreateVersion7();
        ConsultorioId = consultorioId;
        UserId = userId;
        Action = action;
        EntityType = entityType.Length > 100 ? entityType[..100] : entityType;
        EntityId = entityId;
        Details = details?.Length > 4000 ? details[..4000] : details;
        IpAddress = ipAddress?.Length > 64 ? ipAddress[..64] : ipAddress;
        OccurredAtUtc = atUtc;
    }

    public Guid Id { get; private set; }
    public Guid? ConsultorioId { get; private set; }
    public Guid? UserId { get; private set; }
    public AuditAction Action { get; private set; }
    public string EntityType { get; private set; } = default!;
    public string? EntityId { get; private set; }
    public string? Details { get; private set; }
    public string? IpAddress { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
}

/// <summary>Contador de numeración correlativa por consultorio (presupuestos, recibos).</summary>
public class ConsultorioSequence
{
    private ConsultorioSequence() { }
    public ConsultorioSequence(Guid consultorioId, string name) { ConsultorioId = consultorioId; Name = name; }

    public Guid ConsultorioId { get; private set; }
    public string Name { get; private set; } = default!;
    public int LastValue { get; private set; }

    public const string Budget = "budget";
    public const string Receipt = "receipt";
}
