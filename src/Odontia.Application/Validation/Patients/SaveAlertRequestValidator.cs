using FluentValidation;
using Odontia.Application.Features.Patients;

namespace Odontia.Application.Validation;

public sealed class SaveAlertRequestValidator : AbstractValidator<SaveAlertRequest>
{
    public SaveAlertRequestValidator()
    {
        RuleFor(x => x.Title).Required(150, "El título");
        RuleFor(x => x.Details).Text(1000);
        RuleFor(x => x.Severity).IsInEnum();
    }
}
