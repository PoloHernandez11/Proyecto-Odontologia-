using FluentValidation;
using Odontia.Application.Features.Patients;

namespace Odontia.Application.Validation;

public sealed class MedicalHistoryRequestValidator : AbstractValidator<MedicalHistoryRequest>
{
    public MedicalHistoryRequestValidator()
    {
        RuleFor(x => x.Allergies).Text(500);
        RuleFor(x => x.CurrentMedication).Text(1000);
        RuleFor(x => x.OtherConditions).Text(2000);
    }
}
