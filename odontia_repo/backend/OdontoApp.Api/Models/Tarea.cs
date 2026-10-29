using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OdontoApp.Api.Models;

/// <summary>Tarea del consultorio (confirmar turnos, llamar a un paciente, retirar un trabajo de laboratorio…).</summary>
[Table("tarea")]
public class Tarea
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("texto"), MaxLength(255)]
    public string Texto { get; set; } = string.Empty;

    [Column("prioridad")]
    public PrioridadTarea Prioridad { get; set; } = PrioridadTarea.Media;

    /// <summary>Etiqueta libre: Recepción, Clínica, Laboratorio, Insumos, Administración…</summary>
    [Column("categoria"), MaxLength(30)]
    public string? Categoria { get; set; }

    [Column("vence")]
    public DateOnly? Vence { get; set; }

    [Column("completada")]
    public bool Completada { get; set; }

    [Column("completada_en")]
    public DateTime? CompletadaEn { get; set; }

    [Column("usuario_id")]
    public int UsuarioId { get; set; }

    [Column("creado_en")]
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

    public Usuario Usuario { get; set; } = null!;
}
