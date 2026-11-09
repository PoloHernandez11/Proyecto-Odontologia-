using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Odontia.Application.Abstractions;
using Odontia.Domain.Entities;

namespace Odontia.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string Section = "Jwt";
    public string Issuer { get; set; } = "odontia-api";
    public string Audience { get; set; } = "odontia-frontend";
    /// <summary>Clave HMAC-SHA256 (mínimo 32 bytes). Debe provenir de User Secrets / variables de entorno.</summary>
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 15;

    public SymmetricSecurityKey GetKey()
    {
        if (string.IsNullOrWhiteSpace(SigningKey) || Encoding.UTF8.GetByteCount(SigningKey) < 32)
            throw new InvalidOperationException("Jwt:SigningKey no está configurada o tiene menos de 32 bytes. Configúrela con User Secrets o la variable de entorno Jwt__SigningKey.");
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
    }
}

public static class OdontiaClaims
{
    public const string Consultorio = "cid";
    public const string Session = "sid";
    public const string Purpose = "purpose";
    public const string SecurityStamp = "sst";
}

/// <summary>
/// Emite access tokens JWT de corta duración. Los permisos NO viajan en el token: se resuelven en el
/// servidor en cada solicitud a partir de la membresía activa, de modo que una revocación es inmediata.
/// </summary>
public sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider clock) : IJwtTokenService
{
    private readonly JwtOptions _opt = options.Value;
    private readonly JsonWebTokenHandler _handler = new();

    public AccessTokenResult CreateAccessToken(ApplicationUser user, Guid? consultorioId, Guid sessionId)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(_opt.AccessTokenMinutes);
        var claims = new Dictionary<string, object>
        {
            [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
            [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
            [OdontiaClaims.Session] = sessionId.ToString(),
            [OdontiaClaims.SecurityStamp] = Stamp(user.SecurityStamp),
        };
        if (consultorioId is not null) claims[OdontiaClaims.Consultorio] = consultorioId.Value.ToString();
        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _opt.Issuer,
            Audience = _opt.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            Claims = claims,
            SigningCredentials = new SigningCredentials(_opt.GetKey(), SecurityAlgorithms.HmacSha256),
        });
        return new AccessTokenResult(token, expires);
    }

    public string CreateTwoFactorChallenge(Guid userId)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        return _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _opt.Issuer,
            Audience = _opt.Audience + ":2fa",
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(5),
            Claims = new Dictionary<string, object> { [JwtRegisteredClaimNames.Sub] = userId.ToString(), [OdontiaClaims.Purpose] = "2fa" },
            SigningCredentials = new SigningCredentials(_opt.GetKey(), SecurityAlgorithms.HmacSha256),
        });
    }

    public Guid? ValidateTwoFactorChallenge(string token)
    {
        var result = _handler.ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = _opt.Issuer,
            ValidAudience = _opt.Audience + ":2fa",
            IssuerSigningKey = _opt.GetKey(),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        }).GetAwaiter().GetResult();
        if (!result.IsValid) return null;
        if (!result.Claims.TryGetValue(OdontiaClaims.Purpose, out var purpose) || purpose as string != "2fa") return null;
        return result.Claims.TryGetValue(JwtRegisteredClaimNames.Sub, out var sub) && Guid.TryParse(sub as string, out var id) ? id : null;
    }

    /// <summary>Huella corta del security stamp (permite invalidar tokens al cambiar credenciales sin exponer el stamp).</summary>
    public static string Stamp(string? securityStamp) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(securityStamp ?? "")))[..16];

    public static Guid? GetUserId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
