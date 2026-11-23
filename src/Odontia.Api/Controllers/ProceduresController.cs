using Microsoft.AspNetCore.Mvc;
using Odontia.Api.Infrastructure;
using Odontia.Application.Common;
using Odontia.Application.Features.Clinical;
using Odontia.Application.Features.Odontograms;
using Odontia.Application.Features.Treatments;
using Odontia.Domain.Enums;
using P = Odontia.Domain.Authorization.Permissions;

namespace Odontia.Api.Controllers;

/// <summary>Catálogo de prestaciones y valores.</summary>
[Route("api/v{version:apiVersion}/procedures")]
public sealed class ProceduresController(ITreatmentService service) : ApiControllerBase
{
    /// <summary>Requiere treatments.read, budgets.read o catalog.manage.</summary>
    [HttpGet]
    public Task<PagedResult<ProcedureDto>> List([FromQuery] ProcedureQuery query, CancellationToken ct) => service.GetProceduresAsync(query, ct);

    [HttpPost, HasPermission(P.CatalogManage)]
    [ProducesResponseType<ProcedureDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(SaveProcedureRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.CreateProcedureAsync(request, ct));

    /// <summary>Actualiza la prestación. Los planes/presupuestos existentes conservan su valor histórico.</summary>
    [HttpPut("{id:guid}"), HasPermission(P.CatalogManage)]
    public Task<ProcedureDto> Update(Guid id, SaveProcedureRequest request, CancellationToken ct) => service.UpdateProcedureAsync(id, request, ct);
}
