using FluentValidation;
using Odontia.Application.Features.Finance;

namespace Odontia.Application.Validation;

public sealed class RegisterPaymentRequestValidator : AbstractValidator<RegisterPaymentRequest>
{
    public RegisterPaymentRequestValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("El importe debe ser mayor que cero.").Money();
        RuleFor(x => x.Method).IsInEnum();
        RuleFor(x => x.Reference).Text(100);
        RuleFor(x => x.Notes).Text(500);
    }
}
