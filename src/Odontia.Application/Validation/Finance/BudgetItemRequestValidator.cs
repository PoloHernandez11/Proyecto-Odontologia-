using FluentValidation;
using Odontia.Application.Features.Finance;

namespace Odontia.Application.Validation;

public sealed class BudgetItemRequestValidator : AbstractValidator<BudgetItemRequest>
{
    public BudgetItemRequestValidator()
    {
        RuleFor(x => x).Must(x => x.ProcedureId is not null || !string.IsNullOrWhiteSpace(x.Description))
            .WithName("description").WithMessage("Indique una prestación o una descripción.");
        RuleFor(x => x.Description).Text(200);
        RuleFor(x => x.Quantity).InclusiveBetween(1, 1000);
        RuleFor(x => x.UnitPrice!.Value).Money().When(x => x.UnitPrice is not null);
        RuleFor(x => x.DiscountPercent).InclusiveBetween(0, 100).PrecisionScale(5, 2, true);
        RuleFor(x => x.ToothCode).Tooth();
    }
}
