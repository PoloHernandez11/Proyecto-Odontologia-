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

/// <summary>Datos personales, cobertura y antecedentes médicos: los gestionan ambos roles.</summary>
[ApiController]
[Route("api/pacientes")]
[Authorize(Roles = Roles.Staff)]
public class PacientesController(OdontoDbContext db, IOptions<AgendaOptions> agendaOptions) : ControllerBase
{
    private readonly AgendaOptions _agenda = agendaOptions.Value;

    /// <summary>Búsqueda por apellido, nombre, DNI o teléfono. Incluye última visita y próximo turno.</summary>
    [HttpGet]
    public async Task<ActionResult<List<PacienteListaDto>>> Buscar([FromQuery] string? q)
    {
        var query = db.Pacientes.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var t = q.Trim();
            query = query.Where(p => p.Dni.StartsWith(t) || p.Apellido.Contains(t) || p.Nombre.Contains(t)
                                     || (p.Telefono != null && p.Telefono.Contains(t)));
        }
        var pacientes = await query.OrderBy(p => p.Apellido).ThenBy(p => p.Nombre).Take(50).ToListAsync();

        var ids = pacientes.Select(p => p.Id).ToList();
        var hoy = _agenda.HoyLocal();
        var ultimas = await db.Turnos.AsNoTracking()
            .Where(t => ids.Contains(t.PacienteId) && t.Estado == EstadoTurno.Atendido)
            .GroupBy(t => t.PacienteId)
            .Select(g => new { g.Key, Fecha = g.Max(t => t.Fecha) })
            .ToDictionaryAsync(x => x.Key, x => x.Fecha);
        var proximos = (await db.Turnos.AsNoTracking()
                .Where(t => ids.Contains(t.PacienteId) && t.Fecha >= hoy
                            && (t.Estado == EstadoTurno.Pendiente || t.Estado == EstadoTurno.Confirmado || t.Estado == EstadoTurno.EnSala))
                .Select(t => new { t.PacienteId, t.Fecha, t.Hora })
                .ToListAsync())
            .GroupBy(t => t.PacienteId)
            .ToDictionary(g => g.Key, g => g.OrderBy(t => t.Fecha).ThenBy(t => t.Hora).First());

        return pacientes.Select(p =>
        {
            var prox = proximos.GetValueOrDefault(p.Id);
            return new PacienteListaDto(p.Id, p.Nombre, p.Apellido, p.Dni, p.Telefono, p.FechaNacimiento,
                p.ObraSocial, p.Plan, p.NroAfiliado, p.Alertas(),
                ultimas.TryGetValue(p.Id, out var u) ? u : null, prox?.Fecha, prox?.Hora, p.CreadoEn);
        }).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PacienteDto>> Obtener(int id)
    {
        var p = await db.Pacientes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return p is null ? NotFound() : Mapeos.ADto(p);
    }

    [HttpPost]
    public async Task<ActionResult<PacienteDto>> Crear(PacienteGuardarDto dto)
    {
        if (await db.Pacientes.AnyAsync(p => p.Dni == dto.Dni))
            return Conflict(new { mensaje = $"Ya existe un paciente con DNI {dto.Dni}." });

        var p = new Paciente { CreadoEn = DateTime.UtcNow };
        Aplicar(p, dto);
        db.Pacientes.Add(p);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Obtener), new { id = p.Id }, Mapeos.ADto(p));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<PacienteDto>> Modificar(int id, PacienteGuardarDto dto)
    {
        var p = await db.Pacientes.FindAsync(id);
        if (p is null) return NotFound();
        if (await db.Pacientes.AnyAsync(x => x.Dni == dto.Dni && x.Id != id))
            return Conflict(new { mensaje = $"Ya existe otro paciente con DNI {dto.Dni}." });

        Aplicar(p, dto);
        await db.SaveChangesAsync();
        return Mapeos.ADto(p);
    }

    /// <summary>Turnos del paciente, del más reciente al más antiguo.</summary>
    [HttpGet("{id:int}/turnos")]
    public async Task<ActionResult<List<TurnoDto>>> Turnos(int id)
    {
        if (!await db.Pacientes.AnyAsync(p => p.Id == id)) return NotFound();
        var turnos = await db.Turnos.AsNoTracking().Include(t => t.Paciente).Include(t => t.Usuario)
            .Where(t => t.PacienteId == id)
            .OrderByDescending(t => t.Fecha).ThenByDescending(t => t.Hora)
            .ToListAsync();
        var hoy = _agenda.HoyLocal();
        return turnos.Select(t => Mapeos.ADto(t, hoy)).ToList();
    }

    private static string? Limpio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static void Aplicar(Paciente p, PacienteGuardarDto dto)
    {
        p.Nombre = dto.Nombre.Trim();
        p.Apellido = dto.Apellido.Trim();
        p.Dni = dto.Dni.Trim();
        p.Telefono = Limpio(dto.Telefono);
        p.Email = Limpio(dto.Email);
        p.FechaNacimiento = dto.FechaNacimiento;
        p.Sexo = Limpio(dto.Sexo);
        p.Ocupacion = Limpio(dto.Ocupacion);
        p.Domicilio = Limpio(dto.Domicilio);
        p.ContactoEmergencia = Limpio(dto.ContactoEmergencia);
        p.ObraSocial = Limpio(dto.ObraSocial);
        p.Plan = Limpio(dto.Plan);
        p.NroAfiliado = Limpio(dto.NroAfiliado);
        p.GrupoSanguineo = Limpio(dto.GrupoSanguineo);
        p.Diabetes = dto.Diabetes;
        p.Hipertension = dto.Hipertension;
        p.Embarazo = dto.Embarazo;
        p.Asma = dto.Asma;
        p.Cardiopatia = dto.Cardiopatia;
        p.Anticoagulado = dto.Anticoagulado;
        p.Hepatitis = dto.Hepatitis;
        p.Fumador = dto.Fumador;
        p.Alergias = Limpio(dto.Alergias);
        p.MedicacionActual = Limpio(dto.MedicacionActual);
        p.OtrosAntecedentes = Limpio(dto.OtrosAntecedentes);
        p.ConsentimientoFirmado = dto.ConsentimientoFirmado;
    }
}
