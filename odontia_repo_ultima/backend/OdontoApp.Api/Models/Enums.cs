namespace OdontoApp.Api.Models;

// Todos los enums se guardan como texto (ver OdontoDbContext.ConfigureConventions),
// lo que se mapea 1:1 con las columnas ENUM del DER en MySQL.

/// <summary>usuario.rol</summary>
public enum RolUsuario
{
    Secretaria,
    Odontologo
}

/// <summary>turno.estado. "EnSala" = el paciente llegó y espera en recepción.</summary>
public enum EstadoTurno
{
    Pendiente,
    Confirmado,
    EnSala,
    Atendido,
    Cancelado,
    Ausente
}

/// <summary>historial_clinico.tipo_registro: filtros de la historia clínica.</summary>
public enum TipoRegistroClinico
{
    Consulta,
    Evolucion,
    Diagnostico,
    Receta,
    Estudio
}

/// <summary>historial_clinico.tipo_consulta: primera vez o ulterior (seguimiento).</summary>
public enum TipoConsulta
{
    PrimeraVez,
    Ulterior
}

/// <summary>diagnostico_atencion.caracter: P (presuntivo) o C (confirmado).</summary>
public enum CaracterDiagnostico
{
    Presuntivo,
    Confirmado
}

/// <summary>tarea.prioridad</summary>
public enum PrioridadTarea
{
    Alta,
    Media,
    Baja
}

/// <summary>pago.medio_pago y movimiento_caja.medio_pago</summary>
public enum MedioPago
{
    Efectivo,
    Transferencia,
    Debito,
    Credito,
    MercadoPago,
    ObraSocial,
    Otro
}

/// <summary>movimiento_caja.tipo</summary>
public enum TipoMovimiento
{
    Ingreso,
    Egreso
}

/// <summary>
/// movimiento_caja.categoria. Las de ingreso y egreso están separadas;
/// ver <see cref="CategoriasMovimiento.Corresponde"/>.
/// </summary>
public enum CategoriaMovimiento
{
    // Ingresos
    CobroPaciente,
    ObraSocial,
    OtroIngreso,
    // Egresos
    Laboratorio,
    Insumos,
    Alquiler,
    Servicios,
    Sueldos,
    Impuestos,
    Mantenimiento,
    OtroEgreso
}

public static class CategoriasMovimiento
{
    private static readonly HashSet<CategoriaMovimiento> DeIngreso =
        [CategoriaMovimiento.CobroPaciente, CategoriaMovimiento.ObraSocial, CategoriaMovimiento.OtroIngreso];

    public static bool Corresponde(TipoMovimiento tipo, CategoriaMovimiento categoria) =>
        DeIngreso.Contains(categoria) == (tipo == TipoMovimiento.Ingreso);
}

/// <summary>trabajo_laboratorio.estado: el trabajo sale del consultorio (Enviado) y vuelve (Recibido).</summary>
public enum EstadoTrabajoLaboratorio
{
    Enviado,
    Recibido,
    Colocado,
    Cancelado
}

/// <summary>
/// Estado de cada cara del diente (oclusal / mesial / distal / vestibular / lingual).
/// Los nombres coinciden con las opciones de la pantalla y con la base de datos.
/// Color: ARealizar = rojo (a realizar); Existente = azul; Sellante = verde.
/// </summary>
public enum EstadoCara
{
    Sano,
    CariesARealizar,
    CariesExistente,
    ObturacionARealizar,
    ObturacionExistente,
    Sellante
}

/// <summary>
/// Estado general de la pieza. Los nombres coinciden con las opciones de la pantalla y con la base de datos.
/// Color: ARealizar = rojo; Existente = azul.
/// </summary>
public enum EstadoGeneralDiente
{
    Presente,
    AusenteExistente,
    ExtraccionARealizar,
    CoronaARealizar,
    CoronaExistente,
    EndodonciaARealizar,
    EndodonciaExistente,
    ImplanteARealizar,
    ImplanteExistente,
    ProtesisFijaARealizar,
    ProtesisFijaExistente,
    ProtesisRemovibleARealizar,
    ProtesisRemovibleExistente
}
