using FluentValidation;
using Odontia.Application.Features.Documents;

namespace Odontia.Application.Validation;

public sealed class DeleteDocumentRequestValidator : AbstractValidator<DeleteDocumentRequest>
{
    public DeleteDocumentRequestValidator() => RuleFor(x => x.Reason).Required(250, "El motivo");
}
