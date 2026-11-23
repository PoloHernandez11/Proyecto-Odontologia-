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
