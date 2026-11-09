using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Odontia.Application.Common;

namespace Odontia.Api.Infrastructure;

public sealed class AuthCookieOptions
{
    public const string Section = "Auth:Cookies";
    public string RefreshCookieName { get; set; } = "odontia_rt";
    public string CsrfCookieName { get; set; } = "odontia_csrf";
    public string CsrfHeaderName { get; set; } = "X-CSRF-Token";
    /// <summary>Siempre true fuera de pruebas automatizadas (los navegadores aceptan cookies Secure en http://localhost).</summary>
    public bool Secure { get; set; } = true;
    /// <summary>Strict si front y API comparten sitio; None (con Secure) si están en sitios distintos.</summary>
    public SameSiteMode SameSite { get; set; } = SameSiteMode.Strict;
    public string? Domain { get; set; }
    public string RefreshCookiePath { get; set; } = "/api/v1/auth";
}

/// <summary>
/// Refresh token en cookie HttpOnly (inaccesible para JavaScript). Protección CSRF de doble envío:
/// el token CSRF viaja en una cookie no-HttpOnly y en el cuerpo de la respuesta; los endpoints que
/// usan la cookie de refresh exigen que el encabezado X-CSRF-Token coincida con la cookie.
/// </summary>
public sealed class AuthCookieManager(IOptions<AuthCookieOptions> options)
{
    private readonly AuthCookieOptions _o = options.Value;

    public string? ReadRefreshToken(HttpRequest request) => request.Cookies.TryGetValue(_o.RefreshCookieName, out var v) ? v : null;

    public string Issue(HttpResponse response, string refreshToken, DateTime expiresUtc)
    {
        var csrf = SecureTokens.Create(32);
        response.Cookies.Append(_o.RefreshCookieName, refreshToken, new CookieOptions
        {
            HttpOnly = true, Secure = _o.Secure, SameSite = _o.SameSite, Path = _o.RefreshCookiePath, Domain = _o.Domain,
            Expires = expiresUtc, IsEssential = true,
        });
        response.Cookies.Append(_o.CsrfCookieName, csrf, new CookieOptions
        {
            HttpOnly = false, Secure = _o.Secure, SameSite = _o.SameSite, Path = "/", Domain = _o.Domain, Expires = expiresUtc, IsEssential = true,
        });
        return csrf;
    }

    public void Clear(HttpResponse response)
    {
        response.Cookies.Delete(_o.RefreshCookieName, new CookieOptions { Path = _o.RefreshCookiePath, Domain = _o.Domain, Secure = _o.Secure, SameSite = _o.SameSite });
        response.Cookies.Delete(_o.CsrfCookieName, new CookieOptions { Path = "/", Domain = _o.Domain, Secure = _o.Secure, SameSite = _o.SameSite });
    }

    /// <summary>Comparación en tiempo constante del encabezado CSRF contra la cookie.</summary>
    public void ValidateCsrf(HttpRequest request)
    {
        request.Cookies.TryGetValue(_o.CsrfCookieName, out var cookie);
        var header = request.Headers[_o.CsrfHeaderName].ToString();
        if (string.IsNullOrEmpty(cookie) || string.IsNullOrEmpty(header)
            || !CryptographicOperations.FixedTimeEquals(System.Text.Encoding.UTF8.GetBytes(cookie), System.Text.Encoding.UTF8.GetBytes(header)))
            throw new ForbiddenAccessException("Token CSRF ausente o inválido.");
    }
}
