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
/// Trabajos enviados a laboratorio: salida (envío) y entrada (recepción).
/// Ambos roles registran y actualizan; pagar al laboratorio (egreso de caja) es solo del odontólogo.
/// </summary>
[ApiController]
[Route("api/laboratorio")]
[Authorize(Roles = Roles.Staff)]
public class LaboratorioController(OdontoDbContext db, IOptions<AgendaOptions> agendaOptions) : ControllerBase
{
    private readonly AgendaOptions _agenda = agendaOptions.Value;
    private DateOnly Hoy => DateOnly.FromDateTime(_agenda.AhoraLocal());

    [HttpGet]
    public async Task<ActionResult<List<TrabajoLabDto>>> Listar(
        [FromQuery] EstadoTrabajoLaboratorio? estado, [FromQuery] int? pacienteId, [FromQuery] bool? pendientesDePago)
    {
        var query = db.TrabajosLaboratorio.AsNoTracking();
        if (estado is not null) query = query.Where(t => t.Estado == estado);
        if (pacienteId is not null) query = query.Where(t => t.PacienteId == pacienteId);
        if (pendientesDePago == true)
            query = query.Where(t => !t.Pagado && t.Costo != null && t.Estado != EstadoTrabajoLaboratorio.Cancelado);

        return await query.OrderByDescending(t => t.FechaEnvio).ThenByDescending(t => t.Id)
            .Select(Proyectar).ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TrabajoLabDto>> Obtener(int id)
    {
        var t = await db.TrabajosLaboratorio.AsNoTracking().Where(x => x.Id == id).Select(Proyectar).FirstOrDefaultAsync();
        return t is null ? NotFound() : t;
    }

    /// <summary>Registra la salida de un trabajo hacia el laboratorio.</summary>
    [HttpPost]
    [Permiso(ModuloPermiso.LaboratorioTrabajos, AccionPermiso.Registrar)]
    public async Task<ActionResult<TrabajoLabDto>> Enviar(TrabajoLabGuardarDto dto)
    {
        if (!await db.Pacientes.AnyAsync(p => p.Id == dto.PacienteId))
            return NotFound(new { mensaje = "El paciente no existe." });
        var error = ValidarFechas(dto);
        if (error is not null) return BadRequest(new { mensaje = error });

        var trabajo = new TrabajoLaboratorio { UsuarioId = User.GetUsuarioId(), CreadoEn = DateTime.UtcNow };
        Aplicar(trabajo, dto);
        db.TrabajosLaboratorio.Add(trabajo);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Obtener), new { id = trabajo.Id },
            await db.TrabajosLaboratorio.AsNoTracking().Where(t => t.Id == trabajo.Id).Select(Proyectar).FirstAsync());
    }

    [HttpPut("{id:int}")]
    [Permiso(ModuloPermiso.LaboratorioTrabajos, AccionPermiso.EditarCorregir)]
    public async Task<ActionResult<TrabajoLabDto>> Modificar(int id, TrabajoLabGuardarDto dto)
    {
        var trabajo = await db.TrabajosLaboratorio.FindAsync(id);
        if (trabajo is null) return NotFound();
        if (trabajo.Pagado && dto.Costo != trabajo.Costo)
            return Conflict(new { mensaje = "El trabajo ya fue pagado: no se puede cambiar el costo." });
        var error = ValidarFechas(dto);
        if (error is not null) return BadRequest(new { mensaje = error });

        Aplicar(trabajo, dto);
        await db.SaveChangesAsync();
        return await db.TrabajosLaboratorio.AsNoTracking().Where(t => t.Id == id).Select(Proyectar).FirstAsync();
    }

    /// <summary>
    /// Recibido = vuelve del laboratorio (registra fecha_recepcion). Colocado = entregado al paciente.
    /// Enviado de nuevo = se devolvió al laboratorio para corregir (limpia la recepción).
    /// </summary>
    [HttpPatch("{id:int}/estado")]
    [Permiso(ModuloPermiso.LaboratorioTrabajos, AccionPermiso.EditarCorregir)]
    public async Task<IActionResult> CambiarEstado(int id, TrabajoLabEstadoDto dto)
    {
        var trabajo = await db.TrabajosLaboratorio.FindAsync(id);
        if (trabajo is null) return NotFound();
        if (trabajo.Estado == EstadoTrabajoLaboratorio.Cancelado)
            return Conflict(new { mensaje = "El trabajo está cancelado." });

        var fecha = dto.Fecha ?? Hoy;
        switch (dto.Estado)
        {
            case EstadoTrabajoLaboratorio.Recibido:
                if (fecha < trabajo.FechaEnvio)
                    return BadRequest(new { mensaje = "La recepción no puede ser anterior al envío." });
                trabajo.FechaRecepcion = fecha;
                break;
            case EstadoTrabajoLaboratorio.Colocado:
                if (trabajo.FechaRecepcion is null)
                    return Conflict(new { mensaje = "Primero registrá la recepción del trabajo." });
                break;
            case EstadoTrabajoLaboratorio.Enviado:
                trabajo.FechaEnvio = fecha;
                trabajo.FechaRecepcion = null;
                break;
            case EstadoTrabajoLaboratorio.Cancelado:
                if (trabajo.Pagado)
                    return Conflict(new { mensaje = "No se puede cancelar un trabajo ya pagado." });
                break;
        }
        trabajo.Estado = dto.Estado;
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Paga el trabajo al laboratorio y registra el egreso en la caja.</summary>
    [HttpPatch("{id:int}/pagar")]
    [Permiso(ModuloPermiso.LaboratorioPagos, AccionPermiso.Registrar)]
    public async Task<IActionResult> Pagar(int id, TrabajoLabPagarDto dto)
    {
        var trabajo = await db.TrabajosLaboratorio.Include(t => t.Paciente).FirstOrDefaultAsync(t => t.Id == id);
        if (trabajo is null) return NotFound();
        if (trabajo.Pagado) return Conflict(new { mensaje = "El trabajo ya está pagado." });
        if (trabajo.Costo is not > 0) return BadRequest(new { mensaje = "Cargá el costo del trabajo antes de pagarlo." });
        if (trabajo.Estado == EstadoTrabajoLaboratorio.Cancelado)
            return Conflict(new { mensaje = "El trabajo está cancelado." });

        trabajo.Pagado = true;
        trabajo.Movimiento = new MovimientoCaja
        {
            Tipo = TipoMovimiento.Egreso,
            Categoria = CategoriaMovimiento.Laboratorio,
            Fecha = dto.Fecha ?? Hoy,
            Descripcion = $"{trabajo.Laboratorio}: {trabajo.TipoTrabajo} — {trabajo.Paciente.Apellido}, {trabajo.Paciente.Nombre}",
            Monto = trabajo.Costo.Value,
            MedioPago = dto.MedioPago,
            UsuarioId = User.GetUsuarioId(),
        };
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static string? ValidarFechas(TrabajoLabGuardarDto dto) =>
        dto.FechaEntregaEstimada < dto.FechaEnvio ? "La entrega estimada no puede ser anterior al envío." : null;

    private static void Aplicar(TrabajoLaboratorio t, TrabajoLabGuardarDto dto)
    {
        t.PacienteId = dto.PacienteId;
        t.Laboratorio = dto.Laboratorio.Trim();
        t.TipoTrabajo = dto.TipoTrabajo.Trim();
        t.Piezas = string.IsNullOrWhiteSpace(dto.Piezas) ? null : dto.Piezas.Trim();
        t.Descripcion = string.IsNullOrWhiteSpace(dto.Descripcion) ? null : dto.Descripcion.Trim();
        t.FechaEnvio = dto.FechaEnvio;
        t.FechaEntregaEstimada = dto.FechaEntregaEstimada;
        t.Costo = dto.Costo;
    }

    private static readonly System.Linq.Expressions.Expression<Func<TrabajoLaboratorio, TrabajoLabDto>> Proyectar = t =>
        new TrabajoLabDto(t.Id, t.PacienteId, t.Paciente.Apellido + ", " + t.Paciente.Nombre, t.Laboratorio,
            t.TipoTrabajo, t.Piezas, t.Descripcion, t.FechaEnvio, t.FechaEntregaEstimada, t.FechaRecepcion,
            t.Estado, t.Costo, t.Pagado);
}
