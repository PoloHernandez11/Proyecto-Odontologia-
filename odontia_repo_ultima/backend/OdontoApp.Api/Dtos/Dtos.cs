using System.ComponentModel.DataAnnotations;
using OdontoApp.Api.Models;

namespace OdontoApp.Api.Dtos;

// ---------- Auth (solo Google: no hay contraseñas ni usuarios propios) ----------

/// <summary>Inicio de sesión: el navegador envía el ID token de Firebase (proveedor Google).</summary>
public record GoogleLoginRequest([Required] string Credential);

/// <summary>Respuesta de sesión: token propio de la API, de vida corta, y los datos del usuario.</summary>
public record LoginResponse(
    string Token, DateTime Expira, int Id, string Nombre, string Email, RolUsuario Rol,
    int ConsultorioId, string ConsultorioNombre);

/// <summary>Si ya existe un consultorio registrado (hay usuarios).</summary>
public record EstadoConsultorioDto(bool Registrado, bool GoogleConfigurado);

/// <summary>Primer ingreso: el odontólogo inicia sesión con Google y nombra el consultorio.</summary>
public record RegistroDuenoDto(
    [Required] string Credential,
    [Required, MaxLength(120)] string NombreConsultorio);

/// <summary>Datos que muestra la pantalla de aceptación de invitación antes de iniciar sesión.</summary>
public record InvitacionPublicaDto(string Email, RolUsuario Rol, string ConsultorioNombre, DateTime ExpiraEn);

/// <summary>La secretaria acepta la invitación iniciando sesión con Google (debe ser el mismo correo invitado).</summary>
public record AceptarInvitacionDto([Required] string Token, [Required] string Credential);

/// <summary>Miembros del consultorio, para "Equipo" en Perfil.</summary>
public record UsuarioDto(int Id, string Nombre, string Email, RolUsuario Rol, bool Activo);

public record ActivarUsuarioDto([Required] bool Activo);

// ---------- Invitaciones ----------
/// <summary>Estado calculado: Pendiente, Aceptada, Vencida o Revocada.</summary>
public record InvitacionDto(
    int Id, string Email, RolUsuario Rol, string Estado,
    DateTime CreadaEn, DateTime ExpiraEn, DateTime? UsadaEn, string CreadaPorNombre);

/// <summary>Respuesta al crear una invitación. <c>Token</c> se muestra una sola vez para armar el enlace.</summary>
public record InvitacionCreadaDto(InvitacionDto Invitacion, string Token);

public record CrearInvitacionDto([Required, EmailAddress, MaxLength(150)] string Email);

// ---------- Pacientes ----------
public record PacienteDto(
    int Id, string Nombre, string Apellido, string Dni, string? Telefono, string? Email,
    DateOnly? FechaNacimiento, string? Sexo, string? Ocupacion, string? Domicilio, string? ContactoEmergencia,
    string? ObraSocial, string? Plan, string? NroAfiliado,
    string? GrupoSanguineo, bool Diabetes, bool Hipertension, bool Embarazo, bool Asma, bool Cardiopatia,
    bool Anticoagulado, bool Hepatitis, bool Fumador, string? Alergias, string? MedicacionActual,
    string? OtrosAntecedentes, bool ConsentimientoFirmado, DateTime CreadoEn,
    IReadOnlyList<string> Alertas);

/// <summary>Fila del listado de pacientes: datos básicos + última visita y próximo turno.</summary>
public record PacienteListaDto(
    int Id, string Nombre, string Apellido, string Dni, string? Telefono, DateOnly? FechaNacimiento,
    string? ObraSocial, string? Plan, string? NroAfiliado, IReadOnlyList<string> Alertas,
    DateOnly? UltimaVisita, DateOnly? ProximoTurnoFecha, TimeOnly? ProximoTurnoHora, DateTime CreadoEn);

public record PacienteGuardarDto(
    [Required, MaxLength(100)] string Nombre,
    [Required, MaxLength(100)] string Apellido,
    [Required, RegularExpression(@"^\d{7,9}$", ErrorMessage = "El DNI debe tener entre 7 y 9 dígitos, sin puntos.")] string Dni,
    [MaxLength(30)] string? Telefono,
    [EmailAddress, MaxLength(150)] string? Email,
    DateOnly? FechaNacimiento,
    [MaxLength(20)] string? Sexo,
    [MaxLength(100)] string? Ocupacion,
    [MaxLength(200)] string? Domicilio,
    [MaxLength(150)] string? ContactoEmergencia,
    [MaxLength(100)] string? ObraSocial,
    [MaxLength(50)] string? Plan,
    [MaxLength(50)] string? NroAfiliado,
    [MaxLength(5)] string? GrupoSanguineo,
    bool Diabetes, bool Hipertension, bool Embarazo, bool Asma, bool Cardiopatia,
    bool Anticoagulado, bool Hepatitis, bool Fumador,
    [MaxLength(255)] string? Alergias,
    [MaxLength(255)] string? MedicacionActual,
    string? OtrosAntecedentes,
    bool ConsentimientoFirmado);

// ---------- Turnos ----------
public record TurnoDto(
    int Id, int PacienteId, string Paciente, string PacienteDni, string? PacienteTelefono,
    string? PacienteObraSocial, int? PacienteEdad, IReadOnlyList<string> PacienteAlertas,
    DateOnly Fecha, TimeOnly Hora, int DuracionMinutos, EstadoTurno Estado, string? Motivo, string? Notas,
    bool Urgencia, TimeOnly? HoraLlegada, string RegistradoPor, DateTime CreadoEn);

public record TurnoCrearDto(
    [Range(1, int.MaxValue)] int PacienteId,
    DateOnly Fecha,
    TimeOnly Hora,
    [Range(10, 240)] int? DuracionMinutos,
    [MaxLength(255)] string? Motivo,
    [MaxLength(255)] string? Notas,
    bool Urgencia = false);

public record TurnoModificarDto(
    DateOnly Fecha, TimeOnly Hora, [Range(10, 240)] int? DuracionMinutos,
    [MaxLength(255)] string? Motivo, [MaxLength(255)] string? Notas, bool Urgencia = false);

public record TurnoCambiarEstadoDto(EstadoTurno Estado);

// ---------- Historial clínico ----------
public record DiagnosticoDto(string? Codigo, [Required, MaxLength(200)] string Descripcion, CaracterDiagnostico Caracter);

public record PracticaDto(
    [MaxLength(10)] string? Codigo, [Required, MaxLength(200)] string Descripcion,
    [MaxLength(100)] string? Piezas, [MaxLength(500)] string? Observaciones);

public record HistorialDto(
    int Id, DateTime Fecha, TipoRegistroClinico TipoRegistro, TipoConsulta? TipoConsulta,
    int? TaMax, int? TaMin, decimal? Peso, decimal? Talla, decimal? Temperatura,
    int? FrecuenciaCardiaca, int? FrecuenciaRespiratoria, int? SaturacionO2,
    string? MotivoConsulta, string? AntecedentesExamen, string? Diagnostico,
    IReadOnlyList<DiagnosticoDto> Diagnosticos, string? Tratamiento, string? Prescripcion,
    IReadOnlyList<PracticaDto> Practicas, string? Informe, string? Piezas,
    string Descripcion, decimal? Importe, int? TurnoId, DateOnly? TurnoFecha, TimeOnly? TurnoHora, string Profesional);

public record HistorialCrearDto(
    int? TurnoId,
    TipoRegistroClinico TipoRegistro,
    TipoConsulta? TipoConsulta,
    [Range(40, 300)] int? TaMax,
    [Range(20, 200)] int? TaMin,
    [Range(1, 400)] decimal? Peso,
    [Range(30, 250)] decimal? Talla,
    [Range(30, 45)] decimal? Temperatura,
    [Range(20, 250)] int? FrecuenciaCardiaca,
    [Range(5, 80)] int? FrecuenciaRespiratoria,
    [Range(50, 100)] int? SaturacionO2,
    [MaxLength(255)] string? MotivoConsulta,
    string? AntecedentesExamen,
    string? Diagnostico,
    List<DiagnosticoDto>? Diagnosticos,
    string? Tratamiento,
    string? Prescripcion,
    List<PracticaDto>? Practicas,
    string? Informe,
    [MaxLength(100)] string? Piezas,
    string? Descripcion,
    [Range(0, 99_999_999)] decimal? Importe);

// ---------- Tareas ----------
public record TareaDto(
    int Id, string Texto, PrioridadTarea Prioridad, string? Categoria, DateOnly? Vence,
    bool Completada, DateTime? CompletadaEn, string CreadaPor, DateTime CreadoEn);

public record TareaGuardarDto(
    [Required, MaxLength(255)] string Texto, PrioridadTarea Prioridad, [MaxLength(30)] string? Categoria, DateOnly? Vence);

// ---------- Pagos y cuenta corriente ----------
public record PagoDto(
    int Id, int PacienteId, string Paciente, DateTime Fecha, decimal Monto, MedioPago MedioPago,
    string? Concepto, bool Anulado, string RegistradoPor);

public record PagoCrearDto([Range(0.01, 99_999_999)] decimal Monto, MedioPago MedioPago, [MaxLength(255)] string? Concepto);

/// <summary>Cargo = atención con importe. Sin datos clínicos: lo ve también la secretaria.</summary>
public record CargoDto(int HistorialId, DateTime Fecha, decimal Importe);

public record CuentaCorrienteDto(
    IReadOnlyList<CargoDto> Cargos, IReadOnlyList<PagoDto> Pagos,
    decimal TotalCargos, decimal TotalPagos, decimal Saldo);

// ---------- Caja: ingresos y egresos ----------
public record MovimientoDto(
    int Id, TipoMovimiento Tipo, CategoriaMovimiento Categoria, DateOnly Fecha, string Descripcion,
    decimal Monto, MedioPago MedioPago, string RegistradoPor, bool Automatico);

public record MovimientoCrearDto(
    TipoMovimiento Tipo,
    CategoriaMovimiento Categoria,
    DateOnly Fecha,
    [Required, MaxLength(255)] string Descripcion,
    [Range(0.01, 99_999_999)] decimal Monto,
    MedioPago MedioPago);

public record TotalCategoriaDto(TipoMovimiento Tipo, CategoriaMovimiento Categoria, decimal Total);

public record ResumenCajaDto(
    DateOnly Desde, DateOnly Hasta, decimal TotalIngresos, decimal TotalEgresos, decimal Balance,
    IReadOnlyList<TotalCategoriaDto> PorCategoria, IReadOnlyList<MovimientoDto> Movimientos);

// ---------- Laboratorio ----------
public record TrabajoLabDto(
    int Id, int PacienteId, string Paciente, string Laboratorio, string TipoTrabajo, string? Piezas,
    string? Descripcion, DateOnly FechaEnvio, DateOnly? FechaEntregaEstimada, DateOnly? FechaRecepcion,
    EstadoTrabajoLaboratorio Estado, decimal? Costo, bool Pagado);

public record TrabajoLabGuardarDto(
    [Range(1, int.MaxValue)] int PacienteId,
    [Required, MaxLength(100)] string Laboratorio,
    [Required, MaxLength(100)] string TipoTrabajo,
    [MaxLength(100)] string? Piezas,
    string? Descripcion,
    DateOnly FechaEnvio,
    DateOnly? FechaEntregaEstimada,
    [Range(0, 99_999_999)] decimal? Costo);

public record TrabajoLabEstadoDto(EstadoTrabajoLaboratorio Estado, DateOnly? Fecha);
public record TrabajoLabPagarDto(MedioPago MedioPago, DateOnly? Fecha);

// ---------- Odontograma ----------
public record DienteDto(
    byte NumeroDiente,
    EstadoGeneralDiente EstadoGeneral,
    EstadoCara Oclusal, EstadoCara Mesial, EstadoCara Distal, EstadoCara Vestibular, EstadoCara Lingual,
    string? Nota = null);

/// <summary>Una atención de la historia clínica que menciona la pieza.</summary>
public record HistorialPiezaDto(int Id, DateTime Fecha, string Profesional, string Titulo, string? Detalle);

public record OdontogramaDto(int? Id, DateTime? FechaRegistro, string? Profesional, string? Observaciones, IReadOnlyList<DienteDto> Dientes);
public record OdontogramaResumenDto(int Id, DateTime FechaRegistro, string Profesional, int PiezasRegistradas);
public record OdontogramaGuardarDto(string? Observaciones, [Required] List<DienteDto> Dientes);

// ---------- Dashboard ----------
public record EstadisticasDto(
    int TurnosHoy, int AtendidosHoy, int PendientesHoy, int EnSalaHoy, int PorConfirmar, int CanceladosMes, int AusentesMes,
    int PacientesNuevosMes, int TotalPacientes,
    decimal IngresosMes, decimal EgresosMes, int TrabajosLabPendientes, int TrabajosLabRecibidos, int TareasAbiertas);
