namespace Odontia.Application.Features.Auth;

/// <summary>Motivos de revocación de refresh tokens. Sólo <see cref="Rotated"/> mantiene viva la sesión (familia).</summary>
public static class RefreshTokenReasons
{
    public const string Rotated = "rotated";
    public const string ReuseDetected = "reuse_detected";
    public const string UserInactive = "user_inactive";
    public const string Logout = "logout";
    public const string RevokedByUser = "revoked_by_user";
    public const string RevokedOthers = "revoked_others";
    public const string CredentialsChanged = "credentials_changed";
}

public static class AuthErrorCodes
{
    /// <summary>Renovación perdida frente a otra simultánea con el mismo refresh token: no borra cookies ni revoca la sesión.</summary>
    public const string RefreshConflict = "refresh_conflict";
}

/// <summary>
/// Registro de valores de un solo uso (jti de desafíos 2FA, códigos TOTP ya usados) hasta su vencimiento.
/// La implementación es en memoria: válida mientras la API corra en una única instancia.
/// </summary>
public interface IOneTimeUseStore
{
    /// <summary>Reserva la clave de forma atómica. False si ya estaba reservada y no venció.</summary>
    bool TryReserve(string key, DateTimeOffset expiresAtUtc);

    /// <summary>Libera una reserva (p. ej. el desafío 2FA cuando el código fue incorrecto, para permitir reintentar).</summary>
    void Release(string key);
}

/// <summary>Correo transaccional a enviar fuera del ciclo de la solicitud.</summary>
public sealed record DeferredEmail(Guid? ConsultorioId, string To, string Subject, string Html, string Text, string Purpose,
    bool ContainsSecret, string? RelatedType, Guid? RelatedId);

/// <summary>
/// Cola de correos en segundo plano: la respuesta HTTP no espera al proveedor SMTP, de modo que el tiempo de
/// respuesta no revela si la cuenta existe y un SMTP lento no bloquea la solicitud.
/// </summary>
public interface IEmailQueue
{
    void Enqueue(DeferredEmail email);
}
