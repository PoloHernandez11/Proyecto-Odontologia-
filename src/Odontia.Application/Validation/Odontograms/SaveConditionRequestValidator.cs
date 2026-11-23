using FluentValidation;
using Odontia.Application.Features.Odontograms;

namespace Odontia.Application.Validation;

public sealed class SaveConditionRequestValidator : AbstractValidator<SaveConditionRequest>
{
    public SaveConditionRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(30).Matches("^[A-Za-z0-9_]+$");
        RuleFor(x => x.Name).Required(80, "El nombre");
        RuleFor(x => x.Scope).IsInEnum();
        RuleFor(x => x.Color).Matches("^#[0-9A-Fa-f]{6}$").When(x => x.Color is not null);
    }
}
