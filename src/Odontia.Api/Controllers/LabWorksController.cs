using Microsoft.AspNetCore.Mvc;
using Odontia.Api.Infrastructure;
using Odontia.Application.Common;
using Odontia.Application.Features.Dashboard;
using Odontia.Application.Features.Documents;
using Odontia.Application.Features.Finance;
using Odontia.Application.Features.Lab;
using Odontia.Application.Features.Notifications;
using Odontia.Domain.Enums;
using P = Odontia.Domain.Authorization.Permissions;

namespace Odontia.Api.Controllers;

/// <summary>Trabajos de laboratorio y seguimiento de entregas.</summary>
[Route("api/v{version:apiVersion}/lab-works")]
public sealed class LabWorksController(ILabService service) : ApiControllerBase
{
    /// <summary>Listado. view: open | overdue | this_week | closed.</summary>
    [HttpGet, HasPermission(P.LabRead)]
    public Task<PagedResult<LabWorkDto>> Search([FromQuery] LabWorkQuery query, CancellationToken ct) => service.SearchAsync(query, ct);

    /// <summary>Tablero: conteos por estado, atrasados y entregas de la semana.</summary>
    [HttpGet("board"), HasPermission(P.LabRead)]
    public Task<LabBoardDto> Board(CancellationToken ct) => service.GetBoardAsync(ct);

    [HttpPost, HasPermission(P.LabWrite)]
    [ProducesResponseType<LabWorkDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(SaveLabWorkRequest request, CancellationToken ct)
    {
        var w = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = w.Id, version = "1" }, w);
    }

    [HttpGet("{id:guid}"), HasPermission(P.LabRead)]
    public Task<LabWorkDto> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);

    [HttpPut("{id:guid}"), HasPermission(P.LabWrite)]
    public Task<LabWorkDto> Update(Guid id, SaveLabWorkRequest request, CancellationToken ct) => service.UpdateAsync(id, request, ct);

    /// <summary>Avance logístico (lab.track o lab.write). Colocado/Cancelado requieren lab.write.</summary>
    [HttpPost("{id:guid}/status")]
    public Task<LabWorkDto> ChangeStatus(Guid id, ChangeLabWorkStatusRequest request, CancellationToken ct) => service.ChangeStatusAsync(id, request, ct);

    [HttpGet("{id:guid}/history"), HasPermission(P.LabRead)]
    public Task<IReadOnlyList<LabWorkChangeDto>> History(Guid id, CancellationToken ct) => service.GetHistoryAsync(id, ct);
}
