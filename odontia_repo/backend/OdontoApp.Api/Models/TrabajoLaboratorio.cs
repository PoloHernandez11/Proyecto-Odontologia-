using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OdontoApp.Api.Models;

/// <summary>
/// Trabajo enviado a un laboratorio dental (corona, puente, prótesis, placa…).
/// Registra la salida del consultorio (fecha_envio) y el regreso (fecha_recepcion).
/// </summary>
[Table("trabajo_laboratorio")]
public class TrabajoLaboratorio
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("paciente_id")]
    public int PacienteId { get; set; }

    [Column("usuario_id")]
    public int UsuarioId { get; set; }

    [Column("laboratorio"), MaxLength(100)]
    public string Laboratorio { get; set; } = string.Empty;

    [Column("tipo_trabajo"), MaxLength(100)]
    public string TipoTrabajo { get; set; } = string.Empty;

    /// <summary>Piezas FDI involucradas, ej. "11, 21".</summary>
    [Column("piezas"), MaxLength(100)]
    public string? Piezas { get; set; }

    [Column("descripcion", TypeName = "text")]
    public string? Descripcion { get; set; }

    [Column("fecha_envio")]
    public DateOnly FechaEnvio { get; set; }

    [Column("fecha_entrega_estimada")]
    public DateOnly? FechaEntregaEstimada { get; set; }

    [Column("fecha_recepcion")]
    public DateOnly? FechaRecepcion { get; set; }

    [Column("estado")]
    public EstadoTrabajoLaboratorio Estado { get; set; } = EstadoTrabajoLaboratorio.Enviado;

    [Column("costo", TypeName = "decimal(12,2)")]
    public decimal? Costo { get; set; }

    [Column("pagado")]
    public bool Pagado { get; set; }

    [Column("creado_en")]
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

    public Paciente Paciente { get; set; } = null!;
    public Usuario Usuario { get; set; } = null!;
    public MovimientoCaja? Movimiento { get; set; }
}
