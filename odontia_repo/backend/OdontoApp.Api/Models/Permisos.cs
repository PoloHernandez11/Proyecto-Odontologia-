using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OdontoApp.Api.Models;

/// <summary>Módulos con permisos configurables. Los nombres coinciden con la pantalla y con la base de datos.</summary>
public enum ModuloPermiso
{
    PagosPacientes,
    FinanzasGenerales,
    LaboratorioTrabajos,
    LaboratorioPagos,
    LaboratorioResultadoEconomico,
    HistoriaClinicaOdontograma
}

/// <summary>Acciones de la matriz de permisos.</summary>
public enum AccionPermiso
{
    Registrar,
    EditarCorregir,
    Anular,
    VerResumenes,
    Confidencial
}

/// <summary>
/// Nivel de una celda de la matriz. En pantalla: Permitido = "Con acceso", ConAprobacion = "Con registro".
/// Los nombres del enum no cambian para no tocar los datos guardados en la base.
/// Ambos niveles se aplican al instante; "Con registro" además queda marcado en el historial.
/// </summary>
public enum NivelPermiso
{
    SinAcceso,
    Permitido,
    ConAprobacion
}

/// <summary>Permiso configurado por el odontólogo para un rol del consultorio.</summary>
[Table("permiso_rol")]
public class PermisoRol
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("consultorio_id")]
    public int ConsultorioId { get; set; }

    [Column("rol")]
    public RolUsuario Rol { get; set; }

    [Column("modulo")]
    public ModuloPermiso Modulo { get; set; }

    [Column("accion")]
    public AccionPermiso Accion { get; set; }

    [Column("nivel")]
    public NivelPermiso Nivel { get; set; }
}

/// <summary>Historial de las acciones que hace la secretaria en módulos con permiso (Con acceso o Con registro).</summary>
[Table("actividad_permiso")]
public class ActividadPermiso
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("consultorio_id")]
    public int ConsultorioId { get; set; }

    [Column("usuario_id")]
    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    [Column("modulo")]
    public ModuloPermiso Modulo { get; set; }

    [Column("accion")]
    public AccionPermiso Accion { get; set; }

    /// <summary>Nivel con el que se hizo la acción: Permitido (Con acceso) o ConAprobacion (Con registro).</summary>
    [Column("nivel")]
    public NivelPermiso Nivel { get; set; }

    [Column("creado_en")]
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}
