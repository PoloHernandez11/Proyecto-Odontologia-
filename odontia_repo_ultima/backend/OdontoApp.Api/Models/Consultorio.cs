using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OdontoApp.Api.Models;

/// <summary>
/// La "empresa" del sistema: el consultorio. Lo crea el primer odontólogo al registrarse.
/// Una base de datos sirve a un único consultorio, pero todos los usuarios apuntan a él
/// (usuario.consultorio_id), así el modelo queda listo para más de un consultorio.
/// </summary>
[Table("consultorio")]
public class Consultorio
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("nombre"), MaxLength(120)]
    public string Nombre { get; set; } = string.Empty;

    [Column("creado_en")]
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

    public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
    public ICollection<Invitacion> Invitaciones { get; set; } = new List<Invitacion>();
}
