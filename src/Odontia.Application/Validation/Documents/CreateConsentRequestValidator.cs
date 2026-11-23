using FluentValidation;
using Odontia.Application.Features.Documents;

namespace Odontia.Application.Validation;

public sealed class CreateConsentRequestValidator : AbstractValidator<CreateConsentRequest>
{
    public CreateConsentRequestValidator()
    {
        RuleFor(x => x.Title).Required(200, "El título");
        RuleFor(x => x.Content).Required(20000, "El texto del consentimiento");
    }
}
