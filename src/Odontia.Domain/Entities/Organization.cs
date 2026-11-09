using Odontia.Domain.Common;

namespace Odontia.Domain.Entities;

/// <summary>Consultorio (tenant). Cada consultorio aísla lógicamente toda su información.</summary>
public class Consultorio : AuditableEntity
{
    public const int DefaultAppointmentMinutesFallback = 30;

    private Consultorio() { }

    public Consultorio(string name, string timeZoneId)
    {
        Name = Guard.Required(name, "Nombre", 150);
        TimeZoneId = Guard.Required(timeZoneId, "Zona horaria", 64);
        IsActive = true;
        DefaultAppointmentMinutes = DefaultAppointmentMinutesFallback;
        SlotIntervalMinutes = 15;
    }

    public string Name { get; private set; } = default!;
    public string? LegalName { get; private set; }
    public string? TaxId { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? Website { get; private set; }
    public string? AddressLine { get; private set; }
    public string? City { get; private set; }
    public string? Province { get; private set; }
    public string? PostalCode { get; private set; }
    public string Country { get; private set; } = "AR";
    public string TimeZoneId { get; private set; } = default!;
    public int DefaultAppointmentMinutes { get; private set; }
    public int SlotIntervalMinutes { get; private set; }
    /// <summary>Horas de anticipación para recordatorios automáticos (0 = desactivado).</summary>
    public int ReminderHoursBefore { get; private set; } = 24;
    public bool IsActive { get; private set; }

    public void UpdateDetails(string name, string? legalName, string? taxId, string? phone, string? email, string? website,
        string? addressLine, string? city, string? province, string? postalCode, string country)
    {
        Name = Guard.Required(name, "Nombre", 150);
        LegalName = Guard.Optional(legalName, "Razón social", 200);
        TaxId = Guard.Optional(taxId, "CUIT", 20);
        Phone = Guard.Optional(phone, "Teléfono", 40);
        Email = Guard.Optional(email, "Correo", 256);
        Website = Guard.Optional(website, "Sitio web", 256);
        AddressLine = Guard.Optional(addressLine, "Dirección", 250);
        City = Guard.Optional(city, "Ciudad", 100);
        Province = Guard.Optional(province, "Provincia", 100);
        PostalCode = Guard.Optional(postalCode, "Código postal", 20);
        Country = Guard.Required(country, "País", 2).ToUpperInvariant();
    }

    public void UpdateSettings(string timeZoneId, int defaultAppointmentMinutes, int slotIntervalMinutes, int reminderHoursBefore)
    {
        if (defaultAppointmentMinutes is < 5 or > 480)
            throw new DomainException("La duración predeterminada debe estar entre 5 y 480 minutos.");
        if (slotIntervalMinutes is not (5 or 10 or 15 or 20 or 30 or 60))
            throw new DomainException("El intervalo de agenda debe ser 5, 10, 15, 20, 30 o 60 minutos.");
        if (reminderHoursBefore is < 0 or > 168)
            throw new DomainException("La anticipación de recordatorios debe estar entre 0 y 168 horas.");
        TimeZoneId = Guard.Required(timeZoneId, "Zona horaria", 64);
        DefaultAppointmentMinutes = defaultAppointmentMinutes;
        SlotIntervalMinutes = slotIntervalMinutes;
        ReminderHoursBefore = reminderHoursBefore;
    }

    public void SetActive(bool active) => IsActive = active;
}

/// <summary>Consultorio físico / gabinete / sillón dentro de un consultorio.</summary>
public class Gabinete : TenantEntity
{
    private Gabinete() { }

    public Gabinete(Guid consultorioId, string name, string? description)
    {
        ConsultorioId = consultorioId;
        Rename(name, description);
        IsActive = true;
    }

    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    /// <summary>Contador usado como bloqueo de fila para serializar reservas concurrentes.</summary>
    public long LockVersion { get; private set; }

    public void Rename(string name, string? description)
    {
        Name = Guard.Required(name, "Nombre", 80);
        Description = Guard.Optional(description, "Descripción", 250);
    }

    public void SetActive(bool active) => IsActive = active;
}
