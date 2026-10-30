using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace OdontoApp.Api.Auth;

public class FirebaseSettings
{
    /// <summary>ID del proyecto de Firebase (Configuración del proyecto → General). No es secreto.</summary>
    public string ProjectId { get; set; } = string.Empty;
}

/// <summary>Identidad verificada por Firebase a partir del ID token que envía el navegador.</summary>
public record IdentidadFirebase(string Sub, string Email, string? Nombre);

/// <summary>
/// Verifica el ID token de Firebase sin cuenta de servicio: usa los certificados públicos que Google publica
/// para Firebase Authentication (la forma documentada de validar tokens). Firma, emisor, audiencia (el proyecto),
/// expiración y correo verificado. Cualquier fallo devuelve null: el sistema falla cerrado.
/// </summary>
public class FirebaseIdentidad(IOptions<FirebaseSettings> options)
{
    private const string UrlCertificados =
        "https://www.googleapis.com/robot/v1/metadata/x509/securetoken@system.gserviceaccount.com";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };
    // MapInboundClaims = false: conserva los nombres originales del token ("sub", "email", "email_verified").
    private static readonly JwtSecurityTokenHandler Manejador = new() { MapInboundClaims = false };
    private static readonly SemaphoreSlim Candado = new(1, 1);
    private static IReadOnlyList<SecurityKey> _claves = Array.Empty<SecurityKey>();
    private static DateTime _clavesVencenEn = DateTime.MinValue;

    private readonly FirebaseSettings _opt = options.Value;

    public bool Configurado => !string.IsNullOrWhiteSpace(_opt.ProjectId);

    /// <summary>Devuelve null si el token no es válido, expiró o el correo no está verificado.</summary>
    public async Task<IdentidadFirebase?> ValidarAsync(string idToken)
    {
        if (!Configurado || string.IsNullOrWhiteSpace(idToken)) return null;

        try
        {
            var parametros = new TokenValidationParameters
            {
                ValidIssuer = $"https://securetoken.google.com/{_opt.ProjectId}",
                ValidAudience = _opt.ProjectId,
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = await ObtenerClavesAsync(),
                ClockSkew = TimeSpan.FromMinutes(2),
            };

            var principal = Manejador.ValidateToken(idToken, parametros, out _);

            var email = principal.FindFirst("email")?.Value;
            var verificado = string.Equals(principal.FindFirst("email_verified")?.Value, "true", StringComparison.OrdinalIgnoreCase);
            if (!verificado || string.IsNullOrWhiteSpace(email)) return null;

            var uid = principal.FindFirst("sub")?.Value;
            if (string.IsNullOrWhiteSpace(uid)) return null;

            var nombre = principal.FindFirst("name")?.Value;
            return new IdentidadFirebase(uid, email.Trim().ToLowerInvariant(), nombre);
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException or HttpRequestException or JsonException or TaskCanceledException)
        {
            return null;
        }
    }

    /// <summary>Certificados públicos de Firebase, cacheados una hora.</summary>
    private static async Task<IReadOnlyList<SecurityKey>> ObtenerClavesAsync()
    {
        if (DateTime.UtcNow < _clavesVencenEn) return _claves;

        await Candado.WaitAsync();
        try
        {
            if (DateTime.UtcNow < _clavesVencenEn) return _claves;

            var json = await Http.GetStringAsync(UrlCertificados);
            var certificados = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();

            var claves = new List<SecurityKey>();
            foreach (var (kid, pem) in certificados)
            {
                var certificado = X509Certificate2.CreateFromPem(pem);
                claves.Add(new X509SecurityKey(certificado) { KeyId = kid });
            }

            _claves = claves;
            _clavesVencenEn = DateTime.UtcNow.AddHours(1);
            return _claves;
        }
        finally
        {
            Candado.Release();
        }
    }
}
