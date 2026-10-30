using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OdontoApp.Api.Models;

/// <summary>
/// Cabecera de un odontograma. Cada vez que el odontólogo guarda cambios se crea un registro nuevo
/// (snapshot fechado), así queda la evolución dental del paciente a lo largo del tiempo.
/// </summary>
[Table("odontograma")]
public class Odontograma
{
    [Key, Column("id")]
    public int Id { get; set; }

    // En el DER la FK se llama "cliente_id"; en el código la exponemos como PacienteId.
    [Column("cliente_id")]
    public int PacienteId { get; set; }

    [Column("usuario_id")]
    public int UsuarioId { get; set; }

    [Column("fecha_registro")]
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    [Column("observaciones", TypeName = "text")]
    public string? Observaciones { get; set; }

    public Paciente Paciente { get; set; } = null!;
    public Usuario Usuario { get; set; } = null!;
    public ICollection<OdontogramaDiente> Dientes { get; set; } = new List<OdontogramaDiente>();
}
