using FluentValidation;
using Odontia.Domain.Dental;

namespace Odontia.Application.Validation;

internal static class Rules
{
    public static IRuleBuilderOptions<T, string> Email<T>(this IRuleBuilder<T, string> r) =>
        r.NotEmpty().WithMessage("El correo es obligatorio.").MaximumLength(256).EmailAddress().WithMessage("El correo no es válido.");

    public static IRuleBuilderOptions<T, string> Password<T>(this IRuleBuilder<T, string> r) =>
        r.NotEmpty().WithMessage("La contraseña es obligatoria.").MinimumLength(10).WithMessage("La contraseña debe tener al menos 10 caracteres.")
            .MaximumLength(128);

    public static IRuleBuilderOptions<T, string?> Text<T>(this IRuleBuilder<T, string?> r, int max) => r.MaximumLength(max);

    public static IRuleBuilderOptions<T, string> Required<T>(this IRuleBuilder<T, string> r, int max, string label) =>
        r.NotEmpty().WithMessage($"{label} es obligatorio.").MaximumLength(max);

    public static IRuleBuilderOptions<T, decimal> Money<T>(this IRuleBuilder<T, decimal> r) =>
        r.GreaterThanOrEqualTo(0).LessThanOrEqualTo(999_999_999.99m).PrecisionScale(14, 2, true).WithMessage("Importe inválido (máximo 2 decimales).");

    public static IRuleBuilderOptions<T, int?> Tooth<T>(this IRuleBuilder<T, int?> r) =>
        r.Must(t => t is null || Fdi.IsValid(t.Value)).WithMessage("Pieza dental FDI inválida.");
}
