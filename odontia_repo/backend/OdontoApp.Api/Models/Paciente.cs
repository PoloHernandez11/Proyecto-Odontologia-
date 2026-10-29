using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OdontoApp.Api.Models;

[Table("paciente")]
public class Paciente
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("nombre"), MaxLength(100)]
    public string Nombre { get; set; } = string.Empty;

    [Column("apellido"), MaxLength(100)]
    public string Apellido { get; set; } = string.Empty;

    [Column("dni"), MaxLength(15)]
    public string Dni { get; set; } = string.Empty;

    [Column("telefono"), MaxLength(30)]
    public string? Telefono { get; set; }

    [Column("email"), MaxLength(150)]
    public string? Email { get; set; }

    [Column("fecha_nacimiento")]
    public DateOnly? FechaNacimiento { get; set; }

    [Column("sexo"), MaxLength(20)]
    public string? Sexo { get; set; }

    [Column("ocupacion"), MaxLength(100)]
    public string? Ocupacion { get; set; }

    [Column("domicilio"), MaxLength(200)]
    public string? Domicilio { get; set; }

    [Column("contacto_emergencia"), MaxLength(150)]
    public string? ContactoEmergencia { get; set; }

    [Column("obra_social"), MaxLength(100)]
    public string? ObraSocial { get; set; }

    [Column("plan"), MaxLength(50)]
    public string? Plan { get; set; }

    [Column("nro_afiliado"), MaxLength(50)]
    public string? NroAfiliado { get; set; }

    // ---------- Antecedentes médicos (los carga la recepción al registrar; los ve el odontólogo en la ficha) ----------

    [Column("grupo_sanguineo"), MaxLength(5)]
    public string? GrupoSanguineo { get; set; }

    [Column("diabetes")] public bool Diabetes { get; set; }
    [Column("hipertension")] public bool Hipertension { get; set; }
    [Column("embarazo")] public bool Embarazo { get; set; }
    [Column("asma")] public bool Asma { get; set; }
    [Column("cardiopatia")] public bool Cardiopatia { get; set; }
    [Column("anticoagulado")] public bool Anticoagulado { get; set; }
    [Column("hepatitis")] public bool Hepatitis { get; set; }
    [Column("fumador")] public bool Fumador { get; set; }

    [Column("alergias"), MaxLength(255)]
    public string? Alergias { get; set; }

    [Column("medicacion_actual"), MaxLength(255)]
    public string? MedicacionActual { get; set; }

    [Column("otros_antecedentes", TypeName = "text")]
    public string? OtrosAntecedentes { get; set; }

    [Column("consentimiento_firmado")]
    public bool ConsentimientoFirmado { get; set; }

    [Column("creado_en")]
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

    public ICollection<Turno> Turnos { get; set; } = new List<Turno>();
    public ICollection<Odontograma> Odontogramas { get; set; } = new List<Odontograma>();
    public ICollection<HistorialClinico> Historiales { get; set; } = new List<HistorialClinico>();
    public ICollection<Pago> Pagos { get; set; } = new List<Pago>();
    public ICollection<TrabajoLaboratorio> TrabajosLaboratorio { get; set; } = new List<TrabajoLaboratorio>();

    /// <summary>
    /// Alertas médicas relevantes para la atención (alergias, anticoagulación, etc.).
    /// Las ve también la secretaria: no son el contenido clínico, sino avisos de seguridad.
    /// </summary>
    public List<string> Alertas()
    {
        var alertas = new List<string>();
        if (!string.IsNullOrWhiteSpace(Alergias) && !Alergias.Trim().StartsWith("ningun", StringComparison.OrdinalIgnoreCase))
            alertas.Add($"Alergia: {Alergias.Trim()}");
        if (Anticoagulado) alertas.Add("Anticoagulado");
        if (Cardiopatia) alertas.Add("Cardiopatía");
        if (Diabetes) alertas.Add("Diabetes");
        if (Hipertension) alertas.Add("Hipertensión");
        if (Embarazo) alertas.Add("Embarazo");
        if (Hepatitis) alertas.Add("Hepatitis");
        if (Asma) alertas.Add("Asma");
        return alertas;
    }
}
