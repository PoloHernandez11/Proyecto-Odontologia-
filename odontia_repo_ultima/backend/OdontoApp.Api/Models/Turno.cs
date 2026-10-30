using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OdontoApp.Api.Models;

[Table("turno")]
public class Turno
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("paciente_id")]
    public int PacienteId { get; set; }

    /// <summary>Usuario (secretaria u odontólogo) que registró el turno.</summary>
    [Column("usuario_id")]
    public int UsuarioId { get; set; }

    [Column("fecha")]
    public DateOnly Fecha { get; set; }

    [Column("hora")]
    public TimeOnly Hora { get; set; }

    /// <summary>Duración reservada. Al haber un solo odontólogo, dos turnos no pueden superponerse (salvo urgencia).</summary>
    [Column("duracion_minutos")]
    public int DuracionMinutos { get; set; } = 30;

    [Column("estado")]
    public EstadoTurno Estado { get; set; } = EstadoTurno.Pendiente;

    [Column("motivo"), MaxLength(255)]
    public string? Motivo { get; set; }

    /// <summary>Notas para el turno (ej: "traer radiografía panorámica").</summary>
    [Column("notas"), MaxLength(255)]
    public string? Notas { get; set; }

    /// <summary>Urgencia / sobreturno: se permite aunque se superponga con otro turno.</summary>
    [Column("urgencia")]
    public bool Urgencia { get; set; }

    /// <summary>Hora en que el paciente llegó a la sala de espera (estado EnSala).</summary>
    [Column("hora_llegada")]
    public TimeOnly? HoraLlegada { get; set; }

    [Column("creado_en")]
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

    public Paciente Paciente { get; set; } = null!;
    public Usuario Usuario { get; set; } = null!;

    /// <summary>Un turno puede tener 0..N registros de historial clínico (relación opcional del DER).</summary>
    public ICollection<HistorialClinico> Historiales { get; set; } = new List<HistorialClinico>();
}
