using FluentValidation;
using Odontia.Application.Features.Organization;

namespace Odontia.Application.Validation;

public sealed class UpdateMemberRolesRequestValidator : AbstractValidator<UpdateMemberRolesRequest>
{
    public UpdateMemberRolesRequestValidator() => RuleFor(x => x.RoleIds).NotEmpty().WithMessage("Debe indicar al menos un rol.");
}
