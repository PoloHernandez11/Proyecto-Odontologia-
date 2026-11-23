using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.JsonWebTokens;
using Odontia.Application.Abstractions;

namespace Odontia.Api.Infrastructure;

/// <summary>Requiere un permiso granular en el consultorio activo. Se documenta automáticamente en OpenAPI.</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class HasPermissionAttribute(string permission) : AuthorizeAttribute, IAuthorizationRequirementData
{
    public string Permission { get; } = permission;
    public IEnumerable<IAuthorizationRequirement> GetRequirements() => [new PermissionRequirement(Permission)];
}

/// <summary>Requiere sesión con consultorio activo validado (sin permiso específico).</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class RequireConsultorioAttribute : AuthorizeAttribute, IAuthorizationRequirementData
{
    public IEnumerable<IAuthorizationRequirement> GetRequirements() => [new PermissionRequirement(null)];
}

public sealed record PermissionRequirement(string? Permission) : IAuthorizationRequirement;

internal sealed class PermissionAuthorizationHandler(ITenantContext tenant) : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (tenant.HasTenant && (requirement.Permission is null || tenant.Has(requirement.Permission)))
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Resuelve el contexto de la solicitud en el servidor: valida que el token no haya sido invalidado
/// (cambio de credenciales), que la sesión (sid) no haya sido revocada y que el usuario siga siendo miembro activo
/// del consultorio del token. Las tres verificaciones usan una caché corta invalidada ante cambios (ver MembershipResolver).
/// El consultorio NUNCA se toma de parámetros enviados por el cliente.
/// </summary>
internal sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext http, ITenantContextInitializer initializer, IMembershipResolver memberships,
        IJwtTokenService tokens)
    {
        var user = http.User;
        // En endpoints públicos (login, refresh, etc.) un access token vencido o invalidado no debe bloquear la operación.
        var anonymousEndpoint = http.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null;
        if (user.Identity?.IsAuthenticated == true && tokens.GetUserId(user) is { } userId)
        {
            var stamp = await memberships.GetSecurityStampAsync(userId, http.RequestAborted);
            if (!tokens.MatchesSecurityStamp(user, stamp))
            {
                if (anonymousEndpoint) { await next(http); return; }
                await WriteProblemAsync(http, StatusCodes.Status401Unauthorized, "session_invalidated", "La sesión ya no es válida. Inicie sesión nuevamente.");
                return;
            }
            ResolvedMembership? membership = null;
            var hasConsultorio = Guid.TryParse(user.FindFirstValue(OdontiaClaims.Consultorio), out var consultorioId);
            if (hasConsultorio)
            {
                // La membresía se evalúa antes que la sesión: un miembro desactivado recibe 403 membership_inactive.
                membership = await memberships.ResolveAsync(userId, consultorioId, http.RequestAborted);
                if (membership is null)
                {
                    if (anonymousEndpoint) { await next(http); return; }
                    await WriteProblemAsync(http, StatusCodes.Status403Forbidden, "membership_inactive", "No tiene acceso activo a este consultorio.");
                    return;
                }
            }

            // Logout / revocación de sesión: el access token deja de valer de inmediato, no al vencer (QA API-09).
            if (!Guid.TryParse(user.FindFirstValue(OdontiaClaims.Session), out var sessionId)
                || !await memberships.IsSessionActiveAsync(userId, sessionId, http.RequestAborted))
            {
                if (anonymousEndpoint) { await next(http); return; }
                await WriteProblemAsync(http, StatusCodes.Status401Unauthorized, "session_invalidated", "La sesión ya no es válida. Inicie sesión nuevamente.");
                return;
            }

            initializer.InitializeUser(userId, http.Connection.RemoteIpAddress?.ToString());
            if (membership is not null)
                initializer.InitializeTenant(consultorioId, membership.MemberId, membership.IsProfessional, membership.Permissions);
        }
        await next(http);
    }

    private static Task WriteProblemAsync(HttpContext http, int status, string code, string detail)
    {
        http.Response.StatusCode = status;
        http.Response.ContentType = "application/problem+json";
        return http.Response.WriteAsJsonAsync(new
        {
            type = $"https://httpstatuses.io/{status}",
            title = status == 401 ? "No autenticado" : "Acceso denegado",
            status,
            detail,
            code,
            traceId = http.TraceIdentifier,
        });
    }
}

internal static class ClaimsPrincipalExtensions
{
    public static Guid SessionId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(OdontiaClaims.Session), out var sid) ? sid : Guid.Empty;

    public static string? Subject(this ClaimsPrincipal user) => user.FindFirstValue(JwtRegisteredClaimNames.Sub);
}

/// <summary>Cabeceras de seguridad para respuestas de la API.</summary>
internal sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext http)
    {
        // OnStarting: se aplican también a respuestas de error generadas por el manejador global.
        http.Response.OnStarting(() =>
        {
            var h = http.Response.Headers;
            h.XContentTypeOptions = "nosniff";
            h.XFrameOptions = "DENY";
            h["Referrer-Policy"] = "no-referrer";
            h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            if (http.Request.Path.StartsWithSegments("/api"))
            {
                h.CacheControl = "no-store";
                h["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
            }
            return Task.CompletedTask;
        });
        return next(http);
    }
}
