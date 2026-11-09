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

| Método | Ruta | Descripción | Parámetros | Cuerpo | Respuesta | Permiso |
|---|---|---|---|---|---|---|
| POST | `/auth/2fa/disable` | Desactiva 2FA (requiere contraseña). | — | `Auth.DisableTwoFactorRequest` | 204 | Autenticado |
| POST | `/auth/2fa/enable` | Activa 2FA verificando un código. Devuelve códigos de recuperación (se muestran una única vez). | — | `Auth.EnableTwoFactorRequest` | 200 `Auth.RecoveryCodesDto` | Autenticado |
| GET | `/auth/2fa/setup` | Clave y URI otpauth para configurar una app autenticadora. | — | — | 200 `Auth.AuthenticatorSetupDto` | Autenticado |
| POST | `/auth/change-password` | Cambio de contraseña del usuario autenticado. Revoca las demás sesiones. | — | `Auth.ChangePasswordRequest` | 200 `Auth.MessageResponse` | Autenticado |
| POST | `/auth/confirm-email` | Confirma el correo electrónico. | — | `Auth.ConfirmEmailRequest` | 200 `Auth.MessageResponse` | Público |
| POST | `/auth/forgot-password` | Solicita el correo de recuperación. Responde siempre igual (protección contra enumeración). | — | `Auth.ForgotPasswordRequest` | 202 `Auth.MessageResponse` | Público |
| GET | `/auth/invitations/{token}` | Datos públicos de una invitación (para la pantalla de aceptación). | token (ruta) | — | 200 `Auth.InvitationInfoDto` | Público |
| POST | `/auth/invitations/accept` | Acepta una invitación: crea/activa la cuenta y la incorpora al consultorio. | — | `Auth.AcceptInvitationRequest` | 200 `Auth.MessageResponse` | Público |
| POST | `/auth/login` | Inicio de sesión. Si el usuario tiene 2FA, devuelve un desafío para completar en /auth/login/2fa. | — | `Auth.LoginRequest` | 200 `Odontia.Api.Controllers.LoginResponseDto` | Público |
| POST | `/auth/login/2fa` | Completa el inicio de sesión con código TOTP o código de recuperación. | — | `Auth.TwoFactorLoginRequest` | 200 `Odontia.Api.Controllers.SessionResponse` | Público |
| POST | `/auth/logout` | Cierra la sesión actual (revoca la familia de refresh tokens y borra las cookies). | — | — | 204 | Público |
| GET | `/auth/me` | Usuario autenticado, consultorio activo, permisos efectivos y membresías. | — | — | 200 `Auth.AuthSessionDto` | Autenticado |
| POST | `/auth/refresh` | Renueva la sesión (rotación del refresh token de la cookie). Requiere encabezado X-CSRF-Token. | — | — | 200 `Odontia.Api.Controllers.SessionResponse` | Público |
| POST | `/auth/register` | Alta de un nuevo consultorio y su usuario administrador (si el registro público está habilitado). | — | `Auth.RegisterRequest` | 202 `Auth.MessageResponse` | Público |
| POST | `/auth/resend-confirmation` | Reenvía el correo de confirmación (respuesta idéntica exista o no la cuenta). | — | `Auth.ResendConfirmationRequest` | 202 `Auth.MessageResponse` | Público |
| POST | `/auth/reset-password` | Restablece la contraseña con el token recibido por correo. Revoca todas las sesiones. | — | `Auth.ResetPasswordRequest` | 200 `Auth.MessageResponse` | Público |
| GET | `/auth/sessions` | Sesiones/dispositivos activos del usuario. | — | — | 200 `Auth.SessionInfoDto[]` | Autenticado |
| DELETE | `/auth/sessions/{sessionId}` | Revoca una sesión/dispositivo. | sessionId (ruta) | — | 204 | Autenticado |
| POST | `/auth/sessions/revoke-others` | Revoca todas las sesiones excepto la actual. | — | — | 200 | Autenticado |
| POST | `/auth/switch-consultorio` | Cambia el consultorio activo (emite nuevos tokens para ese consultorio). | — | `Auth.SwitchConsultorioRequest` | 200 `Odontia.Api.Controllers.SessionResponse` | Autenticado |

## Consultorio

| Método | Ruta | Descripción | Parámetros | Cuerpo | Respuesta | Permiso |
|---|---|---|---|---|---|---|
| GET | `/consultorio` | Datos del consultorio activo. | — | — | 200 `Organization.ConsultorioDto` | `consultorio.read` |
| PUT | `/consultorio` | Actualiza nombre, datos de contacto y dirección. | — | `Organization.UpdateConsultorioRequest` | 200 `Organization.ConsultorioDto` | `consultorio.manage` |
| GET | `/consultorio/gabinetes` | Consultorios físicos / gabinetes. | includeInactive | — | 200 `Organization.GabineteDto[]` | `consultorio.read` |
| POST | `/consultorio/gabinetes` | — | — | `Organization.SaveGabineteRequest` | 201 `Organization.GabineteDto` | `consultorio.manage` |
| PUT | `/consultorio/gabinetes/{id}` | — | id (ruta) | `Organization.SaveGabineteRequest` | 200 `Organization.GabineteDto` | `consultorio.manage` |
| GET | `/consultorio/professionals` | Profesionales que atienden (sólo el propio si no tiene acceso a todas las agendas). | — | — | 200 `Organization.ProfessionalDto[]` | `agenda.read` |
| PUT | `/consultorio/settings` | Configuración general: zona horaria, duración de turno, intervalo de agenda, recordatorios. | — | `Organization.UpdateConsultorioSettingsRequest` | 200 `Organization.ConsultorioDto` | `consultorio.manage` |

## Usuarios, roles e invitaciones

| Método | Ruta | Descripción | Parámetros | Cuerpo | Respuesta | Permiso |
|---|---|---|---|---|---|---|
| GET | `/invitations` | — | — | — | 200 `Organization.InvitationDto[]` | `users.manage` |
| POST | `/invitations` | Invita por correo a un usuario con los roles indicados. | — | `Organization.CreateInvitationRequest` | 201 `Organization.InvitationDto` | `users.manage` |
| DELETE | `/invitations/{id}` | — | id (ruta) | — | 204 | `users.manage` |
| GET | `/permissions` | Catálogo de permisos granulares. | — | — | 200 `Organization.PermissionInfoDto[]` | `users.read` |
| GET | `/roles` | Roles disponibles (de sistema y personalizados del consultorio). | — | — | 200 `Organization.RoleDto[]` | `users.read` |
| POST | `/roles` | — | — | `Organization.SaveRoleRequest` | 201 `Organization.RoleDto` | `roles.manage` |
| PUT | `/roles/{roleId}` | — | roleId (ruta) | `Organization.SaveRoleRequest` | 200 `Organization.RoleDto` | `roles.manage` |
| DELETE | `/roles/{roleId}` | — | roleId (ruta) | — | 204 | `roles.manage` |
| GET | `/users` | Usuarios (miembros) del consultorio. | includeInactive | — | 200 `Organization.MemberDto[]` | `users.read` |
| GET | `/users/{memberId}` | — | memberId (ruta) | — | 200 `Organization.MemberDto` | `users.read` |
| PUT | `/users/{memberId}` | Marca como profesional y preferencias de agenda. | memberId (ruta) | `Organization.UpdateMemberRequest` | 200 `Organization.MemberDto` | `users.manage` |
| PUT | `/users/{memberId}/active` | Activa o desactiva un usuario en el consultorio (desactivar revoca sus sesiones). | memberId (ruta) | `Organization.SetActiveRequest` | 200 `Organization.MemberDto` | `users.manage` |
| PUT | `/users/{memberId}/roles` | Asigna roles. No se pueden otorgar permisos que el actor no posee ni modificar los propios. | memberId (ruta) | `Organization.UpdateMemberRolesRequest` | 200 `Organization.MemberDto` | `users.manage` |

## Perfil

| Método | Ruta | Descripción | Parámetros | Cuerpo | Respuesta | Permiso |
|---|---|---|---|---|---|---|
| GET | `/profile` | — | — | — | 200 `Organization.ProfileDto` | Autenticado |
| PUT | `/profile` | Datos personales. | — | `Organization.UpdateProfileRequest` | 200 `Organization.ProfileDto` | Autenticado |
| PUT | `/profile/email` | Cambio de correo (requiere contraseña y nueva confirmación). | — | `Organization.ChangeEmailRequest` | 200 `Auth.MessageResponse` | Autenticado |
| PUT | `/profile/notification-settings` | — | — | `Organization.NotificationSettingsDto` | 200 `Organization.NotificationSettingsDto` | Autenticado |
| PUT | `/profile/professional` | Especialidad, matrícula y duración de turno por defecto. | — | `Organization.UpdateProfessionalProfileRequest` | 200 `Organization.ProfileDto` | Autenticado |

## Auditoría

| Método | Ruta | Descripción | Parámetros | Cuerpo | Respuesta | Permiso |
|---|---|---|---|---|---|---|
| GET | `/audit` | — | EntityType, EntityId, UserId, FromUtc, ToUtc, Page, PageSize, Sort | — | 200 `Odontia.Application.Common.PagedResult`1[[Organization.AuditLogDto, Odontia.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]` | `audit.read` |

## Pacientes

| Método | Ruta | Descripción | Parámetros | Cuerpo | Respuesta | Permiso |
|---|---|---|---|---|---|---|
| GET | `/patients` | Búsqueda paginada por nombre, apellido, DNI o teléfono, con filtros. Orden: name, document, created, status, birthdate. | Search, Status, HealthInsuranceId, Private, Page, PageSize, Sort | — | 200 `Odontia.Application.Common.PagedResult`1[[Patients.PatientListItemDto, Odontia.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]` | `patients.read` |
| POST | `/patients` | Alta de paciente (DNI único por consultorio). Antecedentes opcionales: requieren patients.medical.write. | — | `Patients.CreatePatientRequest` | 201 `Patients.PatientDto` | `patients.write` |
| GET | `/patients/{id}` | — | id (ruta) | — | 200 `Patients.PatientDto` | `patients.read` |
| PUT | `/patients/{id}` | — | id (ruta) | `Patients.SavePatientRequest` | 200 `Patients.PatientDto` | `patients.write` |
| GET | `/patients/{id}/alerts` | Alertas médicas relevantes. Recepción ve sólo títulos de alertas necesarias para la atención. | id (ruta) | — | 200 `Patients.AlertDto[]` | `patients.alerts.read` |
| POST | `/patients/{id}/alerts` | — | id (ruta) | `Patients.SaveAlertRequest` | 201 `Patients.AlertDto` | `patients.medical.write` |
| PUT | `/patients/{id}/alerts/{alertId}` | — | id (ruta), alertId (ruta) | `Patients.SaveAlertRequest` | 200 `Patients.AlertDto` | `patients.medical.write` |
| DELETE | `/patients/{id}/alerts/{alertId}` | — | id (ruta), alertId (ruta) | — | 204 | `patients.medical.write` |
| GET | `/patients/{id}/history` | Historial administrativo (turnos, pagos, presupuestos, documentos no clínicos). | id (ruta) | — | 200 `Patients.PatientHistoryItemDto[]` | `patients.read` |
| GET | `/patients/{id}/medical-history` | Antecedentes médicos completos (información clínica restringida). | id (ruta) | — | 200 `Patients.MedicalHistoryDto` | `patients.medical.read` |
| PUT | `/patients/{id}/medical-history` | — | id (ruta) | `Patients.MedicalHistoryRequest` | 200 `Patients.MedicalHistoryDto` | `patients.medical.write` |
| PUT | `/patients/{id}/status` | — | id (ruta) | `Patients.ChangePatientStatusRequest` | 200 `Patients.PatientDto` | `patients.write` |
| GET | `/patients/{id}/summary` | Resumen de la ficha (secciones según permisos: cuenta, tratamiento, última evolución, laboratorio). | id (ruta) | — | 200 `Patients.PatientSummaryDto` | `patients.read` |

## Obras sociales

| Método | Ruta | Descripción | Parámetros | Cuerpo | Respuesta | Permiso |
|---|---|---|---|---|---|---|
| GET | `/health-insurances` | — | includeInactive | — | 200 `Patients.HealthInsuranceDto[]` | `patients.read` |
| POST | `/health-insurances` | — | — | `Patients.SaveHealthInsuranceRequest` | 201 `Patients.HealthInsuranceDto` | `insurances.manage` |
| PUT | `/health-insurances/{id}` | — | id (ruta) | `Patients.SaveHealthInsuranceRequest` | 200 `Patients.HealthInsuranceDto` | `insurances.manage` |

## Agenda

| Método | Ruta | Descripción | Parámetros | Cuerpo | Respuesta | Permiso |
|---|---|---|---|---|---|---|
| GET | `/agenda` | Agenda de un rango de fechas locales (día o semana; máx. 42 días), por profesional y/o consultorio físico. | From, To, ProfessionalUserId, GabineteId, IncludeCancelled | — | 200 `Scheduling.AgendaDto` | `agenda.read` |
| GET | `/agenda/availability` | Horarios disponibles para reservar (la disponibilidad se revalida al confirmar el turno). | Date, ProfessionalUserId, GabineteId, DurationMinutes | — | 200 `Scheduling.AvailabilityDto` | `agenda.read` |
| GET | `/agenda/blocks` | — | from, to, professionalUserId | — | 200 `Scheduling.ScheduleBlockDto[]` | `agenda.read` |
| POST | `/agenda/blocks` | Crea un bloqueo, día no laborable o excepción de disponibilidad. | — | `Scheduling.SaveScheduleBlockRequest` | 201 `Scheduling.ScheduleBlockDto` | `agenda.manage` |
| PUT | `/agenda/blocks/{id}` | — | id (ruta) | `Scheduling.SaveScheduleBlockRequest` | 200 `Scheduling.ScheduleBlockDto` | `agenda.manage` |
| DELETE | `/agenda/blocks/{id}` | — | id (ruta) | — | 204 | `agenda.manage` |
| POST | `/agenda/blocks/non-working-day` | Marca un día completo como no laborable (consultorio o profesional). | — | `Scheduling.NonWorkingDayRequest` | 201 `Scheduling.ScheduleBlockDto` | `agenda.manage` |
| GET | `/agenda/day-summary` | Resumen del día: turnos, minutos ocupados y huecos libres. | date, professionalUserId | — | 200 `Scheduling.DaySummaryDto` | `agenda.read` |
| GET | `/agenda/month` | Vista mensual: cantidad de turnos por día y primeros turnos. | year, month, professionalUserId, gabineteId | — | 200 `Scheduling.AgendaMonthDto` | `agenda.read` |
| GET | `/agenda/professionals/{professionalUserId}/working-hours` | — | professionalUserId (ruta) | — | 200 `Scheduling.WorkingHoursDto[]` | `agenda.read` |
| PUT | `/agenda/professionals/{professionalUserId}/working-hours` | Reemplaza el horario laboral semanal del profesional. | professionalUserId (ruta) | `Scheduling.SetWorkingHoursRequest` | 200 `Scheduling.WorkingHoursDto[]` | `agenda.manage` |

## Turnos

| Método | Ruta | Descripción | Parámetros | Cuerpo | Respuesta | Permiso |
|---|---|---|---|---|---|---|
| GET | `/appointments` | Listado paginado con filtros (rango, estado, profesional, paciente, búsqueda). Orden: start, status, patient, created. | From, To, Status, ProfessionalUserId, PatientId, GabineteId, Search, Page, PageSize, Sort | — | 200 `Odontia.Application.Common.PagedResult`1[[Scheduling.AppointmentDto, Odontia.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]` | `appointments.read` |
| POST | `/appointments` | Crea un turno validando disponibilidad y superposición de forma atómica. | — | `Scheduling.CreateAppointmentRequest` | 201 `Scheduling.AppointmentDto` | `appointments.write` |
| GET | `/appointments/{id}` | — | id (ruta) | — | 200 `Scheduling.AppointmentDto` | `appointments.read` |
| PUT | `/appointments/{id}` | Modifica paciente, motivo y observaciones (requiere la versión vigente para evitar pisar cambios). | id (ruta) | `Scheduling.UpdateAppointmentRequest` | 200 `Scheduling.AppointmentDto` | `appointments.write` |
| POST | `/appointments/{id}/attend` | — | id (ruta) | — | 200 `Scheduling.AppointmentDto` | `appointments.write` |
| POST | `/appointments/{id}/cancel` | — | id (ruta) | `Scheduling.CancelAppointmentRequest` | 200 `Scheduling.AppointmentDto` | `appointments.write` |
| POST | `/appointments/{id}/check-in` | Registra la llegada del paciente. | id (ruta) | — | 200 `Scheduling.AppointmentDto` | `appointments.write` |
| POST | `/appointments/{id}/confirm` | — | id (ruta) | — | 200 `Scheduling.AppointmentDto` | `appointments.write` |
| GET | `/appointments/{id}/history` | — | id (ruta) | — | 200 `Scheduling.AppointmentHistoryDto[]` | `appointments.read` |
| POST | `/appointments/{id}/mark-pending` | — | id (ruta) | — | 200 `Scheduling.AppointmentDto` | `appointments.write` |
| POST | `/appointments/{id}/no-show` | Registra ausencia (sólo luego del horario del turno). | id (ruta) | — | 200 `Scheduling.AppointmentDto` | `appointments.write` |
| POST | `/appointments/{id}/reminder` | Envía un recordatorio. El estado devuelto refleja el resultado real del proveedor (NotSentNoProvider si no hay proveedor). | id (ruta), channel | — | 200 `Notifications.OutboundMessageDto` | `messaging.send` |
| POST | `/appointments/{id}/reschedule` | Reprograma (horario, profesional, consultorio físico). El turno vuelve a "pendiente". | id (ruta) | `Scheduling.RescheduleAppointmentRequest` | 200 `Scheduling.AppointmentDto` | `appointments.write` |
| POST | `/appointments/{id}/start` | Inicia la consulta. | id (ruta) | — | 200 `Scheduling.AppointmentDto` | `appointments.write` |
| POST | `/appointments/{id}/status` | Cambio de estado genérico (máquina de estados controlada). | id (ruta) | `Scheduling.ChangeAppointmentStatusRequest` | 200 `Scheduling.AppointmentDto` | `appointments.write` |
| POST | `/appointments/{id}/waiting-room` | — | id (ruta) | — | 200 `Scheduling.AppointmentDto` | `appointments.write` |
| GET | `/appointments/status-counts` | Conteo por estado en un rango (pestañas de "Gestión de turnos"). | from, to, professionalUserId | — | 200 `Scheduling.AppointmentStatusCountsDto` | `appointments.read` |

## Tareas

| Método | Ruta | Descripción | Parámetros | Cuerpo | Respuesta | Permiso |
|---|---|---|---|---|---|---|
| GET | `/tasks` | Listado con filtros. view: open | overdue | today | upcoming | completed. Orden: due, priority, created, status. | Status, View, AssignedToUserId, Mine, Priority, Category, PatientId, Page, PageSize, Sort | — | 200 `Odontia.Application.Common.PagedResult`1[[Tasks.TaskDto, Odontia.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]` | `tasks.read` |
| POST | `/tasks` | — | — | `Tasks.SaveTaskRequest` | 201 `Tasks.TaskDto` | `tasks.write` |
| GET | `/tasks/{id}` | — | id (ruta) | — | 200 `Tasks.TaskDto` | `tasks.read` |
| PUT | `/tasks/{id}` | — | id (ruta) | `Tasks.SaveTaskRequest` | 200 `Tasks.TaskDto` | `tasks.write` |
| PUT | `/tasks/{id}/assignee` | Asigna o reasigna el responsable. | id (ruta) | `Tasks.AssignTaskRequest` | 200 `Tasks.TaskDto` | `tasks.write` |
| PUT | `/tasks/{id}/status` | Cambia el estado (p. ej. Completed). | id (ruta) | `Tasks.ChangeTaskStatusRequest` | 200 `Tasks.TaskDto` | `tasks.write` |

## Historia clínica

| Método | Ruta | Descripción | Parámetros | Cuerpo | Respuesta | Permiso |
|---|---|---|---|---|---|---|
| GET | `/patients/{patientId}/clinical-record` | Encabezado de la historia clínica con antecedentes y alertas. | patientId (ruta) | — | 200 `Clinical.ClinicalRecordDto` | `clinical.read` |
| GET | `/patients/{patientId}/clinical-record/diagnoses` | — | patientId (ruta), status | — | 200 `Clinical.DiagnosisDto[]` | `clinical.read` |
| POST | `/patients/{patientId}/clinical-record/diagnoses` | — | patientId (ruta) | `Clinical.CreateDiagnosisRequest` | 201 `Clinical.DiagnosisDto` | `clinical.write` |
| PUT | `/patients/{patientId}/clinical-record/diagnoses/{diagnosisId}/status` | — | patientId (ruta), diagnosisId (ruta) | `Clinical.ChangeDiagnosisStatusRequest` | 200 `Clinical.DiagnosisDto` | `clinical.write` |
| GET | `/patients/{patientId}/clinical-record/entries` | Evolución cronológica (más recientes primero). Filtros: type, toothCode, includeVoided. | patientId (ruta), Type, ToothCode, IncludeVoided, Page, PageSize, Sort | — | 200 `Odontia.Application.Common.PagedResult`1[[Clinical.ClinicalEntryDto, Odontia.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]` | `clinical.read` |
| POST | `/patients/{patientId}/clinical-record/entries` | Registra una evolución, consulta, observación, procedimiento, receta o estudio. El responsable es el profesional autenticado. | patientId (ruta) | `Clinical.CreateClinicalEntryRequest` | 201 `Clinical.ClinicalEntryDto` | `clinical.write` |
| GET | `/patients/{patientId}/clinical-record/entries/{entryId}` | — | patientId (ruta), entryId (ruta) | — | 200 `Clinical.ClinicalEntryDto` | `clinical.read` |
| POST | `/patients/{patientId}/clinical-record/entries/{entryId}/amendments` | Corrige un registro (sólo el autor, con motivo). La versión previa se conserva y se audita. | patientId (ruta), entryId (ruta) | `Clinical.AmendClinicalEntryRequest` | 200 `Clinical.ClinicalEntryDto` | `clinical.write` |
| GET | `/patients/{patientId}/clinical-record/entries/{entryId}/revisions` | Versiones anteriores del registro. | patientId (ruta), entryId (ruta) | — | 200 `Clinical.ClinicalEntryRevisionDto[]` | `clinical.read` |
| POST | `/patients/{patientId}/clinical-record/entries/{entryId}/void` | Anula un registro con motivo (no se elimina). | patientId (ruta), entryId (ruta) | `Clinical.VoidClinicalEntryRequest` | 200 `Clinical.ClinicalEntryDto` | `clinical.write` |

## Odontograma · catálogo

| Método | Ruta | Descripción | Parámetros | Cuerpo | Respuesta | Permiso |
|---|---|---|---|---|---|---|
| GET | `/odontogram/catalog` | Piezas FDI (permanentes y temporales) con superficies válidas, superficies y condiciones. | — | — | 200 `Odontograms.OdontogramCatalogDto` | `odontogram.read` |
| POST | `/odontogram/conditions` | — | — | `Odontograms.SaveConditionRequest` | 201 `Odontograms.ConditionDto` | `catalog.manage` |
| PUT | `/odontogram/conditions/{id}` | — | id (ruta) | `Odontograms.SaveConditionRequest` | 200 `Odontograms.ConditionDto` | `catalog.manage` |

## Odontograma

| Método | Ruta | Descripción | Parámetros | Cuerpo | Respuesta | Permiso |
|---|---|---|---|---|---|---|
| GET | `/patients/{patientId}/odontogram` | — | patientId (ruta) | — | 200 `Odontograms.OdontogramDto` | `odontogram.read` |
| POST | `/patients/{patientId}/odontogram/findings` | Aplica una o varias condiciones (pieza o superficie; existente o a realizar) en una operación atómica. | patientId (ruta) | `Odontograms.ApplyConditionsRequest` | 200 `Odontograms.OdontogramDto` | `odontogram.write` |
| POST | `/patients/{patientId}/odontogram/findings/{findingId}/remove` | Retira un hallazgo cargado por error (queda en el historial con motivo). | patientId (ruta), findingId (ruta) | `Odontograms.RemoveFindingRequest` | 200 `Odontograms.OdontogramDto` | `odontogram.write` |
| POST | `/patients/{patientId}/odontogram/findings/{findingId}/resolve` | Marca un hallazgo como resuelto. | patientId (ruta), findingId (ruta) | `Odontograms.ResolveFindingRequest` | 200 `Odontograms.OdontogramDto` | `odontogram.write` |
| GET | `/patients/{patientId}/odontogram/history` | Historial de modificaciones (filtrable por pieza) con profesional y fecha. | patientId (ruta), ToothCode, Page, PageSize, Sort | — | 200 `Odontia.Application.Common.PagedResult`1[[Odontograms.OdontogramHistoryDto, Odontia.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]` | `odontogram.read` |
| POST | `/patients/{patientId}/odontogram/teeth/{toothCode}/notes` | — | patientId (ruta), toothCode (ruta) | `Odontograms.AddToothNoteRequest` | 201 `Odontograms.ToothNoteDto` | `odontogram.write` |

## Prestaciones

| Método | Ruta | Descripción | Parámetros | Cuerpo | Respuesta | Permiso |
|---|---|---|---|---|---|---|
| GET | `/procedures` | Requiere treatments.read, budgets.read o catalog.manage. | Search, IncludeInactive, Page, PageSize, Sort | — | 200 `Odontia.Application.Common.PagedResult`1[[Treatments.ProcedureDto, Odontia.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]` | Autenticado |
| POST | `/procedures` | — | — | `Treatments.SaveProcedureRequest` | 201 `Treatments.ProcedureDto` | `catalog.manage` |
| PUT | `/procedures/{id}` | Actualiza la prestación. Los planes/presupuestos existentes conservan su valor histórico. | id (ruta) | `Treatments.SaveProcedureRequest` | 200 `Treatments.ProcedureDto` | `catalog.manage` |

## Planes de tratamiento

| Método | Ruta | Descripción | Parámetros | Cuerpo | Respuesta | Permiso |
|---|---|---|---|---|---|---|
| GET | `/patients/{patientId}/treatment-plans` | — | patientId (ruta) | — | 200 `Treatments.TreatmentPlanDto[]` | `treatments.read` |
| POST | `/patients/{patientId}/treatment-plans` | — | patientId (ruta) | `Treatments.CreateTreatmentPlanRequest` | 201 `Treatments.TreatmentPlanDto` | `treatments.write` |
| GET | `/patients/{patientId}/treatment-plans/{planId}` | — | patientId (ruta), planId (ruta) | — | 200 `Treatments.TreatmentPlanDto` | `treatments.read` |
| PUT | `/patients/{patientId}/treatment-plans/{planId}` | — | patientId (ruta), planId (ruta) | `Treatments.UpdateTreatmentPlanRequest` | 200 `Treatments.TreatmentPlanDto` | `treatments.write` |
| POST | `/patients/{patientId}/treatment-plans/{planId}/cancel` | — | patientId (ruta), planId (ruta) | `Treatments.CancelPlanRequest` | 200 `Treatments.TreatmentPlanDto` | `treatments.write` |
| GET | `/patients/{patientId}/treatment-plans/{planId}/history` | — | patientId (ruta), planId (ruta) | — | 200 `Treatments.TreatmentChangeDto[]` | `treatments.read` |
| POST | `/patients/{patientId}/treatment-plans/{planId}/items` | Agrega una prestación (valor tomado del catálogo vigente). | patientId (ruta), planId (ruta) | `Treatments.AddPlanItemRequest` | 200 `Treatments.TreatmentPlanDto` | `treatments.write` |
| PUT | `/patients/{patientId}/treatment-plans/{planId}/items/{itemId}/status` | Cambia el estado de una prestación (Pending, InProgress, Completed, Cancelled); el plan se recalcula. | patientId (ruta), planId (ruta), itemId (ruta) | `Treatments.ChangeItemStatusRequest` | 200 `Treatments.TreatmentPlanDto` | `treatments.write` |

## Presupuestos

| Método | Ruta | Descripción | Parámetros | Cuerpo | Respuesta | Permiso |
|---|---|---|---|---|---|---|
| GET | `/budgets` | — | Status, PatientId, Page, PageSize, Sort | — | 200 `Odontia.Application.Common.PagedResult`1[[Finance.BudgetDto, Odontia.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]` | `budgets.read` |
| GET | `/budgets/{id}` | — | id (ruta) | — | 200 `Finance.BudgetDto` | `budgets.read` |
| PUT | `/budgets/{id}` | Modifica un presupuesto en borrador (reemplaza las líneas). | id (ruta) | `Finance.SaveBudgetRequest` | 200 `Finance.BudgetDto` | `budgets.write` |
| POST | `/budgets/{id}/approve` | — | id (ruta) | — | 200 `Finance.BudgetDto` | `budgets.approve` |
| POST | `/budgets/{id}/cancel` | — | id (ruta) | `Finance.BudgetDecisionRequest` | 200 `Finance.BudgetDto` | `budgets.approve` |
| GET | `/budgets/{id}/history` | — | id (ruta) | — | 200 `Finance.BudgetStatusChangeDto[]` | `budgets.read` |
| POST | `/budgets/{id}/issue` | — | id (ruta) | — | 200 `Finance.BudgetDto` | `budgets.write` |
| POST | `/budgets/{id}/reject` | — | id (ruta) | `Finance.BudgetDecisionRequest` | 200 `Finance.BudgetDto` | `budgets.approve` |
| POST | `/patients/{patientId}/budgets` | Crea un presupuesto en borrador (líneas manuales o importadas del plan de tratamiento). | patientId (ruta) | `Finance.SaveBudgetRequest` | 201 `Finance.BudgetDto` | `budgets.write` |

## Pagos y cuenta corriente

| Método | Ruta | Descripción | Parámetros | Cuerpo | Respuesta | Permiso |
|---|---|---|---|---|---|---|
| GET | `/patients/{patientId}/account` | Cuenta corriente del paciente con movimientos, saldo acumulado y saldos por presupuesto. | patientId (ruta) | — | 200 `Finance.AccountStatementDto` | `finance.read` |
| GET | `/payments` | — | PatientId, From, To, Method, IncludeVoided, Page, PageSize, Sort | — | 200 `Odontia.Application.Common.PagedResult`1[[Finance.PaymentDto, Odontia.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]` | `finance.read` |
| POST | `/payments` | Registra un pago (opcionalmente imputado a un presupuesto aprobado, sin exceder su saldo). | — | `Finance.RegisterPaymentRequest` | 201 `Finance.PaymentDto` | `payments.write` |
| GET | `/payments/{id}` | — | id (ruta) | — | 200 `Finance.PaymentDto` | `finance.read` |
| POST | `/payments/{id}/void` | Anulación controlada con motivo: genera contraasiento; el pago no se elimina. | id (ruta) | `Finance.VoidPaymentRequest` | 200 `Finance.PaymentDto` | `payments.void` |

## Laboratorios

| Método | Ruta | Descripción | Parámetros | Cuerpo | Respuesta | Permiso |
|---|---|---|---|---|---|---|
| GET | `/laboratories` | Requiere lab.read o laboratories.manage. | includeInactive | — | 200 `Lab.LaboratoryDto[]` | Autenticado |
| POST | `/laboratories` | — | — | `Lab.SaveLaboratoryRequest` | 201 `Lab.LaboratoryDto` | `laboratories.manage` |
| PUT | `/laboratories/{id}` | — | id (ruta) | `Lab.SaveLaboratoryRequest` | 200 `Lab.LaboratoryDto` | `laboratories.manage` |

## Trabajos de laboratorio

| Método | Ruta | Descripción | Parámetros | Cuerpo | Respuesta | Permiso |
|---|---|---|---|---|---|---|
| GET | `/lab-works` | Listado. view: open | overdue | this_week | closed. | Status, LaboratoryId, PatientId, View, Page, PageSize, Sort | — | 200 `Odontia.Application.Common.PagedResult`1[[Lab.LabWorkDto, Odontia.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]` | `lab.read` |
| POST | `/lab-works` | — | — | `Lab.SaveLabWorkRequest` | 201 `Lab.LabWorkDto` | `lab.write` |
| GET | `/lab-works/{id}` | — | id (ruta) | — | 200 `Lab.LabWorkDto` | `lab.read` |
| PUT | `/lab-works/{id}` | — | id (ruta) | `Lab.SaveLabWorkRequest` | 200 `Lab.LabWorkDto` | `lab.write` |
| GET | `/lab-works/{id}/history` | — | id (ruta) | — | 200 `Lab.LabWorkChangeDto[]` | `lab.read` |
| POST | `/lab-works/{id}/status` | Avance logístico (lab.track o lab.write). Colocado/Cancelado requieren lab.write. | id (ruta) | `Lab.ChangeLabWorkStatusRequest` | 200 `Lab.LabWorkDto` | Autenticado |
| GET | `/lab-works/board` | Tablero: conteos por estado, atrasados y entregas de la semana. | — | — | 200 `Lab.LabBoardDto` | `lab.read` |

## Documentos y consentimientos

| Método | Ruta | Descripción | Parámetros | Cuerpo | Respuesta | Permiso |
|---|---|---|---|---|---|---|
| DELETE | `/documents/{id}` | Baja lógica con motivo (el archivo se conserva según la política de retención). | id (ruta), reason | — | 204 | `documents.delete` |
| GET | `/documents/{id}/download` | Descarga autorizada (se audita). Nunca se expone una URL pública. | id (ruta) | — | 200 `string` | `documents.read` |
| GET | `/patients/{patientId}/consents` | — | patientId (ruta) | — | 200 `Documents.ConsentDto[]` | `consents.read` |
| POST | `/patients/{patientId}/consents` | — | patientId (ruta) | `Documents.CreateConsentRequest` | 201 `Documents.ConsentDto` | `consents.write` |
| POST | `/patients/{patientId}/consents/{consentId}/accept` | Registra la aceptación en el sistema (constancia interna; NO es una firma digital certificada). | patientId (ruta), consentId (ruta) | `Documents.RecordConsentAcceptanceRequest` | 200 `Documents.ConsentDto` | `consents.write` |
| POST | `/patients/{patientId}/consents/{consentId}/attach-signed` | Vincula el consentimiento firmado en papel (documento previamente cargado con categoría ConsentForm). | patientId (ruta), consentId (ruta) | `Documents.AttachSignedConsentRequest` | 200 `Documents.ConsentDto` | `consents.write` |
| POST | `/patients/{patientId}/consents/{consentId}/reject` | — | patientId (ruta), consentId (ruta) | `Documents.ConsentDecisionRequest` | 200 `Documents.ConsentDto` | `consents.write` |
| POST | `/patients/{patientId}/consents/{consentId}/revoke` | — | patientId (ruta), consentId (ruta) | `Documents.ConsentDecisionRequest` | 200 `Documents.ConsentDto` | `consents.write` |
| GET | `/patients/{patientId}/documents` | Documentos del paciente. Los clínicos sólo se listan con documents.clinical.read. | patientId (ruta), category | — | 200 `Documents.DocumentDto[]` | `documents.read` |
| POST | `/patients/{patientId}/documents` | Carga un archivo (PDF, JPG, PNG, DICOM). Se valida extensión, firma binaria real y tamaño máximo. | patientId (ruta) | multipart/form-data | 201 `Documents.DocumentDto` | `documents.write` |

## Notificaciones y mensajes

| Método | Ruta | Descripción | Parámetros | Cuerpo | Respuesta | Permiso |
|---|---|---|---|---|---|---|
| GET | `/messages` | Registro de mensajes salientes y su estado real (Sent sólo con confirmación del proveedor). | Status, Channel, Page, PageSize, Sort | — | 200 `Odontia.Application.Common.PagedResult`1[[Notifications.OutboundMessageDto, Odontia.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]` | `messaging.send` |
| POST | `/messages/reminders` | Envía recordatorios para los turnos pendientes/confirmados de una fecha. | — | `Notifications.SendRemindersRequest` | 200 `Notifications.SendRemindersResultDto` | `messaging.send` |
| GET | `/notifications` | — | UnreadOnly, Page, PageSize, Sort | — | 200 `Odontia.Application.Common.PagedResult`1[[Notifications.NotificationDto, Odontia.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]` | Autenticado |
| POST | `/notifications/{id}/read` | — | id (ruta) | — | 204 | Autenticado |
| POST | `/notifications/read-all` | — | — | — | 200 | Autenticado |
| GET | `/notifications/unread-count` | — | — | — | 200 `Notifications.UnreadCountDto` | Autenticado |

## Dashboards

| Método | Ruta | Descripción | Parámetros | Cuerpo | Respuesta | Permiso |
|---|---|---|---|---|---|---|
| GET | `/dashboard/clinical` | Odontólogo: pacientes del día, próximo turno, tratamientos, laboratorio, alertas y actividad. | — | — | 200 `Dashboard.ClinicalDashboardDto` | `dashboard.clinical` |
| GET | `/dashboard/reception` | Recepción: turnos del día, confirmaciones, sala de espera, tareas y entregas de laboratorio. Sin información clínica. | — | — | 200 `Dashboard.ReceptionDashboardDto` | `dashboard.reception` |

