using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using OdontoApp.Api.Agenda;
using OdontoApp.Api.Auth;
using OdontoApp.Api.Data;

var builder = WebApplication.CreateBuilder(args);

// ---------- Base de datos (MySQL, por los tipos enum/tinyint del DER) ----------
var cs = builder.Configuration.GetConnectionString("Default")
         ?? throw new InvalidOperationException("Falta ConnectionStrings:Default.");
builder.Services.AddDbContext<OdontoDbContext>(o => o.UseMySql(cs, ServerVersion.AutoDetect(cs)));

// ---------- Opciones ----------
builder.Services.Configure<AgendaOptions>(builder.Configuration.GetSection("Agenda"));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<FirebaseSettings>(builder.Configuration.GetSection("Firebase"));
builder.Services.AddSingleton<FirebaseIdentidad>();
builder.Services.AddScoped<PermisosService>();
builder.Services.AddSingleton<TokenService>();

// ---------- Autenticación JWT + autorización por rol ----------
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (jwt.Key.Length < 32)
    throw new InvalidOperationException("Jwt:Key debe tener al menos 32 caracteres (usá dotnet user-secrets).");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            RoleClaimType = ClaimTypes.Role,
            NameClaimType = ClaimTypes.Name,
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddCors(o => o.AddPolicy("frontend", p => p
    .WithOrigins(builder.Configuration["FrontendUrl"] ?? "http://localhost:5173")
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
    await DbSeeder.SeedAsync(app.Services);

// Página de la app: carpeta "web" junto al servidor (la genera Preparar Odontia).
// Así la app entera corre en http://localhost:5000 sin instalar nada más.
var carpetaWeb = Path.Combine(AppContext.BaseDirectory, "web");
var hayWeb = Directory.Exists(carpetaWeb);
if (hayWeb)
{
    var archivosWeb = new PhysicalFileProvider(carpetaWeb);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = archivosWeb });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = archivosWeb });
}

app.UseCors("frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Rutas de la página (React Router): cualquier otra ruta devuelve index.html.
if (hayWeb)
    app.MapFallbackToFile("index.html", new StaticFileOptions { FileProvider = new PhysicalFileProvider(carpetaWeb) });

app.Run();
