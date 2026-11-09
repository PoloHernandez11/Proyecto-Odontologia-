namespace Odontia.Application.Common;

/// <summary>Excepción de aplicación traducida a una respuesta HTTP consistente (ProblemDetails).</summary>
public abstract class AppException(string message, int statusCode, string code) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
}

public sealed class NotFoundException(string message = "El recurso solicitado no existe.")
    : AppException(message, 404, "not_found");

/// <summary>El usuario está autenticado pero no tiene permiso para la operación.</summary>
public sealed class ForbiddenAccessException(string message = "No tiene permisos para realizar esta operación.")
    : AppException(message, 403, "forbidden");

public sealed class ConflictException(string message, string code = "conflict")
    : AppException(message, 409, code);

public sealed class BusinessRuleException(string message, string code = "business_rule")
    : AppException(message, 422, code);

public sealed class UnauthorizedException(string message = "Credenciales inválidas.", string code = "unauthorized")
    : AppException(message, 401, code);

public sealed class RequestValidationException(IDictionary<string, string[]> errors)
    : AppException("La solicitud contiene datos inválidos.", 400, "validation_error")
{
    public IDictionary<string, string[]> Errors { get; } = errors;

    public RequestValidationException(string field, string message)
        : this(new Dictionary<string, string[]> { [field] = [message] }) { }
}
