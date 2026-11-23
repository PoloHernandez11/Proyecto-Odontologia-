using FluentValidation;
using Odontia.Application.Features.Finance;

namespace Odontia.Application.Validation;

public sealed class VoidExpenseRequestValidator : AbstractValidator<VoidExpenseRequest>
{
    public VoidExpenseRequestValidator() => RuleFor(x => x.Reason).Required(500, "El motivo de anulación");
}
