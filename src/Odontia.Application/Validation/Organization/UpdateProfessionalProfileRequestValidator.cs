using FluentValidation;
using Odontia.Application.Features.Organization;

namespace Odontia.Application.Validation;

public sealed class UpdateProfessionalProfileRequestValidator : AbstractValidator<UpdateProfessionalProfileRequest>
{
    public UpdateProfessionalProfileRequestValidator()
    {
        RuleFor(x => x.Specialty).Text(120);
        RuleFor(x => x.LicenseNumber).Text(40);
        RuleFor(x => x.DefaultAppointmentMinutes).InclusiveBetween(5, 480).When(x => x.DefaultAppointmentMinutes is not null);
    }
}
