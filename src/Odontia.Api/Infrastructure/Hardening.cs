using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Odontia.Api.Infrastructure;

/// <summary>
/// Igual que <c>[Produces("application/json")]</c> (documenta JSON en OpenAPI), pero no pisa el tipo de contenido de las
/// respuestas ProblemDetails: los errores de binding, JSON inválido, enum inválido, 415, etc. salen como application/problem+json.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ProducesJsonAttribute() : ProducesAttribute("application/json")
{
    public const string ProblemJson = "application/problem+json";

    public override void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is ObjectResult { Value: ProblemDetails } problem)
        {
            problem.ContentTypes.Clear();
            problem.ContentTypes.Add(ProblemJson);
            return;
        }
        base.OnResultExecuting(context);
    }
}

/// <summary>Límites de tamaño del cuerpo de las solicitudes.</summary>
public sealed class RequestLimitsOptions
{
    public const string Section = "RequestLimits";
    /// <summary>Máximo global (JSON). Los endpoints de carga declaran el suyo con [RequestSizeLimit].</summary>
    public long MaxBodyBytes { get; set; } = 2 * 1024 * 1024;
}

/// <summary>
/// Rechaza con 413 (ProblemDetails) los cuerpos que declaran un Content-Length mayor al permitido para el endpoint.
/// Complementa el límite de Kestrel (que además corta cuerpos chunked) y respeta [RequestSizeLimit]/[DisableRequestSizeLimit].
/// </summary>
internal sealed class RequestBodyLimitMiddleware(RequestDelegate next, Microsoft.Extensions.Options.IOptions<RequestLimitsOptions> options)
{
    public async Task InvokeAsync(HttpContext http)
    {
        if (http.Request.ContentLength is { } length)
        {
            var metadata = http.GetEndpoint()?.Metadata.GetMetadata<IRequestSizeLimitMetadata>();
            var limit = metadata is not null ? metadata.MaxRequestBodySize : options.Value.MaxBodyBytes;
            if (limit is not null && length > limit)
            {
                http.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                var problems = http.RequestServices.GetRequiredService<IProblemDetailsService>();
                await problems.WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = http,
                    ProblemDetails = new ProblemDetails { Status = StatusCodes.Status413PayloadTooLarge },
                });
                return;
            }
        }
        await next(http);
    }
}

internal static class Hardening
{
    /// <summary>Logs JSON estructurados por consola en producción (los mensajes ya enmascaran correos y teléfonos).</summary>
    public static void ConfigureLogging(ILoggingBuilder logging, IHostEnvironment env)
    {
        if (!env.IsProduction()) return;
        logging.ClearProviders();
        logging.AddJsonConsole(o =>
        {
            o.IncludeScopes = true;
            o.UseUtcTimestamp = true;
            o.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
        });
    }

    /// <summary>Agrega Retry-After (segundos) a las respuestas 429 a partir de la metadata del lease.</summary>
    public static void SetRetryAfter(OnRejectedContext ctx)
    {
        if (ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            ctx.HttpContext.Response.Headers.RetryAfter =
                Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Respuesta mínima de /health/ready: sólo el estado global, sin nombres de checks, excepciones ni tiempos.</summary>
    public static Task WriteMinimalHealth(HttpContext http, HealthReport report)
    {
        http.Response.ContentType = "application/json";
        http.Response.Headers.CacheControl = "no-store";
        return http.Response.WriteAsJsonAsync(new { status = report.Status == HealthStatus.Healthy ? "Healthy" : "Unhealthy" });
    }
}
