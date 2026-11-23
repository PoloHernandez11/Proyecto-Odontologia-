using FluentValidation;
using Odontia.Application.Features.Auth;

namespace Odontia.Application.Validation;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email).Email();
        RuleFor(x => x.Password).Password();
        RuleFor(x => x.FirstName).Required(80, "El nombre");
        RuleFor(x => x.LastName).Required(80, "El apellido");
        RuleFor(x => x.ConsultorioName).Required(150, "El nombre del consultorio");
        RuleFor(x => x.Title).Text(20);
        RuleFor(x => x.Specialty).Text(120);
        RuleFor(x => x.LicenseNumber).Text(40);
        RuleFor(x => x.TimeZoneId).Text(64);
    }
}
