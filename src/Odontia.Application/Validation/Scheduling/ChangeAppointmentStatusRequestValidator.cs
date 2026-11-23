using FluentValidation;
using Odontia.Application.Features.Scheduling;

namespace Odontia.Application.Validation;

public sealed class ChangeAppointmentStatusRequestValidator : AbstractValidator<ChangeAppointmentStatusRequest>
{
    public ChangeAppointmentStatusRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Reason).Text(250);
    }
}
