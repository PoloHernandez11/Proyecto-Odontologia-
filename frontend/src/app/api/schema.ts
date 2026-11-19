// ARCHIVO GENERADO. No editar a mano: ejecutar `node tools/gen-types.js` desde la carpeta Odontia.
// Origen: docs/openapi.v1.json (Odontia API 1.0)
/* eslint-disable */

export interface AcceptInvitationRequest {
  token: string;
  firstName?: string | null;
  lastName?: string | null;
  password: string;
}

export interface AccountMovementDto {
  id: string;
  occurredAtUtc: string;
  type: MovementType;
  direction: MovementDirection;
  amount: number;
  runningBalance: number;
  description: string;
  budgetId?: string | null;
  paymentId?: string | null;
}

export interface AccountStatementDto {
  patientId: string;
  totalCharges: number;
  totalCredits: number;
  balance: number;
  movements: AccountMovementDto[];
  budgets: BudgetBalanceDto[];
}

export interface AccountSummaryDto {
  approvedBudgets: number;
  paid: number;
  balance: number;
}

export interface ActivityItemDto {
  atUtc: string;
  kind: string;
  description: string;
  patientId?: string | null;
  patientName?: string | null;
}

export interface AddPlanItemRequest {
  procedureId: string;
  toothCode?: number | null;
  surfaces?: ToothSurface[] | null;
  professionalUserId?: string | null;
  diagnosisId?: string | null;
  notes?: string | null;
}

export interface AddToothNoteRequest {
  text: string;
}

export interface AgendaAppointmentDto {
  id: string;
  startUtc: string;
  endUtc: string;
  durationMinutes: number;
  status: AppointmentStatus;
  patientId: string;
  patientName: string;
  patientPhone?: string | null;
  professionalUserId: string;
  professionalName: string;
  gabineteId?: string | null;
  gabineteName?: string | null;
  reason?: string | null;
  isOverbooking: boolean;
  labWorks: AgendaLabWorkDto[];
}

export interface AgendaDto {
  from: string;
  to: string;
  timeZoneId: string;
  appointments: AgendaAppointmentDto[];
  blocks: ScheduleBlockDto[];
  workingHours: WorkingHoursDto[];
}

export interface AgendaLabWorkDto {
  id: string;
  workType: string;
  toothCodes: number[];
  laboratoryName: string;
  status: LabWorkStatus;
}

export interface AgendaMonthDto {
  year: number;
  month: number;
  timeZoneId: string;
  days: MonthDayDto[];
}

export interface AlertDto {
  id?: string | null;
  title: string;
  details?: string | null;
  severity: AlertSeverity;
  isDerived: boolean;
  visibleToReception: boolean;
}

export type AlertSeverity = "Info" | "Warning" | "Critical";

export interface AmendClinicalEntryRequest {
  title: string;
  description?: string | null;
  indications?: string | null;
  teeth?: number[] | null;
  reason: string;
  version: number;
}

export interface ApiProblem {
  type: string;
  title: string;
  status: number;
  detail?: string | null;
  code: string;
  traceId: string;
  errors?: {
  [key: string]: string[];
} | null;
}

export interface ApplyConditionRequest {
  toothCode: number;
  surface: ToothSurface;
  conditionId: string;
  status: FindingStatus;
  notes?: string | null;
  treatmentPlanItemId?: string | null;
}

export interface ApplyConditionsRequest {
  items: ApplyConditionRequest[];
}

export interface AppointmentDto {
  id: string;
  patientId: string;
  patientName: string;
  patientDocument: string;
  patientPhone?: string | null;
  patientAge?: number | null;
  patientInsurance?: string | null;
  professionalUserId: string;
  professionalName: string;
  gabineteId?: string | null;
  gabineteName?: string | null;
  startUtc: string;
  endUtc: string;
  durationMinutes: number;
  status: AppointmentStatus;
  reason?: string | null;
  administrativeNotes?: string | null;
  isOverbooking: boolean;
  confirmedAtUtc?: string | null;
  arrivedAtUtc?: string | null;
  attendedAtUtc?: string | null;
  cancelledAtUtc?: string | null;
  cancellationReason?: string | null;
  rescheduleCount: number;
  alerts: string[];
  version: string;
  createdAtUtc: string;
  labWorks: AgendaLabWorkDto[];
}

export interface AppointmentHistoryDto {
  changedAtUtc: string;
  action: string;
  fromStatus: AppointmentStatus;
  toStatus: AppointmentStatus;
  details?: string | null;
  changedBy?: string | null;
}

export type AppointmentStatus = "Pending" | "Confirmed" | "Arrived" | "InWaitingRoom" | "InProgress" | "Attended" | "Cancelled" | "NoShow";

export interface AppointmentStatusCountsDto {
  total: number;
  byStatus: {
  [key: string]: number;
};
}

export interface AssignTaskRequest {
  assignedToUserId?: string | null;
}

export interface AttachSignedConsentRequest {
  documentId: string;
  signedByName: string;
}

export interface AuditLogDto {
  id: string;
  occurredAtUtc: string;
  action: string;
  entityType: string;
  entityId?: string | null;
  userId?: string | null;
  userName?: string | null;
  details?: string | null;
  ipAddress?: string | null;
}

export interface AuthSessionDto {
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  sessionId: string;
  user: AuthUserDto;
  activeConsultorioId?: string | null;
  permissions: string[];
  memberships: MembershipDto[];
}

export interface AuthUserDto {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  displayName: string;
  title?: string | null;
  specialty?: string | null;
  licenseNumber?: string | null;
  twoFactorEnabled: boolean;
}

export interface AuthenticatorSetupDto {
  sharedKey: string;
  authenticatorUri: string;
}

export interface AvailabilityDto {
  date: string;
  professionalUserId: string;
  durationMinutes: number;
  slots: AvailabilitySlotDto[];
  blocks: ScheduleBlockDto[];
}

export interface AvailabilitySlotDto {
  startUtc: string;
  endUtc: string;
  localTime: string;
}

export interface BudgetBalanceDto {
  budgetId: string;
  number: number;
  total: number;
  paid: number;
  outstanding: number;
}

export interface BudgetDecisionRequest {
  reason?: string | null;
}

export interface BudgetDto {
  id: string;
  number: number;
  patientId: string;
  patientName: string;
  treatmentPlanId?: string | null;
  issueDate: string;
  expirationDate: string;
  status: BudgetStatus;
  subtotal: number;
  discountPercent: number;
  discountAmount: number;
  total: number;
  paid: number;
  outstanding: number;
  notes?: string | null;
  statusReason?: string | null;
  approvedAtUtc?: string | null;
  items: BudgetItemDto[];
  createdAtUtc: string;
}

export interface BudgetItemDto {
  id: string;
  procedureId?: string | null;
  description: string;
  toothCode?: number | null;
  quantity: number;
  unitPrice: number;
  discountPercent: number;
  lineTotal: number;
  treatmentPlanItemId?: string | null;
}

export interface BudgetItemRequest {
  procedureId?: string | null;
  description?: string | null;
  toothCode?: number | null;
  quantity: number;
  unitPrice?: number | null;
  discountPercent: number;
  treatmentPlanItemId?: string | null;
}

export type BudgetStatus = "Draft" | "Issued" | "Approved" | "Rejected" | "Expired" | "Cancelled";

export interface BudgetStatusChangeDto {
  changedAtUtc: string;
  fromStatus: BudgetStatus;
  toStatus: BudgetStatus;
  reason?: string | null;
  changedBy?: string | null;
}

export interface CancelAppointmentRequest {
  reason?: string | null;
}

export interface CancelPlanRequest {
  reason: string;
}

export interface ChangeAppointmentStatusRequest {
  status: AppointmentStatus;
  reason?: string | null;
}

export interface ChangeDiagnosisStatusRequest {
  status: DiagnosisStatus;
}

export interface ChangeEmailRequest {
  newEmail: string;
  password: string;
}

export interface ChangeItemStatusRequest {
  status: TreatmentStatus;
  note?: string | null;
}

export interface ChangeLabWorkStatusRequest {
  status: LabWorkStatus;
  date?: string | null;
  note?: string | null;
  newEstimatedDeliveryDate?: string | null;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

export interface ChangePatientStatusRequest {
  status: PatientStatus;
}

export interface ChangeTaskStatusRequest {
  status: TaskItemStatus;
}

export interface ClinicalDashboardDto {
  date: string;
  patientsToday: number;
  attendedToday: number;
  remainingToday: number;
  nextAppointment: DashboardAppointmentDto;
  myAppointmentsToday: DashboardAppointmentDto[];
  inRoom: InRoomPatientDto;
  treatmentsInProgress: number;
  treatmentsNeedingFollowUp: number;
  openLabWorks: number;
  labWorksReadyForPickup: number;
  medicalAlerts: PatientAlertSummaryDto[];
  recentActivity: ActivityItemDto[];
  weeklyActivity: WeeklyActivityDto[];
}

export interface ClinicalDocumentRefDto {
  id: string;
  fileName: string;
  category: DocumentCategory;
}

export interface ClinicalEntryDto {
  id: string;
  type: ClinicalEntryType;
  occurredAtUtc: string;
  title: string;
  description?: string | null;
  indications?: string | null;
  teeth: number[];
  professionalUserId: string;
  professionalName: string;
  appointmentId?: string | null;
  version: number;
  isVoided: boolean;
  voidReason?: string | null;
  createdAtUtc: string;
  lastAmendedAtUtc?: string | null;
  documents: ClinicalDocumentRefDto[];
}

export interface ClinicalEntryRevisionDto {
  version: number;
  title: string;
  description?: string | null;
  indications?: string | null;
  teeth: string;
  reason: string;
  editedBy: string;
  editedAtUtc: string;
}

export type ClinicalEntryType = "Evolution" | "Consultation" | "Observation" | "Procedure" | "Prescription" | "Study";

export interface ClinicalRecordDto {
  patientId: string;
  patientName: string;
  age?: number | null;
  recordId?: string | null;
  openedAtUtc?: string | null;
  medicalHistory: MedicalHistoryDto;
  alerts: AlertDto[];
  entryCount: number;
  activeDiagnoses: number;
}

export interface ConditionDto {
  id: string;
  code: string;
  name: string;
  scope: ConditionScope;
  color?: string | null;
  marksToothAbsent: boolean;
  isSystem: boolean;
  isActive: boolean;
}

export type ConditionScope = "Tooth" | "Surface" | "Both";

export interface ConfirmEmailRequest {
  userId: string;
  token: string;
}

export interface ConsentDecisionRequest {
  notes?: string | null;
}

export interface ConsentDto {
  id: string;
  patientId: string;
  title: string;
  content: string;
  treatmentPlanId?: string | null;
  status: ConsentStatus;
  signatureType: string;
  acceptedByName?: string | null;
  acceptedByDocument?: string | null;
  acceptedByRelationship?: string | null;
  decisionAtUtc?: string | null;
  decisionRecordedBy?: string | null;
  signedDocumentId?: string | null;
  notes?: string | null;
  responsible: string;
  createdAtUtc: string;
}

export type ConsentStatus = "Pending" | "AcceptedInSystem" | "SignedOnPaperAttached" | "Rejected" | "Revoked";

export interface ConsultorioDto {
  id: string;
  name: string;
  legalName?: string | null;
  taxId?: string | null;
  phone?: string | null;
  email?: string | null;
  website?: string | null;
  addressLine?: string | null;
  city?: string | null;
  province?: string | null;
  postalCode?: string | null;
  country: string;
  timeZoneId: string;
  defaultAppointmentMinutes: number;
  slotIntervalMinutes: number;
  reminderHoursBefore: number;
  isActive: boolean;
}

export interface CreateAppointmentRequest {
  patientId: string;
  professionalUserId: string;
  gabineteId?: string | null;
  start: string;
  durationMinutes?: number | null;
  reason?: string | null;
  administrativeNotes?: string | null;
  isOverbooking: boolean;
  confirmed: boolean;
}

export interface CreateClinicalEntryRequest {
  type: ClinicalEntryType;
  occurredAt?: string | null;
  title: string;
  description?: string | null;
  indications?: string | null;
  teeth?: number[] | null;
  appointmentId?: string | null;
}

export interface CreateConsentRequest {
  title: string;
  content: string;
  treatmentPlanId?: string | null;
}

export interface CreateDiagnosisRequest {
  description: string;
  code?: string | null;
  toothCode?: number | null;
  surface: ToothSurface;
  diagnosedAt?: string | null;
  clinicalEntryId?: string | null;
  notes?: string | null;
}

export interface CreateInvitationRequest {
  email: string;
  roleIds: string[];
  isProfessional: boolean;
}

export interface CreatePatientRequest {
  patient: SavePatientRequest;
  medicalHistory: MedicalHistoryRequest;
}

export interface CreateTreatmentPlanRequest {
  title: string;
  notes?: string | null;
  startDate?: string | null;
  items?: AddPlanItemRequest[] | null;
}

export interface DashboardAppointmentDto {
  id: string;
  startUtc: string;
  endUtc: string;
  patientId: string;
  patientName: string;
  professional: string;
  gabinete?: string | null;
  reason?: string | null;
  status: AppointmentStatus;
}

export interface DashboardLabDto {
  id: string;
  patientName: string;
  workType: string;
  toothCode?: number | null;
  laboratory: string;
  status: LabWorkStatus;
  estimatedDeliveryDate?: string | null;
  isOverdue: boolean;
}

export interface DashboardTaskDto {
  id: string;
  title: string;
  priority: TaskPriority;
  category: TaskCategory;
  dueAtUtc?: string | null;
  isOverdue: boolean;
  assignedTo?: string | null;
}

export type DayOfWeek = "Sunday" | "Monday" | "Tuesday" | "Wednesday" | "Thursday" | "Friday" | "Saturday";

export interface DaySummaryDto {
  date: string;
  appointments: number;
  bookedMinutes: number;
  freeGaps: number;
  averageGapMinutes: number;
  byStatus: {
  [key: string]: number;
};
}

export type Dentition = "Permanent" | "Temporary";

export interface DiagnosisDto {
  id: string;
  description: string;
  code?: string | null;
  toothCode?: number | null;
  surface: ToothSurface;
  diagnosedAtUtc: string;
  professionalUserId: string;
  professionalName: string;
  status: DiagnosisStatus;
  resolvedAtUtc?: string | null;
  clinicalEntryId?: string | null;
  notes?: string | null;
}

export type DiagnosisStatus = "Active" | "Resolved" | "Discarded";

export interface DisableTwoFactorRequest {
  password: string;
}

export type DocumentCategory = "Radiograph" | "Study" | "Prescription" | "ClinicalPhoto" | "ConsentForm" | "Administrative" | "Identification" | "InsuranceAuthorization" | "Other";

export interface DocumentDto {
  id: string;
  patientId: string;
  category: DocumentCategory;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  description?: string | null;
  isClinical: boolean;
  clinicalEntryId?: string | null;
  treatmentPlanId?: string | null;
  uploadedAtUtc: string;
  uploadedBy: string;
}

export interface EnableTwoFactorRequest {
  code: string;
}

export type ExpenseCategory = "Supplies" | "Laboratory" | "Salaries" | "Rent" | "Services" | "Taxes" | "Equipment" | "Maintenance" | "Other";

export interface ExpenseCategoryTotalDto {
  category: ExpenseCategory;
  total: number;
}

export interface ExpenseDto {
  id: string;
  description: string;
  category: ExpenseCategory;
  amount: number;
  method: PaymentMethod;
  paidAtUtc: string;
  supplier?: string | null;
  reference?: string | null;
  notes?: string | null;
  laboratoryId?: string | null;
  laboratoryName?: string | null;
  status: ExpenseStatus;
  registeredBy: string;
  voidedAtUtc?: string | null;
  voidReason?: string | null;
}

export type ExpenseStatus = "Registered" | "Voided";

export interface FinanceSummaryDto {
  fromUtc?: string | null;
  toUtc?: string | null;
  income: number;
  paymentCount: number;
  expenses: number;
  expenseCount: number;
  net: number;
  expensesByCategory: ExpenseCategoryTotalDto[];
}

export interface FindingDto {
  id: string;
  toothCode: number;
  surface: ToothSurface;
  conditionId: string;
  conditionCode: string;
  conditionName: string;
  color?: string | null;
  status: FindingStatus;
  notes?: string | null;
  recordedAtUtc: string;
  recordedByUserId: string;
  recordedBy: string;
  treatmentPlanItemId?: string | null;
}

export type FindingStatus = "Existing" | "Planned";

export interface ForgotPasswordRequest {
  email: string;
}

export interface GabineteDto {
  id: string;
  name: string;
  description?: string | null;
  isActive: boolean;
}

export interface HealthInsuranceDto {
  id: string;
  name: string;
  code?: string | null;
  phone?: string | null;
  notes?: string | null;
  isActive: boolean;
}

export interface InRoomPatientDto {
  appointmentId: string;
  patientId: string;
  patientName: string;
  age?: number | null;
  reason?: string | null;
  gabinete?: string | null;
  lastVisitUtc?: string | null;
  alerts: string[];
}

export interface InvitationDto {
  id: string;
  email: string;
  status: string;
  expiresAtUtc: string;
  isProfessional: boolean;
  roles: string[];
  createdAtUtc: string;
  invitedBy: string;
}

export interface InvitationInfoDto {
  email: string;
  consultorioName: string;
  roles: string[];
  expiresAtUtc: string;
  userExists: boolean;
}

export interface LabBoardDto {
  countsByStatus: {
  [key: string]: number;
};
  overdue: number;
  dueThisWeek: number;
  total: number;
}

export interface LabWorkChangeDto {
  changedAtUtc: string;
  fromStatus: LabWorkStatus;
  toStatus: LabWorkStatus;
  note?: string | null;
  changedBy: string;
}

export interface LabWorkDto {
  id: string;
  patientId: string;
  patientName: string;
  laboratoryId: string;
  laboratoryName: string;
  professionalUserId: string;
  professionalName: string;
  workType: string;
  toothCode?: number | null;
  description?: string | null;
  status: LabWorkStatus;
  sentDate?: string | null;
  estimatedDeliveryDate?: string | null;
  actualDeliveryDate?: string | null;
  placementDate?: string | null;
  cost?: number | null;
  notes?: string | null;
  isOverdue: boolean;
  treatmentPlanItemId?: string | null;
  createdAtUtc: string;
  appointmentId?: string | null;
  appointmentStartUtc?: string | null;
  toothCodes: number[];
}

export type LabWorkStatus = "Pending" | "Sent" | "InProduction" | "ReadyForPickup" | "Received" | "Placed" | "Cancelled";

export interface LaboratoryDto {
  id: string;
  name: string;
  contactName?: string | null;
  phone?: string | null;
  email?: string | null;
  addressLine?: string | null;
  notes?: string | null;
  isActive: boolean;
}

export interface LastEvolutionDto {
  id: string;
  occurredAtUtc: string;
  professional: string;
  title: string;
  description?: string | null;
}

export interface LoginRequest {
  email: string;
  password: string;
  consultorioId?: string | null;
}

export interface LoginResponseDto {
  requiresTwoFactor: boolean;
  twoFactorChallengeToken?: string | null;
  result: SessionResponse;
}

export interface MedicalHistoryDto {
  diabetes: boolean;
  hypertension: boolean;
  heartDisease: boolean;
  anticoagulated: boolean;
  pregnant: boolean;
  asthma: boolean;
  hepatitis: boolean;
  smoker: boolean;
  allergies?: string | null;
  currentMedication?: string | null;
  otherConditions?: string | null;
  updatedAtUtc?: string | null;
  updatedBy?: string | null;
}

export interface MedicalHistoryRequest {
  diabetes: boolean;
  hypertension: boolean;
  heartDisease: boolean;
  anticoagulated: boolean;
  pregnant: boolean;
  asthma: boolean;
  hepatitis: boolean;
  smoker: boolean;
  allergies?: string | null;
  currentMedication?: string | null;
  otherConditions?: string | null;
}

export interface MemberDto {
  memberId: string;
  userId: string;
  email: string;
  firstName: string;
  lastName: string;
  displayName: string;
  isActive: boolean;
  isProfessional: boolean;
  userIsActive: boolean;
  roles: RoleSummaryDto[];
  lastLoginAtUtc?: string | null;
  defaultAppointmentMinutes?: number | null;
  agendaColor?: string | null;
}

export interface MembershipDto {
  consultorioId: string;
  consultorioName: string;
  roles: string[];
  isProfessional: boolean;
}

export type MessageChannel = "Email" | "WhatsApp";

export interface MessageResponse {
  message: string;
}

export interface MonthDayDto {
  date: string;
  appointmentCount: number;
  firstAppointments: MonthDayItemDto[];
  isNonWorkingDay: boolean;
}

export interface MonthDayItemDto {
  id: string;
  startUtc: string;
  patientShortName: string;
  status: AppointmentStatus;
}

export type MovementDirection = "Debit" | "Credit";

export type MovementType = "BudgetCharge" | "Payment" | "PaymentReversal" | "BudgetChargeReversal" | "Adjustment";

export interface NonWorkingDayRequest {
  date: string;
  professionalUserId?: string | null;
  reason?: string | null;
}

export interface NotificationDto {
  id: string;
  type: NotificationType;
  title: string;
  message?: string | null;
  entityType?: string | null;
  entityId?: string | null;
  createdAtUtc: string;
  readAtUtc?: string | null;
}

export interface NotificationSettingsDto {
  emailEnabled: boolean;
  inAppEnabled: boolean;
  notifyTaskAssigned: boolean;
  notifyTaskDue: boolean;
  notifyLabUpdates: boolean;
  notifyAppointmentChanges: boolean;
  dailyAgendaSummary: boolean;
}

export type NotificationType = "General" | "AppointmentReminder" | "AppointmentChanged" | "ConfirmationPending" | "TaskAssigned" | "TaskDue" | "LabWorkUpdate" | "LabWorkOverdue" | "System";

export interface OdontogramCatalogDto {
  teeth: ToothCatalogDto[];
  surfaces: SurfaceCatalogDto[];
  conditions: ConditionDto[];
}

export type OdontogramChangeType = "Added" | "Resolved" | "Removed" | "NoteAdded" | "Completed";

export interface OdontogramDto {
  patientId: string;
  odontogramId?: string | null;
  teeth: ToothStateDto[];
  lastChangeUtc?: string | null;
  lastChangeBy?: string | null;
}

export interface OdontogramHistoryDto {
  id: string;
  changedAtUtc: string;
  toothCode: number;
  surface: ToothSurface;
  conditionName?: string | null;
  changeType: OdontogramChangeType;
  status: FindingStatus;
  notes?: string | null;
  professionalUserId: string;
  professional: string;
}

export interface OutboundMessageDto {
  id: string;
  channel: MessageChannel;
  recipient: string;
  subject?: string | null;
  purpose: string;
  status: OutboundMessageStatus;
  provider?: string | null;
  lastError?: string | null;
  createdAtUtc: string;
  sentAtUtc?: string | null;
  relatedEntityType?: string | null;
  relatedEntityId?: string | null;
}

export type OutboundMessageStatus = "Pending" | "Sent" | "Failed" | "NotSentNoProvider" | "Cancelled";

export interface PagedResultOfAppointmentDto {
  items: AppointmentDto[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasNext: boolean;
}

export interface PagedResultOfAuditLogDto {
  items: AuditLogDto[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasNext: boolean;
}

export interface PagedResultOfBudgetDto {
  items: BudgetDto[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasNext: boolean;
}

export interface PagedResultOfClinicalEntryDto {
  items: ClinicalEntryDto[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasNext: boolean;
}

export interface PagedResultOfExpenseDto {
  items: ExpenseDto[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasNext: boolean;
}

export interface PagedResultOfLabWorkDto {
  items: LabWorkDto[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasNext: boolean;
}

export interface PagedResultOfNotificationDto {
  items: NotificationDto[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasNext: boolean;
}

export interface PagedResultOfOdontogramHistoryDto {
  items: OdontogramHistoryDto[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasNext: boolean;
}

export interface PagedResultOfOutboundMessageDto {
  items: OutboundMessageDto[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasNext: boolean;
}

export interface PagedResultOfPatientListItemDto {
  items: PatientListItemDto[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasNext: boolean;
}

export interface PagedResultOfPaymentDto {
  items: PaymentDto[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasNext: boolean;
}

export interface PagedResultOfProcedureDto {
  items: ProcedureDto[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasNext: boolean;
}

export interface PagedResultOfTaskDto {
  items: TaskDto[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasNext: boolean;
}

export interface PatientAlertSummaryDto {
  patientId: string;
  patientName: string;
  alerts: AlertDto[];
}

export interface PatientDto {
  id: string;
  firstName: string;
  lastName: string;
  documentNumber: string;
  birthDate?: string | null;
  age?: number | null;
  sex: Sex;
  occupation?: string | null;
  phone?: string | null;
  email?: string | null;
  addressLine?: string | null;
  city?: string | null;
  province?: string | null;
  postalCode?: string | null;
  emergencyContactName?: string | null;
  emergencyContactPhone?: string | null;
  emergencyContactRelationship?: string | null;
  healthInsuranceId?: string | null;
  healthInsuranceName?: string | null;
  insurancePlan?: string | null;
  affiliateNumber?: string | null;
  administrativeNotes?: string | null;
  status: PatientStatus;
  createdAtUtc: string;
  alerts: AlertDto[];
}

export interface PatientHistoryItemDto {
  atUtc: string;
  kind: string;
  description: string;
  user?: string | null;
}

export interface PatientListItemDto {
  id: string;
  firstName: string;
  lastName: string;
  documentNumber: string;
  age?: number | null;
  phone?: string | null;
  healthInsurance?: string | null;
  insurancePlan?: string | null;
  status: PatientStatus;
  lastVisitUtc?: string | null;
  nextAppointmentUtc?: string | null;
}

export type PatientStatus = "New" | "Active" | "InTreatment" | "Inactive";

export interface PatientSummaryDto {
  patient: PatientDto;
  upcomingAppointments: UpcomingAppointmentDto[];
  lastVisitUtc?: string | null;
  totalAppointments: number;
  noShows: number;
  account: AccountSummaryDto;
  activeTreatment: TreatmentSummaryDto;
  lastEvolution: LastEvolutionDto;
  openLabWorks: number;
  documents: number;
}

export interface PaymentDto {
  id: string;
  receiptNumber: number;
  patientId: string;
  patientName: string;
  budgetId?: string | null;
  budgetNumber?: number | null;
  amount: number;
  method: PaymentMethod;
  paidAtUtc: string;
  reference?: string | null;
  notes?: string | null;
  status: PaymentStatus;
  receivedBy: string;
  voidedAtUtc?: string | null;
  voidReason?: string | null;
}

export type PaymentMethod = "Cash" | "DebitCard" | "CreditCard" | "BankTransfer" | "DigitalWallet" | "HealthInsurance" | "Check" | "Other";

export type PaymentStatus = "Registered" | "Voided";

export interface PermissionInfoDto {
  key: string;
  group: string;
}

export interface ProblemDetails {
  type?: string | null;
  title?: string | null;
  status?: number | null;
  detail?: string | null;
  instance?: string | null;
  [key: string]: unknown;
}

export interface ProcedureDto {
  id: string;
  code: string;
  name: string;
  description?: string | null;
  price: number;
  requiresTooth: boolean;
  defaultDurationMinutes?: number | null;
  isActive: boolean;
}

export interface ProfessionalDto {
  userId: string;
  displayName: string;
  title?: string | null;
  specialty?: string | null;
  licenseNumber?: string | null;
  defaultAppointmentMinutes: number;
  agendaColor?: string | null;
}

export interface ProfileDto {
  userId: string;
  email: string;
  emailConfirmed: boolean;
  firstName: string;
  lastName: string;
  displayName: string;
  title?: string | null;
  specialty?: string | null;
  licenseNumber?: string | null;
  phone?: string | null;
  twoFactorEnabled: boolean;
  defaultAppointmentMinutes?: number | null;
  notificationSettings: NotificationSettingsDto;
}

export interface ReceptionDashboardDto {
  date: string;
  appointmentsToday: number;
  confirmedToday: number;
  pendingConfirmationToday: number;
  pendingConfirmationNext48h: number;
  inWaitingRoom: number;
  averageWaitMinutes: number;
  waitingRoom: WaitingRoomItemDto[];
  todayAgenda: DashboardAppointmentDto[];
  openTasks: number;
  tasksDueToday: number;
  overdueTasks: number;
  pendingTasks: DashboardTaskDto[];
  upcomingLabDeliveries: DashboardLabDto[];
}

export interface RecordConsentAcceptanceRequest {
  acceptedByName: string;
  acceptedByDocument?: string | null;
  relationship?: string | null;
}

export interface RecoveryCodesDto {
  recoveryCodes: string[];
}

export interface RegisterExpenseRequest {
  description: string;
  category: ExpenseCategory;
  amount: number;
  method: PaymentMethod;
  paidAt?: string | null;
  supplier?: string | null;
  reference?: string | null;
  notes?: string | null;
  laboratoryId?: string | null;
}

export interface RegisterPaymentRequest {
  patientId: string;
  budgetId?: string | null;
  amount: number;
  method: PaymentMethod;
  paidAt?: string | null;
  reference?: string | null;
  notes?: string | null;
}

export interface RegisterRequest {
  email: string;
  password: string;
  firstName: string;
  lastName: string;
  consultorioName: string;
  title?: string | null;
  specialty?: string | null;
  licenseNumber?: string | null;
  timeZoneId?: string | null;
  isProfessional: boolean;
}

export interface RemoveFindingRequest {
  reason: string;
}

export interface RescheduleAppointmentRequest {
  start: string;
  durationMinutes?: number | null;
  professionalUserId?: string | null;
  gabineteId?: string | null;
  isOverbooking: boolean;
  reason?: string | null;
  version: string;
}

export interface ResendConfirmationRequest {
  email: string;
}

export interface ResetPasswordRequest {
  email: string;
  token: string;
  newPassword: string;
}

export interface ResolveFindingRequest {
  reason?: string | null;
}

export interface RevokedSessionsDto {
  revoked: number;
}

export interface RoleDto {
  id: string;
  name: string;
  description?: string | null;
  isSystem: boolean;
  permissions: string[];
}

export interface RoleSummaryDto {
  id: string;
  name: string;
  isSystem: boolean;
}

export interface SaveAlertRequest {
  title: string;
  details?: string | null;
  severity: AlertSeverity;
  visibleToReception: boolean;
}

export interface SaveBudgetRequest {
  treatmentPlanId?: string | null;
  issueDate?: string | null;
  expirationDate?: string | null;
  discountPercent: number;
  notes?: string | null;
  items?: BudgetItemRequest[] | null;
  importPlanItems: boolean;
}

export interface SaveConditionRequest {
  code: string;
  name: string;
  scope: ConditionScope;
  color?: string | null;
  marksToothAbsent: boolean;
  isActive: boolean;
}

export interface SaveGabineteRequest {
  name: string;
  description?: string | null;
  isActive: boolean;
}

export interface SaveHealthInsuranceRequest {
  name: string;
  code?: string | null;
  phone?: string | null;
  notes?: string | null;
  isActive: boolean;
}

export interface SaveLabWorkRequest {
  patientId: string;
  laboratoryId: string;
  professionalUserId?: string | null;
  workType: string;
  toothCode?: number | null;
  description?: string | null;
  estimatedDeliveryDate?: string | null;
  cost?: number | null;
  notes?: string | null;
  treatmentPlanItemId?: string | null;
  appointmentId?: string | null;
  toothCodes?: number[] | null;
}

export interface SaveLaboratoryRequest {
  name: string;
  contactName?: string | null;
  phone?: string | null;
  email?: string | null;
  addressLine?: string | null;
  notes?: string | null;
  isActive: boolean;
}

export interface SavePatientRequest {
  firstName: string;
  lastName: string;
  documentNumber: string;
  birthDate?: string | null;
  sex: Sex;
  occupation?: string | null;
  phone?: string | null;
  email?: string | null;
  addressLine?: string | null;
  city?: string | null;
  province?: string | null;
  postalCode?: string | null;
  emergencyContactName?: string | null;
  emergencyContactPhone?: string | null;
  emergencyContactRelationship?: string | null;
  healthInsuranceId?: string | null;
  insurancePlan?: string | null;
  affiliateNumber?: string | null;
  administrativeNotes?: string | null;
}

export interface SaveProcedureRequest {
  code: string;
  name: string;
  description?: string | null;
  price: number;
  requiresTooth: boolean;
  defaultDurationMinutes?: number | null;
  isActive: boolean;
}

export interface SaveRoleRequest {
  name: string;
  description?: string | null;
  permissions: string[];
}

export interface SaveScheduleBlockRequest {
  kind: ScheduleBlockKind;
  start: string;
  end: string;
  professionalUserId?: string | null;
  gabineteId?: string | null;
  reason?: string | null;
}

export interface SaveTaskRequest {
  title: string;
  description?: string | null;
  priority: TaskPriority;
  category: TaskCategory;
  assignedToUserId?: string | null;
  dueAt?: string | null;
  patientId?: string | null;
  appointmentId?: string | null;
}

export interface ScheduleBlockDto {
  id: string;
  kind: ScheduleBlockKind;
  startUtc: string;
  endUtc: string;
  professionalUserId?: string | null;
  gabineteId?: string | null;
  reason?: string | null;
}

export type ScheduleBlockKind = "Blocked" | "NonWorkingDay" | "ExtraAvailability";

export interface SendRemindersRequest {
  date: string;
  channel: MessageChannel;
}

export interface SendRemindersResultDto {
  appointments: number;
  sent: number;
  notSentNoProvider: number;
  failed: number;
  skippedNoContact: number;
}

export interface SessionInfoDto {
  sessionId: string;
  startedAtUtc: string;
  lastUsedAtUtc?: string | null;
  expiresAtUtc: string;
  ipAddress?: string | null;
  userAgent?: string | null;
  isCurrent: boolean;
}

export interface SessionResponse {
  session: AuthSessionDto;
  csrfToken: string;
}

export interface SetActiveRequest {
  isActive: boolean;
}

export interface SetWorkingHoursRequest {
  slots: WorkingHoursSlotRequest[];
}

export type Sex = "NotSpecified" | "Female" | "Male" | "Other";

export interface SurfaceCatalogDto {
  code: ToothSurface;
  name: string;
  abbreviation: string;
}

export interface SwitchConsultorioRequest {
  consultorioId: string;
}

export type TaskCategory = "General" | "Clinical" | "Laboratory" | "Administrative" | "Supplies" | "Appointments";

export interface TaskDto {
  id: string;
  title: string;
  description?: string | null;
  priority: TaskPriority;
  category: TaskCategory;
  status: TaskItemStatus;
  assignedToUserId?: string | null;
  assignedToName?: string | null;
  dueAtUtc?: string | null;
  isOverdue: boolean;
  patientId?: string | null;
  patientName?: string | null;
  appointmentId?: string | null;
  completedAtUtc?: string | null;
  completedBy?: string | null;
  createdAtUtc: string;
  createdBy?: string | null;
}

export type TaskItemStatus = "Pending" | "InProgress" | "Completed" | "Cancelled";

export type TaskPriority = "Low" | "Medium" | "High";

export interface ToothCatalogDto {
  code: number;
  quadrant: number;
  position: number;
  dentition: Dentition;
  type: ToothType;
  name: string;
  surfaces: ToothSurface[];
}

export interface ToothNoteDto {
  id: string;
  text: string;
  writtenAtUtc: string;
  professional: string;
}

export interface ToothStateDto {
  toothCode: number;
  isAbsent: boolean;
  findings: FindingDto[];
  notes: ToothNoteDto[];
}

export type ToothSurface = "Occlusal" | "Incisal" | "Mesial" | "Distal" | "Vestibular" | "Lingual" | "Palatal";

export type ToothType = "Incisor" | "Canine" | "Premolar" | "Molar";

export interface TreatmentChangeDto {
  changedAtUtc: string;
  itemId?: string | null;
  fromStatus: TreatmentStatus;
  toStatus: TreatmentStatus;
  note?: string | null;
  changedBy: string;
}

export interface TreatmentPlanDto {
  id: string;
  patientId: string;
  title: string;
  notes?: string | null;
  status: TreatmentStatus;
  startDate?: string | null;
  endDate?: string | null;
  professionalUserId: string;
  professionalName: string;
  progressPercent: number;
  completedItems: number;
  totalItems: number;
  estimatedTotal: number;
  items: TreatmentPlanItemDto[];
  createdAtUtc: string;
}

export interface TreatmentPlanItemDto {
  id: string;
  procedureId: string;
  procedureName: string;
  unitPrice: number;
  toothCode?: number | null;
  surfaces: ToothSurface[];
  status: TreatmentStatus;
  startDate?: string | null;
  endDate?: string | null;
  professionalUserId: string;
  professionalName: string;
  diagnosisId?: string | null;
  notes?: string | null;
  sortOrder: number;
}

export type TreatmentStatus = "Pending" | "InProgress" | "Completed" | "Cancelled";

export interface TreatmentSummaryDto {
  planId: string;
  title: string;
  status: TreatmentStatus;
  progressPercent: number;
  completedItems: number;
  totalItems: number;
  startDate?: string | null;
}

export interface TwoFactorLoginRequest {
  challengeToken: string;
  code?: string | null;
  recoveryCode?: string | null;
}

export interface UnreadCountDto {
  unread: number;
}

export interface UpcomingAppointmentDto {
  id: string;
  startUtc: string;
  endUtc: string;
  professional: string;
  reason?: string | null;
  status: AppointmentStatus;
}

export interface UpdateAppointmentRequest {
  patientId: string;
  reason?: string | null;
  administrativeNotes?: string | null;
  version: string;
}

export interface UpdateConsultorioRequest {
  name: string;
  legalName?: string | null;
  taxId?: string | null;
  phone?: string | null;
  email?: string | null;
  website?: string | null;
  addressLine?: string | null;
  city?: string | null;
  province?: string | null;
  postalCode?: string | null;
  country: string;
}

export interface UpdateConsultorioSettingsRequest {
  timeZoneId: string;
  defaultAppointmentMinutes: number;
  slotIntervalMinutes: number;
  reminderHoursBefore: number;
}

export interface UpdateMemberRequest {
  isProfessional: boolean;
  defaultAppointmentMinutes?: number | null;
  agendaColor?: string | null;
}

export interface UpdateMemberRolesRequest {
  roleIds: string[];
}

export interface UpdateProfessionalProfileRequest {
  specialty?: string | null;
  licenseNumber?: string | null;
  defaultAppointmentMinutes?: number | null;
}

export interface UpdateProfileRequest {
  firstName: string;
  lastName: string;
  title?: string | null;
  phone?: string | null;
}

export interface UpdateTreatmentPlanRequest {
  title: string;
  notes?: string | null;
}

export interface UpdatedCountDto {
  updated: number;
}

export interface VoidClinicalEntryRequest {
  reason: string;
}

export interface VoidExpenseRequest {
  reason: string;
}

export interface VoidPaymentRequest {
  reason: string;
}

export interface WaitingRoomItemDto {
  appointmentId: string;
  patientId: string;
  patientName: string;
  professional: string;
  gabinete?: string | null;
  arrivedAtUtc?: string | null;
  waitingMinutes: number;
  status: AppointmentStatus;
}

export interface WeeklyActivityDto {
  date: string;
  attended: number;
}

export interface WorkingHoursDto {
  id: string;
  professionalUserId: string;
  dayOfWeek: DayOfWeek;
  startTime: string;
  endTime: string;
  gabineteId?: string | null;
}

export interface WorkingHoursSlotRequest {
  dayOfWeek: DayOfWeek;
  startTime: string;
  endTime: string;
  gabineteId?: string | null;
}
