using FluentValidation;
using Odontia.Application.Features.Patients;

namespace Odontia.Application.Validation;

public sealed class SaveHealthInsuranceRequestValidator : AbstractValidator<SaveHealthInsuranceRequest>
{
    public SaveHealthInsuranceRequestValidator()
    {
        RuleFor(x => x.Name).Required(120, "El nombre");
        RuleFor(x => x.Code).Text(30);
        RuleFor(x => x.Phone).Text(40);
        RuleFor(x => x.Notes).Text(500);
    }
}
