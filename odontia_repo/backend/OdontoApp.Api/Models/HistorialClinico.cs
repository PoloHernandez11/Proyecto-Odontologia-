using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OdontoApp.Api.Models;

/// <summary>
/// Registro de una atención ("Nueva consulta"): tipo de consulta, signos vitales, motivo, antecedentes/examen físico,
/// diagnósticos CIE-10, tratamiento/plan, prescripción, prácticas por pieza, informe y evolución.
/// Solo el odontólogo puede leer y escribir el contenido clínico.
/// Si tiene importe, genera un cargo en la cuenta corriente del paciente (ver PagosController).
/// </summary>
[Table("historial_clinico")]
public class HistorialClinico
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("paciente_id")]
    public int PacienteId { get; set; }

    [Column("usuario_id")]
    public int UsuarioId { get; set; }

    /// <summary>Opcional: la atención puede estar asociada a la consulta (turno) que la originó.</summary>
    [Column("turno_id")]
    public int? TurnoId { get; set; }

    [Column("fecha")]
    public DateTime Fecha { get; set; } = DateTime.UtcNow;

    [Column("tipo_registro")]
    public TipoRegistroClinico TipoRegistro { get; set; } = TipoRegistroClinico.Consulta;

    [Column("tipo_consulta")]
    public TipoConsulta? TipoConsulta { get; set; }

    // ---------- Signos vitales (todos opcionales) ----------
    [Column("ta_max")] public int? TaMax { get; set; }
    [Column("ta_min")] public int? TaMin { get; set; }
    [Column("peso", TypeName = "decimal(5,1)")] public decimal? Peso { get; set; }
    [Column("talla", TypeName = "decimal(5,1)")] public decimal? Talla { get; set; }
    [Column("temperatura", TypeName = "decimal(4,1)")] public decimal? Temperatura { get; set; }
    [Column("frecuencia_cardiaca")] public int? FrecuenciaCardiaca { get; set; }
    [Column("frecuencia_respiratoria")] public int? FrecuenciaRespiratoria { get; set; }
    [Column("saturacion_o2")] public int? SaturacionO2 { get; set; }

    [Column("motivo_consulta"), MaxLength(255)]
    public string? MotivoConsulta { get; set; }

    /// <summary>Antecedentes de la enfermedad actual / examen físico.</summary>
    [Column("antecedentes_examen", TypeName = "text")]
    public string? AntecedentesExamen { get; set; }

    /// <summary>Texto libre del diagnóstico (además de los códigos CIE-10 en diagnostico_atencion).</summary>
    [Column("diagnostico", TypeName = "text")]
    public string? Diagnostico { get; set; }

    /// <summary>Tratamiento / plan.</summary>
    [Column("tratamiento", TypeName = "text")]
    public string? Tratamiento { get; set; }

    [Column("prescripcion", TypeName = "text")]
    public string? Prescripcion { get; set; }

    /// <summary>Informe / resultado (estudios, radiografías).</summary>
    [Column("informe", TypeName = "text")]
    public string? Informe { get; set; }

    /// <summary>Piezas involucradas, en notación FDI separadas por coma (ej: "36" o "14,15").</summary>
    [Column("piezas"), MaxLength(100)]
    public string? Piezas { get; set; }

    /// <summary>Notas de evolución / indicaciones (columna original del DER).</summary>
    [Column("descripcion", TypeName = "text")]
    public string Descripcion { get; set; } = string.Empty;

    /// <summary>Honorarios de la atención. Null = sin cargo.</summary>
    [Column("importe", TypeName = "decimal(12,2)")]
    public decimal? Importe { get; set; }

    public Paciente Paciente { get; set; } = null!;
    public Usuario Usuario { get; set; } = null!;
    public Turno? Turno { get; set; }
    public ICollection<DiagnosticoAtencion> Diagnosticos { get; set; } = new List<DiagnosticoAtencion>();
    public ICollection<PracticaAtencion> Practicas { get; set; } = new List<PracticaAtencion>();
}

/// <summary>Diagnóstico CIE-10 de una atención. El de orden 1 es el principal.</summary>
[Table("diagnostico_atencion")]
public class DiagnosticoAtencion
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("historial_id")]
    public int HistorialId { get; set; }

    [Column("orden")]
    public byte Orden { get; set; }

    [Column("codigo"), MaxLength(10)]
    public string? Codigo { get; set; }

    [Column("descripcion"), MaxLength(200)]
    public string Descripcion { get; set; } = string.Empty;

    [Column("caracter")]
    public CaracterDiagnostico Caracter { get; set; } = CaracterDiagnostico.Presuntivo;

    public HistorialClinico Historial { get; set; } = null!;
}

/// <summary>Hallazgo o práctica realizada en la atención ("Agregar a la lista"), con sus piezas.</summary>
[Table("practica_atencion")]
public class PracticaAtencion
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("historial_id")]
    public int HistorialId { get; set; }

    [Column("codigo"), MaxLength(10)]
    public string? Codigo { get; set; }

    [Column("descripcion"), MaxLength(200)]
    public string Descripcion { get; set; } = string.Empty;

    [Column("piezas"), MaxLength(100)]
    public string? Piezas { get; set; }

    [Column("observaciones"), MaxLength(500)]
    public string? Observaciones { get; set; }

    public HistorialClinico Historial { get; set; } = null!;
}
