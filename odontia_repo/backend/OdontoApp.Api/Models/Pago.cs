using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OdontoApp.Api.Models;

/// <summary>
/// Pago realizado por un paciente. Al registrarse genera automáticamente un ingreso en la caja.
/// Los pagos no se borran: se anulan (y su ingreso deja de contar en la caja).
/// </summary>
[Table("pago")]
public class Pago
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("paciente_id")]
    public int PacienteId { get; set; }

    /// <summary>Quién registró el cobro.</summary>
    [Column("usuario_id")]
    public int UsuarioId { get; set; }

    [Column("fecha")]
    public DateTime Fecha { get; set; } = DateTime.UtcNow;

    [Column("monto", TypeName = "decimal(12,2)")]
    public decimal Monto { get; set; }

    [Column("medio_pago")]
    public MedioPago MedioPago { get; set; }

    [Column("concepto"), MaxLength(255)]
    public string? Concepto { get; set; }

    [Column("anulado")]
    public bool Anulado { get; set; }

    public Paciente Paciente { get; set; } = null!;
    public Usuario Usuario { get; set; } = null!;
    public MovimientoCaja? Movimiento { get; set; }
}
