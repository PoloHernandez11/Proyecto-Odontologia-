using FluentValidation;
using Odontia.Application.Features.Clinical;
using Odontia.Domain.Dental;

namespace Odontia.Application.Validation;

public sealed class CreateClinicalEntryRequestValidator : AbstractValidator<CreateClinicalEntryRequest>
{
    public CreateClinicalEntryRequestValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Title).Required(200, "El procedimiento / título");
        RuleFor(x => x.Description).Text(8000);
        RuleFor(x => x.Indications).Text(2000);
        RuleForEach(x => x.Teeth).Must(Fdi.IsValid).WithMessage("Pieza dental FDI inválida: {PropertyValue}.");
    }
}
