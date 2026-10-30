using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OdontoApp.Api.Models;

/// <summary>
/// Persona autorizada a ingresar al consultorio. La autenticación la hace Google:
/// NO se guarda contraseña, nombre de usuario ni credencial alguna.
/// La fila solo registra quién puede entrar, con qué rol, y el identificador estable de Google (sub).
/// </summary>
[Table("usuario")]
public class Usuario
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("consultorio_id")]
    public int ConsultorioId { get; set; }
    public Consultorio Consultorio { get; set; } = null!;

    [Column("nombre"), MaxLength(100)]
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Correo verificado por Google. Es la forma de identificar a la persona dentro del consultorio.</summary>
    [Column("email"), MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    /// <summary>Identificador estable de la cuenta de Google (claim "sub"). Se completa en el primer ingreso.</summary>
    [Column("google_sub"), MaxLength(255)]
    public string? GoogleSub { get; set; }

    [Column("rol")]
    public RolUsuario Rol { get; set; }

    /// <summary>Un usuario desactivado no puede iniciar sesión, aunque su cuenta de Google siga activa.</summary>
    [Column("activo")]
    public bool Activo { get; set; } = true;

    [Column("creado_en")]
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

    public ICollection<Turno> Turnos { get; set; } = new List<Turno>();
    public ICollection<Odontograma> Odontogramas { get; set; } = new List<Odontograma>();
    public ICollection<HistorialClinico> Historiales { get; set; } = new List<HistorialClinico>();
}
