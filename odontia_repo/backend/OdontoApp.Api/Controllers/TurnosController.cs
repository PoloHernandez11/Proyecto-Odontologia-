using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OdontoApp.Api.Agenda;
using OdontoApp.Api.Auth;
using OdontoApp.Api.Data;
using OdontoApp.Api.Dtos;
using OdontoApp.Api.Models;

namespace OdontoApp.Api.Controllers;

/// <summary>
/// Agenda del consultorio (un solo odontólogo: una sola columna de turnos).
/// Secretaria y Odontólogo pueden crear, modificar, cancelar y registrar la llegada (En sala);
/// solo el Odontólogo puede marcar un turno como Atendido.
/// </summary>
[ApiController]
[Route("api/turnos")]
[Authorize(Roles = Roles.Staff)]
public class TurnosController(OdontoDbContext db, IOptions<AgendaOptions> agendaOptions) : ControllerBase
{
    private readonly AgendaOptions _agenda = agendaOptions.Value;

    /// <summary>Agenda entre dos fechas (por defecto, hoy). Ej: GET /api/turnos?desde=2026-10-15&amp;hasta=2026-10-21</summary>
    [HttpGet]
    public async Task<ActionResult<List<TurnoDto>>> Listar(
        [FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta, [FromQuery] EstadoTurno? estado)
    {
        var hoy = _agenda.HoyLocal();
        var d = desde ?? hoy;
        var h = hasta ?? d;
        if (h < d) return BadRequest(new { mensaje = "'hasta' no puede ser anterior a 'desde'." });
        if (h.DayNumber - d.DayNumber > 62) return BadRequest(new { mensaje = "El rango máximo es de 62 días." });

        var query = db.Turnos.AsNoTracking().Include(t => t.Paciente).Include(t => t.Usuario)
            .Where(t => t.Fecha >= d && t.Fecha <= h);
        if (estado is not null) query = query.Where(t => t.Estado == estado);

        var turnos = await query.OrderBy(t => t.Fecha).ThenBy(t => t.Hora).ToListAsync();
        return turnos.Select(t => Mapeos.ADto(t, hoy)).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TurnoDto>> Obtener(int id)
    {
        var turno = await CargarAsync(id);
        return turno is null ? NotFound() : Mapeos.ADto(turno, _agenda.HoyLocal());
    }

    /// <summary>Configuración de la agenda, para que el frontend dibuje la grilla.</summary>
    [HttpGet("configuracion")]
    public ActionResult<object> Configuracion() => new
    {
        _agenda.DuracionTurnoMinutos,
        _agenda.IntervaloGrillaMinutos,
        _agenda.HoraApertura,
        _agenda.HoraCierre,
        _agenda.AlmuerzoInicio,
        _agenda.AlmuerzoFin,
        _agenda.AtiendeSabados,
    };

    /// <summary>Horarios libres de un día para un turno de la duración indicada.</summary>
    [HttpGet("disponibles")]
    public async Task<ActionResult<List<TimeOnly>>> HorariosDisponibles(
        [FromQuery] DateOnly fecha, [FromQuery] int? duracion, [FromQuery] int? excluir)
    {
        var dur = Math.Clamp(duracion ?? _agenda.DuracionTurnoMinutos, 10, 240);
        if (!DiaHabil(fecha)) return new List<TimeOnly>();

        var ocupados = await OcupadosAsync(fecha, excluir);
        var ahora = _agenda.AhoraLocal();
        var libres = new List<TimeOnly>();
        for (var hora = _agenda.HoraApertura;
             hora.AddMinutes(dur) <= _agenda.HoraCierre && hora >= _agenda.HoraApertura;
             hora = hora.AddMinutes(_agenda.IntervaloGrillaMinutos))
        {
            if (fecha.ToDateTime(hora) <= ahora) continue;
            if (_agenda.PisaAlmuerzo(hora, dur)) continue;
            if (ocupados.Any(o => SeSuperponen(hora, dur, o.Hora, o.DuracionMinutos))) continue;
            libres.Add(hora);
        }
        return libres;
    }

    [HttpPost]
    public async Task<ActionResult<TurnoDto>> Crear(TurnoCrearDto dto)
    {
        if (!await db.Pacientes.AnyAsync(p => p.Id == dto.PacienteId))
            return NotFound(new { mensaje = "El paciente no existe." });

        var duracion = dto.DuracionMinutos ?? _agenda.DuracionTurnoMinutos;
        var error = await ValidarHorarioAsync(dto.Fecha, dto.Hora, duracion, dto.Urgencia, turnoIdExcluido: null);
        if (error is not null) return Conflict(new { mensaje = error });

        var turno = new Turno
        {
            PacienteId = dto.PacienteId,
            UsuarioId = User.GetUsuarioId(),
            Fecha = dto.Fecha,
            Hora = dto.Hora,
            DuracionMinutos = duracion,
            Motivo = Limpio(dto.Motivo),
            Notas = Limpio(dto.Notas),
            Urgencia = dto.Urgencia,
            Estado = EstadoTurno.Pendiente,
            CreadoEn = DateTime.UtcNow,
        };
        db.Turnos.Add(turno);
        await db.SaveChangesAsync();

        var creado = await CargarAsync(turno.Id);
        return CreatedAtAction(nameof(Obtener), new { id = turno.Id }, Mapeos.ADto(creado!, _agenda.HoyLocal()));
    }

    /// <summary>Reprogramar (fecha/hora/duración) o cambiar motivo y notas.</summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<TurnoDto>> Modificar(int id, TurnoModificarDto dto)
    {
        var turno = await db.Turnos.FindAsync(id);
        if (turno is null) return NotFound();
        if (turno.Estado is EstadoTurno.Atendido or EstadoTurno.Cancelado)
            return Conflict(new { mensaje = $"No se puede modificar un turno {turno.Estado.ToString().ToLower()}." });

        var duracion = dto.DuracionMinutos ?? turno.DuracionMinutos;
        if (turno.Fecha != dto.Fecha || turno.Hora != dto.Hora || turno.DuracionMinutos != duracion || turno.Urgencia != dto.Urgencia)
        {
            var error = await ValidarHorarioAsync(dto.Fecha, dto.Hora, duracion, dto.Urgencia, turnoIdExcluido: id);
            if (error is not null) return Conflict(new { mensaje = error });
            if (turno.Fecha != dto.Fecha || turno.Hora != dto.Hora)
            {
                turno.Estado = EstadoTurno.Pendiente; // al reprogramar hay que volver a confirmar
                turno.HoraLlegada = null;
            }
            turno.Fecha = dto.Fecha;
            turno.Hora = dto.Hora;
            turno.DuracionMinutos = duracion;
            turno.Urgencia = dto.Urgencia;
        }
        turno.Motivo = Limpio(dto.Motivo);
        turno.Notas = Limpio(dto.Notas);
        await db.SaveChangesAsync();

        return Mapeos.ADto((await CargarAsync(id))!, _agenda.HoyLocal());
    }

    [HttpPatch("{id:int}/cancelar")]
    public async Task<IActionResult> Cancelar(int id)
    {
        var turno = await db.Turnos.FindAsync(id);
        if (turno is null) return NotFound();
        if (turno.Estado == EstadoTurno.Atendido)
            return Conflict(new { mensaje = "No se puede cancelar un turno ya atendido." });

        turno.Estado = EstadoTurno.Cancelado;
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Cambios de estado del día: Confirmado, EnSala (llegó), Ausente, Pendiente (cualquier rol) y Atendido (solo odontólogo).
    /// La cancelación tiene su propio endpoint.
    /// </summary>
    [HttpPatch("{id:int}/estado")]
    public async Task<IActionResult> CambiarEstado(int id, TurnoCambiarEstadoDto dto)
    {
        if (dto.Estado == EstadoTurno.Cancelado)
            return BadRequest(new { mensaje = "Para cancelar usá PATCH /api/turnos/{id}/cancelar." });

        // Validación de rol a nivel de acción: además del [Authorize] de la clase.
        if (dto.Estado == EstadoTurno.Atendido && !User.EsOdontologo())
            return Forbid();

        var turno = await db.Turnos.FindAsync(id);
        if (turno is null) return NotFound();
        if (turno.Estado == EstadoTurno.Cancelado)
            return Conflict(new { mensaje = "El turno está cancelado. Creá uno nuevo." });

        if (dto.Estado == EstadoTurno.EnSala)
        {
            if (turno.Fecha != _agenda.HoyLocal())
                return Conflict(new { mensaje = "Solo se puede registrar la llegada de un turno de hoy." });
            turno.HoraLlegada ??= TimeOnly.FromDateTime(_agenda.AhoraLocal());
        }
        else if (dto.Estado is EstadoTurno.Pendiente or EstadoTurno.Confirmado or EstadoTurno.Ausente)
        {
            turno.HoraLlegada = null;
        }

        turno.Estado = dto.Estado;
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Estadísticas para el inicio del odontólogo.</summary>
    [HttpGet("estadisticas")]
    [Authorize(Roles = Roles.Odontologo)]
    public async Task<ActionResult<EstadisticasDto>> Estadisticas()
    {
        var hoy = _agenda.HoyLocal();
        var manana = hoy.AddDays(1);
        var inicioMes = new DateOnly(hoy.Year, hoy.Month, 1);
        var inicioMesUtc = inicioMes.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var hoyPorEstado = await db.Turnos.Where(t => t.Fecha == hoy)
            .GroupBy(t => t.Estado).Select(g => new { g.Key, Cantidad = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Cantidad);
        var mesPorEstado = await db.Turnos.Where(t => t.Fecha >= inicioMes && t.Fecha <= hoy)
            .GroupBy(t => t.Estado).Select(g => new { g.Key, Cantidad = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Cantidad);

        var movimientosMes = await db.MovimientosCaja
            .Where(m => m.Fecha >= inicioMes && m.Fecha <= hoy && (m.Pago == null || !m.Pago.Anulado))
            .Select(m => new { m.Tipo, m.Monto })
            .ToListAsync();

        return new EstadisticasDto(
            TurnosHoy: hoyPorEstado.Where(kv => kv.Key != EstadoTurno.Cancelado).Sum(kv => kv.Value),
            AtendidosHoy: hoyPorEstado.GetValueOrDefault(EstadoTurno.Atendido),
            PendientesHoy: hoyPorEstado.GetValueOrDefault(EstadoTurno.Pendiente) + hoyPorEstado.GetValueOrDefault(EstadoTurno.Confirmado)
                           + hoyPorEstado.GetValueOrDefault(EstadoTurno.EnSala),
            EnSalaHoy: hoyPorEstado.GetValueOrDefault(EstadoTurno.EnSala),
            PorConfirmar: await db.Turnos.CountAsync(t => (t.Fecha == hoy || t.Fecha == manana) && t.Estado == EstadoTurno.Pendiente),
            CanceladosMes: mesPorEstado.GetValueOrDefault(EstadoTurno.Cancelado),
            AusentesMes: mesPorEstado.GetValueOrDefault(EstadoTurno.Ausente),
            PacientesNuevosMes: await db.Pacientes.CountAsync(p => p.CreadoEn >= inicioMesUtc),
            TotalPacientes: await db.Pacientes.CountAsync(),
            IngresosMes: movimientosMes.Where(m => m.Tipo == TipoMovimiento.Ingreso).Sum(m => m.Monto),
            EgresosMes: movimientosMes.Where(m => m.Tipo == TipoMovimiento.Egreso).Sum(m => m.Monto),
            TrabajosLabPendientes: await db.TrabajosLaboratorio.CountAsync(t => t.Estado == EstadoTrabajoLaboratorio.Enviado),
            TrabajosLabRecibidos: await db.TrabajosLaboratorio.CountAsync(t => t.Estado == EstadoTrabajoLaboratorio.Recibido),
            TareasAbiertas: await db.Tareas.CountAsync(t => !t.Completada));
    }

    // ----------------------------------------------------------------------------------

    private static string? Limpio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private Task<Turno?> CargarAsync(int id) =>
        db.Turnos.AsNoTracking().Include(t => t.Paciente).Include(t => t.Usuario).FirstOrDefaultAsync(t => t.Id == id);

    private bool DiaHabil(DateOnly fecha) =>
        fecha.DayOfWeek != DayOfWeek.Sunday && (_agenda.AtiendeSabados || fecha.DayOfWeek != DayOfWeek.Saturday);

    /// <summary>[a, a+da) y [b, b+db) se pisan.</summary>
    private static bool SeSuperponen(TimeOnly a, int da, TimeOnly b, int db) =>
        a < b.AddMinutes(db) && b < a.AddMinutes(da);

    private async Task<List<(TimeOnly Hora, int DuracionMinutos)>> OcupadosAsync(DateOnly fecha, int? excluir)
    {
        var lista = await db.Turnos.AsNoTracking()
            .Where(t => t.Fecha == fecha && t.Estado != EstadoTurno.Cancelado && t.Id != (excluir ?? 0))
            .Select(t => new { t.Hora, t.DuracionMinutos })
            .ToListAsync();
        return lista.Select(t => (t.Hora, t.DuracionMinutos)).ToList();
    }

    /// <summary>Devuelve un mensaje de error si el horario no es válido, o null si se puede reservar.</summary>
    private async Task<string?> ValidarHorarioAsync(DateOnly fecha, TimeOnly hora, int duracion, bool urgencia, int? turnoIdExcluido)
    {
        if (fecha.ToDateTime(hora) <= _agenda.AhoraLocal())
            return "No se pueden agendar turnos en el pasado.";

        if (!DiaHabil(fecha))
            return fecha.DayOfWeek == DayOfWeek.Sunday ? "El consultorio no atiende los domingos." : "El consultorio no atiende los sábados.";

        var fin = hora.AddMinutes(duracion);
        if (hora < _agenda.HoraApertura || fin > _agenda.HoraCierre || fin < hora)
            return $"El horario debe estar entre {_agenda.HoraApertura:HH\\:mm} y {_agenda.HoraCierre:HH\\:mm}.";

        // Una urgencia (sobreturno) puede pisar el almuerzo u otro turno: lo decide la recepción a conciencia.
        if (urgencia) return null;

        if (_agenda.PisaAlmuerzo(hora, duracion))
            return $"El horario {_agenda.AlmuerzoInicio:HH\\:mm}–{_agenda.AlmuerzoFin:HH\\:mm} está bloqueado (almuerzo). Marcalo como urgencia si es un sobreturno.";

        var ocupados = await OcupadosAsync(fecha, turnoIdExcluido);
        var choques = ocupados.Where(o => SeSuperponen(hora, duracion, o.Hora, o.DuracionMinutos)).ToList();
        if (choques.Count == 0) return null;
        var (horaChoque, durChoque) = choques[0];
        return $"Ya hay un turno a las {horaChoque:HH\\:mm} ({durChoque} min) que se superpone. Marcalo como urgencia si es un sobreturno.";
    }
}
