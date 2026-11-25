using FluentValidation;
using Odontia.Application.Features.Finance;
using Odontia.Domain.Entities;

namespace Odontia.Application.Validation;

public sealed class RegisterAccountChargeRequestValidator : AbstractValidator<RegisterAccountChargeRequest>
{
    public RegisterAccountChargeRequestValidator()
    {
        RuleFor(x => x.Description).Required(AccountMovement.DescriptionMaxLength, "El concepto");
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("El importe debe ser mayor que cero.").Money();
    }
}

public sealed class VoidAccountChargeRequestValidator : AbstractValidator<VoidAccountChargeRequest>
{
    public VoidAccountChargeRequestValidator() => RuleFor(x => x.Reason).Required(500, "El motivo de anulación");
}
