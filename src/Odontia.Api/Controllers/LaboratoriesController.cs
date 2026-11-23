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

/// <summary>Laboratorios externos.</summary>
[Route("api/v{version:apiVersion}/laboratories")]
public sealed class LaboratoriesController(ILabService service) : ApiControllerBase
{
    /// <summary>Requiere lab.read o laboratories.manage.</summary>
    [HttpGet]
    public Task<IReadOnlyList<LaboratoryDto>> List([FromQuery] bool includeInactive, CancellationToken ct) => service.GetLaboratoriesAsync(includeInactive, ct);

    [HttpPost, HasPermission(P.LaboratoriesManage)]
    [ProducesResponseType<LaboratoryDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(SaveLaboratoryRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.CreateLaboratoryAsync(request, ct));

    [HttpPut("{id:guid}"), HasPermission(P.LaboratoriesManage)]
    public Task<LaboratoryDto> Update(Guid id, SaveLaboratoryRequest request, CancellationToken ct) => service.UpdateLaboratoryAsync(id, request, ct);
}
