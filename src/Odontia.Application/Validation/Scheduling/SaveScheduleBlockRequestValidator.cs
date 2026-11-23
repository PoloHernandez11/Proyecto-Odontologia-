using FluentValidation;
using Odontia.Application.Features.Scheduling;

namespace Odontia.Application.Validation;

public sealed class SaveScheduleBlockRequestValidator : AbstractValidator<SaveScheduleBlockRequest>
{
    public SaveScheduleBlockRequestValidator()
    {
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.End).GreaterThan(x => x.Start).WithMessage("El fin debe ser posterior al inicio.");
        RuleFor(x => x.Reason).Text(200);
    }
}
