import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API, Paginado, parametros } from '../core/api';
import {
  AccountStatementDto, AgendaDto, AppointmentDto, AppointmentStatus, ApplyConditionRequest, BudgetDto, ClinicalDashboardDto,
  ClinicalEntryDto, ClinicalRecordDto, ConsultorioDto, CreateAppointmentRequest, CreateClinicalEntryRequest, CreatePatientRequest,
  GabineteDto, HealthInsuranceDto, LabBoardDto, LabWorkDto, LabWorkStatus, LaboratoryDto, MemberDto, NotificationSettingsDto,
  OdontogramCatalogDto, OdontogramDto, PatientDto, PatientListItemDto, PatientStatus, PatientSummaryDto, PaymentDto,
  ProfessionalDto, ProfileDto, ReceptionDashboardDto, RegisterPaymentRequest, SaveBudgetRequest, SaveLabWorkRequest,
  SavePatientRequest, SaveTaskRequest, TaskDto, TaskItemStatus, ToothNoteDto, UpdateConsultorioRequest, UpdateProfessionalProfileRequest, UpdateProfileRequest
} from './schema';

type Filtro = Record<string, string | number | boolean | null | undefined>;

/** Acciones de turno que el backend expone como POST /appointments/{id}/{accion}. */
export type AccionTurno = 'confirm' | 'mark-pending' | 'check-in' | 'waiting-room' | 'start' | 'attend' | 'no-show';

/** Único punto de acceso a la API REST de Odontia. Los totales, saldos y validaciones de negocio vienen siempre del backend. */
@Injectable({ providedIn: 'root' })
export class OdontiaApi {
  private readonly http = inject(HttpClient);

  // ── Consultorio ────────────────────────────────────────────────────────────
  consultorio(): Observable<ConsultorioDto> { return this.http.get<ConsultorioDto>(`${API}/consultorio`); }
  actualizarConsultorio(solicitud: UpdateConsultorioRequest): Observable<ConsultorioDto> { return this.http.put<ConsultorioDto>(`${API}/consultorio`, solicitud); }
  profesionales(): Observable<ProfessionalDto[]> { return this.http.get<ProfessionalDto[]>(`${API}/consultorio/professionals`); }
  gabinetes(): Observable<GabineteDto[]> { return this.http.get<GabineteDto[]>(`${API}/consultorio/gabinetes`); }
  miembros(): Observable<MemberDto[]> { return this.http.get<MemberDto[]>(`${API}/users`); }
  obrasSociales(): Observable<HealthInsuranceDto[]> { return this.http.get<HealthInsuranceDto[]>(`${API}/health-insurances`); }
  crearObraSocial(name: string): Observable<HealthInsuranceDto> {
    return this.http.post<HealthInsuranceDto>(`${API}/health-insurances`, { name, isActive: true });
  }

  // ── Pacientes ──────────────────────────────────────────────────────────────
  pacientes(filtro: Filtro = {}): Observable<Paginado<PatientListItemDto>> {
    return this.http.get<Paginado<PatientListItemDto>>(`${API}/patients`, { params: parametros({ pageSize: 50, sort: 'name', ...filtro }) });
  }
  paciente(id: string): Observable<PatientDto> { return this.http.get<PatientDto>(`${API}/patients/${id}`); }
  resumenPaciente(id: string): Observable<PatientSummaryDto> { return this.http.get<PatientSummaryDto>(`${API}/patients/${id}/summary`); }
  crearPaciente(solicitud: CreatePatientRequest): Observable<PatientDto> { return this.http.post<PatientDto>(`${API}/patients`, solicitud); }
  actualizarPaciente(id: string, solicitud: SavePatientRequest): Observable<PatientDto> { return this.http.put<PatientDto>(`${API}/patients/${id}`, solicitud); }
  cambiarEstadoPaciente(id: string, status: PatientStatus): Observable<PatientDto> { return this.http.put<PatientDto>(`${API}/patients/${id}/status`, { status }); }

  // ── Agenda y turnos ────────────────────────────────────────────────────────
  /** `desde` y `hasta` son fechas de calendario (yyyy-MM-dd), ambas inclusive. */
  agenda(desde: string, hasta: string, professionalUserId?: string | null): Observable<AgendaDto> {
    return this.http.get<AgendaDto>(`${API}/agenda`, {
      params: parametros({ From: desde, To: hasta, ProfessionalUserId: professionalUserId, IncludeCancelled: true })
    });
  }
  turnos(filtro: Filtro = {}): Observable<Paginado<AppointmentDto>> {
    return this.http.get<Paginado<AppointmentDto>>(`${API}/appointments`, { params: parametros({ pageSize: 100, ...filtro }) });
  }
  turno(id: string): Observable<AppointmentDto> { return this.http.get<AppointmentDto>(`${API}/appointments/${id}`); }
  crearTurno(solicitud: CreateAppointmentRequest): Observable<AppointmentDto> { return this.http.post<AppointmentDto>(`${API}/appointments`, solicitud); }
  accionTurno(id: string, accion: AccionTurno): Observable<AppointmentDto> { return this.http.post<AppointmentDto>(`${API}/appointments/${id}/${accion}`, null); }
  cancelarTurno(id: string, reason: string | null): Observable<AppointmentDto> { return this.http.post<AppointmentDto>(`${API}/appointments/${id}/cancel`, { reason }); }

  // ── Tareas ─────────────────────────────────────────────────────────────────
  tareas(filtro: Filtro = {}): Observable<Paginado<TaskDto>> {
    return this.http.get<Paginado<TaskDto>>(`${API}/tasks`, { params: parametros({ pageSize: 100, ...filtro }) });
  }
  crearTarea(solicitud: SaveTaskRequest): Observable<TaskDto> { return this.http.post<TaskDto>(`${API}/tasks`, solicitud); }
  actualizarTarea(id: string, solicitud: SaveTaskRequest): Observable<TaskDto> { return this.http.put<TaskDto>(`${API}/tasks/${id}`, solicitud); }
  estadoTarea(id: string, status: TaskItemStatus): Observable<TaskDto> { return this.http.put<TaskDto>(`${API}/tasks/${id}/status`, { status }); }

  // ── Historia clínica ───────────────────────────────────────────────────────
  historiaClinica(pacienteId: string): Observable<ClinicalRecordDto> { return this.http.get<ClinicalRecordDto>(`${API}/patients/${pacienteId}/clinical-record`); }
  evoluciones(pacienteId: string): Observable<Paginado<ClinicalEntryDto>> {
    return this.http.get<Paginado<ClinicalEntryDto>>(`${API}/patients/${pacienteId}/clinical-record/entries`, { params: parametros({ pageSize: 100 }) });
  }
  crearEvolucion(pacienteId: string, solicitud: CreateClinicalEntryRequest): Observable<ClinicalEntryDto> {
    return this.http.post<ClinicalEntryDto>(`${API}/patients/${pacienteId}/clinical-record/entries`, solicitud);
  }

  // ── Odontograma ────────────────────────────────────────────────────────────
  catalogoOdontograma(): Observable<OdontogramCatalogDto> { return this.http.get<OdontogramCatalogDto>(`${API}/odontogram/catalog`); }
  odontograma(pacienteId: string): Observable<OdontogramDto> { return this.http.get<OdontogramDto>(`${API}/patients/${pacienteId}/odontogram`); }
  aplicarHallazgos(pacienteId: string, items: ApplyConditionRequest[]): Observable<OdontogramDto> {
    return this.http.post<OdontogramDto>(`${API}/patients/${pacienteId}/odontogram/findings`, { items });
  }
  quitarHallazgo(pacienteId: string, hallazgoId: string, reason: string): Observable<OdontogramDto> {
    return this.http.post<OdontogramDto>(`${API}/patients/${pacienteId}/odontogram/findings/${hallazgoId}/remove`, { reason });
  }
  notaDePieza(pacienteId: string, pieza: number, text: string): Observable<ToothNoteDto> {
    return this.http.post<ToothNoteDto>(`${API}/patients/${pacienteId}/odontogram/teeth/${pieza}/notes`, { text });
  }

  // ── Laboratorio ────────────────────────────────────────────────────────────
  laboratorios(): Observable<LaboratoryDto[]> { return this.http.get<LaboratoryDto[]>(`${API}/laboratories`); }
  crearLaboratorio(name: string): Observable<LaboratoryDto> { return this.http.post<LaboratoryDto>(`${API}/laboratories`, { name, isActive: true }); }
  trabajosDeLaboratorio(filtro: Filtro = {}): Observable<Paginado<LabWorkDto>> {
    return this.http.get<Paginado<LabWorkDto>>(`${API}/lab-works`, { params: parametros({ pageSize: 100, ...filtro }) });
  }
  tableroDeLaboratorio(): Observable<LabBoardDto> { return this.http.get<LabBoardDto>(`${API}/lab-works/board`); }
  crearTrabajoDeLaboratorio(solicitud: SaveLabWorkRequest): Observable<LabWorkDto> { return this.http.post<LabWorkDto>(`${API}/lab-works`, solicitud); }
  estadoTrabajoDeLaboratorio(id: string, status: LabWorkStatus): Observable<LabWorkDto> {
    return this.http.post<LabWorkDto>(`${API}/lab-works/${id}/status`, { status });
  }

  // ── Pagos, presupuestos y cuenta corriente ─────────────────────────────────
  pagos(filtro: Filtro = {}): Observable<Paginado<PaymentDto>> {
    return this.http.get<Paginado<PaymentDto>>(`${API}/payments`, { params: parametros({ pageSize: 100, ...filtro }) });
  }
  registrarPago(solicitud: RegisterPaymentRequest): Observable<PaymentDto> { return this.http.post<PaymentDto>(`${API}/payments`, solicitud); }
  anularPago(id: string, reason: string): Observable<PaymentDto> { return this.http.post<PaymentDto>(`${API}/payments/${id}/void`, { reason }); }
  cuentaCorriente(pacienteId: string): Observable<AccountStatementDto> { return this.http.get<AccountStatementDto>(`${API}/patients/${pacienteId}/account`); }
  presupuestos(filtro: Filtro = {}): Observable<Paginado<BudgetDto>> {
    return this.http.get<Paginado<BudgetDto>>(`${API}/budgets`, { params: parametros({ pageSize: 100, ...filtro }) });
  }
  crearPresupuesto(pacienteId: string, solicitud: SaveBudgetRequest): Observable<BudgetDto> {
    return this.http.post<BudgetDto>(`${API}/patients/${pacienteId}/budgets`, solicitud);
  }
  emitirPresupuesto(id: string): Observable<BudgetDto> { return this.http.post<BudgetDto>(`${API}/budgets/${id}/issue`, null); }
  aprobarPresupuesto(id: string): Observable<BudgetDto> { return this.http.post<BudgetDto>(`${API}/budgets/${id}/approve`, null); }

  // ── Dashboards ─────────────────────────────────────────────────────────────
  dashboardRecepcion(): Observable<ReceptionDashboardDto> { return this.http.get<ReceptionDashboardDto>(`${API}/dashboard/reception`); }
  dashboardClinico(): Observable<ClinicalDashboardDto> { return this.http.get<ClinicalDashboardDto>(`${API}/dashboard/clinical`); }

  // ── Perfil ─────────────────────────────────────────────────────────────────
  perfil(): Observable<ProfileDto> { return this.http.get<ProfileDto>(`${API}/profile`); }
  actualizarPerfil(solicitud: UpdateProfileRequest): Observable<ProfileDto> { return this.http.put<ProfileDto>(`${API}/profile`, solicitud); }
  actualizarPerfilProfesional(solicitud: UpdateProfessionalProfileRequest): Observable<ProfileDto> { return this.http.put<ProfileDto>(`${API}/profile/professional`, solicitud); }
  actualizarNotificaciones(ajustes: NotificationSettingsDto): Observable<NotificationSettingsDto> {
    return this.http.put<NotificationSettingsDto>(`${API}/profile/notification-settings`, ajustes);
  }
  cambiarContrasena(currentPassword: string, newPassword: string): Observable<unknown> {
    return this.http.post(`${API}/auth/change-password`, { currentPassword, newPassword });
  }
}

export const ESTADOS_DE_TURNO: Record<AppointmentStatus, string> = {
  Pending: 'Pendiente', Confirmed: 'Confirmado', Arrived: 'Llegó', InWaitingRoom: 'En sala', InProgress: 'En atención',
  Attended: 'Atendido', Cancelled: 'Cancelado', NoShow: 'Ausente'
};
