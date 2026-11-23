using FluentValidation;
using Odontia.Application.Features.Odontograms;

namespace Odontia.Application.Validation;

public sealed class ApplyConditionsRequestValidator : AbstractValidator<ApplyConditionsRequest>
{
    public ApplyConditionsRequestValidator()
    {
        RuleFor(x => x.Items).NotEmpty().Must(i => i.Count <= 64);
        RuleForEach(x => x.Items).SetValidator(new ApplyConditionRequestValidator());
    }
}
