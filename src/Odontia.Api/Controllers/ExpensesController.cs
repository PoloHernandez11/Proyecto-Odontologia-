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

/// <summary>Egresos del consultorio y resumen de ingresos y egresos por período.</summary>
[Route("api/v{version:apiVersion}")]
public sealed class ExpensesController(IExpenseService service) : ApiControllerBase
{
    [HttpGet("expenses"), HasPermission(P.FinanceRead)]
    public Task<PagedResult<ExpenseDto>> Search([FromQuery] ExpenseQuery query, CancellationToken ct) => service.SearchAsync(query, ct);

    /// <summary>Registra un egreso (insumos, laboratorio, alquiler, servicios, etc.).</summary>
    [HttpPost("expenses"), HasPermission(P.PaymentsWrite)]
    [ProducesResponseType<ExpenseDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Register(RegisterExpenseRequest request, CancellationToken ct)
    {
        var e = await service.RegisterAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = e.Id, version = "1" }, e);
    }

    [HttpGet("expenses/{id:guid}"), HasPermission(P.FinanceRead)]
    public Task<ExpenseDto> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);

    /// <summary>Anulación controlada con motivo: el egreso no se elimina.</summary>
    [HttpPost("expenses/{id:guid}/void"), HasPermission(P.PaymentsVoid)]
    public Task<ExpenseDto> Void(Guid id, VoidExpenseRequest request, CancellationToken ct) => service.VoidAsync(id, request, ct);

    /// <summary>Ingresos (pagos de pacientes), egresos y balance neto de un período. No incluye movimientos anulados.</summary>
    [HttpGet("finance/summary"), HasPermission(P.FinanceRead)]
    public Task<FinanceSummaryDto> Summary([FromQuery] FinanceSummaryQuery query, CancellationToken ct) => service.GetSummaryAsync(query, ct);
}
