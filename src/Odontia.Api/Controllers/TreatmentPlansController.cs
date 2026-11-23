using Microsoft.AspNetCore.Mvc;
using Odontia.Api.Infrastructure;
using Odontia.Application.Common;
using Odontia.Application.Features.Clinical;
using Odontia.Application.Features.Odontograms;
using Odontia.Application.Features.Treatments;
using Odontia.Domain.Enums;
using P = Odontia.Domain.Authorization.Permissions;

namespace Odontia.Api.Controllers;

/// <summary>Planes de tratamiento y prestaciones con estados y progreso.</summary>
[Route("api/v{version:apiVersion}/patients/{patientId:guid}/treatment-plans")]
public sealed class TreatmentPlansController(ITreatmentService service) : ApiControllerBase
{
    [HttpGet, HasPermission(P.TreatmentsRead)]
    public Task<IReadOnlyList<TreatmentPlanDto>> List(Guid patientId, CancellationToken ct) => service.GetPlansAsync(patientId, ct);

    [HttpPost, HasPermission(P.TreatmentsWrite)]
    [ProducesResponseType<TreatmentPlanDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(Guid patientId, CreateTreatmentPlanRequest request, CancellationToken ct)
    {
        var plan = await service.CreatePlanAsync(patientId, request, ct);
        return CreatedAtAction(nameof(Get), new { patientId, planId = plan.Id, version = "1" }, plan);
    }

    [HttpGet("{planId:guid}"), HasPermission(P.TreatmentsRead)]
    public Task<TreatmentPlanDto> Get(Guid patientId, Guid planId, CancellationToken ct) => service.GetPlanAsync(patientId, planId, ct);

    [HttpPut("{planId:guid}"), HasPermission(P.TreatmentsWrite)]
    public Task<TreatmentPlanDto> Update(Guid patientId, Guid planId, UpdateTreatmentPlanRequest request, CancellationToken ct) =>
        service.UpdatePlanAsync(patientId, planId, request, ct);

    /// <summary>Agrega una prestación (valor tomado del catálogo vigente).</summary>
    [HttpPost("{planId:guid}/items"), HasPermission(P.TreatmentsWrite)]
    public Task<TreatmentPlanDto> AddItem(Guid patientId, Guid planId, AddPlanItemRequest request, CancellationToken ct) =>
        service.AddItemAsync(patientId, planId, request, ct);

    /// <summary>Cambia el estado de una prestación (Pending, InProgress, Completed, Cancelled); el plan se recalcula.</summary>
    [HttpPut("{planId:guid}/items/{itemId:guid}/status"), HasPermission(P.TreatmentsWrite)]
    public Task<TreatmentPlanDto> ItemStatus(Guid patientId, Guid planId, Guid itemId, ChangeItemStatusRequest request, CancellationToken ct) =>
        service.ChangeItemStatusAsync(patientId, planId, itemId, request, ct);

    [HttpPost("{planId:guid}/cancel"), HasPermission(P.TreatmentsWrite)]
    public Task<TreatmentPlanDto> Cancel(Guid patientId, Guid planId, CancelPlanRequest request, CancellationToken ct) =>
        service.CancelPlanAsync(patientId, planId, request, ct);

    [HttpGet("{planId:guid}/history"), HasPermission(P.TreatmentsRead)]
    public Task<IReadOnlyList<TreatmentChangeDto>> History(Guid patientId, Guid planId, CancellationToken ct) =>
        service.GetPlanHistoryAsync(patientId, planId, ct);
}
