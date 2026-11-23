using FluentValidation;
using Odontia.Application.Features.Lab;

namespace Odontia.Application.Validation;

public sealed class ChangeLabWorkStatusRequestValidator : AbstractValidator<ChangeLabWorkStatusRequest>
{
    public ChangeLabWorkStatusRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Note).Text(500);
    }
}
