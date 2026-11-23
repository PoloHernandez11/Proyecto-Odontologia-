using FluentValidation;
using Odontia.Application.Features.Odontograms;

namespace Odontia.Application.Validation;

public sealed class RemoveFindingRequestValidator : AbstractValidator<RemoveFindingRequest>
{
    public RemoveFindingRequestValidator() => RuleFor(x => x.Reason).Required(250, "El motivo");
}
