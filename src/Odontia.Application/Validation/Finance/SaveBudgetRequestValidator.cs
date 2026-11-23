using FluentValidation;
using Odontia.Application.Features.Finance;

namespace Odontia.Application.Validation;

public sealed class SaveBudgetRequestValidator : AbstractValidator<SaveBudgetRequest>
{
    public SaveBudgetRequestValidator()
    {
        RuleFor(x => x.DiscountPercent).InclusiveBetween(0, 100).PrecisionScale(5, 2, true);
        RuleFor(x => x.Notes).Text(2000);
        RuleFor(x => x.ExpirationDate).GreaterThanOrEqualTo(x => x.IssueDate).When(x => x.IssueDate is not null && x.ExpirationDate is not null);
        RuleForEach(x => x.Items).SetValidator(new BudgetItemRequestValidator());
        RuleFor(x => x).Must(x => x.ImportPlanItems || (x.Items?.Count ?? 0) > 0).WithName("items").WithMessage("El presupuesto debe tener al menos una línea.");
    }
}
