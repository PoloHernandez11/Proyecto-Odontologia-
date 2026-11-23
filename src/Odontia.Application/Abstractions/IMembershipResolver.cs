namespace Odontia.Application.Abstractions;

/// <summary>Membresía activa resuelta en el servidor para la solicitud: miembro, si es profesional y permisos efectivos.</summary>
public sealed record ResolvedMembership(Guid MemberId, bool IsProfessional, IReadOnlyList<string> Permissions);

/// <summary>
/// Verificaciones por solicitud del estado de autenticación (security stamp, sesión y membresía activa).
/// La implementación puede cachear resultados siempre que invalide ante cambios.
/// </summary>
public interface IMembershipResolver
{
    /// <summary>Membresía activa del usuario en el consultorio (usuario, consultorio y membresía activos), o null.</summary>
    Task<ResolvedMembership?> ResolveAsync(Guid userId, Guid consultorioId, CancellationToken ct);
    Task<string?> GetSecurityStampAsync(Guid userId, CancellationToken ct);
    /// <summary>False si la sesión (familia de refresh tokens) fue revocada: logout, revocación manual, reuso o cambio de credenciales.</summary>
    Task<bool> IsSessionActiveAsync(Guid userId, Guid sessionId, CancellationToken ct);
}
