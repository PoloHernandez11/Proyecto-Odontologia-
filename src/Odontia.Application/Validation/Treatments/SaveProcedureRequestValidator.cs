using FluentValidation;
using Odontia.Application.Features.Treatments;

namespace Odontia.Application.Validation;

public sealed class SaveProcedureRequestValidator : AbstractValidator<SaveProcedureRequest>
{
    public SaveProcedureRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(30).Matches("^[A-Za-z0-9_.\\-]+$");
        RuleFor(x => x.Name).Required(150, "El nombre");
        RuleFor(x => x.Description).Text(500);
        RuleFor(x => x.Price).Money();
        RuleFor(x => x.DefaultDurationMinutes).InclusiveBetween(5, 480).When(x => x.DefaultDurationMinutes is not null);
    }
}
