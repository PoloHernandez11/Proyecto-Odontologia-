namespace OdontoApp.Api.Agenda;

/// <summary>
/// Configuración del consultorio (sección "Agenda" de appsettings.json).
/// Hay un único odontólogo, así que la agenda es una sola columna: dos turnos no pueden superponerse.
/// </summary>
public class AgendaOptions
{
    /// <summary>Duración por defecto de un turno nuevo (cada turno puede tener la suya).</summary>
    public int DuracionTurnoMinutos { get; set; } = 30;

    /// <summary>Cada cuántos minutos se ofrecen horarios en el selector.</summary>
    public int IntervaloGrillaMinutos { get; set; } = 15;

    public TimeOnly HoraApertura { get; set; } = new(8, 0);
    public TimeOnly HoraCierre { get; set; } = new(20, 0);

    /// <summary>Horario bloqueado (almuerzo). Si alguno es null, no hay bloqueo.</summary>
    public TimeOnly? AlmuerzoInicio { get; set; } = new(13, 0);
    public TimeOnly? AlmuerzoFin { get; set; } = new(14, 0);

    /// <summary>¿Se atiende los sábados? Los domingos nunca.</summary>
    public bool AtiendeSabados { get; set; } = true;

    public string ZonaHoraria { get; set; } = "America/Argentina/Buenos_Aires";

    /// <summary>Fecha y hora actuales en la zona del consultorio (el servidor puede estar en UTC).</summary>
    public DateTime AhoraLocal() =>
        TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, ZonaHoraria);

    public DateOnly HoyLocal() => DateOnly.FromDateTime(AhoraLocal());

    /// <summary>True si [inicio, inicio+duracion) pisa el horario de almuerzo.</summary>
    public bool PisaAlmuerzo(TimeOnly inicio, int duracion) =>
        AlmuerzoInicio is TimeOnly a && AlmuerzoFin is TimeOnly b &&
        inicio < b && a < inicio.AddMinutes(duracion);
}
