using System.Security.Cryptography;
using System.Text;

namespace OdontoApp.Api.Auth;

/// <summary>
/// Tokens de invitación de un solo uso. El token viaja en el enlace; en la base solo se guarda su SHA-256.
/// </summary>
public static class InvitacionToken
{
    public static readonly TimeSpan Vigencia = TimeSpan.FromDays(7);

    /// <summary>Genera 32 bytes aleatorios en base64url (sin relleno, apto para URL).</summary>
    public static string Generar()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>SHA-256 en hexadecimal minúscula (64 caracteres), igual al que guarda la base.</summary>
    public static string Hash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
