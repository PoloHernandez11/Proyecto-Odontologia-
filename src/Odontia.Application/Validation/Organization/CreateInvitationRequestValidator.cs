using FluentValidation;
using Odontia.Application.Features.Organization;

namespace Odontia.Application.Validation;

public sealed class CreateInvitationRequestValidator : AbstractValidator<CreateInvitationRequest>
{
    public CreateInvitationRequestValidator()
    {
        RuleFor(x => x.Email).Email();
        RuleFor(x => x.RoleIds).NotEmpty().WithMessage("Debe indicar al menos un rol.");
    }
}
