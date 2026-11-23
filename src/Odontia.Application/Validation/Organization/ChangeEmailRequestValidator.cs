using FluentValidation;
using Odontia.Application.Features.Organization;

namespace Odontia.Application.Validation;

public sealed class ChangeEmailRequestValidator : AbstractValidator<ChangeEmailRequest>
{
    public ChangeEmailRequestValidator()
    {
        RuleFor(x => x.NewEmail).Email();
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
    }
}
