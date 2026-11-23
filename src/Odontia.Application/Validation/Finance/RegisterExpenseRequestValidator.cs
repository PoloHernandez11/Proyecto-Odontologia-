using FluentValidation;
using Odontia.Application.Features.Finance;

namespace Odontia.Application.Validation;

public sealed class RegisterExpenseRequestValidator : AbstractValidator<RegisterExpenseRequest>
{
    public RegisterExpenseRequestValidator()
    {
        RuleFor(x => x.Description).Required(250, "El concepto");
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("El importe debe ser mayor que cero.").Money();
        RuleFor(x => x.Method).IsInEnum();
        RuleFor(x => x.Supplier).Text(150);
        RuleFor(x => x.Reference).Text(100);
        RuleFor(x => x.Notes).Text(500);
    }
}
