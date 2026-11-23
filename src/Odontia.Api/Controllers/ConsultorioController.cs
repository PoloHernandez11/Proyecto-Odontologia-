using Microsoft.AspNetCore.Mvc;
using Odontia.Api.Infrastructure;
using Odontia.Application.Common;
using Odontia.Application.Features.Auth;
using Odontia.Application.Features.Organization;
using P = Odontia.Domain.Authorization.Permissions;

namespace Odontia.Api.Controllers;

/// <summary>Datos y configuración del consultorio activo, consultorios físicos y profesionales.</summary>
[Route("api/v{version:apiVersion}/consultorio")]
public sealed class ConsultorioController(IConsultorioService service) : ApiControllerBase
{
    /// <summary>Datos del consultorio activo.</summary>
    [HttpGet, HasPermission(P.ConsultorioRead)]
    public Task<ConsultorioDto> Get(CancellationToken ct) => service.GetCurrentAsync(ct);

    /// <summary>Actualiza nombre, datos de contacto y dirección.</summary>
    [HttpPut, HasPermission(P.ConsultorioManage)]
    public Task<ConsultorioDto> Update(UpdateConsultorioRequest request, CancellationToken ct) => service.UpdateAsync(request, ct);

    /// <summary>Configuración general: zona horaria, duración de turno, intervalo de agenda, recordatorios.</summary>
    [HttpPut("settings"), HasPermission(P.ConsultorioManage)]
    public Task<ConsultorioDto> UpdateSettings(UpdateConsultorioSettingsRequest request, CancellationToken ct) => service.UpdateSettingsAsync(request, ct);

    /// <summary>Consultorios físicos / gabinetes.</summary>
    [HttpGet("gabinetes"), HasPermission(P.ConsultorioRead)]
    public Task<IReadOnlyList<GabineteDto>> Gabinetes([FromQuery] bool includeInactive, CancellationToken ct) => service.GetGabinetesAsync(includeInactive, ct);

    [HttpPost("gabinetes"), HasPermission(P.ConsultorioManage)]
    [ProducesResponseType<GabineteDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateGabinete(SaveGabineteRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.CreateGabineteAsync(request, ct));

    [HttpPut("gabinetes/{id:guid}"), HasPermission(P.ConsultorioManage)]
    public Task<GabineteDto> UpdateGabinete(Guid id, SaveGabineteRequest request, CancellationToken ct) => service.UpdateGabineteAsync(id, request, ct);

    /// <summary>Profesionales que atienden (sólo el propio si no tiene acceso a todas las agendas).</summary>
    [HttpGet("professionals"), HasPermission(P.AgendaRead)]
    public Task<IReadOnlyList<ProfessionalDto>> Professionals(CancellationToken ct) => service.GetProfessionalsAsync(ct);
}
