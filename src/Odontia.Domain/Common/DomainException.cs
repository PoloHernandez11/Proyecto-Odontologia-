namespace Odontia.Domain.Common;

/// <summary>Violación de una regla de negocio o invariante del dominio.</summary>
public class DomainException(string message, string code = "domain_rule_violation") : Exception(message)
{
    public string Code { get; } = code;
}
