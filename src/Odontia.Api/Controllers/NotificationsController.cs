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

/// <summary>Notificaciones internas del usuario y mensajes salientes (recordatorios).</summary>
[Route("api/v{version:apiVersion}")]
public sealed class NotificationsController(INotificationService service) : ApiControllerBase
{
    [HttpGet("notifications"), RequireConsultorio]
    public Task<PagedResult<NotificationDto>> Mine([FromQuery] NotificationQuery query, CancellationToken ct) => service.GetMineAsync(query, ct);

    [HttpGet("notifications/unread-count"), RequireConsultorio]
    public Task<UnreadCountDto> Unread(CancellationToken ct) => service.GetUnreadCountAsync(ct);

    [HttpPost("notifications/{id:guid}/read"), RequireConsultorio]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        await service.MarkReadAsync(id, ct);
        return NoContent();
    }

    [HttpPost("notifications/read-all"), RequireConsultorio]
    public async Task<UpdatedCountDto> MarkAllRead(CancellationToken ct) => new(await service.MarkAllReadAsync(ct));

    /// <summary>Registro de mensajes salientes y su estado real (Sent sólo con confirmación del proveedor).</summary>
    [HttpGet("messages"), HasPermission(P.MessagingSend)]
    public Task<PagedResult<OutboundMessageDto>> Outbound([FromQuery] OutboundMessageQuery query, CancellationToken ct) => service.GetOutboundAsync(query, ct);

    /// <summary>Envía recordatorios para los turnos pendientes/confirmados de una fecha.</summary>
    [HttpPost("messages/reminders"), HasPermission(P.MessagingSend)]
    public Task<SendRemindersResultDto> SendReminders(SendRemindersRequest request, CancellationToken ct) => service.SendDayRemindersAsync(request, ct);
}
