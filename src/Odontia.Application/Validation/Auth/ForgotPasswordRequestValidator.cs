using FluentValidation;
using Odontia.Application.Features.Auth;

namespace Odontia.Application.Validation;

public sealed class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator() => RuleFor(x => x.Email).Email();
}
