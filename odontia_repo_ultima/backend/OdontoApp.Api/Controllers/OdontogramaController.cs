using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OdontoApp.Api.Auth;
using OdontoApp.Api.Data;
using OdontoApp.Api.Dtos;
using OdontoApp.Api.Models;
using OdontoApp.Api.Odontologia;

namespace OdontoApp.Api.Controllers;

/// <summary>
/// Odontograma versionado: cada POST crea un snapshot nuevo (odontograma + sus dientes).
/// El "actual" es el de fecha_registro más reciente. Acceso exclusivo del odontólogo.
/// </summary>
[ApiController]
[Route("api/pacientes/{pacienteId:int}/odontograma")]
[Authorize(Roles = Roles.Staff)]
public class OdontogramaController(OdontoDbContext db) : ControllerBase
{
    /// <summary>Estado actual. Si el paciente nunca tuvo odontograma devuelve uno vacío (todas las piezas sanas).</summary>
    [HttpGet]
    [Permiso(ModuloPermiso.HistoriaClinicaOdontograma, AccionPermiso.VerResumenes)]
    public async Task<ActionResult<OdontogramaDto>> Actual(int pacienteId)
    {
        if (!await db.Pacientes.AnyAsync(p => p.Id == pacienteId)) return NotFound();

        var ultimo = await Consulta()
            .Where(o => o.PacienteId == pacienteId)
            .OrderByDescending(o => o.FechaRegistro).ThenByDescending(o => o.Id)
            .FirstOrDefaultAsync();

        return ultimo is null ? new OdontogramaDto(null, null, null, null, []) : ADto(ultimo);
    }

    [HttpGet("versiones")]
    [Permiso(ModuloPermiso.HistoriaClinicaOdontograma, AccionPermiso.VerResumenes)]
    public async Task<ActionResult<List<OdontogramaResumenDto>>> Versiones(int pacienteId) =>
        await db.Odontogramas.AsNoTracking()
            .Where(o => o.PacienteId == pacienteId)
            .OrderByDescending(o => o.FechaRegistro)
            .Select(o => new OdontogramaResumenDto(o.Id, o.FechaRegistro, o.Usuario.Nombre, o.Dientes.Count))
            .ToListAsync();

    [HttpGet("{id:int}")]
    [Permiso(ModuloPermiso.HistoriaClinicaOdontograma, AccionPermiso.VerResumenes)]
    public async Task<ActionResult<OdontogramaDto>> Version(int pacienteId, int id)
    {
        var o = await Consulta().FirstOrDefaultAsync(x => x.Id == id && x.PacienteId == pacienteId);
        return o is null ? NotFound() : ADto(o);
    }

    [HttpPost]
    [Permiso(ModuloPermiso.HistoriaClinicaOdontograma, AccionPermiso.EditarCorregir)]
    public async Task<ActionResult<OdontogramaDto>> Guardar(int pacienteId, OdontogramaGuardarDto dto)
    {
        if (!await db.Pacientes.AnyAsync(p => p.Id == pacienteId)) return NotFound();

        var invalidas = dto.Dientes.Where(d => !Fdi.EsValida(d.NumeroDiente)).Select(d => d.NumeroDiente).ToList();
        if (invalidas.Count > 0)
            return BadRequest(new { mensaje = $"Piezas inexistentes en notación FDI: {string.Join(", ", invalidas)}." });
        if (dto.Dientes.GroupBy(d => d.NumeroDiente).Any(g => g.Count() > 1))
            return BadRequest(new { mensaje = "Hay piezas repetidas." });

        var odontograma = new Odontograma
        {
            PacienteId = pacienteId,
            UsuarioId = User.GetUsuarioId(),
            FechaRegistro = DateTime.UtcNow,
            Observaciones = string.IsNullOrWhiteSpace(dto.Observaciones) ? null : dto.Observaciones.Trim(),
            // Solo se persisten piezas con algún dato: una pieza ausente de la lista = sana.
            Dientes = dto.Dientes.Where(d => !EsSana(d)).Select(d => new OdontogramaDiente
            {
                NumeroDiente = d.NumeroDiente,
                EstadoGeneral = d.EstadoGeneral,
                Oclusal = d.Oclusal,
                Mesial = d.Mesial,
                Distal = d.Distal,
                Vestibular = d.Vestibular,
                Lingual = d.Lingual,
                Nota = string.IsNullOrWhiteSpace(d.Nota) ? null : d.Nota.Trim(),
            }).ToList(),
        };
        db.Odontogramas.Add(odontograma);
        await db.SaveChangesAsync();

        var guardado = await Consulta().FirstAsync(o => o.Id == odontograma.Id);
        return CreatedAtAction(nameof(Version), new { pacienteId, id = odontograma.Id }, ADto(guardado));
    }

    /// <summary>Atenciones de la historia clínica que mencionan la pieza (campo "piezas", por ejemplo "36" o "14,15").</summary>
    [HttpGet("piezas/{numero:int}/historial")]
    [Permiso(ModuloPermiso.HistoriaClinicaOdontograma, AccionPermiso.VerResumenes)]
    public async Task<ActionResult<List<HistorialPiezaDto>>> HistorialPieza(int pacienteId, int numero)
    {
        if (numero is < 0 or > 255 || !Fdi.EsValida((byte)numero)) return BadRequest(new { mensaje = $"La pieza {numero} no existe en notación FDI." });

        var registros = await db.HistorialesClinicos.AsNoTracking()
            .Where(h => h.PacienteId == pacienteId && h.Piezas != null)
            .Select(h => new { h.Id, h.Fecha, h.Piezas, h.Tratamiento, h.Diagnostico, h.Descripcion, Profesional = h.Usuario.Nombre })
            .ToListAsync();

        var numeroTexto = numero.ToString();
        return registros
            .Where(r => r.Piezas!.Split(',', StringSplitOptions.TrimEntries).Contains(numeroTexto))
            .OrderByDescending(r => r.Fecha)
            .Select(r => new HistorialPiezaDto(
                r.Id,
                r.Fecha,
                r.Profesional,
                r.Tratamiento ?? r.Diagnostico ?? r.Descripcion,
                string.IsNullOrWhiteSpace(r.Descripcion) ? null : r.Descripcion))
            .ToList();
    }

    private IQueryable<Odontograma> Consulta() =>
        db.Odontogramas.AsNoTracking().Include(o => o.Dientes).Include(o => o.Usuario);

    private static bool EsSana(DienteDto d) =>
        d.EstadoGeneral == EstadoGeneralDiente.Presente &&
        d is { Oclusal: EstadoCara.Sano, Mesial: EstadoCara.Sano, Distal: EstadoCara.Sano,
               Vestibular: EstadoCara.Sano, Lingual: EstadoCara.Sano }
               && string.IsNullOrWhiteSpace(d.Nota);

    private static OdontogramaDto ADto(Odontograma o) => new(
        o.Id, o.FechaRegistro, o.Usuario.Nombre, o.Observaciones,
        o.Dientes.OrderBy(d => d.NumeroDiente)
            .Select(d => new DienteDto(d.NumeroDiente, d.EstadoGeneral, d.Oclusal, d.Mesial, d.Distal, d.Vestibular, d.Lingual, d.Nota))
            .ToList());
}
