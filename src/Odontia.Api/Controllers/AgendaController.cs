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
