using Microsoft.AspNetCore.Mvc;
using Odontia.Application.Common;
using Odontia.Application.Features.Auth;
using Odontia.Application.Features.Organization;

namespace Odontia.Api.Controllers;

/// <summary>Perfil personal y profesional del usuario autenticado.</summary>
[Route("api/v{version:apiVersion}/profile")]
public sealed class ProfileController(IProfileService service) : ApiControllerBase
{
    [HttpGet]
    public Task<ProfileDto> Get(CancellationToken ct) => service.GetAsync(ct);

    /// <summary>Datos personales.</summary>
    [HttpPut]
    public Task<ProfileDto> Update(UpdateProfileRequest request, CancellationToken ct) => service.UpdateAsync(request, ct);

    /// <summary>Especialidad, matrícula y duración de turno por defecto.</summary>
    [HttpPut("professional")]
    public Task<ProfileDto> UpdateProfessional(UpdateProfessionalProfileRequest request, CancellationToken ct) => service.UpdateProfessionalAsync(request, ct);

    /// <summary>Cambio de correo (requiere contraseña y nueva confirmación).</summary>
    [HttpPut("email")]
    public Task<MessageResponse> ChangeEmail(ChangeEmailRequest request, CancellationToken ct) => service.ChangeEmailAsync(request, ct);

    [HttpPut("notification-settings")]
    public Task<NotificationSettingsDto> UpdateNotifications(NotificationSettingsDto request, CancellationToken ct) => service.UpdateNotificationSettingsAsync(request, ct);
}
