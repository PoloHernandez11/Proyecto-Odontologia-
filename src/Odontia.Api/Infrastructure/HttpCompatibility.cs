using System.Globalization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Odontia.Api.Infrastructure;

/// <summary>
/// Contrato de error documentado en OpenAPI (ProblemDetails RFC 9457 + extensiones de Odontia).
/// Sólo se usa para describir el esquema; las respuestas reales las produce <see cref="OdontiaProblemDetails"/>.
/// </summary>
/// <param name="Type">URI del tipo de error.</param>
/// <param name="Title">Título corto en español.</param>
/// <param name="Status">Código HTTP.</param>
/// <param name="Detail">Mensaje apto para mostrar al usuario.</param>
/// <param name="Code">Código estable para la lógica del cliente (p. ej. validation_error, slot_taken, forbidden).</param>
/// <param name="TraceId">Identificador de la solicitud para soporte.</param>
/// <param name="Errors">Errores por campo (camelCase), sólo en validation_error.</param>
public sealed record ApiProblem(
    string Type,
    string Title,
    int Status,
    string? Detail,
    string Code,
    string TraceId,
    IDictionary<string, string[]>? Errors);

/// <summary>
/// Uniformiza los ProblemDetails que genera el propio framework (401/403 de autenticación, 404/405 de ruteo,
/// 415, 429, etc.): agrega <c>code</c>, <c>traceId</c> y título en español, igual que los errores de negocio.
/// </summary>
internal static class OdontiaProblemDetails
{
    public static void Customize(ProblemDetailsContext ctx)
    {
        var pd = ctx.ProblemDetails;
        var status = pd.Status ?? ctx.HttpContext.Response.StatusCode;
        pd.Status = status;
        pd.Extensions.TryAdd("traceId", ctx.HttpContext.TraceIdentifier);
        if (pd.Extensions.ContainsKey("code")) return; // ya viene de la aplicación

        var (code, title, detail) = status switch
        {
            400 => ("bad_request", "Solicitud inválida", "La solicitud no pudo procesarse."),
            401 => ("unauthorized", "No autenticado", "Se requiere un access token válido."),
            403 => ("forbidden", "Acceso denegado", "No tiene permisos para realizar esta operación."),
            404 => ("not_found", "No encontrado", "El recurso solicitado no existe."),
            405 => ("method_not_allowed", "Método no permitido", "El método HTTP no está permitido para este recurso."),
            413 => ("payload_too_large", "Contenido demasiado grande", "La solicitud supera el tamaño máximo permitido."),
            415 => ("unsupported_media_type", "Tipo de contenido no soportado", "Use application/json (o multipart/form-data para archivos)."),
            429 => ("rate_limited", "Demasiadas solicitudes", "Espere unos instantes y vuelva a intentar."),
            >= 500 => ("internal_error", "Error interno", "Ocurrió un error inesperado. Si persiste, informe el traceId a soporte."),
            _ => ("error", "Error", null),
        };
        pd.Extensions["code"] = code;
        pd.Title = title;
        pd.Detail ??= detail;
        pd.Type = $"https://httpstatuses.io/{status}";
    }
}

/// <summary>
/// Acepta fechas en query string cuyo signo '+' llegó como espacio. Angular (HttpParams) y otros clientes no
/// codifican '+', y el servidor lo decodifica como espacio: "2026-10-20T10:00:00 03:00" se interpreta como "+03:00".
/// </summary>
internal sealed class TolerantDateModelBinderProvider : IModelBinderProvider
{
    private static readonly HashSet<Type> Types = [typeof(DateTimeOffset), typeof(DateTimeOffset?), typeof(DateTime), typeof(DateTime?)];

    public IModelBinder? GetBinder(ModelBinderProviderContext context) =>
        Types.Contains(context.Metadata.ModelType) ? new TolerantDateModelBinder() : null;

    private sealed class TolerantDateModelBinder : IModelBinder
    {
        public Task BindModelAsync(ModelBindingContext ctx)
        {
            var raw = ctx.ValueProvider.GetValue(ctx.ModelName).FirstValue;
            if (string.IsNullOrWhiteSpace(raw))
            {
                if (Nullable.GetUnderlyingType(ctx.ModelType) is null && ctx.ValueProvider.GetValue(ctx.ModelName) != ValueProviderResult.None)
                    ctx.ModelState.TryAddModelError(ctx.ModelName, "La fecha es obligatoria.");
                return Task.CompletedTask;
            }
            ctx.ModelState.SetModelValue(ctx.ModelName, raw, raw);
            var value = raw.Trim();
            // "…T10:00:00 03:00" → "…T10:00:00+03:00" (sólo un espacio antes de un offset hh:mm al final).
            if (value.Length > 6 && value[^6] == ' ' && value[^3] == ':') value = string.Concat(value.AsSpan(0, value.Length - 6), "+", value.AsSpan(value.Length - 5));

            if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto))
            {
                ctx.ModelState.TryAddModelError(ctx.ModelName, "Fecha inválida. Use ISO 8601, p. ej. 2026-10-20T13:00:00Z.");
                return Task.CompletedTask;
            }
            var underlying = Nullable.GetUnderlyingType(ctx.ModelType) ?? ctx.ModelType;
            ctx.Result = ModelBindingResult.Success(underlying == typeof(DateTime) ? dto.UtcDateTime : dto);
            return Task.CompletedTask;
        }
    }
}

/// <summary>Respuestas tipadas simples (para que los clientes TypeScript generados tengan su interfaz).</summary>
public sealed record RevokedSessionsDto(int Revoked);

public sealed record UpdatedCountDto(int Updated);
