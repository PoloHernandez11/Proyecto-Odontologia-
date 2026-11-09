using System.Security.Cryptography;
using System.Text;

namespace Odontia.Application.Common;

/// <summary>Generación de tokens opacos aleatorios y su hash (sólo el hash se persiste).</summary>
public static class SecureTokens
{
    public static string Create(int bytes = 48) => Base64UrlEncode(RandomNumberGenerator.GetBytes(bytes));

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

/// <summary>Enmascarado de datos personales para logs.</summary>
public static class Mask
{
    public static string Email(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return "-";
        var at = email.IndexOf('@');
        return at <= 1 ? "***" : $"{email[0]}***{email[at..]}";
    }

    public static string Phone(string? phone) =>
        string.IsNullOrWhiteSpace(phone) || phone.Length < 4 ? "***" : $"***{phone[^4..]}";
}
