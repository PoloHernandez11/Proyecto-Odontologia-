using FluentValidation;
using Odontia.Application.Features.Organization;

namespace Odontia.Application.Validation;

public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.FirstName).Required(80, "El nombre");
        RuleFor(x => x.LastName).Required(80, "El apellido");
        RuleFor(x => x.Title).Text(20);
        RuleFor(x => x.Phone).Text(40);
    }
}
