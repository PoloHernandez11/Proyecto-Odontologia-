using FluentValidation;
using Odontia.Application.Features.Scheduling;

namespace Odontia.Application.Validation;

public sealed class NonWorkingDayRequestValidator : AbstractValidator<NonWorkingDayRequest>
{
    public NonWorkingDayRequestValidator() => RuleFor(x => x.Reason).Text(200);
}
