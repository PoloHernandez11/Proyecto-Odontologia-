using Microsoft.AspNetCore.Mvc;
using Odontia.Api.Infrastructure;
using Odontia.Application.Common;
using Odontia.Application.Features.Auth;
using Odontia.Application.Features.Organization;
using P = Odontia.Domain.Authorization.Permissions;

namespace Odontia.Api.Controllers;

/// <summary>Registro de auditoría del consultorio.</summary>
[Route("api/v{version:apiVersion}/audit")]
public sealed class AuditController(IAuditQueryService service) : ApiControllerBase
{
    [HttpGet, HasPermission(P.AuditRead)]
    public Task<PagedResult<AuditLogDto>> Search([FromQuery] AuditQuery query, CancellationToken ct) => service.SearchAsync(query, ct);
}
