namespace Odontia.Domain.Common;

/// <summary>Entidad base con identificador GUID (estrategia uniforme en todo el sistema).</summary>
public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.CreateVersion7();
}

/// <summary>Entidad con campos de auditoría completados automáticamente por la infraestructura.</summary>
public abstract class AuditableEntity : Entity, IAuditable
{
    public DateTime CreatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}

/// <summary>Entidad perteneciente a un consultorio (aislamiento lógico multi-tenant).</summary>
public abstract class TenantEntity : AuditableEntity, ITenantEntity
{
    public Guid ConsultorioId { get; set; }
}

public interface IAuditable
{
    DateTime CreatedAtUtc { get; set; }
    Guid? CreatedByUserId { get; set; }
    DateTime? UpdatedAtUtc { get; set; }
    Guid? UpdatedByUserId { get; set; }
}

public interface ITenantEntity
{
    Guid ConsultorioId { get; set; }
}

/// <summary>
/// Marca entidades cuyo contenido es clínico/sensible: la auditoría automática registra
/// únicamente los nombres de las propiedades modificadas, nunca sus valores.
/// </summary>
public interface ISensitiveData;
