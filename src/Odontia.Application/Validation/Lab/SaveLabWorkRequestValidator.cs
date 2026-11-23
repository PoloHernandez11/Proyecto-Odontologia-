using FluentValidation;
using Odontia.Application.Features.Lab;
using Odontia.Domain.Dental;

namespace Odontia.Application.Validation;

public sealed class SaveLabWorkRequestValidator : AbstractValidator<SaveLabWorkRequest>
{
    public SaveLabWorkRequestValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty();
        RuleFor(x => x.LaboratoryId).NotEmpty();
        RuleFor(x => x.WorkType).Required(120, "El tipo de trabajo");
        RuleFor(x => x.ToothCode).Tooth();
        RuleFor(x => x.ToothCodes!.Count).LessThanOrEqualTo(Domain.Entities.LabWork.MaxTeeth)
            .WithMessage($"Un trabajo puede abarcar como máximo {Domain.Entities.LabWork.MaxTeeth} piezas.")
            .OverridePropertyName("toothCodes").When(x => x.ToothCodes is not null);
        RuleForEach(x => x.ToothCodes).Must(Domain.Dental.Fdi.IsValid).WithMessage("Pieza dental FDI inválida.").When(x => x.ToothCodes is not null);
        RuleFor(x => x.AppointmentId).NotEqual(Guid.Empty).WithMessage("Turno de entrega inválido.");
        RuleFor(x => x.Description).Text(1000);
        RuleFor(x => x.Notes).Text(1000);
        RuleFor(x => x.Cost!.Value).Money().When(x => x.Cost is not null);
    }
}
