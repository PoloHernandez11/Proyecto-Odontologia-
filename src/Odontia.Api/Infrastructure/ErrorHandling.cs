using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Odontia.Application.Common;
using Odontia.Domain.Common;

namespace Odontia.Api.Infrastructure;

/// <summary>
/// Manejo centralizado de errores: toda respuesta de error es ProblemDetails (RFC 9457) con
/// <c>code</c> estable y <c>traceId</c>. Nunca expone excepciones internas ni datos sensibles.
/// </summary>
internal sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        var (status, code, title, detail, errors) = exception switch
        {
            RequestValidationException v => (400, v.Code, "Solicitud inválida", v.Message, v.Errors),
            AppException a => (a.StatusCode, a.Code, Title(a.StatusCode), a.Message, (IDictionary<string, string[]>?)null),
            DomainException d => (422, d.Code, "Regla de negocio", d.Message, null),
            DbUpdateConcurrencyException => (409, "concurrency_conflict", "Conflicto", "El registro fue modificado por otra operación. Actualice y reintente.", null),
            DbUpdateException => (409, "persistence_conflict", "Conflicto", "La operación entra en conflicto con datos existentes (p. ej. un registro duplicado).", null),
            BadHttpRequestException b => (b.StatusCode, "bad_request", "Solicitud inválida", "La solicitud no pudo procesarse.", null),
            OperationCanceledException when ct.IsCancellationRequested => (499, "request_cancelled", "Solicitud cancelada", "La solicitud fue cancelada.", null),
            _ => (500, "internal_error", "Error interno", "Ocurrió un error inesperado. Si persiste, informe el traceId a soporte.", null),
        };

        if (status >= 500) logger.LogError(exception, "Error no controlado {TraceId}", http.TraceIdentifier);
        else if (exception is DbUpdateException) logger.LogWarning("Conflicto de persistencia {TraceId}: {Type}", http.TraceIdentifier, exception.InnerException?.GetType().Name);
        else logger.LogInformation("Solicitud rechazada {Status} {Code} {TraceId}", status, code, http.TraceIdentifier);

        http.Response.StatusCode = status;
        var pd = new ProblemDetails { Status = status, Title = title, Detail = detail, Type = $"https://httpstatuses.io/{status}" };
        pd.Extensions["code"] = code;
        pd.Extensions["traceId"] = http.TraceIdentifier;
        if (errors is not null) pd.Extensions["errors"] = errors;
        return await problems.TryWriteAsync(new ProblemDetailsContext { HttpContext = http, ProblemDetails = pd, Exception = exception });
    }

    private static string Title(int status) => status switch
    {
        401 => "No autenticado",
        403 => "Acceso denegado",
        404 => "No encontrado",
        409 => "Conflicto",
        422 => "Regla de negocio",
        _ => "Error",
    };
}

/// <summary>Valida automáticamente con FluentValidation todo argumento de acción que tenga validador registrado.</summary>
internal sealed class ValidationFilter(IServiceProvider services) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var errors = new Dictionary<string, List<string>>();
        foreach (var arg in context.ActionArguments.Values)
        {
            if (arg is null) continue;
            var validatorType = typeof(IValidator<>).MakeGenericType(arg.GetType());
            if (services.GetService(validatorType) is not IValidator validator) continue;
            var result = await validator.ValidateAsync(new ValidationContext<object>(arg), context.HttpContext.RequestAborted);
            foreach (var f in result.Errors)
            {
                var key = JsonNamingPolicy.CamelCase.ConvertName(f.PropertyName.Split('.').Last().Split('[')[0]);
                if (string.IsNullOrEmpty(key)) key = "request";
                if (!errors.TryGetValue(key, out var list)) errors[key] = list = [];
                list.Add(f.ErrorMessage);
            }
        }
        if (errors.Count > 0) throw new RequestValidationException(errors.ToDictionary(e => e.Key, e => e.Value.ToArray()));
        await next();
    }
}
