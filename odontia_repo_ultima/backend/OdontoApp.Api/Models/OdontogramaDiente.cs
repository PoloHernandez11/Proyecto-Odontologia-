using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OdontoApp.Api.Models;

/// <summary>
/// Estado de una pieza dental dentro de un odontograma.
/// NumeroDiente usa notación FDI (11–48 permanentes, 51–85 temporales), igual que la ficha en papel.
/// Las 5 caras corresponden a los 5 sectores del cuadrado dibujado en la ficha.
/// Solo se guardan las piezas que tienen algo registrado; las que no aparecen se consideran sanas.
/// </summary>
[Table("odontograma_diente")]
public class OdontogramaDiente
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("odontograma_id")]
    public int OdontogramaId { get; set; }

    [Column("numero_diente", TypeName = "tinyint unsigned")]
    public byte NumeroDiente { get; set; }

    [Column("estado_general")]
    public EstadoGeneralDiente EstadoGeneral { get; set; } = EstadoGeneralDiente.Presente;

    [Column("oclusal")]
    public EstadoCara Oclusal { get; set; }

    [Column("mesial")]
    public EstadoCara Mesial { get; set; }

    [Column("distal")]
    public EstadoCara Distal { get; set; }

    [Column("vestibular")]
    public EstadoCara Vestibular { get; set; }

    [Column("lingual")]
    public EstadoCara Lingual { get; set; }

    /// <summary>Nota libre de la pieza, guardada con cada versión del odontograma.</summary>
    [Column("nota"), MaxLength(255)]
    public string? Nota { get; set; }

    public Odontograma Odontograma { get; set; } = null!;
}
