using FluentValidation;
using Odontia.Application.Features.Scheduling;

namespace Odontia.Application.Validation;

public sealed class CreateAppointmentRequestValidator : AbstractValidator<CreateAppointmentRequest>
{
    public CreateAppointmentRequestValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty();
        RuleFor(x => x.ProfessionalUserId).NotEmpty();
        RuleFor(x => x.Start).NotEmpty();
        RuleFor(x => x.DurationMinutes).InclusiveBetween(5, 480).When(x => x.DurationMinutes is not null);
        RuleFor(x => x.Reason).Text(250);
        RuleFor(x => x.AdministrativeNotes).Text(1000);
    }
}
