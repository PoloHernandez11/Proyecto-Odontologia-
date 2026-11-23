using FluentValidation;
using Odontia.Application.Features.Scheduling;

namespace Odontia.Application.Validation;

public sealed class UpdateAppointmentRequestValidator : AbstractValidator<UpdateAppointmentRequest>
{
    public UpdateAppointmentRequestValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty();
        RuleFor(x => x.Version).NotEmpty();
        RuleFor(x => x.Reason).Text(250);
        RuleFor(x => x.AdministrativeNotes).Text(1000);
    }
}
