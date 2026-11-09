namespace Odontia.Domain.Common;

internal static class Guard
{
    public static string Required(string? value, string field, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException($"El campo '{field}' es obligatorio.", "required");
        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
            throw new DomainException($"El campo '{field}' supera la longitud máxima de {maxLength}.", "max_length");
        return trimmed;
    }

    public static string? Optional(string? value, string field, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
            throw new DomainException($"El campo '{field}' supera la longitud máxima de {maxLength}.", "max_length");
        return trimmed;
    }

    public static decimal Money(decimal value, string field, bool allowZero = true)
    {
        if (value < 0 || (!allowZero && value == 0))
            throw new DomainException($"El importe '{field}' debe ser {(allowZero ? "mayor o igual a" : "mayor que")} cero.", "invalid_amount");
        if (decimal.Round(value, 2) != value)
            throw new DomainException($"El importe '{field}' admite como máximo dos decimales.", "invalid_amount");
        if (value > 999_999_999.99m)
            throw new DomainException($"El importe '{field}' excede el máximo permitido.", "invalid_amount");
        return value;
    }
}
