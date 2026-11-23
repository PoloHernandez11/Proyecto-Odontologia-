using FluentValidation;
using Odontia.Application.Features.Clinical;
using Odontia.Domain.Dental;

namespace Odontia.Application.Validation;

public sealed class AmendClinicalEntryRequestValidator : AbstractValidator<AmendClinicalEntryRequest>
{
    public AmendClinicalEntryRequestValidator()
    {
        RuleFor(x => x.Title).Required(200, "El procedimiento / título");
        RuleFor(x => x.Description).Text(8000);
        RuleFor(x => x.Indications).Text(2000);
        RuleFor(x => x.Reason).Required(500, "El motivo de la corrección");
        RuleForEach(x => x.Teeth).Must(Fdi.IsValid).WithMessage("Pieza dental FDI inválida: {PropertyValue}.");
    }
}
