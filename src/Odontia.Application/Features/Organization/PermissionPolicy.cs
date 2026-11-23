using Odontia.Application.Abstractions;
using Odontia.Application.Common;

namespace Odontia.Application.Features.Organization;

/// <summary>Reglas anti-escalada de privilegios.</summary>
public static class PermissionPolicy
{
    /// <summary>Un usuario sólo puede otorgar (o quitar) permisos que él mismo posee.</summary>
    public static void EnsureCanGrant(ITenantContext tenant, IEnumerable<string> permissions)
    {
        var missing = permissions.Distinct().Where(p => !tenant.Has(p)).ToList();
        if (missing.Count > 0)
            throw new ForbiddenAccessException($"No puede otorgar permisos que no posee: {string.Join(", ", missing)}.");
    }
}
