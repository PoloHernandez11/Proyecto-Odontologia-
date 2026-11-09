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

        if (!requiresAuth) return;
        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = [] });
        operation.Responses ??= new OpenApiResponses();
        operation.Responses.TryAdd("401", new OpenApiResponse { Description = "No autenticado o sesión invalidada." });
        operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Sin permiso o sin consultorio activo." });
    }
}
