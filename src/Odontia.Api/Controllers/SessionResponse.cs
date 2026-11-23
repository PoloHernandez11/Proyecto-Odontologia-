using Odontia.Application.Common;
using Odontia.Application.Features.Auth;

namespace Odontia.Api.Controllers;

/// <summary>Respuesta de sesión para el frontend (access token + token CSRF para renovar).</summary>
public sealed record SessionResponse(AuthSessionDto Session, string CsrfToken);
