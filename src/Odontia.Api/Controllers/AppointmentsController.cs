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
