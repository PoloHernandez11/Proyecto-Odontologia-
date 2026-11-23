using FluentValidation;
using Odontia.Application.Features.Clinical;

namespace Odontia.Application.Validation;

public sealed class CreateDiagnosisRequestValidator : AbstractValidator<CreateDiagnosisRequest>
{
    public CreateDiagnosisRequestValidator()
    {
        RuleFor(x => x.Description).Required(300, "El diagnóstico");
        RuleFor(x => x.Code).Text(20);
        RuleFor(x => x.ToothCode).Tooth();
        RuleFor(x => x.Notes).Text(2000);
    }
}
