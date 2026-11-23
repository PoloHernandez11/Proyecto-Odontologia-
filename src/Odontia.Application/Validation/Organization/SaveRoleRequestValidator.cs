using FluentValidation;
using Odontia.Application.Features.Organization;
using Odontia.Domain.Authorization;

namespace Odontia.Application.Validation;

public sealed class SaveRoleRequestValidator : AbstractValidator<SaveRoleRequest>
{
    public SaveRoleRequestValidator()
    {
        RuleFor(x => x.Name).Required(64, "El nombre");
        RuleFor(x => x.Description).Text(250);
        RuleFor(x => x.Permissions).NotEmpty().WithMessage("Debe incluir al menos un permiso.");
        RuleForEach(x => x.Permissions).Must(Permissions.IsValid).WithMessage("Permiso desconocido: {PropertyValue}.");
    }
}
