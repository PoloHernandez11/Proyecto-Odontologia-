namespace Odontia.Domain.Enums;

public enum InvitationStatus { Pending = 1, Accepted = 2, Revoked = 3 }

public enum Sex { NotSpecified = 0, Female = 1, Male = 2, Other = 3 }

public enum PatientStatus { New = 1, Active = 2, InTreatment = 3, Inactive = 4 }

public enum AlertSeverity { Info = 1, Warning = 2, Critical = 3 }

public enum ScheduleBlockKind
{
    /// <summary>Horario bloqueado (almuerzo, reunión, etc.).</summary>
    Blocked = 1,
    /// <summary>Día no laborable / feriado completo.</summary>
    NonWorkingDay = 2,
    /// <summary>Excepción: disponibilidad adicional fuera del horario habitual.</summary>
    ExtraAvailability = 3,
}

public enum AppointmentStatus
{
    Pending = 1,
    Confirmed = 2,
    Arrived = 3,
    InWaitingRoom = 4,
    InProgress = 5,
    Attended = 6,
    Cancelled = 7,
    NoShow = 8,
}

public enum TaskPriority { Low = 1, Medium = 2, High = 3 }

public enum TaskItemStatus { Pending = 1, InProgress = 2, Completed = 3, Cancelled = 4 }

public enum TaskCategory { General = 1, Clinical = 2, Laboratory = 3, Administrative = 4, Supplies = 5, Appointments = 6 }

public enum ClinicalEntryType
{
    Evolution = 1,
    Consultation = 2,
    Observation = 3,
    Procedure = 4,
    Prescription = 5,
    Study = 6,
}

public enum DiagnosisStatus { Active = 1, Resolved = 2, Discarded = 3 }

public enum Dentition { Permanent = 1, Temporary = 2 }

public enum ToothType { Incisor = 1, Canine = 2, Premolar = 3, Molar = 4 }

/// <summary>Superficies dentarias. Oclusal aplica a premolares/molares; incisal a incisivos/caninos.</summary>
public enum ToothSurface
{
    Occlusal = 1,
    Incisal = 2,
    Mesial = 3,
    Distal = 4,
    Vestibular = 5,
    Lingual = 6,
    Palatal = 7,
}

public enum ConditionScope { Tooth = 1, Surface = 2, Both = 3 }

public enum FindingStatus
{
    /// <summary>Condición/tratamiento existente.</summary>
    Existing = 1,
    /// <summary>Tratamiento indicado / a realizar (pendiente).</summary>
    Planned = 2,
}

public enum OdontogramChangeType { Added = 1, Resolved = 2, Removed = 3, NoteAdded = 4, Completed = 5 }

public enum TreatmentStatus { Pending = 1, InProgress = 2, Completed = 3, Cancelled = 4 }

public enum BudgetStatus { Draft = 1, Issued = 2, Approved = 3, Rejected = 4, Expired = 5, Cancelled = 6 }

public enum PaymentMethod { Cash = 1, DebitCard = 2, CreditCard = 3, BankTransfer = 4, DigitalWallet = 5, HealthInsurance = 6, Check = 7, Other = 99 }

public enum PaymentStatus { Registered = 1, Voided = 2 }

public enum MovementType
{
    /// <summary>Cargo por presupuesto aprobado (débito).</summary>
    BudgetCharge = 1,
    /// <summary>Pago recibido (crédito).</summary>
    Payment = 2,
    /// <summary>Contraasiento por anulación de pago (débito).</summary>
    PaymentReversal = 3,
    /// <summary>Contraasiento por cancelación de presupuesto aprobado (crédito).</summary>
    BudgetChargeReversal = 4,
    /// <summary>Ajuste manual (débito o crédito).</summary>
    Adjustment = 5,
}

public enum MovementDirection { Debit = 1, Credit = 2 }

public enum LabWorkStatus
{
    Pending = 1,
    Sent = 2,
    InProduction = 3,
    ReadyForPickup = 4,
    Received = 5,
    Placed = 6,
    Cancelled = 7,
}

public enum DocumentCategory
{
    Radiograph = 1,
    Study = 2,
    Prescription = 3,
    ClinicalPhoto = 4,
    ConsentForm = 5,
    Administrative = 6,
    Identification = 7,
    InsuranceAuthorization = 8,
    Other = 99,
}

public enum ConsentStatus
{
    Pending = 1,
    /// <summary>Aceptación registrada en el sistema (NO es firma digital certificada).</summary>
    AcceptedInSystem = 2,
    /// <summary>Consentimiento firmado en papel, digitalizado y adjunto.</summary>
    SignedOnPaperAttached = 3,
    Rejected = 4,
    Revoked = 5,
}

public enum NotificationType
{
    General = 1,
    AppointmentReminder = 2,
    AppointmentChanged = 3,
    ConfirmationPending = 4,
    TaskAssigned = 5,
    TaskDue = 6,
    LabWorkUpdate = 7,
    LabWorkOverdue = 8,
    System = 9,
}

public enum MessageChannel { Email = 1, WhatsApp = 2 }

public enum OutboundMessageStatus
{
    Pending = 1,
    /// <summary>El proveedor confirmó la aceptación del mensaje.</summary>
    Sent = 2,
    Failed = 3,
    /// <summary>No se envió: no hay proveedor configurado (modo desarrollo).</summary>
    NotSentNoProvider = 4,
    Cancelled = 5,
}

public enum AuditAction
{
    Created = 1,
    Updated = 2,
    Deleted = 3,
    LoginSucceeded = 10,
    LoginFailed = 11,
    Logout = 12,
    PasswordChanged = 13,
    PasswordReset = 14,
    SessionRevoked = 15,
    TwoFactorChanged = 16,
    RolesChanged = 20,
    MemberActivationChanged = 21,
    InvitationSent = 22,
    InvitationAccepted = 23,
    ClinicalRecordViewed = 30,
    ClinicalEntryAmended = 31,
    ClinicalEntryVoided = 32,
    DocumentDownloaded = 40,
    PaymentVoided = 50,
    BudgetStatusChanged = 51,
    AccessDenied = 90,
}
