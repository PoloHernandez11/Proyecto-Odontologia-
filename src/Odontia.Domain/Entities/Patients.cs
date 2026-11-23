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
    /// <summary>Token de concurrencia optimista: ediciones simultáneas del paciente responden 409.</summary>
    public Guid Version { get; private set; } = Guid.NewGuid();

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

    // Cada método valida todos sus datos antes de modificar la entidad (sin mutaciones parciales).

    public void SetIdentity(string firstName, string lastName, string documentNumber)
    {
        var first = Guard.Required(firstName, "Nombre", 80);
        var last = Guard.Required(lastName, "Apellido", 80);
        var doc = NormalizeDocument(Guard.Required(documentNumber, "DNI", 20));
        if (doc.Length is < 6 or > 15) throw new DomainException("El DNI informado no es válido.", "invalid_document");
        FirstName = first;
        LastName = last;
        DocumentNumber = doc;
        SearchName = NormalizeText($"{LastName} {FirstName}");
        Touch();
    }

    public void SetPersonalData(DateOnly? birthDate, Sex sex, string? occupation, DateOnly today)
    {
        if (birthDate is not null && (birthDate > today || birthDate < today.AddYears(-130)))
            throw new DomainException("La fecha de nacimiento no es válida.", "invalid_birthdate");
        var occ = Guard.Optional(occupation, "Ocupación", 100);
        BirthDate = birthDate;
        Sex = sex;
        Occupation = occ;
        Touch();
    }

    public void SetContact(string? phone, string? email, string? addressLine, string? city, string? province, string? postalCode)
    {
        var ph = Guard.Optional(phone, "Teléfono", 40);
        var mail = Guard.Optional(email, "Correo", 256)?.ToLowerInvariant();
        var addr = Guard.Optional(addressLine, "Domicilio", 250);
        var c = Guard.Optional(city, "Ciudad", 100);
        var prov = Guard.Optional(province, "Provincia", 100);
        var zip = Guard.Optional(postalCode, "Código postal", 20);
        Phone = ph;
        PhoneNormalized = NormalizePhone(ph);
        Email = mail;
        AddressLine = addr;
        City = c;
        Province = prov;
        PostalCode = zip;
        Touch();
    }

    public void SetEmergencyContact(string? name, string? phone, string? relationship)
    {
        var n = Guard.Optional(name, "Contacto de emergencia", 120);
        var ph = Guard.Optional(phone, "Teléfono de emergencia", 40);
        var rel = Guard.Optional(relationship, "Vínculo", 60);
        EmergencyContactName = n;
        EmergencyContactPhone = ph;
        EmergencyContactRelationship = rel;
        Touch();
    }

    public void SetCoverage(Guid? healthInsuranceId, string? plan, string? affiliateNumber)
    {
        var p = healthInsuranceId is null ? null : Guard.Optional(plan, "Plan", 60);
        var affiliate = healthInsuranceId is null ? null : Guard.Optional(affiliateNumber, "Número de afiliado", 40);
        HealthInsuranceId = healthInsuranceId;
        InsurancePlan = p;
        AffiliateNumber = affiliate;
        Touch();
    }

    public void SetAdministrativeNotes(string? notes)
    {
        AdministrativeNotes = Guard.Optional(notes, "Observaciones", 2000);
        Touch();
    }

    public void SetStatus(PatientStatus status)
    {
        if (Status == status) return;
        Status = status;
        Touch();
    }

    private void Touch() => Version = Guid.NewGuid();

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
    /// <summary>Token de concurrencia optimista; se renueva en <see cref="Validate"/> (paso obligatorio tras modificar).</summary>
    public Guid Version { get; private set; } = Guid.NewGuid();

    /// <summary>Valida y normaliza los antecedentes cargados y marca una nueva versión.</summary>
    public void Validate()
    {
        Allergies = Guard.Optional(Allergies, "Alergias", 500);
        CurrentMedication = Guard.Optional(CurrentMedication, "Medicación actual", 1000);
        OtherConditions = Guard.Optional(OtherConditions, "Otros antecedentes", 2000);
        Version = Guid.NewGuid();
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

    /// <summary>Respuestas negativas habituales (normalizadas: minúsculas, sin acentos ni puntuación).</summary>
    private static readonly HashSet<string> NegativeAnswers =
    [
        "no", "ninguna", "ninguno", "ningunas", "ningunos", "ninguna conocida", "ninguno conocido", "ningunas conocidas",
        "niega", "niega alergias", "no refiere", "no refiere alergias", "refiere ninguna", "no posee", "no tiene", "no presenta",
        "no conocidas", "no conoce", "sin alergias", "sin alergias conocidas", "sin datos", "nada", "negativo", "n a", "na",
    ];

    private static bool IsNoneKnown(string text)
    {
        var normalized = Patient.NormalizeText(text);
        var words = new string(normalized.Select(c => char.IsLetterOrDigit(c) ? c : ' ').ToArray())
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        // Sin letras ni dígitos (p. ej. "-", "--", ".") equivale a no informar alergias.
        return words.Length == 0 || NegativeAnswers.Contains(string.Join(' ', words));
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
