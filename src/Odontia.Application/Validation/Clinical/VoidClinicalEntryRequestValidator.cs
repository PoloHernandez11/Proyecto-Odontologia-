using FluentValidation;
using Odontia.Application.Features.Clinical;

namespace Odontia.Application.Validation;

public sealed class VoidClinicalEntryRequestValidator : AbstractValidator<VoidClinicalEntryRequest>
{
    public VoidClinicalEntryRequestValidator() => RuleFor(x => x.Reason).Required(500, "El motivo");
}
