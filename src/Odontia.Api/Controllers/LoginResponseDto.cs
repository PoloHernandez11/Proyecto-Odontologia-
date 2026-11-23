using Odontia.Application.Common;
using Odontia.Application.Features.Auth;

namespace Odontia.Api.Controllers;

public sealed record LoginResponseDto(bool RequiresTwoFactor, string? TwoFactorChallengeToken, SessionResponse? Result);
