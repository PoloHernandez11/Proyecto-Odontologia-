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

/// <summary>Pagos y cuenta corriente (saldos calculados en el servidor).</summary>
[Route("api/v{version:apiVersion}")]
public sealed class PaymentsController(IPaymentService service) : ApiControllerBase
{
    [HttpGet("payments"), HasPermission(P.FinanceRead)]
    public Task<PagedResult<PaymentDto>> Search([FromQuery] PaymentQuery query, CancellationToken ct) => service.SearchAsync(query, ct);

    /// <summary>Registra un pago (opcionalmente imputado a un presupuesto aprobado, sin exceder su saldo).</summary>
    [HttpPost("payments"), HasPermission(P.PaymentsWrite)]
    [ProducesResponseType<PaymentDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Register(RegisterPaymentRequest request, CancellationToken ct)
    {
        var p = await service.RegisterAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = p.Id, version = "1" }, p);
    }

    [HttpGet("payments/{id:guid}"), HasPermission(P.FinanceRead)]
    public Task<PaymentDto> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);

    /// <summary>Anulación controlada con motivo: genera contraasiento; el pago no se elimina.</summary>
    [HttpPost("payments/{id:guid}/void"), HasPermission(P.PaymentsVoid)]
    public Task<PaymentDto> Void(Guid id, VoidPaymentRequest request, CancellationToken ct) => service.VoidAsync(id, request, ct);

    /// <summary>Cuenta corriente del paciente con movimientos, saldo acumulado y saldos por presupuesto.</summary>
    [HttpGet("patients/{patientId:guid}/account"), HasPermission(P.FinanceRead)]
    public Task<AccountStatementDto> Account(Guid patientId, CancellationToken ct) => service.GetStatementAsync(patientId, ct);
}
