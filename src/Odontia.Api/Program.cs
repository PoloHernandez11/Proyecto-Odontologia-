using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Odontia.Api.Infrastructure;
using Odontia.Application;
using Odontia.Infrastructure;
using Odontia.Infrastructure.Persistence;
using Odontia.Infrastructure.Security;
using Swashbuckle.AspNetCore.SwaggerGen;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// ---------------- Servicios ----------------
builder.Services.AddApplication(config);
builder.Services.AddInfrastructure(config, builder.Environment, OdontiaTestHooks.ConfigureDatabase);
builder.Services.Configure<AuthCookieOptions>(config.GetSection(AuthCookieOptions.Section));
builder.Services.AddScoped<AuthCookieManager>();
builder.Services.AddScoped<ValidationFilter>();

builder.Services.AddControllers(o =>
    {
        o.ModelBinderProviders.Insert(0, new TolerantDateModelBinderProvider());
        o.Filters.AddService<ValidationFilter>();
        // Política por defecto: todo endpoint requiere autenticación salvo [AllowAnonymous] explícito.
        o.Filters.Add(new Microsoft.AspNetCore.Mvc.Authorization.AuthorizeFilter());
    })
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
    })
    .ConfigureApiBehaviorOptions(o =>
    {
        // Errores de binding/formato en ProblemDetails consistentes con el resto de la API.
        o.InvalidModelStateResponseFactory = ctx =>
        {
            // No se exponen mensajes internos del deserializador (nombres de tipos, rutas de código).
            var errors = ctx.ModelState.Where(e => e.Value?.Errors.Count > 0)
                .GroupBy(e => string.IsNullOrEmpty(e.Key) || e.Key == "request" ? "request" : System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(e.Key.TrimStart('$', '.')))
                .ToDictionary(g => g.Key, g => g.Key == "request"
                    ? ["El cuerpo de la solicitud es obligatorio o no es un JSON válido."]
                    : g.SelectMany(e => e.Value!.Errors).Select(x => x.Exception is not null || x.ErrorMessage.Contains("JSON") || x.ErrorMessage.Contains("could not be converted")
                        ? "Valor con formato o tipo inválido."
                        : string.IsNullOrWhiteSpace(x.ErrorMessage) ? "Valor inválido." : x.ErrorMessage).Distinct().ToArray());
            var pd = new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 400, Title = "Solicitud inválida", Detail = "La solicitud contiene datos inválidos.", Type = "https://httpstatuses.io/400" };
            pd.Extensions["code"] = "validation_error";
            pd.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier;
            pd.Extensions["errors"] = errors;
            return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(pd) { ContentTypes = { "application/problem+json" } };
        };
    });

builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = OdontiaProblemDetails.Customize);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddApiVersioning(o =>
    {
        o.DefaultApiVersion = new ApiVersion(1, 0);
        o.AssumeDefaultVersionWhenUnspecified = false;
        o.ReportApiVersions = true;
        o.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddMvc()
    .AddApiExplorer(o =>
    {
        o.GroupNameFormat = "'v'VVV";
        o.SubstituteApiVersionInUrl = true;
    });

// Autenticación JWT (los permisos se resuelven por solicitud en TenantResolutionMiddleware).
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((o, jwt) =>
    {
        var j = jwt.Value;
        o.MapInboundClaims = false;
        o.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = j.Issuer,
            ValidAudience = j.Audience,
            IssuerSigningKey = j.GetKey(),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "sub",
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

// CORS restringido y configurable por entorno (con credenciales para la cookie de refresh).
// Se lee de forma diferida (al resolver las opciones) para respetar toda fuente de configuración del host.
builder.Services.AddCors();
builder.Services.AddOptions<Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions>()
    .Configure<IConfiguration, IWebHostEnvironment, IOptions<AuthCookieOptions>>((o, cfg, env, cookies) =>
    {
        var allowedOrigins = (cfg.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        if (!env.IsDevelopment() && allowedOrigins.Any(x => x.Trim() == "*"))
            throw new InvalidOperationException("Cors:AllowedOrigins no puede contener '*' fuera de desarrollo.");
        o.AddPolicy("frontend", p => p
            .WithOrigins(allowedOrigins)
            .AllowCredentials()
            .WithHeaders("Authorization", "Content-Type", cookies.Value.CsrfHeaderName, "Accept", "X-Requested-With")
            .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
            .WithExposedHeaders("api-supported-versions", "Content-Disposition", "Location")
            .SetPreflightMaxAge(TimeSpan.FromMinutes(10)));
    });

// Rate limiting: estricto en endpoints sensibles y general por usuario/IP.
var rl = config.GetSection("RateLimiting");
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.OnRejected = async (ctx, ct) =>
    {
        var problems = ctx.HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Http.IProblemDetailsService>();
        await problems.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = ctx.HttpContext,
            ProblemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = StatusCodes.Status429TooManyRequests },
        });
    };
    o.AddPolicy(RateLimitPolicies.Auth, http => RateLimitPartition.GetFixedWindowLimiter(
        $"auth:{http.Connection.RemoteIpAddress}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = rl.GetValue("AuthPermitPerMinute", 10), Window = TimeSpan.FromMinutes(1), QueueLimit = 0,
        }));
    o.AddPolicy(RateLimitPolicies.Refresh, http => RateLimitPartition.GetFixedWindowLimiter(
        $"refresh:{http.Connection.RemoteIpAddress}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = rl.GetValue("RefreshPermitPerMinute", 30), Window = TimeSpan.FromMinutes(1), QueueLimit = 0,
        }));
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http => RateLimitPartition.GetFixedWindowLimiter(
        http.User.Subject() ?? http.Connection.RemoteIpAddress?.ToString() ?? "anon", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = rl.GetValue("GlobalPermitPerMinute", 600), Window = TimeSpan.FromMinutes(1), QueueLimit = 0,
        }));
});

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    foreach (var proxy in config.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
        if (System.Net.IPAddress.TryParse(proxy, out var ip)) o.KnownProxies.Add(ip);
});

builder.Services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
builder.Services.AddSwaggerGen(o =>
{
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Access token obtenido en /api/v1/auth/login o /api/v1/auth/refresh.",
    });
    o.OperationFilter<PermissionsOperationFilter>();
    o.SupportNonNullableReferenceTypes();
    // Nombres aptos para generadores TypeScript (ng-openapi-gen, openapi-generator typescript-angular).
    o.CustomSchemaIds(OpenApiNaming.SchemaId);
    o.CustomOperationIds(OpenApiNaming.OperationId);
    foreach (var xml in Directory.GetFiles(AppContext.BaseDirectory, "Odontia.*.xml")) o.IncludeXmlComments(xml);
});

builder.Services.AddHsts(o => { o.MaxAge = TimeSpan.FromDays(180); o.IncludeSubDomains = true; });

// ---------------- Pipeline ----------------
var app = builder.Build();

// Valida la configuración de CORS al iniciar (p. ej. rechaza '*' fuera de desarrollo).
_ = app.Services.GetRequiredService<IOptions<Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions>>().Value;

if (config.GetValue("Database:ApplyMigrationsOnStartup", false))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<OdontiaDbContext>().Database.MigrateAsync();
}

if (config.GetValue("ReverseProxy:Enabled", false)) app.UseForwardedHeaders();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();
if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

if (app.Environment.IsDevelopment() || config.GetValue("Swagger:Enabled", false))
{
    app.UseSwagger(o => o.RouteTemplate = "swagger/{documentName}/swagger.{extension:regex(^(json|yaml)$)}");
    app.UseSwaggerUI(o =>
    {
        foreach (var d in app.DescribeApiVersions()) o.SwaggerEndpoint($"/swagger/{d.GroupName}/swagger.json", $"Odontia API {d.GroupName}");
        o.DocumentTitle = "Odontia API";
    });
}

app.UseCors("frontend");
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseRateLimiter();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous().DisableRateLimiting();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") }).AllowAnonymous().DisableRateLimiting();
app.MapControllers().RequireCors("frontend");

app.Run();

public static class RateLimitPolicies
{
    public const string Auth = "auth";
    public const string Refresh = "refresh";
}

/// <summary>Punto de extensión sólo para pruebas de integración (reemplaza el proveedor de base de datos).</summary>
public static class OdontiaTestHooks
{
    public static Action<DbContextOptionsBuilder>? ConfigureDatabase { get; set; }
}

public partial class Program;
