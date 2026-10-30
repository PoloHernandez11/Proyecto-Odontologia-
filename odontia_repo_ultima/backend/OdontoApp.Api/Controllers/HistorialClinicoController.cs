using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OdontoApp.Api.Auth;
using OdontoApp.Api.Data;
using OdontoApp.Api.Dtos;
using OdontoApp.Api.Models;

namespace OdontoApp.Api.Controllers;

/// <summary>
/// Historia clínica: consultas, evoluciones, diagnósticos (CIE-10), recetas y estudios.
/// Acceso exclusivo del odontólogo.
/// </summary>
[ApiController]
[Route("api/pacientes/{pacienteId:int}/historial")]
[Authorize(Roles = Roles.Staff)]
public class HistorialClinicoController(OdontoDbContext db) : ControllerBase
{
    [HttpGet]
    [Permiso(ModuloPermiso.HistoriaClinicaOdontograma, AccionPermiso.VerResumenes)]
    public async Task<ActionResult<List<HistorialDto>>> Listar(int pacienteId, [FromQuery] TipoRegistroClinico? tipo)
    {
        if (!await db.Pacientes.AnyAsync(p => p.Id == pacienteId)) return NotFound();
        var query = db.HistorialesClinicos.AsNoTracking()
            .Include(h => h.Usuario).Include(h => h.Turno).Include(h => h.Diagnosticos).Include(h => h.Practicas)
            .Where(h => h.PacienteId == pacienteId);
        if (tipo is not null) query = query.Where(h => h.TipoRegistro == tipo);

        var registros = await query.OrderByDescending(h => h.Fecha).AsSplitQuery().ToListAsync();
        return registros.Select(h => Mapeos.ADto(h)).ToList();
    }

    /// <summary>
    /// Registra una atención. Si se indica el turno, debe pertenecer al paciente y queda marcado como Atendido.
    /// Los registros no se editan ni borran: la historia clínica es un documento legal; las correcciones se agregan como registro nuevo.
    /// </summary>
    [HttpPost]
    [Permiso(ModuloPermiso.HistoriaClinicaOdontograma, AccionPermiso.Registrar)]
    public async Task<ActionResult<HistorialDto>> Crear(int pacienteId, HistorialCrearDto dto)
    {
        if (!await db.Pacientes.AnyAsync(p => p.Id == pacienteId)) return NotFound();

        static string? Limpio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        var diagnosticos = (dto.Diagnosticos ?? [])
            .Where(d => !string.IsNullOrWhiteSpace(d.Descripcion))
            .Take(3)
            .Select((d, i) => new DiagnosticoAtencion
            {
                Orden = (byte)(i + 1),
                Codigo = Limpio(d.Codigo)?.ToUpperInvariant(),
                Descripcion = d.Descripcion.Trim(),
                Caracter = d.Caracter,
            })
            .ToList();
        var practicas = (dto.Practicas ?? [])
            .Where(p => !string.IsNullOrWhiteSpace(p.Descripcion))
            .Select(p => new PracticaAtencion
            {
                Codigo = Limpio(p.Codigo),
                Descripcion = p.Descripcion.Trim(),
                Piezas = Limpio(p.Piezas),
                Observaciones = Limpio(p.Observaciones),
            })
            .ToList();

        var motivo = Limpio(dto.MotivoConsulta);
        var textos = new[]
        {
            motivo, Limpio(dto.AntecedentesExamen), Limpio(dto.Diagnostico), Limpio(dto.Tratamiento),
            Limpio(dto.Prescripcion), Limpio(dto.Informe), Limpio(dto.Descripcion),
        };
        if (textos.All(t => t is null) && diagnosticos.Count == 0 && practicas.Count == 0)
            return BadRequest(new { mensaje = "Completá al menos el motivo, un diagnóstico, una práctica o la evolución." });

        Turno? turno = null;
        if (dto.TurnoId is int turnoId)
        {
            turno = await db.Turnos.FirstOrDefaultAsync(t => t.Id == turnoId && t.PacienteId == pacienteId);
            if (turno is null) return BadRequest(new { mensaje = "El turno no corresponde a este paciente." });
            if (turno.Estado == EstadoTurno.Cancelado)
                return Conflict(new { mensaje = "No se puede registrar una atención en un turno cancelado." });
            turno.Estado = EstadoTurno.Atendido;
        }

        var registro = new HistorialClinico
        {
            PacienteId = pacienteId,
            UsuarioId = User.GetUsuarioId(),
            TurnoId = turno?.Id,
            Fecha = DateTime.UtcNow,
            TipoRegistro = dto.TipoRegistro,
            TipoConsulta = dto.TipoConsulta,
            TaMax = dto.TaMax,
            TaMin = dto.TaMin,
            Peso = dto.Peso,
            Talla = dto.Talla,
            Temperatura = dto.Temperatura,
            FrecuenciaCardiaca = dto.FrecuenciaCardiaca,
            FrecuenciaRespiratoria = dto.FrecuenciaRespiratoria,
            SaturacionO2 = dto.SaturacionO2,
            // Si no se escribió motivo, se toma el que cargó la secretaria en el turno.
            MotivoConsulta = motivo ?? turno?.Motivo,
            AntecedentesExamen = Limpio(dto.AntecedentesExamen),
            Diagnostico = Limpio(dto.Diagnostico),
            Tratamiento = Limpio(dto.Tratamiento),
            Prescripcion = Limpio(dto.Prescripcion),
            Informe = Limpio(dto.Informe),
            Piezas = Limpio(dto.Piezas),
            Descripcion = Limpio(dto.Descripcion) ?? string.Empty,
            Importe = dto.Importe is > 0 ? dto.Importe : null,
            Diagnosticos = diagnosticos,
            Practicas = practicas,
        };
        db.HistorialesClinicos.Add(registro);
        await db.SaveChangesAsync();

        var creado = await db.HistorialesClinicos.AsNoTracking()
            .Include(h => h.Usuario).Include(h => h.Turno).Include(h => h.Diagnosticos).Include(h => h.Practicas)
            .AsSplitQuery()
            .FirstAsync(h => h.Id == registro.Id);
        return Created($"api/pacientes/{pacienteId}/historial/{registro.Id}", Mapeos.ADto(creado));
    }
}
