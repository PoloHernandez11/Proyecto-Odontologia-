using FluentValidation;
using Odontia.Application.Features.Tasks;

namespace Odontia.Application.Validation;

public sealed class SaveTaskRequestValidator : AbstractValidator<SaveTaskRequest>
{
    public SaveTaskRequestValidator()
    {
        RuleFor(x => x.Title).Required(200, "El título");
        RuleFor(x => x.Description).Text(2000);
        RuleFor(x => x.Priority).IsInEnum();
        RuleFor(x => x.Category).IsInEnum();
    }
}
