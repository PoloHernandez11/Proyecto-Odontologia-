using OdontoApp.Api.Models;

namespace OdontoApp.Api.Dtos;

/// <summary>Conversión entidad → DTO que no se puede traducir a SQL (alertas, edad). Requiere Include de Paciente/Usuario.</summary>
public static class Mapeos
{
    public static int? Edad(DateOnly? nacimiento, DateOnly hoy)
    {
        if (nacimiento is not DateOnly n) return null;
        var edad = hoy.Year - n.Year;
        if (hoy < n.AddYears(edad)) edad--;
        return edad;
    }

    public static TurnoDto ADto(Turno t, DateOnly hoy) => new(
        t.Id, t.PacienteId, t.Paciente.Apellido + ", " + t.Paciente.Nombre,
        t.Paciente.Dni, t.Paciente.Telefono, t.Paciente.ObraSocial, Edad(t.Paciente.FechaNacimiento, hoy),
        t.Paciente.Alertas(),
        t.Fecha, t.Hora, t.DuracionMinutos, t.Estado, t.Motivo, t.Notas, t.Urgencia, t.HoraLlegada,
        t.Usuario.Nombre, t.CreadoEn);

    public static PacienteDto ADto(Paciente p) => new(
        p.Id, p.Nombre, p.Apellido, p.Dni, p.Telefono, p.Email, p.FechaNacimiento,
        p.Sexo, p.Ocupacion, p.Domicilio, p.ContactoEmergencia, p.ObraSocial, p.Plan, p.NroAfiliado,
        p.GrupoSanguineo, p.Diabetes, p.Hipertension, p.Embarazo, p.Asma, p.Cardiopatia,
        p.Anticoagulado, p.Hepatitis, p.Fumador, p.Alergias, p.MedicacionActual,
        p.OtrosAntecedentes, p.ConsentimientoFirmado, p.CreadoEn, p.Alertas());

    public static HistorialDto ADto(HistorialClinico h) => new(
        h.Id, h.Fecha, h.TipoRegistro, h.TipoConsulta,
        h.TaMax, h.TaMin, h.Peso, h.Talla, h.Temperatura, h.FrecuenciaCardiaca, h.FrecuenciaRespiratoria, h.SaturacionO2,
        h.MotivoConsulta, h.AntecedentesExamen, h.Diagnostico,
        h.Diagnosticos.OrderBy(d => d.Orden).Select(d => new DiagnosticoDto(d.Codigo, d.Descripcion, d.Caracter)).ToList(),
        h.Tratamiento, h.Prescripcion,
        h.Practicas.OrderBy(p => p.Id).Select(p => new PracticaDto(p.Codigo, p.Descripcion, p.Piezas, p.Observaciones)).ToList(),
        h.Informe, h.Piezas, h.Descripcion, h.Importe, h.TurnoId, h.Turno?.Fecha, h.Turno?.Hora, h.Usuario.Nombre);
}
