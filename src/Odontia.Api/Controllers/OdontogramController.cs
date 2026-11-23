using Microsoft.AspNetCore.Mvc;
using Odontia.Api.Infrastructure;
using Odontia.Application.Common;
using Odontia.Application.Features.Clinical;
using Odontia.Application.Features.Odontograms;
using Odontia.Application.Features.Treatments;
using Odontia.Domain.Enums;
using P = Odontia.Domain.Authorization.Permissions;

namespace Odontia.Api.Controllers;

/// <summary>Odontograma del paciente: estado actual, registro de condiciones, notas e historial.</summary>
[Route("api/v{version:apiVersion}/patients/{patientId:guid}/odontogram")]
public sealed class OdontogramController(IOdontogramService service) : ApiControllerBase
{
    [HttpGet, HasPermission(P.OdontogramRead)]
    public Task<OdontogramDto> Get(Guid patientId, CancellationToken ct) => service.GetAsync(patientId, ct);

    /// <summary>Aplica una o varias condiciones (pieza o superficie; existente o a realizar) en una operación atómica.</summary>
    [HttpPost("findings"), HasPermission(P.OdontogramWrite)]
    public Task<OdontogramDto> Apply(Guid patientId, ApplyConditionsRequest request, CancellationToken ct) => service.ApplyAsync(patientId, request, ct);

    /// <summary>Marca un hallazgo como resuelto.</summary>
    [HttpPost("findings/{findingId:guid}/resolve"), HasPermission(P.OdontogramWrite)]
    public Task<OdontogramDto> Resolve(Guid patientId, Guid findingId, ResolveFindingRequest request, CancellationToken ct) =>
        service.ResolveAsync(patientId, findingId, request, ct);

    /// <summary>Retira un hallazgo cargado por error (queda en el historial con motivo).</summary>
    [HttpPost("findings/{findingId:guid}/remove"), HasPermission(P.OdontogramWrite)]
    public Task<OdontogramDto> Remove(Guid patientId, Guid findingId, RemoveFindingRequest request, CancellationToken ct) =>
        service.RemoveAsync(patientId, findingId, request, ct);

    [HttpPost("teeth/{toothCode:int}/notes"), HasPermission(P.OdontogramWrite)]
    [ProducesResponseType<ToothNoteDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> AddNote(Guid patientId, int toothCode, AddToothNoteRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.AddNoteAsync(patientId, toothCode, request, ct));

    /// <summary>Historial de modificaciones (filtrable por pieza) con profesional y fecha.</summary>
    [HttpGet("history"), HasPermission(P.OdontogramRead)]
    public Task<PagedResult<OdontogramHistoryDto>> History(Guid patientId, [FromQuery] OdontogramHistoryQuery query, CancellationToken ct) =>
        service.GetHistoryAsync(patientId, query, ct);
}
