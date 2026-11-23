using FluentValidation;
using Odontia.Application.Features.Scheduling;

namespace Odontia.Application.Validation;

public sealed class SetWorkingHoursRequestValidator : AbstractValidator<SetWorkingHoursRequest>
{
    public SetWorkingHoursRequestValidator()
    {
        RuleFor(x => x.Slots).NotNull().Must(s => s.Count <= 50).WithMessage("Máximo 50 franjas.");
        RuleForEach(x => x.Slots).ChildRules(s =>
        {
            s.RuleFor(x => x.DayOfWeek).IsInEnum();
            s.RuleFor(x => x.EndTime).GreaterThan(x => x.StartTime).WithMessage("La hora de fin debe ser posterior a la de inicio.");
        });
    }
}
