using FluentValidation;
using Odontia.Application.Features.Finance;

namespace Odontia.Application.Validation;

public sealed class VoidPaymentRequestValidator : AbstractValidator<VoidPaymentRequest>
{
    public VoidPaymentRequestValidator() => RuleFor(x => x.Reason).Required(500, "El motivo de anulación");
}
