using FluentValidation;
using Odontia.Application.Features.Documents;

namespace Odontia.Application.Validation;

public sealed class RecordConsentAcceptanceRequestValidator : AbstractValidator<RecordConsentAcceptanceRequest>
{
    public RecordConsentAcceptanceRequestValidator()
    {
        RuleFor(x => x.AcceptedByName).Required(150, "El nombre de quien acepta");
        RuleFor(x => x.AcceptedByDocument).Text(20);
        RuleFor(x => x.Relationship).Text(60);
    }
}
