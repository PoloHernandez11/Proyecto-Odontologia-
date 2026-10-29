using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OdontoApp.Api.Models;

/// <summary>
/// Ingreso o egreso del consultorio. Puede ser manual (alquiler, insumos…) o automático:
/// un cobro a paciente (PagoId) o el pago de un trabajo de laboratorio (TrabajoLaboratorioId).
/// </summary>
[Table("movimiento_caja")]
public class MovimientoCaja
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("tipo")]
    public TipoMovimiento Tipo { get; set; }

    [Column("categoria")]
    public CategoriaMovimiento Categoria { get; set; }

    [Column("fecha")]
    public DateOnly Fecha { get; set; }

    [Column("descripcion"), MaxLength(255)]
    public string Descripcion { get; set; } = string.Empty;

    [Column("monto", TypeName = "decimal(12,2)")]
    public decimal Monto { get; set; }

    [Column("medio_pago")]
    public MedioPago MedioPago { get; set; }

    [Column("usuario_id")]
    public int UsuarioId { get; set; }

    [Column("pago_id")]
    public int? PagoId { get; set; }

    [Column("trabajo_laboratorio_id")]
    public int? TrabajoLaboratorioId { get; set; }

    [Column("creado_en")]
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

    public Usuario Usuario { get; set; } = null!;
    public Pago? Pago { get; set; }
    public TrabajoLaboratorio? TrabajoLaboratorio { get; set; }

    [NotMapped]
    public bool EsAutomatico => PagoId is not null || TrabajoLaboratorioId is not null;
}
