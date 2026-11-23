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

/// <summary>Presupuestos: borrador, emisión, aprobación (genera cargo), rechazo, cancelación e historial.</summary>
[Route("api/v{version:apiVersion}")]
public sealed class BudgetsController(IBudgetService service) : ApiControllerBase
{
    [HttpGet("budgets"), HasPermission(P.BudgetsRead)]
    public Task<PagedResult<BudgetDto>> Search([FromQuery] BudgetQuery query, CancellationToken ct) => service.SearchAsync(query, ct);

    /// <summary>Crea un presupuesto en borrador (líneas manuales o importadas del plan de tratamiento).</summary>
    [HttpPost("patients/{patientId:guid}/budgets"), HasPermission(P.BudgetsWrite)]
    [ProducesResponseType<BudgetDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(Guid patientId, SaveBudgetRequest request, CancellationToken ct)
    {
        var b = await service.CreateAsync(patientId, request, ct);
        return CreatedAtAction(nameof(Get), new { id = b.Id, version = "1" }, b);
    }

    [HttpGet("budgets/{id:guid}"), HasPermission(P.BudgetsRead)]
    public Task<BudgetDto> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);

    /// <summary>Modifica un presupuesto en borrador (reemplaza las líneas).</summary>
    [HttpPut("budgets/{id:guid}"), HasPermission(P.BudgetsWrite)]
    public Task<BudgetDto> Update(Guid id, SaveBudgetRequest request, CancellationToken ct) => service.UpdateAsync(id, request, ct);

    [HttpPost("budgets/{id:guid}/issue"), HasPermission(P.BudgetsWrite)]
    public Task<BudgetDto> Issue(Guid id, CancellationToken ct) => service.IssueAsync(id, ct);

    [HttpPost("budgets/{id:guid}/approve"), HasPermission(P.BudgetsApprove)]
    public Task<BudgetDto> Approve(Guid id, CancellationToken ct) => service.ApproveAsync(id, ct);

    [HttpPost("budgets/{id:guid}/reject"), HasPermission(P.BudgetsApprove)]
    public Task<BudgetDto> Reject(Guid id, BudgetDecisionRequest request, CancellationToken ct) => service.RejectAsync(id, request, ct);

    [HttpPost("budgets/{id:guid}/cancel"), HasPermission(P.BudgetsApprove)]
    public Task<BudgetDto> Cancel(Guid id, BudgetDecisionRequest request, CancellationToken ct) => service.CancelAsync(id, request, ct);

    [HttpGet("budgets/{id:guid}/history"), HasPermission(P.BudgetsRead)]
    public Task<IReadOnlyList<BudgetStatusChangeDto>> History(Guid id, CancellationToken ct) => service.GetHistoryAsync(id, ct);
}
