using Odontia.Application.Abstractions;
using Odontia.Domain.Authorization;

namespace Odontia.Application.Common;

/// <summary>
/// Implementación scoped del contexto de solicitud. Se inicializa únicamente desde el servidor
/// (middleware de resolución de consultorio o trabajos en segundo plano); nunca desde el cuerpo de la solicitud.
/// </summary>
public sealed class TenantContext : ITenantContext, ITenantContextInitializer
{
    private Guid? _userId;
    private Guid? _consultorioId;
    private Guid _memberId;
    private HashSet<string> _permissions = [];

    public bool IsAuthenticated => _userId is not null;
    public bool HasTenant => _consultorioId is not null;
    public bool IsBackgroundJob { get; private set; }
    public bool IsProfessional { get; private set; }
    public string? IpAddress { get; private set; }

    public Guid UserId => _userId ?? throw new UnauthorizedException("Se requiere autenticación.");
    public Guid ConsultorioId => _consultorioId ?? throw new ForbiddenAccessException("No hay un consultorio activo para la sesión.");
    public Guid? ConsultorioIdOrNull => _consultorioId;
    public Guid MemberId => HasTenant ? _memberId : throw new ForbiddenAccessException("No hay un consultorio activo para la sesión.");
    public IReadOnlySet<string> Permissions => _permissions;

    public bool Has(string permission) => IsBackgroundJob || _permissions.Contains(permission);

    public void Require(string permission)
    {
        if (!HasTenant) throw new ForbiddenAccessException("No hay un consultorio activo para la sesión.");
        if (!Has(permission)) throw new ForbiddenAccessException();
    }

    public bool CanActOnProfessional(Guid professionalUserId) =>
        Has(Domain.Authorization.Permissions.AgendaAllProfessionals) || (_userId == professionalUserId && IsProfessional);

    public void InitializeUser(Guid userId, string? ipAddress)
    {
        _userId = userId;
        IpAddress = ipAddress;
    }

    public void InitializeTenant(Guid consultorioId, Guid memberId, bool isProfessional, IEnumerable<string> permissions)
    {
        _consultorioId = consultorioId;
        _memberId = memberId;
        IsProfessional = isProfessional;
        _permissions = permissions.ToHashSet(StringComparer.Ordinal);
    }

    public void InitializeBackground(Guid consultorioId)
    {
        IsBackgroundJob = true;
        _consultorioId = consultorioId;
        _permissions = Domain.Authorization.Permissions.All.ToHashSet(StringComparer.Ordinal);
    }
}
