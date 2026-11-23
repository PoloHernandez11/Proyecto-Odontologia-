using FluentValidation;
using Odontia.Application.Features.Auth;

namespace Odontia.Application.Validation;

public sealed class TwoFactorLoginRequestValidator : AbstractValidator<TwoFactorLoginRequest>
{
    public TwoFactorLoginRequestValidator()
    {
        RuleFor(x => x.ChallengeToken).NotEmpty().MaximumLength(2048);
        RuleFor(x => x).Must(x => !string.IsNullOrWhiteSpace(x.Code) || !string.IsNullOrWhiteSpace(x.RecoveryCode))
            .WithName("code").WithMessage("Indique el código o un código de recuperación.");
        RuleFor(x => x.Code).Text(10);
        RuleFor(x => x.RecoveryCode).Text(20);
    }
}
