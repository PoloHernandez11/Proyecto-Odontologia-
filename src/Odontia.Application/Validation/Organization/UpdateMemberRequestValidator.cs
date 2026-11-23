using FluentValidation;
using Odontia.Application.Features.Organization;

namespace Odontia.Application.Validation;

public sealed class UpdateMemberRequestValidator : AbstractValidator<UpdateMemberRequest>
{
    public UpdateMemberRequestValidator()
    {
        RuleFor(x => x.DefaultAppointmentMinutes).InclusiveBetween(5, 480).When(x => x.DefaultAppointmentMinutes is not null);
        RuleFor(x => x.AgendaColor).Matches("^#[0-9A-Fa-f]{6}$").When(x => x.AgendaColor is not null).WithMessage("Color hexadecimal inválido (#RRGGBB).");
    }
}
