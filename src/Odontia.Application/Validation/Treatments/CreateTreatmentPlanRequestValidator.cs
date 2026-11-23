using FluentValidation;
using Odontia.Application.Features.Treatments;

namespace Odontia.Application.Validation;

public sealed class CreateTreatmentPlanRequestValidator : AbstractValidator<CreateTreatmentPlanRequest>
{
    public CreateTreatmentPlanRequestValidator()
    {
        RuleFor(x => x.Title).Required(150, "El título");
        RuleFor(x => x.Notes).Text(2000);
        RuleForEach(x => x.Items).SetValidator(new AddPlanItemRequestValidator());
    }
}
