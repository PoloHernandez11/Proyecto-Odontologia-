using Microsoft.EntityFrameworkCore;
using Odontia.Application.Abstractions;
using Odontia.Application.Common;
using Odontia.Domain.Authorization;
using Odontia.Domain.Entities;
using Odontia.Domain.Enums;

namespace Odontia.Application.Features.Auth;

internal sealed partial class AuthService
{
    private IQueryable<ConsultorioMember> ActiveMembershipsQuery(Guid userId) =>
        db.Members.IgnoreQueryFilters()
            .Where(m => m.UserId == userId && m.IsActive)
            .Where(m => db.Consultorios.Any(c => c.Id == m.ConsultorioId && c.IsActive));

    private async Task<IssuedSession> BuildSessionAsync(ApplicationUser user, Guid? consultorioId, Guid familyId, string rawRefresh,
        DateTime refreshExpires, CancellationToken ct)
    {
        var access = jwt.CreateAccessToken(user, consultorioId, familyId);
        var dto = await BuildSessionDtoAsync(user, consultorioId, familyId, access, ct);
        return new IssuedSession(dto, rawRefresh, refreshExpires);
    }

    private async Task<AuthSessionDto> BuildSessionDtoAsync(ApplicationUser user, Guid? consultorioId, Guid familyId, AccessTokenResult access, CancellationToken ct)
    {
        var memberships = await ActiveMembershipsQuery(user.Id)
            .Select(m => new
            {
                m.ConsultorioId,
                Name = db.Consultorios.Where(c => c.Id == m.ConsultorioId).Select(c => c.Name).First(),
                TimeZoneId = db.Consultorios.Where(c => c.Id == m.ConsultorioId).Select(c => c.TimeZoneId).First(),
                Roles = m.Roles.Select(r => r.Role!.Name).ToList(),
                m.IsProfessional,
                Permissions = m.Roles.SelectMany(r => r.Role!.Permissions.Select(p => p.Permission)).ToList(),
            })
            .ToListAsync(ct);

        var permissions = memberships.FirstOrDefault(m => m.ConsultorioId == consultorioId)?.Permissions.Distinct().Order().ToList() ?? [];
        return new AuthSessionDto(access.Token, access.ExpiresAtUtc, familyId, ToUserDto(user), consultorioId, permissions,
            memberships.Select(m => new MembershipDto(m.ConsultorioId, m.Name, m.Roles, m.IsProfessional, m.TimeZoneId)).ToList());
    }

    private static AuthUserDto ToUserDto(ApplicationUser u) =>
        new(u.Id, u.Email!, u.FirstName, u.LastName, u.DisplayName, u.Title, u.Specialty, u.LicenseNumber, u.TwoFactorEnabled);

    // ---------------- Refresh / rotación ----------------

    public async Task<IssuedSession> RefreshAsync(string refreshToken, string? userAgent, CancellationToken ct)
    {
        var (user, current) = await ValidateRefreshAsync(refreshToken, ct);
        var consultorioId = current.ConsultorioId;
        if (consultorioId is not null && !await ActiveMembershipsQuery(user.Id).AnyAsync(m => m.ConsultorioId == consultorioId, ct))
            consultorioId = await ResolveConsultorioAsync(user, null, ct);
        return await RotateAsync(user, current, consultorioId, userAgent, ct);
    }

    public async Task<IssuedSession> SwitchConsultorioAsync(string refreshToken, Guid consultorioId, string? userAgent, CancellationToken ct)
    {
        var (user, current) = await ValidateRefreshAsync(refreshToken, ct);
        if (current.UserId != tenant.UserId) throw new UnauthorizedException("La sesión no corresponde al usuario autenticado.");
        if (!await ActiveMembershipsQuery(user.Id).AnyAsync(m => m.ConsultorioId == consultorioId, ct))
            throw new ForbiddenAccessException("No pertenece al consultorio solicitado.");
        await TouchUserAsync(user, consultorioId, login: false, ct);
        return await RotateAsync(user, current, consultorioId, userAgent, ct);
    }

    private async Task<(ApplicationUser User, RefreshToken Token)> ValidateRefreshAsync(string refreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) throw new UnauthorizedException("Sesión inválida.", "invalid_refresh_token");
        var hash = SecureTokens.Hash(refreshToken);
        var token = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct)
                    ?? throw new UnauthorizedException("Sesión inválida.", "invalid_refresh_token");

        if (token.RevokedAtUtc is not null)
        {
            if (token.ReplacedByTokenId is { } replacementId)
            {
                // Token rotado hace instantes y con su reemplazo aún activo: es el mismo cliente (otra pestaña o un
                // reintento lento) que llegó tarde a la rotación. No se revoca la sesión ni se borran las cookies.
                if (_opt.RefreshReuseGraceSeconds > 0 && token.RevokedAtUtc.Value.AddSeconds(_opt.RefreshReuseGraceSeconds) > Now)
                {
                    var now = Now;
                    if (await db.RefreshTokens.AnyAsync(t => t.Id == replacementId && t.RevokedAtUtc == null && t.ExpiresAtUtc > now, ct))
                        throw new UnauthorizedException("La sesión ya fue renovada por otra solicitud simultánea.", AuthErrorCodes.RefreshConflict);
                }

                // Reuso de un token ya rotado fuera de la ventana de gracia: posible robo. Se revoca toda la familia (sesión).
                await RevokeFamilyAsync(token.UserId, token.FamilyId, RefreshTokenReasons.ReuseDetected, ct);
                await audit.WriteAsync(AuditAction.SessionRevoked, "Session", token.FamilyId.ToString(), "Reuso de refresh token detectado", token.ConsultorioId, token.UserId);
            }
            throw new UnauthorizedException("Sesión inválida.", "invalid_refresh_token");
        }

        var familyStart = await db.RefreshTokens.Where(t => t.FamilyId == token.FamilyId).MinAsync(t => t.CreatedAtUtc, ct);
        if (token.ExpiresAtUtc <= Now || familyStart.AddDays(_opt.SessionMaxDays) <= Now)
            throw new UnauthorizedException("La sesión expiró.", "session_expired");

        // El bloqueo por intentos fallidos (lockout) NO afecta a las sesiones ya establecidas: de lo contrario, quien
        // conozca el correo podría forzar 5 fallos de login y cerrar todas las sesiones legítimas (QA API-08).
        // Sólo un usuario inexistente o desactivado invalida la sesión.
        var user = await users.FindByIdAsync(token.UserId.ToString());
        if (user is null || !user.IsActive)
        {
            await RevokeFamilyAsync(token.UserId, token.FamilyId, RefreshTokenReasons.UserInactive, ct);
            throw new UnauthorizedException("Sesión inválida.", "invalid_refresh_token");
        }
        return (user, token);
    }

    /// <summary>
    /// Rotación atómica: primero se inserta el nuevo token y luego se "reclama" el actual con un UPDATE condicional
    /// (<c>WHERE Id = @id AND RevokedAtUtc IS NULL</c>). Sólo una solicitud concurrente puede reclamarlo; las demás
    /// reciben 401 <c>refresh_conflict</c> sin revocar la familia (es el mismo cliente renovando en paralelo, no un robo).
    /// El reuso posterior de un token ya rotado sí se trata como robo y revoca la sesión (ver <see cref="ValidateRefreshAsync"/>).
    /// Insertar antes de reclamar evita que la sesión quede un instante sin token activo.
    /// </summary>
    private async Task<IssuedSession> RotateAsync(ApplicationUser user, RefreshToken current, Guid? consultorioId, string? userAgent, CancellationToken ct)
    {
        var now = Now;
        var raw = SecureTokens.Create();
        var next = new RefreshToken(user.Id, current.FamilyId, SecureTokens.Hash(raw), now, now.AddDays(_opt.RefreshTokenDays),
            consultorioId, tenant.IpAddress, userAgent);
        db.RefreshTokens.Add(next);
        await db.SaveChangesAsync(ct);

        var currentId = current.Id;
        var nextId = next.Id;
        var claimed = await db.RefreshTokens.Where(t => t.Id == currentId && t.RevokedAtUtc == null).ExecuteUpdateAsync(s =>
        {
            s.SetProperty(t => t.RevokedAtUtc, now);
            s.SetProperty(t => t.RevokedReason, RefreshTokenReasons.Rotated);
            s.SetProperty(t => t.ReplacedByTokenId, nextId);
            s.SetProperty(t => t.LastUsedAtUtc, now);
        }, ct);

        if (claimed != 1)
        {
            // Otra solicitud ganó la rotación: se descarta el token recién creado (nunca se entregó).
            await db.RefreshTokens.Where(t => t.Id == nextId).ExecuteDeleteAsync(CancellationToken.None);
            throw new UnauthorizedException("La sesión ya fue renovada por otra solicitud simultánea.", AuthErrorCodes.RefreshConflict);
        }
        return await BuildSessionAsync(user, consultorioId, current.FamilyId, raw, next.ExpiresAtUtc, ct);
    }

    private async Task RevokeFamilyAsync(Guid userId, Guid familyId, string reason, CancellationToken ct)
    {
        var tokens = await db.RefreshTokens.Where(t => t.UserId == userId && t.FamilyId == familyId && t.RevokedAtUtc == null).ToListAsync(ct);
        foreach (var t in tokens) t.Revoke(Now, reason);
        await db.SaveChangesAsync(ct);
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return;
        var hash = SecureTokens.Hash(refreshToken);
        var token = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (token is null) return;
        await RevokeFamilyAsync(token.UserId, token.FamilyId, RefreshTokenReasons.Logout, ct);
        await audit.WriteAsync(AuditAction.Logout, "Session", token.FamilyId.ToString(), null, token.ConsultorioId, token.UserId);
    }

    public async Task<AuthSessionDto> GetCurrentAsync(Guid sessionId, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(tenant.UserId.ToString()) ?? throw new UnauthorizedException();
        var dto = await BuildSessionDtoAsync(user, tenant.ConsultorioIdOrNull, sessionId, new AccessTokenResult("", default), ct);
        return dto with { AccessToken = string.Empty };
    }

    // ---------------- Sesiones / dispositivos ----------------

    public async Task<IReadOnlyList<SessionInfoDto>> GetSessionsAsync(Guid currentSessionId, CancellationToken ct)
    {
        var uid = tenant.UserId;
        var tokens = await db.RefreshTokens.Where(t => t.UserId == uid).ToListAsync(ct);
        return tokens.GroupBy(t => t.FamilyId)
            .Select(g => new { Family = g.Key, Active = g.FirstOrDefault(t => t.IsActive(Now)), Start = g.Min(t => t.CreatedAtUtc) })
            .Where(x => x.Active is not null)
            .Select(x => new SessionInfoDto(x.Family, x.Start, x.Active!.LastUsedAtUtc ?? x.Active.CreatedAtUtc, x.Active.ExpiresAtUtc,
                x.Active.IpAddress, x.Active.UserAgent, x.Family == currentSessionId))
            .OrderByDescending(s => s.LastUsedAtUtc)
            .ToList();
    }

    public async Task RevokeSessionAsync(Guid sessionId, CancellationToken ct)
    {
        var uid = tenant.UserId;
        if (!await db.RefreshTokens.AnyAsync(t => t.UserId == uid && t.FamilyId == sessionId, ct)) throw new NotFoundException("La sesión no existe.");
        await RevokeFamilyAsync(uid, sessionId, RefreshTokenReasons.RevokedByUser, ct);
        await audit.WriteAsync(AuditAction.SessionRevoked, "Session", sessionId.ToString(), null, tenant.ConsultorioIdOrNull, uid);
    }

    public async Task<int> RevokeOtherSessionsAsync(Guid currentSessionId, CancellationToken ct)
    {
        var uid = tenant.UserId;
        var tokens = await db.RefreshTokens.Where(t => t.UserId == uid && t.FamilyId != currentSessionId && t.RevokedAtUtc == null).ToListAsync(ct);
        foreach (var t in tokens) t.Revoke(Now, RefreshTokenReasons.RevokedOthers);
        await db.SaveChangesAsync(ct);
        var families = tokens.Select(t => t.FamilyId).Distinct().Count();
        await audit.WriteAsync(AuditAction.SessionRevoked, "Session", null, $"Sesiones revocadas: {families}", tenant.ConsultorioIdOrNull, uid);
        return families;
    }

    private async Task RevokeAllSessionsAsync(Guid userId, Guid? exceptFamily, CancellationToken ct)
    {
        var tokens = await db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAtUtc == null && t.FamilyId != exceptFamily).ToListAsync(ct);
        foreach (var t in tokens) t.Revoke(Now, RefreshTokenReasons.CredentialsChanged);
        await db.SaveChangesAsync(ct);
    }
}
