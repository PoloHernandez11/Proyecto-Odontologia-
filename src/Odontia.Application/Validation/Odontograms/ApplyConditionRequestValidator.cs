using FluentValidation;
using Odontia.Application.Features.Odontograms;
using Odontia.Domain.Dental;

namespace Odontia.Application.Validation;

public sealed class ApplyConditionRequestValidator : AbstractValidator<ApplyConditionRequest>
{
    public ApplyConditionRequestValidator()
    {
        RuleFor(x => x.ToothCode).Must(Fdi.IsValid).WithMessage("Pieza dental FDI inválida.");
        RuleFor(x => x.ConditionId).NotEmpty();
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Notes).Text(500);
    }
}
