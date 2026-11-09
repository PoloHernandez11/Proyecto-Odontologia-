using Odontia.Domain.Common;
using Odontia.Domain.Enums;

namespace Odontia.Domain.Entities;

/// <summary>Obra social / prepaga (catálogo por consultorio).</summary>
public class HealthInsurance : TenantEntity
{
    private HealthInsurance() { }

    public HealthInsurance(Guid consultorioId, string name, string? code)
    {
        ConsultorioId = consultorioId;
        Update(name, code, null, null);
        IsActive = true;
    }

    public string Name { get; private set; } = default!;
    public string? Code { get; private set; }
    public string? Phone { get; private set; }
    public string? Notes { get; private set; }
    public bool IsActive { get; private set; }

    public void Update(string name, string? code, string? phone, string? notes)
    {
        Name = Guard.Required(name, "Nombre", 120);
        Code = Guard.Optional(code, "Código", 30);
        Phone = Guard.Optional(phone, "Teléfono", 40);
        Notes = Guard.Optional(notes, "Notas", 500);
    }

    public void SetActive(bool active) => IsActive = active;
}

/// <summary>Paciente del consultorio (datos personales y administrativos).</summary>
public class Patient : TenantEntity
{
    private Patient() { }

    public Patient(Guid consultorioId, string firstName, string lastName, string documentNumber)
    {
        ConsultorioId = consultorioId;
        SetIdentity(firstName, lastName, documentNumber);
        Status = PatientStatus.New;
    }

    public string FirstName { get; private set; } = default!;
    public string LastName { get; private set; } = default!;
    /// <summary>DNI normalizado (sólo dígitos/letras, sin puntos). Único por consultorio.</summary>
    public string DocumentNumber { get; private set; } = default!;
    public DateOnly? BirthDate { get; private set; }
    public Sex Sex { get; private set; }
    public string? Occupation { get; private set; }
    public string? Phone { get; private set; }
    /// <summary>Teléfono normalizado (sólo dígitos) para búsquedas.</summary>
    public string? PhoneNormalized { get; private set; }
    public string? Email { get; private set; }
    public string? AddressLine { get; private set; }
    public string? City { get; private set; }
    public string? Province { get; private set; }
    public string? PostalCode { get; private set; }
    public string? EmergencyContactName { get; private set; }
    public string? EmergencyContactPhone { get; private set; }
    public string? EmergencyContactRelationship { get; private set; }
    public Guid? HealthInsuranceId { get; private set; }
    public string? InsurancePlan { get; private set; }
    public string? AffiliateNumber { get; private set; }
    public string? AdministrativeNotes { get; private set; }
    public PatientStatus Status { get; private set; }
    /// <summary>Campo normalizado para búsqueda insensible a mayúsculas/acentos ("apellido nombre").</summary>
    public string SearchName { get; private set; } = default!;

    public HealthInsurance? HealthInsurance { get; private set; }
    public MedicalHistory? MedicalHistory { get; private set; }

    public static string NormalizeDocument(string value) =>
        new string(value.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    public static string? NormalizePhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var digits = new string(value.Where(char.IsDigit).ToArray());
        return digits.Length == 0 ? null : digits;
    }

    public static string NormalizeText(string value)
    {
        var normalized = value.Trim().ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
        var chars = normalized.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                                          != System.Globalization.UnicodeCategory.NonSpacingMark);
        return new string(chars.ToArray()).Normalize(System.Text.NormalizationForm.FormC);
    }

    public void SetIdentity(string firstName, string lastName, string documentNumber)
    {
        FirstName = Guard.Required(firstName, "Nombre", 80);
        LastName = Guard.Required(lastName, "Apellido", 80);
        var doc = NormalizeDocument(Guard.Required(documentNumber, "DNI", 20));
        if (doc.Length is < 6 or > 15) throw new DomainException("El DNI informado no es válido.", "invalid_document");
        DocumentNumber = doc;
        SearchName = NormalizeText($"{LastName} {FirstName}");
    }

    public void SetPersonalData(DateOnly? birthDate, Sex sex, string? occupation, DateOnly today)
    {
        if (birthDate is not null && (birthDate > today || birthDate < today.AddYears(-130)))
            throw new DomainException("La fecha de nacimiento no es válida.", "invalid_birthdate");
        BirthDate = birthDate;
        Sex = sex;
        Occupation = Guard.Optional(occupation, "Ocupación", 100);
    }

    public void SetContact(string? phone, string? email, string? addressLine, string? city, string? province, string? postalCode)
    {
        Phone = Guard.Optional(phone, "Teléfono", 40);
        PhoneNormalized = NormalizePhone(Phone);
        Email = Guard.Optional(email, "Correo", 256)?.ToLowerInvariant();
        AddressLine = Guard.Optional(addressLine, "Domicilio", 250);
        City = Guard.Optional(city, "Ciudad", 100);
        Province = Guard.Optional(province, "Provincia", 100);
        PostalCode = Guard.Optional(postalCode, "Código postal", 20);
    }

    public void SetEmergencyContact(string? name, string? phone, string? relationship)
    {
        EmergencyContactName = Guard.Optional(name, "Contacto de emergencia", 120);
        EmergencyContactPhone = Guard.Optional(phone, "Teléfono de emergencia", 40);
        EmergencyContactRelationship = Guard.Optional(relationship, "Vínculo", 60);
    }

    public void SetCoverage(Guid? healthInsuranceId, string? plan, string? affiliateNumber)
    {
        HealthInsuranceId = healthInsuranceId;
        InsurancePlan = healthInsuranceId is null ? null : Guard.Optional(plan, "Plan", 60);
        AffiliateNumber = healthInsuranceId is null ? null : Guard.Optional(affiliateNumber, "Número de afiliado", 40);
    }

    public void SetAdministrativeNotes(string? notes) => AdministrativeNotes = Guard.Optional(notes, "Observaciones", 2000);

    public void SetStatus(PatientStatus status) => Status = status;

    public int? AgeAt(DateOnly today)
    {
        if (BirthDate is not { } b) return null;
        var age = today.Year - b.Year;
        if (today < b.AddYears(age)) age--;
        return age;
    }
}

/// <summary>Antecedentes médicos (1:1 con el paciente). Información clínica restringida.</summary>
public class MedicalHistory : TenantEntity, ISensitiveData
{
    private MedicalHistory() { }

    public MedicalHistory(Guid consultorioId, Guid patientId)
    {
        ConsultorioId = consultorioId;
        PatientId = patientId;
    }

    public Guid PatientId { get; private set; }
    public bool Diabetes { get; set; }
    public bool Hypertension { get; set; }
    public bool HeartDisease { get; set; }
    public bool Anticoagulated { get; set; }
    public bool Pregnant { get; set; }
    public bool Asthma { get; set; }
    public bool Hepatitis { get; set; }
    public bool Smoker { get; set; }
    public string? Allergies { get; set; }
    public string? CurrentMedication { get; set; }
    public string? OtherConditions { get; set; }

    public void Validate()
    {
        Allergies = Guard.Optional(Allergies, "Alergias", 500);
        CurrentMedication = Guard.Optional(CurrentMedication, "Medicación actual", 1000);
        OtherConditions = Guard.Optional(OtherConditions, "Otros antecedentes", 2000);
    }

    /// <summary>
    /// Alertas derivadas mínimas y necesarias para la atención (visibles para recepción):
    /// sólo etiquetas, nunca el detalle completo de los antecedentes.
    /// </summary>
    public IEnumerable<(string Title, AlertSeverity Severity)> DerivedAlerts()
    {
        if (!string.IsNullOrWhiteSpace(Allergies) && !IsNoneKnown(Allergies))
            yield return ($"Alergia: {Allergies}", AlertSeverity.Critical);
        if (Anticoagulated) yield return ("Anticoagulado", AlertSeverity.Critical);
        if (HeartDisease) yield return ("Cardiopatía", AlertSeverity.Warning);
        if (Diabetes) yield return ("Diabetes", AlertSeverity.Warning);
        if (Hypertension) yield return ("Hipertensión", AlertSeverity.Warning);
        if (Pregnant) yield return ("Embarazo", AlertSeverity.Warning);
        if (Asthma) yield return ("Asma", AlertSeverity.Info);
        if (Hepatitis) yield return ("Hepatitis", AlertSeverity.Warning);
    }

    private static bool IsNoneKnown(string text)
    {
        var t = Patient.NormalizeText(text);
        return t is "ninguna" or "ninguna conocida" or "no" or "niega" or "sin alergias" or "-";
    }
}

/// <summary>Alerta médica cargada manualmente por el odontólogo.</summary>
public class PatientAlert : TenantEntity
{
    private PatientAlert() { }

    public PatientAlert(Guid consultorioId, Guid patientId, string title, string? details, AlertSeverity severity, bool visibleToReception)
    {
        ConsultorioId = consultorioId;
        PatientId = patientId;
        Update(title, details, severity, visibleToReception);
        IsActive = true;
    }

    public Guid PatientId { get; private set; }
    public string Title { get; private set; } = default!;
    /// <summary>Detalle clínico (sólo visible con permiso de antecedentes médicos).</summary>
    public string? Details { get; private set; }
    public AlertSeverity Severity { get; private set; }
    /// <summary>Si es necesaria para la atención, la recepción ve el título (no el detalle).</summary>
    public bool VisibleToReception { get; private set; }
    public bool IsActive { get; private set; }

    public void Update(string title, string? details, AlertSeverity severity, bool visibleToReception)
    {
        Title = Guard.Required(title, "Título", 150);
        Details = Guard.Optional(details, "Detalle", 1000);
        Severity = severity;
        VisibleToReception = visibleToReception;
    }

    public void Deactivate() => IsActive = false;
}
