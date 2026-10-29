using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OdontoApp.Api.Models;

/// <summary>
/// Invitación para que una persona se una al consultorio con un rol.
/// Solo el odontólogo puede crear invitaciones (rol Secretaria).
/// El enlace contiene un token aleatorio; en la base se guarda únicamente su hash SHA-256,
/// así una filtración de la base no permite usar invitaciones pendientes.
/// Estado: pendiente = UsadaEn nulo y ExpiraEn en el futuro; aceptada = UsadaEn con valor;
/// vencida = ExpiraEn pasado sin uso; revocada = RevocadaEn con valor.
/// </summary>
[Table("invitacion")]
public class Invitacion
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("consultorio_id")]
    public int ConsultorioId { get; set; }
    public Consultorio Consultorio { get; set; } = null!;

    [Column("email"), MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    [Column("rol")]
    public RolUsuario Rol { get; set; } = RolUsuario.Secretaria;

    /// <summary>SHA-256 en hexadecimal (64 caracteres) del token entregado en el enlace.</summary>
    [Column("token_hash"), MaxLength(64)]
    public string TokenHash { get; set; } = string.Empty;

    [Column("creada_por_id")]
    public int CreadaPorId { get; set; }
    public Usuario CreadaPor { get; set; } = null!;

    [Column("creada_en")]
    public DateTime CreadaEn { get; set; } = DateTime.UtcNow;

    [Column("expira_en")]
    public DateTime ExpiraEn { get; set; }

    [Column("usada_en")]
    public DateTime? UsadaEn { get; set; }

    /// <summary>Usuario creado al aceptar la invitación.</summary>
    [Column("usuario_creado_id")]
    public int? UsuarioCreadoId { get; set; }

    [Column("revocada_en")]
    public DateTime? RevocadaEn { get; set; }
}
