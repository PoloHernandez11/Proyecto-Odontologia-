using FluentValidation;
using Odontia.Application.Features.Odontograms;

namespace Odontia.Application.Validation;

public sealed class AddToothNoteRequestValidator : AbstractValidator<AddToothNoteRequest>
{
    public AddToothNoteRequestValidator() => RuleFor(x => x.Text).Required(1000, "La nota");
}
