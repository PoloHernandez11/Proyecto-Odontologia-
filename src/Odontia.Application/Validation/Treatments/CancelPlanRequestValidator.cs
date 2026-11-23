using FluentValidation;
using Odontia.Application.Features.Treatments;

namespace Odontia.Application.Validation;

public sealed class CancelPlanRequestValidator : AbstractValidator<CancelPlanRequest>
{
    public CancelPlanRequestValidator() => RuleFor(x => x.Reason).Required(500, "El motivo");
}
