# Endpoints de la API Odontia v1

> Generado automáticamente a partir del documento OpenAPI (`docs/openapi.v1.json`).
> Total: **168 operaciones**. Base: `/api/v1`. Errores: ProblemDetails (ver `docs/FRONTEND_INTEGRATION.md`).

Códigos comunes en todas las operaciones autenticadas: `401` (sin sesión o sesión invalidada), `403` (sin permiso / sin consultorio activo), `400` (validación), `404` (no existe **o pertenece a otro consultorio**), `409` (conflicto/concurrencia), `422` (regla de negocio), `429` (rate limit).

## Índice

- [Autenticación y sesiones](#autenticación-y-sesiones)
- [Consultorio](#consultorio)
- [Usuarios, roles e invitaciones](#usuarios-roles-e-invitaciones)
- [Perfil](#perfil)
- [Auditoría](#auditoría)
- [Pacientes](#pacientes)
- [Obras sociales](#obras-sociales)
- [Agenda](#agenda)
- [Turnos](#turnos)
- [Tareas](#tareas)
- [Historia clínica](#historia-clínica)
- [Odontograma · catálogo](#odontograma--catálogo)
- [Odontograma](#odontograma)
- [Prestaciones](#prestaciones)
- [Planes de tratamiento](#planes-de-tratamiento)
- [Presupuestos](#presupuestos)
- [Pagos y cuenta corriente](#pagos-y-cuenta-corriente)
- [Laboratorios](#laboratorios)
- [Trabajos de laboratorio](#trabajos-de-laboratorio)
- [Documentos y consentimientos](#documentos-y-consentimientos)
- [Notificaciones y mensajes](#notificaciones-y-mensajes)
- [Dashboards](#dashboards)

## Autenticación y sesiones

| Método | Ruta | operationId | Descripción | Parámetros | Cuerpo | Respuesta | Permiso |
|---|---|---|---|---|---|---|---|
| POST | `/auth/2fa/disable` | `authDisableTwoFactor` | Desactiva 2FA (requiere contraseña). | — | `DisableTwoFactorRequest` | 204 | Autenticado |
| POST | `/auth/2fa/enable` | `authEnableTwoFactor` | Activa 2FA verificando un código. Devuelve códigos de recuperación (se muestran una única vez). | — | `EnableTwoFactorRequest` | 200 `RecoveryCodesDto` | Autenticado |
| GET | `/auth/2fa/setup` | `authTwoFactorSetup` | Clave y URI otpauth para configurar una app autenticadora. | — | — | 200 `AuthenticatorSetupDto` | Autenticado |
| POST | `/auth/change-password` | `authChangePassword` | Cambio de contraseña del usuario autenticado. Revoca las demás sesiones. | — | `ChangePasswordRequest` | 200 `MessageResponse` | Autenticado |
| POST | `/auth/confirm-email` | `authConfirmEmail` | Confirma el correo electrónico. | — | `ConfirmEmailRequest` | 200 `MessageResponse` | Público |
| POST | `/auth/forgot-password` | `authForgotPassword` | Solicita el correo de recuperación. Responde siempre igual (protección contra enumeración). | — | `ForgotPasswordRequest` | 202 `MessageResponse` | Público |
| GET | `/auth/invitations/{token}` | `authInvitationInfo` | Datos públicos de una invitación (para la pantalla de aceptación). | token (ruta) | — | 200 `InvitationInfoDto` | Público |
| POST | `/auth/invitations/accept` | `authAcceptInvitation` | Acepta una invitación: crea/activa la cuenta y la incorpora al consultorio. | — | `AcceptInvitationRequest` | 200 `MessageResponse` | Público |
| POST | `/auth/login` | `authLogin` | Inicio de sesión. Si el usuario tiene 2FA, devuelve un desafío para completar en /auth/login/2fa. | — | `LoginRequest` | 200 `LoginResponseDto` | Público |
| POST | `/auth/login/2fa` | `authLoginTwoFactor` | Completa el inicio de sesión con código TOTP o código de recuperación. | — | `TwoFactorLoginRequest` | 200 `SessionResponse` | Público |
| POST | `/auth/logout` | `authLogout` | Cierra la sesión actual (revoca la familia de refresh tokens y borra las cookies). | — | — | 204 | Público |
| GET | `/auth/me` | `authMe` | Usuario autenticado, consultorio activo, permisos efectivos y membresías. | — | — | 200 `AuthSessionDto` | Autenticado |
| POST | `/auth/refresh` | `authRefresh` | Renueva la sesión (rotación del refresh token de la cookie). Requiere encabezado X-CSRF-Token. | — | — | 200 `SessionResponse` | Público |
| POST | `/auth/register` | `authRegister` | Alta de un nuevo consultorio y su usuario administrador (si el registro público está habilitado). | — | `RegisterRequest` | 202 `MessageResponse` | Público |
| POST | `/auth/resend-confirmation` | `authResendConfirmation` | Reenvía el correo de confirmación (respuesta idéntica exista o no la cuenta). | — | `ResendConfirmationRequest` | 202 `MessageResponse` | Público |
| POST | `/auth/reset-password` | `authResetPassword` | Restablece la contraseña con el token recibido por correo. Revoca todas las sesiones. | — | `ResetPasswordRequest` | 200 `MessageResponse` | Público |
| GET | `/auth/sessions` | `authSessions` | Sesiones/dispositivos activos del usuario. | — | — | 200 `SessionInfoDto[]` | Autenticado |
| DELETE | `/auth/sessions/{sessionId}` | `authRevokeSession` | Revoca una sesión/dispositivo. | sessionId (ruta) | — | 204 | Autenticado |
| POST | `/auth/sessions/revoke-others` | `authRevokeOthers` | Revoca todas las sesiones excepto la actual. | — | — | 200 `RevokedSessionsDto` | Autenticado |
| POST | `/auth/switch-consultorio` | `authSwitchConsultorio` | Cambia el consultorio activo (emite nuevos tokens para ese consultorio). | — | `SwitchConsultorioRequest` | 200 `SessionResponse` | Autenticado |

## Consultorio

| Método | Ruta | operationId | Descripción | Parámetros | Cuerpo | Respuesta | Permiso |
|---|---|---|---|---|---|---|---|
| GET | `/consultorio` | `consultorioGet` | Datos del consultorio activo. | — | — | 200 `ConsultorioDto` | `consultorio.read` |
| PUT | `/consultorio` | `consultorioUpdate` | Actualiza nombre, datos de contacto y dirección. | — | `UpdateConsultorioRequest` | 200 `ConsultorioDto` | `consultorio.manage` |
| GET | `/consultorio/gabinetes` | `consultorioGabinetes` | Consultorios físicos / gabinetes. | includeInactive | — | 200 `GabineteDto[]` | `consultorio.read` |
| POST | `/consultorio/gabinetes` | `consultorioCreateGabinete` | — | — | `SaveGabineteRequest` | 201 `GabineteDto` | `consultorio.manage` |
| PUT | `/consultorio/gabinetes/{id}` | `consultorioUpdateGabinete` | — | id (ruta) | `SaveGabineteRequest` | 200 `GabineteDto` | `consultorio.manage` |
| GET | `/consultorio/professionals` | `consultorioProfessionals` | Profesionales que atienden (sólo el propio si no tiene acceso a todas las agendas). | — | — | 200 `ProfessionalDto[]` | `agenda.read` |
| PUT | `/consultorio/settings` | `consultorioUpdateSettings` | Configuración general: zona horaria, duración de turno, intervalo de agenda, recordatorios. | — | `UpdateConsultorioSettingsRequest` | 200 `ConsultorioDto` | `consultorio.manage` |

## Usuarios, roles e invitaciones

| Método | Ruta | operationId | Descripción | Parámetros | Cuerpo | Respuesta | Permiso |
|---|---|---|---|---|---|---|---|
| GET | `/invitations` | `usersInvitations` | — | — | — | 200 `InvitationDto[]` | `users.manage` |
| POST | `/invitations` | `usersInvite` | Invita por correo a un usuario con los roles indicados. | — | `CreateInvitationRequest` | 201 `InvitationDto` | `users.manage` |
| DELETE | `/invitations/{id}` | `usersRevokeInvitation` | — | id (ruta) | — | 204 | `users.manage` |
| GET | `/permissions` | `usersPermissions` | Catálogo de permisos granulares. | — | — | 200 `PermissionInfoDto[]` | `users.read` |
| GET | `/roles` | `usersRoles` | Roles disponibles (de sistema y personalizados del consultorio). | — | — | 200 `RoleDto[]` | `users.read` |
| POST | `/roles` | `usersCreateRole` | — | — | `SaveRoleRequest` | 201 `RoleDto` | `roles.manage` |
| PUT | `/roles/{roleId}` | `usersUpdateRole` | — | roleId (ruta) | `SaveRoleRequest` | 200 `RoleDto` | `roles.manage` |
| DELETE | `/roles/{roleId}` | `usersDeleteRole` | — | roleId (ruta) | — | 204 | `roles.manage` |
| GET | `/users` | `usersList` | Usuarios (miembros) del consultorio. | includeInactive | — | 200 `MemberDto[]` | `users.read` |
| GET | `/users/{memberId}` | `usersGet` | — | memberId (ruta) | — | 200 `MemberDto` | `users.read` |
| PUT | `/users/{memberId}` | `usersUpdate` | Marca como profesional y preferencias de agenda. | memberId (ruta) | `UpdateMemberRequest` | 200 `MemberDto` | `users.manage` |
| PUT | `/users/{memberId}/active` | `usersSetActive` | Activa o desactiva un usuario en el consultorio (desactivar revoca sus sesiones). | memberId (ruta) | `SetActiveRequest` | 200 `MemberDto` | `users.manage` |
| PUT | `/users/{memberId}/roles` | `usersUpdateRoles` | Asigna roles. No se pueden otorgar permisos que el actor no posee ni modificar los propios. | memberId (ruta) | `UpdateMemberRolesRequest` | 200 `MemberDto` | `users.manage` |

## Perfil

| Método | Ruta | operationId | Descripción | Parámetros | Cuerpo | Respuesta | Permiso |
|---|---|---|---|---|---|---|---|
| GET | `/profile` | `profileGet` | — | — | — | 200 `ProfileDto` | Autenticado |
| PUT | `/profile` | `profileUpdate` | Datos personales. | — | `UpdateProfileRequest` | 200 `ProfileDto` | Autenticado |
| PUT | `/profile/email` | `profileChangeEmail` | Cambio de correo (requiere contraseña y nueva confirmación). | — | `ChangeEmailRequest` | 200 `MessageResponse` | Autenticado |
| PUT | `/profile/notification-settings` | `profileUpdateNotifications` | — | — | `NotificationSettingsDto` | 200 `NotificationSettingsDto` | Autenticado |
| PUT | `/profile/professional` | `profileUpdateProfessional` | Especialidad, matrícula y duración de turno por defecto. | — | `UpdateProfessionalProfileRequest` | 200 `ProfileDto` | Autenticado |

## Auditoría

| Método | Ruta | operationId | Descripción | Parámetros | Cuerpo | Respuesta | Permiso |
|---|---|---|---|---|---|---|---|
| GET | `/audit` | `auditSearch` | — | EntityType, EntityId, UserId, FromUtc, ToUtc, Page, PageSize, Sort | — | 200 `PagedResultOfAuditLogDto` | `audit.read` |

## Pacientes

| Método | Ruta | operationId | Descripción | Parámetros | Cuerpo | Respuesta | Permiso |
|---|---|---|---|---|---|---|---|
| GET | `/patients` | `patientsSearch` | Búsqueda paginada por nombre, apellido, DNI o teléfono, con filtros. Orden: name, document, created, status, birthdate. | Search, Status, HealthInsuranceId, Private, Page, PageSize, Sort | — | 200 `PagedResultOfPatientListItemDto` | `patients.read` |
| POST | `/patients` | `patientsCreate` | Alta de paciente (DNI único por consultorio). Antecedentes opcionales: requieren patients.medical.write. | — | `CreatePatientRequest` | 201 `PatientDto` | `patients.write` |
| GET | `/patients/{id}` | `patientsGet` | — | id (ruta) | — | 200 `PatientDto` | `patients.read` |
| PUT | `/patients/{id}` | `patientsUpdate` | — | id (ruta) | `SavePatientRequest` | 200 `PatientDto` | `patients.write` |
| GET | `/patients/{id}/alerts` | `patientsAlerts` | Alertas médicas relevantes. Recepción ve sólo títulos de alertas necesarias para la atención. | id (ruta) | — | 200 `AlertDto[]` | `patients.alerts.read` |
| POST | `/patients/{id}/alerts` | `patientsAddAlert` | — | id (ruta) | `SaveAlertRequest` | 201 `AlertDto` | `patients.medical.write` |
| PUT | `/patients/{id}/alerts/{alertId}` | `patientsUpdateAlert` | — | id (ruta), alertId (ruta) | `SaveAlertRequest` | 200 `AlertDto` | `patients.medical.write` |
| DELETE | `/patients/{id}/alerts/{alertId}` | `patientsDeactivateAlert` | — | id (ruta), alertId (ruta) | — | 204 | `patients.medical.write` |
| GET | `/patients/{id}/history` | `patientsHistory` | Historial administrativo (turnos, pagos, presupuestos, documentos no clínicos). | id (ruta) | — | 200 `PatientHistoryItemDto[]` | `patients.read` |
| GET | `/patients/{id}/medical-history` | `patientsMedicalHistory` | Antecedentes médicos completos (información clínica restringida). | id (ruta) | — | 200 `MedicalHistoryDto` | `patients.medical.read` |
| PUT | `/patients/{id}/medical-history` | `patientsUpdateMedicalHistory` | — | id (ruta) | `MedicalHistoryRequest` | 200 `MedicalHistoryDto` | `patients.medical.write` |
| PUT | `/patients/{id}/status` | `patientsChangeStatus` | — | id (ruta) | `ChangePatientStatusRequest` | 200 `PatientDto` | `patients.write` |
| GET | `/patients/{id}/summary` | `patientsSummary` | Resumen de la ficha (secciones según permisos: cuenta, tratamiento, última evolución, laboratorio). | id (ruta) | — | 200 `PatientSummaryDto` | `patients.read` |

## Obras sociales

| Método | Ruta | operationId | Descripción | Parámetros | Cuerpo | Respuesta | Permiso |
|---|---|---|---|---|---|---|---|
| GET | `/health-insurances` | `healthInsurancesList` | — | includeInactive | — | 200 `HealthInsuranceDto[]` | `patients.read` |
| POST | `/health-insurances` | `healthInsurancesCreate` | — | — | `SaveHealthInsuranceRequest` | 201 `HealthInsuranceDto` | `insurances.manage` |
| PUT | `/health-insurances/{id}` | `healthInsurancesUpdate` | — | id (ruta) | `SaveHealthInsuranceRequest` | 200 `HealthInsuranceDto` | `insurances.manage` |

## Agenda

| Método | Ruta | operationId | Descripción | Parámetros | Cuerpo | Respuesta | Permiso |
|---|---|---|---|---|---|---|---|
| GET | `/agenda` | `agendaGet` | Agenda de un rango de fechas locales (día o semana; máx. 42 días), por profesional y/o consultorio físico. | From, To, ProfessionalUserId, GabineteId, IncludeCancelled | — | 200 `AgendaDto` | `agenda.read` |
| GET | `/agenda/availability` | `agendaAvailability` | Horarios disponibles para reservar (la disponibilidad se revalida al confirmar el turno). | Date, ProfessionalUserId, GabineteId, DurationMinutes | — | 200 `AvailabilityDto` | `agenda.read` |
| GET | `/agenda/blocks` | `agendaBlocks` | — | from, to, professionalUserId | — | 200 `ScheduleBlockDto[]` | `agenda.read` |
| POST | `/agenda/blocks` | `agendaCreateBlock` | Crea un bloqueo, día no laborable o excepción de disponibilidad. | — | `SaveScheduleBlockRequest` | 201 `ScheduleBlockDto` | `agenda.manage` |
| PUT | `/agenda/blocks/{id}` | `agendaUpdateBlock` | — | id (ruta) | `SaveScheduleBlockRequest` | 200 `ScheduleBlockDto` | `agenda.manage` |
| DELETE | `/agenda/blocks/{id}` | `agendaDeleteBlock` | — | id (ruta) | — | 204 | `agenda.manage` |
| POST | `/agenda/blocks/non-working-day` | `agendaNonWorkingDay` | Marca un día completo como no laborable (consultorio o profesional). | — | `NonWorkingDayRequest` | 201 `ScheduleBlockDto` | `agenda.manage` |
| GET | `/agenda/day-summary` | `agendaDaySummary` | Resumen del día: turnos, minutos ocupados y huecos libres. | date, professionalUserId | — | 200 `DaySummaryDto` | `agenda.read` |
| GET | `/agenda/month` | `agendaMonth` | Vista mensual: cantidad de turnos por día y primeros turnos. | year, month, professionalUserId, gabineteId | — | 200 `AgendaMonthDto` | `agenda.read` |
| GET | `/agenda/professionals/{professionalUserId}/working-hours` | `agendaWorkingHours` | — | professionalUserId (ruta) | — | 200 `WorkingHoursDto[]` | `agenda.read` |
| PUT | `/agenda/professionals/{professionalUserId}/working-hours` | `agendaSetWorkingHours` | Reemplaza el horario laboral semanal del profesional. | professionalUserId (ruta) | `SetWorkingHoursRequest` | 200 `WorkingHoursDto[]` | `agenda.manage` |

## Turnos

| Método | Ruta | operationId | Descripción | Parámetros | Cuerpo | Respuesta | Permiso |
|---|---|---|---|---|---|---|---|
| GET | `/appointments` | `appointmentsSearch` | Listado paginado con filtros (rango, estado, profesional, paciente, búsqueda). Orden: start, status, patient, created. | From, To, Status, ProfessionalUserId, PatientId, GabineteId, Search, Page, PageSize, Sort | — | 200 `PagedResultOfAppointmentDto` | `appointments.read` |
| POST | `/appointments` | `appointmentsCreate` | Crea un turno validando disponibilidad y superposición de forma atómica. | — | `CreateAppointmentRequest` | 201 `AppointmentDto` | `appointments.write` |
| GET | `/appointments/{id}` | `appointmentsGet` | — | id (ruta) | — | 200 `AppointmentDto` | `appointments.read` |
| PUT | `/appointments/{id}` | `appointmentsUpdate` | Modifica paciente, motivo y observaciones (requiere la versión vigente para evitar pisar cambios). | id (ruta) | `UpdateAppointmentRequest` | 200 `AppointmentDto` | `appointments.write` |
| POST | `/appointments/{id}/attend` | `appointmentsAttend` | — | id (ruta) | — | 200 `AppointmentDto` | `appointments.write` |
| POST | `/appointments/{id}/cancel` | `appointmentsCancel` | — | id (ruta) | `CancelAppointmentRequest` | 200 `AppointmentDto` | `appointments.write` |
| POST | `/appointments/{id}/check-in` | `appointmentsCheckIn` | Registra la llegada del paciente. | id (ruta) | — | 200 `AppointmentDto` | `appointments.write` |
| POST | `/appointments/{id}/confirm` | `appointmentsConfirm` | — | id (ruta) | — | 200 `AppointmentDto` | `appointments.write` |
| GET | `/appointments/{id}/history` | `appointmentsHistory` | — | id (ruta) | — | 200 `AppointmentHistoryDto[]` | `appointments.read` |
| POST | `/appointments/{id}/mark-pending` | `appointmentsMarkPending` | — | id (ruta) | — | 200 `AppointmentDto` | `appointments.write` |
| POST | `/appointments/{id}/no-show` | `appointmentsNoShow` | Registra ausencia (sólo luego del horario del turno). | id (ruta) | — | 200 `AppointmentDto` | `appointments.write` |
| POST | `/appointments/{id}/reminder` | `appointmentsReminder` | Envía un recordatorio. El estado devuelto refleja el resultado real del proveedor (NotSentNoProvider si no hay proveedor). | id (ruta), channel | — | 200 `OutboundMessageDto` | `messaging.send` |
| POST | `/appointments/{id}/reschedule` | `appointmentsReschedule` | Reprograma (horario, profesional, consultorio físico). El turno vuelve a "pendiente". | id (ruta) | `RescheduleAppointmentRequest` | 200 `AppointmentDto` | `appointments.write` |
| POST | `/appointments/{id}/start` | `appointmentsStart` | Inicia la consulta. | id (ruta) | — | 200 `AppointmentDto` | `appointments.write` |
| POST | `/appointments/{id}/status` | `appointmentsChangeStatus` | Cambio de estado genérico (máquina de estados controlada). | id (ruta) | `ChangeAppointmentStatusRequest` | 200 `AppointmentDto` | `appointments.write` |
| POST | `/appointments/{id}/waiting-room` | `appointmentsWaitingRoom` | — | id (ruta) | — | 200 `AppointmentDto` | `appointments.write` |
| GET | `/appointments/status-counts` | `appointmentsStatusCounts` | Conteo por estado en un rango (pestañas de "Gestión de turnos"). | from, to, professionalUserId | — | 200 `AppointmentStatusCountsDto` | `appointments.read` |

## Tareas

| Método | Ruta | operationId | Descripción | Parámetros | Cuerpo | Respuesta | Permiso |
|---|---|---|---|---|---|---|---|
| GET | `/tasks` | `tasksSearch` | Listado con filtros. view: open | overdue | today | upcoming | completed. Orden: due, priority, created, status. | Status, View, AssignedToUserId, Mine, Priority, Category, PatientId, Page, PageSize, Sort | — | 200 `PagedResultOfTaskDto` | `tasks.read` |
| POST | `/tasks` | `tasksCreate` | — | — | `SaveTaskRequest` | 201 `TaskDto` | `tasks.write` |
| GET | `/tasks/{id}` | `tasksGet` | — | id (ruta) | — | 200 `TaskDto` | `tasks.read` |
| PUT | `/tasks/{id}` | `tasksUpdate` | — | id (ruta) | `SaveTaskRequest` | 200 `TaskDto` | `tasks.write` |
| PUT | `/tasks/{id}/assignee` | `tasksAssign` | Asigna o reasigna el responsable. | id (ruta) | `AssignTaskRequest` | 200 `TaskDto` | `tasks.write` |
| PUT | `/tasks/{id}/status` | `tasksChangeStatus` | Cambia el estado (p. ej. Completed). | id (ruta) | `ChangeTaskStatusRequest` | 200 `TaskDto` | `tasks.write` |

## Historia clínica

| Método | Ruta | operationId | Descripción | Parámetros | Cuerpo | Respuesta | Permiso |
|---|---|---|---|---|---|---|---|
| GET | `/patients/{patientId}/clinical-record` | `clinicalRecordGet` | Encabezado de la historia clínica con antecedentes y alertas. | patientId (ruta) | — | 200 `ClinicalRecordDto` | `clinical.read` |
| GET | `/patients/{patientId}/clinical-record/diagnoses` | `clinicalRecordDiagnoses` | — | patientId (ruta), status | — | 200 `DiagnosisDto[]` | `clinical.read` |
| POST | `/patients/{patientId}/clinical-record/diagnoses` | `clinicalRecordCreateDiagnosis` | — | patientId (ruta) | `CreateDiagnosisRequest` | 201 `DiagnosisDto` | `clinical.write` |
| PUT | `/patients/{patientId}/clinical-record/diagnoses/{diagnosisId}/status` | `clinicalRecordDiagnosisStatus` | — | patientId (ruta), diagnosisId (ruta) | `ChangeDiagnosisStatusRequest` | 200 `DiagnosisDto` | `clinical.write` |
| GET | `/patients/{patientId}/clinical-record/entries` | `clinicalRecordEntries` | Evolución cronológica (más recientes primero). Filtros: type, toothCode, includeVoided. | patientId (ruta), Type, ToothCode, IncludeVoided, Page, PageSize, Sort | — | 200 `PagedResultOfClinicalEntryDto` | `clinical.read` |
| POST | `/patients/{patientId}/clinical-record/entries` | `clinicalRecordCreateEntry` | Registra una evolución, consulta, observación, procedimiento, receta o estudio. El responsable es el profesional autenticado. | patientId (ruta) | `CreateClinicalEntryRequest` | 201 `ClinicalEntryDto` | `clinical.write` |
| GET | `/patients/{patientId}/clinical-record/entries/{entryId}` | `clinicalRecordGetEntry` | — | patientId (ruta), entryId (ruta) | — | 200 `ClinicalEntryDto` | `clinical.read` |
| POST | `/patients/{patientId}/clinical-record/entries/{entryId}/amendments` | `clinicalRecordAmend` | Corrige un registro (sólo el autor, con motivo). La versión previa se conserva y se audita. | patientId (ruta), entryId (ruta) | `AmendClinicalEntryRequest` | 200 `ClinicalEntryDto` | `clinical.write` |
| GET | `/patients/{patientId}/clinical-record/entries/{entryId}/revisions` | `clinicalRecordRevisions` | Versiones anteriores del registro. | patientId (ruta), entryId (ruta) | — | 200 `ClinicalEntryRevisionDto[]` | `clinical.read` |
| POST | `/patients/{patientId}/clinical-record/entries/{entryId}/void` | `clinicalRecordVoid` | Anula un registro con motivo (no se elimina). | patientId (ruta), entryId (ruta) | `VoidClinicalEntryRequest` | 200 `ClinicalEntryDto` | `clinical.write` |

## Odontograma · catálogo

| Método | Ruta | operationId | Descripción | Parámetros | Cuerpo | Respuesta | Permiso |
|---|---|---|---|---|---|---|---|
| GET | `/odontogram/catalog` | `odontogramCatalogCatalog` | Piezas FDI (permanentes y temporales) con superficies válidas, superficies y condiciones. | — | — | 200 `OdontogramCatalogDto` | `odontogram.read` |
| POST | `/odontogram/conditions` | `odontogramCatalogCreateCondition` | — | — | `SaveConditionRequest` | 201 `ConditionDto` | `catalog.manage` |
| PUT | `/odontogram/conditions/{id}` | `odontogramCatalogUpdateCondition` | — | id (ruta) | `SaveConditionRequest` | 200 `ConditionDto` | `catalog.manage` |

## Odontograma

| Método | Ruta | operationId | Descripción | Parámetros | Cuerpo | Respuesta | Permiso |
|---|---|---|---|---|---|---|---|
| GET | `/patients/{patientId}/odontogram` | `odontogramGet` | — | patientId (ruta) | — | 200 `OdontogramDto` | `odontogram.read` |
| POST | `/patients/{patientId}/odontogram/findings` | `odontogramApply` | Aplica una o varias condiciones (pieza o superficie; existente o a realizar) en una operación atómica. | patientId (ruta) | `ApplyConditionsRequest` | 200 `OdontogramDto` | `odontogram.write` |
| POST | `/patients/{patientId}/odontogram/findings/{findingId}/remove` | `odontogramRemove` | Retira un hallazgo cargado por error (queda en el historial con motivo). | patientId (ruta), findingId (ruta) | `RemoveFindingRequest` | 200 `OdontogramDto` | `odontogram.write` |
| POST | `/patients/{patientId}/odontogram/findings/{findingId}/resolve` | `odontogramResolve` | Marca un hallazgo como resuelto. | patientId (ruta), findingId (ruta) | `ResolveFindingRequest` | 200 `OdontogramDto` | `odontogram.write` |
| GET | `/patients/{patientId}/odontogram/history` | `odontogramHistory` | Historial de modificaciones (filtrable por pieza) con profesional y fecha. | patientId (ruta), ToothCode, Page, PageSize, Sort | — | 200 `PagedResultOfOdontogramHistoryDto` | `odontogram.read` |
| POST | `/patients/{patientId}/odontogram/teeth/{toothCode}/notes` | `odontogramAddNote` | — | patientId (ruta), toothCode (ruta) | `AddToothNoteRequest` | 201 `ToothNoteDto` | `odontogram.write` |

## Prestaciones

| Método | Ruta | operationId | Descripción | Parámetros | Cuerpo | Respuesta | Permiso |
|---|---|---|---|---|---|---|---|
| GET | `/procedures` | `proceduresList` | Requiere treatments.read, budgets.read o catalog.manage. | Search, IncludeInactive, Page, PageSize, Sort | — | 200 `PagedResultOfProcedureDto` | Autenticado |
| POST | `/procedures` | `proceduresCreate` | — | — | `SaveProcedureRequest` | 201 `ProcedureDto` | `catalog.manage` |
| PUT | `/procedures/{id}` | `proceduresUpdate` | Actualiza la prestación. Los planes/presupuestos existentes conservan su valor histórico. | id (ruta) | `SaveProcedureRequest` | 200 `ProcedureDto` | `catalog.manage` |

## Planes de tratamiento

| Método | Ruta | operationId | Descripción | Parámetros | Cuerpo | Respuesta | Permiso |
|---|---|---|---|---|---|---|---|
| GET | `/patients/{patientId}/treatment-plans` | `treatmentPlansList` | — | patientId (ruta) | — | 200 `TreatmentPlanDto[]` | `treatments.read` |
| POST | `/patients/{patientId}/treatment-plans` | `treatmentPlansCreate` | — | patientId (ruta) | `CreateTreatmentPlanRequest` | 201 `TreatmentPlanDto` | `treatments.write` |
| GET | `/patients/{patientId}/treatment-plans/{planId}` | `treatmentPlansGet` | — | patientId (ruta), planId (ruta) | — | 200 `TreatmentPlanDto` | `treatments.read` |
| PUT | `/patients/{patientId}/treatment-plans/{planId}` | `treatmentPlansUpdate` | — | patientId (ruta), planId (ruta) | `UpdateTreatmentPlanRequest` | 200 `TreatmentPlanDto` | `treatments.write` |
| POST | `/patients/{patientId}/treatment-plans/{planId}/cancel` | `treatmentPlansCancel` | — | patientId (ruta), planId (ruta) | `CancelPlanRequest` | 200 `TreatmentPlanDto` | `treatments.write` |
| GET | `/patients/{patientId}/treatment-plans/{planId}/history` | `treatmentPlansHistory` | — | patientId (ruta), planId (ruta) | — | 200 `TreatmentChangeDto[]` | `treatments.read` |
| POST | `/patients/{patientId}/treatment-plans/{planId}/items` | `treatmentPlansAddItem` | Agrega una prestación (valor tomado del catálogo vigente). | patientId (ruta), planId (ruta) | `AddPlanItemRequest` | 200 `TreatmentPlanDto` | `treatments.write` |
| PUT | `/patients/{patientId}/treatment-plans/{planId}/items/{itemId}/status` | `treatmentPlansItemStatus` | Cambia el estado de una prestación (Pending, InProgress, Completed, Cancelled); el plan se recalcula. | patientId (ruta), planId (ruta), itemId (ruta) | `ChangeItemStatusRequest` | 200 `TreatmentPlanDto` | `treatments.write` |

## Presupuestos

| Método | Ruta | operationId | Descripción | Parámetros | Cuerpo | Respuesta | Permiso |
|---|---|---|---|---|---|---|---|
| GET | `/budgets` | `budgetsSearch` | — | Status, PatientId, Page, PageSize, Sort | — | 200 `PagedResultOfBudgetDto` | `budgets.read` |
| GET | `/budgets/{id}` | `budgetsGet` | — | id (ruta) | — | 200 `BudgetDto` | `budgets.read` |
| PUT | `/budgets/{id}` | `budgetsUpdate` | Modifica un presupuesto en borrador (reemplaza las líneas). | id (ruta) | `SaveBudgetRequest` | 200 `BudgetDto` | `budgets.write` |
| POST | `/budgets/{id}/approve` | `budgetsApprove` | — | id (ruta) | — | 200 `BudgetDto` | `budgets.approve` |
| POST | `/budgets/{id}/cancel` | `budgetsCancel` | — | id (ruta) | `BudgetDecisionRequest` | 200 `BudgetDto` | `budgets.approve` |
| GET | `/budgets/{id}/history` | `budgetsHistory` | — | id (ruta) | — | 200 `BudgetStatusChangeDto[]` | `budgets.read` |
| POST | `/budgets/{id}/issue` | `budgetsIssue` | — | id (ruta) | — | 200 `BudgetDto` | `budgets.write` |
| POST | `/budgets/{id}/reject` | `budgetsReject` | — | id (ruta) | `BudgetDecisionRequest` | 200 `BudgetDto` | `budgets.approve` |
| POST | `/patients/{patientId}/budgets` | `budgetsCreate` | Crea un presupuesto en borrador (líneas manuales o importadas del plan de tratamiento). | patientId (ruta) | `SaveBudgetRequest` | 201 `BudgetDto` | `budgets.write` |

## Pagos y cuenta corriente

| Método | Ruta | operationId | Descripción | Parámetros | Cuerpo | Respuesta | Permiso |
|---|---|---|---|---|---|---|---|
| GET | `/patients/{patientId}/account` | `paymentsAccount` | Cuenta corriente del paciente con movimientos, saldo acumulado y saldos por presupuesto. | patientId (ruta) | — | 200 `AccountStatementDto` | `finance.read` |
| GET | `/payments` | `paymentsSearch` | — | PatientId, From, To, Method, IncludeVoided, Page, PageSize, Sort | — | 200 `PagedResultOfPaymentDto` | `finance.read` |
| POST | `/payments` | `paymentsRegister` | Registra un pago (opcionalmente imputado a un presupuesto aprobado, sin exceder su saldo). | — | `RegisterPaymentRequest` | 201 `PaymentDto` | `payments.write` |
| GET | `/payments/{id}` | `paymentsGet` | — | id (ruta) | — | 200 `PaymentDto` | `finance.read` |
| POST | `/payments/{id}/void` | `paymentsVoid` | Anulación controlada con motivo: genera contraasiento; el pago no se elimina. | id (ruta) | `VoidPaymentRequest` | 200 `PaymentDto` | `payments.void` |

## Laboratorios

| Método | Ruta | operationId | Descripción | Parámetros | Cuerpo | Respuesta | Permiso |
|---|---|---|---|---|---|---|---|
| GET | `/laboratories` | `laboratoriesList` | Requiere lab.read o laboratories.manage. | includeInactive | — | 200 `LaboratoryDto[]` | Autenticado |
| POST | `/laboratories` | `laboratoriesCreate` | — | — | `SaveLaboratoryRequest` | 201 `LaboratoryDto` | `laboratories.manage` |
| PUT | `/laboratories/{id}` | `laboratoriesUpdate` | — | id (ruta) | `SaveLaboratoryRequest` | 200 `LaboratoryDto` | `laboratories.manage` |

## Trabajos de laboratorio

| Método | Ruta | operationId | Descripción | Parámetros | Cuerpo | Respuesta | Permiso |
|---|---|---|---|---|---|---|---|
| GET | `/lab-works` | `labWorksSearch` | Listado. view: open | overdue | this_week | closed. | Status, LaboratoryId, PatientId, View, Page, PageSize, Sort | — | 200 `PagedResultOfLabWorkDto` | `lab.read` |
| POST | `/lab-works` | `labWorksCreate` | — | — | `SaveLabWorkRequest` | 201 `LabWorkDto` | `lab.write` |
| GET | `/lab-works/{id}` | `labWorksGet` | — | id (ruta) | — | 200 `LabWorkDto` | `lab.read` |
| PUT | `/lab-works/{id}` | `labWorksUpdate` | — | id (ruta) | `SaveLabWorkRequest` | 200 `LabWorkDto` | `lab.write` |
| GET | `/lab-works/{id}/history` | `labWorksHistory` | — | id (ruta) | — | 200 `LabWorkChangeDto[]` | `lab.read` |
| POST | `/lab-works/{id}/status` | `labWorksChangeStatus` | Avance logístico (lab.track o lab.write). Colocado/Cancelado requieren lab.write. | id (ruta) | `ChangeLabWorkStatusRequest` | 200 `LabWorkDto` | Autenticado |
| GET | `/lab-works/board` | `labWorksBoard` | Tablero: conteos por estado, atrasados y entregas de la semana. | — | — | 200 `LabBoardDto` | `lab.read` |

## Documentos y consentimientos

| Método | Ruta | operationId | Descripción | Parámetros | Cuerpo | Respuesta | Permiso |
|---|---|---|---|---|---|---|---|
| DELETE | `/documents/{id}` | `documentsDelete` | Baja lógica con motivo (el archivo se conserva según la política de retención). | id (ruta), reason | — | 204 | `documents.delete` |
| GET | `/documents/{id}/download` | `documentsDownload` | Descarga autorizada (se audita). Nunca se expone una URL pública. | id (ruta) | — | 200 | `documents.read` |
| GET | `/patients/{patientId}/consents` | `documentsConsents` | — | patientId (ruta) | — | 200 `ConsentDto[]` | `consents.read` |
| POST | `/patients/{patientId}/consents` | `documentsCreateConsent` | — | patientId (ruta) | `CreateConsentRequest` | 201 `ConsentDto` | `consents.write` |
| POST | `/patients/{patientId}/consents/{consentId}/accept` | `documentsAccept` | Registra la aceptación en el sistema (constancia interna; NO es una firma digital certificada). | patientId (ruta), consentId (ruta) | `RecordConsentAcceptanceRequest` | 200 `ConsentDto` | `consents.write` |
| POST | `/patients/{patientId}/consents/{consentId}/attach-signed` | `documentsAttachSigned` | Vincula el consentimiento firmado en papel (documento previamente cargado con categoría ConsentForm). | patientId (ruta), consentId (ruta) | `AttachSignedConsentRequest` | 200 `ConsentDto` | `consents.write` |
| POST | `/patients/{patientId}/consents/{consentId}/reject` | `documentsReject` | — | patientId (ruta), consentId (ruta) | `ConsentDecisionRequest` | 200 `ConsentDto` | `consents.write` |
| POST | `/patients/{patientId}/consents/{consentId}/revoke` | `documentsRevoke` | — | patientId (ruta), consentId (ruta) | `ConsentDecisionRequest` | 200 `ConsentDto` | `consents.write` |
| GET | `/patients/{patientId}/documents` | `documentsList` | Documentos del paciente. Los clínicos sólo se listan con documents.clinical.read. | patientId (ruta), category | — | 200 `DocumentDto[]` | `documents.read` |
| POST | `/patients/{patientId}/documents` | `documentsUpload` | Carga un archivo (PDF, JPG, PNG, DICOM). Se valida extensión, firma binaria real y tamaño máximo. | patientId (ruta) | multipart/form-data | 201 `DocumentDto` | `documents.write` |

## Notificaciones y mensajes

| Método | Ruta | operationId | Descripción | Parámetros | Cuerpo | Respuesta | Permiso |
|---|---|---|---|---|---|---|---|
| GET | `/messages` | `notificationsOutbound` | Registro de mensajes salientes y su estado real (Sent sólo con confirmación del proveedor). | Status, Channel, Page, PageSize, Sort | — | 200 `PagedResultOfOutboundMessageDto` | `messaging.send` |
| POST | `/messages/reminders` | `notificationsSendReminders` | Envía recordatorios para los turnos pendientes/confirmados de una fecha. | — | `SendRemindersRequest` | 200 `SendRemindersResultDto` | `messaging.send` |
| GET | `/notifications` | `notificationsMine` | — | UnreadOnly, Page, PageSize, Sort | — | 200 `PagedResultOfNotificationDto` | Autenticado |
| POST | `/notifications/{id}/read` | `notificationsMarkRead` | — | id (ruta) | — | 204 | Autenticado |
| POST | `/notifications/read-all` | `notificationsMarkAllRead` | — | — | — | 200 `UpdatedCountDto` | Autenticado |
| GET | `/notifications/unread-count` | `notificationsUnread` | — | — | — | 200 `UnreadCountDto` | Autenticado |

## Dashboards

| Método | Ruta | operationId | Descripción | Parámetros | Cuerpo | Respuesta | Permiso |
|---|---|---|---|---|---|---|---|
| GET | `/dashboard/clinical` | `dashboardClinical` | Odontólogo: pacientes del día, próximo turno, tratamientos, laboratorio, alertas y actividad. | — | — | 200 `ClinicalDashboardDto` | `dashboard.clinical` |
| GET | `/dashboard/reception` | `dashboardReception` | Recepción: turnos del día, confirmaciones, sala de espera, tareas y entregas de laboratorio. Sin información clínica. | — | — | 200 `ReceptionDashboardDto` | `dashboard.reception` |

