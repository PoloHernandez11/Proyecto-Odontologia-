using FluentValidation;
using Odontia.Application.Features.Treatments;

namespace Odontia.Application.Validation;

public sealed class AddPlanItemRequestValidator : AbstractValidator<AddPlanItemRequest>
{
    public AddPlanItemRequestValidator()
    {
        RuleFor(x => x.ProcedureId).NotEmpty();
        RuleFor(x => x.ToothCode).Tooth();
        RuleFor(x => x.Notes).Text(1000);
    }
}
