using FluentValidation;
using Odontia.Application.Features.Organization;

namespace Odontia.Application.Validation;

public sealed class SaveGabineteRequestValidator : AbstractValidator<SaveGabineteRequest>
{
    public SaveGabineteRequestValidator()
    {
        RuleFor(x => x.Name).Required(80, "El nombre");
        RuleFor(x => x.Description).Text(250);
    }
}
