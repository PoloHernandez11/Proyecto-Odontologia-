using FluentValidation;
using Odontia.Application.Features.Treatments;

namespace Odontia.Application.Validation;

public sealed class ChangeItemStatusRequestValidator : AbstractValidator<ChangeItemStatusRequest>
{
    public ChangeItemStatusRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Note).Text(500);
    }
}
