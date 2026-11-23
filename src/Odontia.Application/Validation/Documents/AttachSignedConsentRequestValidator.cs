using FluentValidation;
using Odontia.Application.Features.Documents;

namespace Odontia.Application.Validation;

public sealed class AttachSignedConsentRequestValidator : AbstractValidator<AttachSignedConsentRequest>
{
    public AttachSignedConsentRequestValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty();
        RuleFor(x => x.SignedByName).Required(150, "El nombre de quien firma");
    }
}
