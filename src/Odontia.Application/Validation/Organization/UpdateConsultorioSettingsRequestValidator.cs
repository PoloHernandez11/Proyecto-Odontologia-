using FluentValidation;
using Odontia.Application.Features.Organization;

namespace Odontia.Application.Validation;

public sealed class UpdateConsultorioSettingsRequestValidator : AbstractValidator<UpdateConsultorioSettingsRequest>
{
    public UpdateConsultorioSettingsRequestValidator()
    {
        RuleFor(x => x.TimeZoneId).NotEmpty().MaximumLength(64);
        RuleFor(x => x.DefaultAppointmentMinutes).InclusiveBetween(5, 480);
        RuleFor(x => x.SlotIntervalMinutes).Must(v => v is 5 or 10 or 15 or 20 or 30 or 60).WithMessage("Intervalo inválido.");
        RuleFor(x => x.ReminderHoursBefore).InclusiveBetween(0, 168);
    }
}
