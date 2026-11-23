using FluentValidation;
using Odontia.Application.Features.Patients;

namespace Odontia.Application.Validation;

public sealed class SavePatientRequestValidator : AbstractValidator<SavePatientRequest>
{
    public SavePatientRequestValidator()
    {
        RuleFor(x => x.FirstName).Required(80, "El nombre");
        RuleFor(x => x.LastName).Required(80, "El apellido");
        RuleFor(x => x.DocumentNumber).NotEmpty().WithMessage("El DNI es obligatorio.").MaximumLength(20)
            .Matches(@"^[0-9A-Za-z.\- ]+$").WithMessage("El DNI contiene caracteres inválidos.")
            // Misma regla que Patient.SetIdentity: entre 6 y 15 caracteres una vez normalizado.
            .Must(d => string.IsNullOrWhiteSpace(d) || Domain.Entities.Patient.NormalizeDocument(d).Length is >= 6 and <= 15).WithMessage("El DNI informado no es válido.");
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email)).WithMessage("El correo no es válido.");
        RuleFor(x => x.Phone).Text(40).Matches(@"^[0-9+()\-\s]*$").When(x => x.Phone is not null).WithMessage("Teléfono inválido.");
        RuleFor(x => x.EmergencyContactPhone).Text(40);
        RuleFor(x => x.Occupation).Text(100);
        RuleFor(x => x.AddressLine).Text(250);
        RuleFor(x => x.City).Text(100);
        RuleFor(x => x.Province).Text(100);
        RuleFor(x => x.PostalCode).Text(20);
        RuleFor(x => x.EmergencyContactName).Text(120);
        RuleFor(x => x.EmergencyContactRelationship).Text(60);
        RuleFor(x => x.InsurancePlan).Text(60);
        RuleFor(x => x.AffiliateNumber).Text(40);
        RuleFor(x => x.AdministrativeNotes).Text(2000);
        RuleFor(x => x.Sex).IsInEnum();
    }
}
