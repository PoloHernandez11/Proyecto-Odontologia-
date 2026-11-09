using Asp.Versioning.ApiExplorer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Odontia.Api.Infrastructure;

/// <summary>Un documento OpenAPI por versión de la API.</summary>
internal sealed class ConfigureSwaggerOptions(IApiVersionDescriptionProvider provider) : IConfigureOptions<SwaggerGenOptions>
{
    public void Configure(SwaggerGenOptions options)
    {
        foreach (var d in provider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(d.GroupName, new OpenApiInfo
            {
                Title = "Odontia API",
                Version = d.ApiVersion.ToString(),
                Description = """
                    API REST de Odontia (gestión odontológica multi-consultorio).

                    **Autenticación:** `POST /api/v1/auth/login` devuelve un access token JWT (enviar como `Authorization: Bearer <token>`)
                    y establece el refresh token en una cookie HttpOnly. Renovar con `POST /api/v1/auth/refresh` enviando el
                    encabezado `X-CSRF-Token` (valor recibido en `csrfToken`).

                    **Permisos:** cada operación indica el permiso requerido en el consultorio activo.
                    **Errores:** formato ProblemDetails con `code`, `traceId` y, en validaciones, `errors`.
                    """,
            });
        }
    }
}

/// <summary>Documenta en cada operación los permisos requeridos, el esquema de seguridad y respuestas 401/403.</summary>
internal sealed class PermissionsOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;
        var allowAnonymous = metadata.OfType<IAllowAnonymous>().Any();
        var permissions = metadata.OfType<HasPermissionAttribute>().Select(p => p.Permission).Distinct().ToList();
        var requiresAuth = !allowAnonymous && metadata.OfType<IAuthorizeData>().Any();

        if (permissions.Count > 0)
        {
            var text = $"**Permisos requeridos:** {string.Join(", ", permissions.Select(p => $"`{p}`"))}";
            operation.Description = string.IsNullOrWhiteSpace(operation.Description) ? text : $"{operation.Description}\n\n{text}";
            operation.Extensions ??= new Dictionary<string, IOpenApiExtension>();
            operation.Extensions["x-permissions"] = new JsonNodeExtension(new System.Text.Json.Nodes.JsonArray(permissions.Select(p => (System.Text.Json.Nodes.JsonNode)p!).ToArray()));
        }
        else if (requiresAuth)
        {
            const string text = "**Requiere autenticación.**";
            operation.Description = string.IsNullOrWhiteSpace(operation.Description) ? text : $"{operation.Description}\n\n{text}";
        }
        else if (allowAnonymous)
        {
            const string text = "**Público (sin autenticación).**";
            operation.Description = string.IsNullOrWhiteSpace(operation.Description) ? text : $"{operation.Description}\n\n{text}";
        }

        // Respuestas de archivo: no deben figurar como JSON (el cliente generado usaría responseType 'blob').
        foreach (var response in operation.Responses?.Values.ToList() ?? [])
            if (response.Content is { } content && content.Values.Any(m => m.Schema?.Format == "binary") && content.Count > 1)
                content.Remove("application/json");

        AddErrorResponses(operation, context, requiresAuth);
        if (!requiresAuth) return;
        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = [] });
    }

    /// <summary>Documenta las respuestas de error (ProblemDetails) que puede devolver cada operación.</summary>
    private static void AddErrorResponses(OpenApiOperation operation, OperationFilterContext context, bool requiresAuth)
    {
        var schema = context.SchemaGenerator.GenerateSchema(typeof(ApiProblem), context.SchemaRepository);
        operation.Responses ??= new OpenApiResponses();
        var api = context.ApiDescription;
        var method = api.HttpMethod?.ToUpperInvariant() ?? "GET";
        var hasInput = api.ParameterDescriptions.Any(p => p.Source.Id is "Body" or "Form" or "Query");
        var hasPathIds = api.ParameterDescriptions.Any(p => p.Source.Id == "Path" && p.Name != "version");
        var mutating = method is "POST" or "PUT" or "PATCH" or "DELETE";

        void Add(string code, string description)
        {
            // Reemplaza respuestas de error sin contenido (p. ej. declaradas sólo con [ProducesResponseType]).
            if (operation.Responses.TryGetValue(code, out var existing) && existing.Content?.Count > 0
                && existing.Content.Values.Any(c => c.Schema is OpenApiSchemaReference r && r.Reference.Id == nameof(ApiProblem))) return;
            operation.Responses[code] = new OpenApiResponse
            {
                Description = description,
                Content = new Dictionary<string, OpenApiMediaType> { ["application/problem+json"] = new() { Schema = schema } },
            };
        }

        if (hasInput) Add("400", "Datos inválidos (`validation_error`, con `errors` por campo).");
        if (requiresAuth)
        {
            Add("401", "No autenticado o sesión invalidada (`unauthorized`, `session_invalidated`).");
            Add("403", "Sin permiso o sin consultorio activo (`forbidden`, `membership_inactive`).");
        }
        else if (operation.Responses.ContainsKey("401"))
        {
            Add("401", "Credenciales o sesión inválidas (`invalid_credentials`, `invalid_refresh_token`, …).");
        }
        if (hasPathIds) Add("404", "No existe o pertenece a otro consultorio (`not_found`).");
        if (mutating)
        {
            Add("409", "Conflicto: duplicado, horario ocupado o versión desactualizada (`slot_taken`, `concurrency_conflict`, …).");
            Add("422", "Regla de negocio incumplida (el `detail` es apto para mostrar al usuario).");
        }
        Add("429", "Demasiadas solicitudes (`rate_limited`).");
    }
}

/// <summary>Identificadores de schema legibles para generadores TypeScript (sin puntos ni nombres de ensamblado).</summary>
internal static class OpenApiNaming
{
    public static string SchemaId(Type type)
    {
        if (!type.IsGenericType) return type.Name;
        var name = type.Name[..type.Name.IndexOf('`')];
        return $"{name}Of{string.Concat(type.GetGenericArguments().Select(SchemaId))}";
    }

    /// <summary>operationId en camelCase: {controlador}{Acción}, p. ej. patientsSearch, appointmentsCreate.</summary>
    public static string? OperationId(Microsoft.AspNetCore.Mvc.ApiExplorer.ApiDescription api)
    {
        if (api.ActionDescriptor is not Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor a) return null;
        var controller = char.ToLowerInvariant(a.ControllerName[0]) + a.ControllerName[1..];
        return controller + a.ActionName;
    }
}
