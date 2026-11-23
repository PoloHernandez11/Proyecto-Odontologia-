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
