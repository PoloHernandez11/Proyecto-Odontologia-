using FluentValidation;
using Odontia.Application.Features.Patients;

namespace Odontia.Application.Validation;

public sealed class CreatePatientRequestValidator : AbstractValidator<CreatePatientRequest>
{
    public CreatePatientRequestValidator()
    {
        RuleFor(x => x.Patient).NotNull().SetValidator(new SavePatientRequestValidator());
        RuleFor(x => x.MedicalHistory!).SetValidator(new MedicalHistoryRequestValidator()).When(x => x.MedicalHistory is not null);
    }
}
