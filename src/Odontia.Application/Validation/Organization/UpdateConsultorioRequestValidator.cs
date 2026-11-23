using FluentValidation;
using Odontia.Application.Features.Organization;

namespace Odontia.Application.Validation;

public sealed class UpdateConsultorioRequestValidator : AbstractValidator<UpdateConsultorioRequest>
{
    public UpdateConsultorioRequestValidator()
    {
        RuleFor(x => x.Name).Required(150, "El nombre");
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        // Mismos límites que Consultorio.UpdateDetails.
        RuleFor(x => x.Email).Text(256);
        RuleFor(x => x.Country).NotEmpty().Length(2);
        RuleFor(x => x.LegalName).Text(200);
        RuleFor(x => x.TaxId).Text(20);
        RuleFor(x => x.Phone).Text(40);
        RuleFor(x => x.Website).Text(256);
        RuleFor(x => x.AddressLine).Text(250);
        RuleFor(x => x.City).Text(100);
        RuleFor(x => x.Province).Text(100);
        RuleFor(x => x.PostalCode).Text(20);
    }
}
