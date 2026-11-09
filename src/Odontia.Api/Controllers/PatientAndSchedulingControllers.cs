using Microsoft.AspNetCore.Mvc;
using Odontia.Api.Infrastructure;
using Odontia.Application.Common;
using Odontia.Application.Features.Notifications;
using Odontia.Application.Features.Patients;
using Odontia.Application.Features.Scheduling;
using Odontia.Application.Features.Tasks;
using Odontia.Domain.Enums;
using P = Odontia.Domain.Authorization.Permissions;

namespace Odontia.Api.Controllers;

/// <summary>Pacientes: datos personales/administrativos, antecedentes médicos (restringidos) y alertas.</summary>
[Route("api/v{version:apiVersion}/patients")]
public sealed class PatientsController(IPatientService service) : ApiControllerBase
{
    /// <summary>Búsqueda paginada por nombre, apellido, DNI o teléfono, con filtros. Orden: name, document, created, status, birthdate.</summary>
    [HttpGet, HasPermission(P.PatientsRead)]
    public Task<PagedResult<PatientListItemDto>> Search([FromQuery] PatientQuery query, CancellationToken ct) => service.SearchAsync(query, ct);

    /// <summary>Alta de paciente (DNI único por consultorio). Antecedentes opcionales: requieren patients.medical.write.</summary>
    [HttpPost, HasPermission(P.PatientsWrite)]
    [ProducesResponseType<PatientDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreatePatientRequest request, CancellationToken ct)
    {
        var p = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = p.Id, version = "1" }, p);
    }

    [HttpGet("{id:guid}"), HasPermission(P.PatientsRead)]
    public Task<PatientDto> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);

    [HttpPut("{id:guid}"), HasPermission(P.PatientsWrite)]
    public Task<PatientDto> Update(Guid id, SavePatientRequest request, CancellationToken ct) => service.UpdateAsync(id, request, ct);

    [HttpPut("{id:guid}/status"), HasPermission(P.PatientsWrite)]
    public Task<PatientDto> ChangeStatus(Guid id, ChangePatientStatusRequest request, CancellationToken ct) => service.ChangeStatusAsync(id, request.Status, ct);

    /// <summary>Resumen de la ficha (secciones según permisos: cuenta, tratamiento, última evolución, laboratorio).</summary>
    [HttpGet("{id:guid}/summary"), HasPermission(P.PatientsRead)]
    public Task<PatientSummaryDto> Summary(Guid id, CancellationToken ct) => service.GetSummaryAsync(id, ct);

    /// <summary>Historial administrativo (turnos, pagos, presupuestos, documentos no clínicos).</summary>
    [HttpGet("{id:guid}/history"), HasPermission(P.PatientsRead)]
    public Task<IReadOnlyList<PatientHistoryItemDto>> History(Guid id, CancellationToken ct) => service.GetAdministrativeHistoryAsync(id, ct);

    /// <summary>Antecedentes médicos completos (información clínica restringida).</summary>
    [HttpGet("{id:guid}/medical-history"), HasPermission(P.PatientsMedicalRead)]
    public Task<MedicalHistoryDto> MedicalHistory(Guid id, CancellationToken ct) => service.GetMedicalHistoryAsync(id, ct);

    [HttpPut("{id:guid}/medical-history"), HasPermission(P.PatientsMedicalWrite)]
    public Task<MedicalHistoryDto> UpdateMedicalHistory(Guid id, MedicalHistoryRequest request, CancellationToken ct) => service.UpdateMedicalHistoryAsync(id, request, ct);

    /// <summary>Alertas médicas relevantes. Recepción ve sólo títulos de alertas necesarias para la atención.</summary>
    [HttpGet("{id:guid}/alerts"), HasPermission(P.PatientsAlertsRead)]
    public Task<IReadOnlyList<AlertDto>> Alerts(Guid id, CancellationToken ct) => service.GetAlertsAsync(id, ct);

    [HttpPost("{id:guid}/alerts"), HasPermission(P.PatientsMedicalWrite)]
    [ProducesResponseType<AlertDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> AddAlert(Guid id, SaveAlertRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.AddAlertAsync(id, request, ct));

    [HttpPut("{id:guid}/alerts/{alertId:guid}"), HasPermission(P.PatientsMedicalWrite)]
    public Task<AlertDto> UpdateAlert(Guid id, Guid alertId, SaveAlertRequest request, CancellationToken ct) => service.UpdateAlertAsync(id, alertId, request, ct);

    [HttpDelete("{id:guid}/alerts/{alertId:guid}"), HasPermission(P.PatientsMedicalWrite)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeactivateAlert(Guid id, Guid alertId, CancellationToken ct)
    {
        await service.DeactivateAlertAsync(id, alertId, ct);
        return NoContent();
    }
}

/// <summary>Obras sociales / prepagas.</summary>
[Route("api/v{version:apiVersion}/health-insurances")]
public sealed class HealthInsurancesController(IPatientService service) : ApiControllerBase
{
    [HttpGet, HasPermission(P.PatientsRead)]
    public Task<IReadOnlyList<HealthInsuranceDto>> List([FromQuery] bool includeInactive, CancellationToken ct) => service.GetInsurancesAsync(includeInactive, ct);

    [HttpPost, HasPermission(P.InsurancesManage)]
    [ProducesResponseType<HealthInsuranceDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(SaveHealthInsuranceRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.CreateInsuranceAsync(request, ct));

    [HttpPut("{id:guid}"), HasPermission(P.InsurancesManage)]
    public Task<HealthInsuranceDto> Update(Guid id, SaveHealthInsuranceRequest request, CancellationToken ct) => service.UpdateInsuranceAsync(id, request, ct);
}

/// <summary>Agenda: vistas día/semana/mes, disponibilidad, horarios laborales, bloqueos y excepciones.</summary>
[Route("api/v{version:apiVersion}/agenda")]
public sealed class AgendaController(IAgendaService service) : ApiControllerBase
{
    /// <summary>Agenda de un rango de fechas locales (día o semana; máx. 42 días), por profesional y/o consultorio físico.</summary>
    [HttpGet, HasPermission(P.AgendaRead)]
    public Task<AgendaDto> Get([FromQuery] AgendaQuery query, CancellationToken ct) => service.GetAgendaAsync(query, ct);

    /// <summary>Vista mensual: cantidad de turnos por día y primeros turnos.</summary>
    [HttpGet("month"), HasPermission(P.AgendaRead)]
    public Task<AgendaMonthDto> Month([FromQuery] int year, [FromQuery] int month, [FromQuery] Guid? professionalUserId, [FromQuery] Guid? gabineteId, CancellationToken ct) =>
        service.GetMonthAsync(year, month, professionalUserId, gabineteId, ct);

    /// <summary>Resumen del día: turnos, minutos ocupados y huecos libres.</summary>
    [HttpGet("day-summary"), HasPermission(P.AgendaRead)]
    public Task<DaySummaryDto> DaySummary([FromQuery] DateOnly date, [FromQuery] Guid? professionalUserId, CancellationToken ct) =>
        service.GetDaySummaryAsync(date, professionalUserId, ct);

    /// <summary>Horarios disponibles para reservar (la disponibilidad se revalida al confirmar el turno).</summary>
    [HttpGet("availability"), HasPermission(P.AgendaRead)]
    public Task<AvailabilityDto> Availability([FromQuery] AvailabilityQuery query, CancellationToken ct) => service.GetAvailabilityAsync(query, ct);

    [HttpGet("professionals/{professionalUserId:guid}/working-hours"), HasPermission(P.AgendaRead)]
    public Task<IReadOnlyList<WorkingHoursDto>> WorkingHours(Guid professionalUserId, CancellationToken ct) => service.GetWorkingHoursAsync(professionalUserId, ct);

    /// <summary>Reemplaza el horario laboral semanal del profesional.</summary>
    [HttpPut("professionals/{professionalUserId:guid}/working-hours"), HasPermission(P.AgendaManage)]
    public Task<IReadOnlyList<WorkingHoursDto>> SetWorkingHours(Guid professionalUserId, SetWorkingHoursRequest request, CancellationToken ct) =>
        service.SetWorkingHoursAsync(professionalUserId, request, ct);

    [HttpGet("blocks"), HasPermission(P.AgendaRead)]
    public Task<IReadOnlyList<ScheduleBlockDto>> Blocks([FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] Guid? professionalUserId, CancellationToken ct) =>
        service.GetBlocksAsync(from, to, professionalUserId, ct);

    /// <summary>Crea un bloqueo, día no laborable o excepción de disponibilidad.</summary>
    [HttpPost("blocks"), HasPermission(P.AgendaManage)]
    [ProducesResponseType<ScheduleBlockDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateBlock(SaveScheduleBlockRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.CreateBlockAsync(request, ct));

    /// <summary>Marca un día completo como no laborable (consultorio o profesional).</summary>
    [HttpPost("blocks/non-working-day"), HasPermission(P.AgendaManage)]
    [ProducesResponseType<ScheduleBlockDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> NonWorkingDay(NonWorkingDayRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.CreateNonWorkingDayAsync(request, ct));

    [HttpPut("blocks/{id:guid}"), HasPermission(P.AgendaManage)]
    public Task<ScheduleBlockDto> UpdateBlock(Guid id, SaveScheduleBlockRequest request, CancellationToken ct) => service.UpdateBlockAsync(id, request, ct);

    [HttpDelete("blocks/{id:guid}"), HasPermission(P.AgendaManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteBlock(Guid id, CancellationToken ct)
    {
        await service.DeleteBlockAsync(id, ct);
        return NoContent();
    }
}

/// <summary>Turnos: alta, modificación, reprogramación, estados (confirmar, llegada, sala de espera, atendido, ausente, cancelar) e historial.</summary>
[Route("api/v{version:apiVersion}/appointments")]
public sealed class AppointmentsController(IAppointmentService service, INotificationService notifications) : ApiControllerBase
{
    /// <summary>Listado paginado con filtros (rango, estado, profesional, paciente, búsqueda). Orden: start, status, patient, created.</summary>
    [HttpGet, HasPermission(P.AppointmentsRead)]
    public Task<PagedResult<AppointmentDto>> Search([FromQuery] AppointmentQuery query, CancellationToken ct) => service.SearchAsync(query, ct);

    /// <summary>Conteo por estado en un rango (pestañas de "Gestión de turnos").</summary>
    [HttpGet("status-counts"), HasPermission(P.AppointmentsRead)]
    public Task<AppointmentStatusCountsDto> StatusCounts([FromQuery] DateTimeOffset from, [FromQuery] DateTimeOffset to, [FromQuery] Guid? professionalUserId, CancellationToken ct) =>
        service.GetStatusCountsAsync(from, to, professionalUserId, ct);

    /// <summary>Crea un turno validando disponibilidad y superposición de forma atómica.</summary>
    [HttpPost, HasPermission(P.AppointmentsWrite)]
    [ProducesResponseType<AppointmentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(CreateAppointmentRequest request, CancellationToken ct)
    {
        var a = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = a.Id, version = "1" }, a);
    }

    [HttpGet("{id:guid}"), HasPermission(P.AppointmentsRead)]
    public Task<AppointmentDto> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);

    /// <summary>Modifica paciente, motivo y observaciones (requiere la versión vigente para evitar pisar cambios).</summary>
    [HttpPut("{id:guid}"), HasPermission(P.AppointmentsWrite)]
    public Task<AppointmentDto> Update(Guid id, UpdateAppointmentRequest request, CancellationToken ct) => service.UpdateAsync(id, request, ct);

    /// <summary>Reprograma (horario, profesional, consultorio físico). El turno vuelve a "pendiente".</summary>
    [HttpPost("{id:guid}/reschedule"), HasPermission(P.AppointmentsWrite)]
    public Task<AppointmentDto> Reschedule(Guid id, RescheduleAppointmentRequest request, CancellationToken ct) => service.RescheduleAsync(id, request, ct);

    /// <summary>Cambio de estado genérico (máquina de estados controlada).</summary>
    [HttpPost("{id:guid}/status"), HasPermission(P.AppointmentsWrite)]
    public Task<AppointmentDto> ChangeStatus(Guid id, ChangeAppointmentStatusRequest request, CancellationToken ct) => service.ChangeStatusAsync(id, request, ct);

    [HttpPost("{id:guid}/confirm"), HasPermission(P.AppointmentsWrite)]
    public Task<AppointmentDto> Confirm(Guid id, CancellationToken ct) => Status(id, AppointmentStatus.Confirmed, null, ct);

    [HttpPost("{id:guid}/mark-pending"), HasPermission(P.AppointmentsWrite)]
    public Task<AppointmentDto> MarkPending(Guid id, CancellationToken ct) => Status(id, AppointmentStatus.Pending, null, ct);

    /// <summary>Registra la llegada del paciente.</summary>
    [HttpPost("{id:guid}/check-in"), HasPermission(P.AppointmentsWrite)]
    public Task<AppointmentDto> CheckIn(Guid id, CancellationToken ct) => Status(id, AppointmentStatus.Arrived, null, ct);

    [HttpPost("{id:guid}/waiting-room"), HasPermission(P.AppointmentsWrite)]
    public Task<AppointmentDto> WaitingRoom(Guid id, CancellationToken ct) => Status(id, AppointmentStatus.InWaitingRoom, null, ct);

    /// <summary>Inicia la consulta.</summary>
    [HttpPost("{id:guid}/start"), HasPermission(P.AppointmentsWrite)]
    public Task<AppointmentDto> Start(Guid id, CancellationToken ct) => Status(id, AppointmentStatus.InProgress, null, ct);

    [HttpPost("{id:guid}/attend"), HasPermission(P.AppointmentsWrite)]
    public Task<AppointmentDto> Attend(Guid id, CancellationToken ct) => Status(id, AppointmentStatus.Attended, null, ct);

    /// <summary>Registra ausencia (sólo luego del horario del turno).</summary>
    [HttpPost("{id:guid}/no-show"), HasPermission(P.AppointmentsWrite)]
    public Task<AppointmentDto> NoShow(Guid id, CancellationToken ct) => Status(id, AppointmentStatus.NoShow, null, ct);

    [HttpPost("{id:guid}/cancel"), HasPermission(P.AppointmentsWrite)]
    public Task<AppointmentDto> Cancel(Guid id, CancelAppointmentRequest request, CancellationToken ct) => Status(id, AppointmentStatus.Cancelled, request.Reason, ct);

    [HttpGet("{id:guid}/history"), HasPermission(P.AppointmentsRead)]
    public Task<IReadOnlyList<AppointmentHistoryDto>> History(Guid id, CancellationToken ct) => service.GetHistoryAsync(id, ct);

    /// <summary>Envía un recordatorio. El estado devuelto refleja el resultado real del proveedor (NotSentNoProvider si no hay proveedor).</summary>
    [HttpPost("{id:guid}/reminder"), HasPermission(P.MessagingSend)]
    public Task<OutboundMessageDto> Reminder(Guid id, [FromQuery] MessageChannel channel, CancellationToken ct) =>
        notifications.SendAppointmentReminderAsync(id, channel == 0 ? MessageChannel.WhatsApp : channel, ct);

    private Task<AppointmentDto> Status(Guid id, AppointmentStatus status, string? reason, CancellationToken ct) =>
        service.ChangeStatusAsync(id, new ChangeAppointmentStatusRequest(status, reason), ct);
}

/// <summary>Tareas internas del consultorio.</summary>
[Route("api/v{version:apiVersion}/tasks")]
public sealed class TasksController(ITaskService service) : ApiControllerBase
{
    /// <summary>Listado con filtros. view: open | overdue | today | upcoming | completed. Orden: due, priority, created, status.</summary>
    [HttpGet, HasPermission(P.TasksRead)]
    public Task<PagedResult<TaskDto>> Search([FromQuery] TaskQuery query, CancellationToken ct) => service.SearchAsync(query, ct);

    [HttpPost, HasPermission(P.TasksWrite)]
    [ProducesResponseType<TaskDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(SaveTaskRequest request, CancellationToken ct)
    {
        var t = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = t.Id, version = "1" }, t);
    }

    [HttpGet("{id:guid}"), HasPermission(P.TasksRead)]
    public Task<TaskDto> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);

    [HttpPut("{id:guid}"), HasPermission(P.TasksWrite)]
    public Task<TaskDto> Update(Guid id, SaveTaskRequest request, CancellationToken ct) => service.UpdateAsync(id, request, ct);

    /// <summary>Asigna o reasigna el responsable.</summary>
    [HttpPut("{id:guid}/assignee"), HasPermission(P.TasksWrite)]
    public Task<TaskDto> Assign(Guid id, AssignTaskRequest request, CancellationToken ct) => service.AssignAsync(id, request, ct);

    /// <summary>Cambia el estado (p. ej. Completed).</summary>
    [HttpPut("{id:guid}/status"), HasPermission(P.TasksWrite)]
    public Task<TaskDto> ChangeStatus(Guid id, ChangeTaskStatusRequest request, CancellationToken ct) => service.ChangeStatusAsync(id, request.Status, ct);
}
