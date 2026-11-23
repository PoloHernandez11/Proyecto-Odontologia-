using FluentValidation;
using Odontia.Application.Features.Lab;

namespace Odontia.Application.Validation;

public sealed class SaveLaboratoryRequestValidator : AbstractValidator<SaveLaboratoryRequest>
{
    public SaveLaboratoryRequestValidator()
    {
        RuleFor(x => x.Name).Required(120, "El nombre");
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.ContactName).Text(120);
        RuleFor(x => x.Phone).Text(40);
        RuleFor(x => x.AddressLine).Text(250);
        RuleFor(x => x.Notes).Text(1000);
    }
}
