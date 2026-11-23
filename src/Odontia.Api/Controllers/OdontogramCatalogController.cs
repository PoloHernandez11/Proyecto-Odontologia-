using Microsoft.AspNetCore.Mvc;
using Odontia.Api.Infrastructure;
using Odontia.Application.Common;
using Odontia.Application.Features.Clinical;
using Odontia.Application.Features.Odontograms;
using Odontia.Application.Features.Treatments;
using Odontia.Domain.Enums;
using P = Odontia.Domain.Authorization.Permissions;

namespace Odontia.Api.Controllers;

/// <summary>Catálogo FDI y condiciones configurables del odontograma.</summary>
[Route("api/v{version:apiVersion}/odontogram")]
public sealed class OdontogramCatalogController(IOdontogramService service) : ApiControllerBase
{
    /// <summary>Piezas FDI (permanentes y temporales) con superficies válidas, superficies y condiciones.</summary>
    [HttpGet("catalog"), HasPermission(P.OdontogramRead)]
    public Task<OdontogramCatalogDto> Catalog(CancellationToken ct) => service.GetCatalogAsync(ct);

    [HttpPost("conditions"), HasPermission(P.CatalogManage)]
    [ProducesResponseType<ConditionDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateCondition(SaveConditionRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.CreateConditionAsync(request, ct));

    [HttpPut("conditions/{id:guid}"), HasPermission(P.CatalogManage)]
    public Task<ConditionDto> UpdateCondition(Guid id, SaveConditionRequest request, CancellationToken ct) => service.UpdateConditionAsync(id, request, ct);
}
