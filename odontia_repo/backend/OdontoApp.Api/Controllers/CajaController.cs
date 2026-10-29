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
/// Ingresos y egresos del consultorio. Información financiera: solo el odontólogo.
/// Los cobros a pacientes y los pagos a laboratorios aparecen solos; acá se cargan los demás (alquiler, insumos…).
/// </summary>
[ApiController]
[Route("api/caja")]
[Authorize(Roles = Roles.Staff)]
public class CajaController(OdontoDbContext db, IOptions<AgendaOptions> agendaOptions) : ControllerBase
{
    private readonly AgendaOptions _agenda = agendaOptions.Value;

    /// <summary>Movimientos y totales del período (por defecto, el mes en curso).</summary>
    [HttpGet]
    [Permiso(ModuloPermiso.FinanzasGenerales, AccionPermiso.VerResumenes)]
    public async Task<ActionResult<ResumenCajaDto>> Resumen(
        [FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta, [FromQuery] TipoMovimiento? tipo)
    {
        var hoy = DateOnly.FromDateTime(_agenda.AhoraLocal());
        var d = desde ?? new DateOnly(hoy.Year, hoy.Month, 1);
        var h = hasta ?? hoy;
        if (h < d) return BadRequest(new { mensaje = "'hasta' no puede ser anterior a 'desde'." });

        var query = MovimientosVigentes().Where(m => m.Fecha >= d && m.Fecha <= h);
        if (tipo is not null) query = query.Where(m => m.Tipo == tipo);

        var movimientos = await query
            .OrderByDescending(m => m.Fecha).ThenByDescending(m => m.Id)
            .Select(m => new MovimientoDto(m.Id, m.Tipo, m.Categoria, m.Fecha, m.Descripcion, m.Monto,
                m.MedioPago, m.Usuario.Nombre, m.PagoId != null || m.TrabajoLaboratorioId != null))
            .ToListAsync();

        // Totales en memoria: el volumen de un consultorio es chico y evita diferencias entre proveedores de BD con decimal.
        var ingresos = movimientos.Where(m => m.Tipo == TipoMovimiento.Ingreso).Sum(m => m.Monto);
        var egresos = movimientos.Where(m => m.Tipo == TipoMovimiento.Egreso).Sum(m => m.Monto);
        var porCategoria = movimientos
            .GroupBy(m => (m.Tipo, m.Categoria))
            .Select(g => new TotalCategoriaDto(g.Key.Tipo, g.Key.Categoria, g.Sum(m => m.Monto)))
            .OrderBy(t => t.Tipo).ThenByDescending(t => t.Total)
            .ToList();

        return new ResumenCajaDto(d, h, ingresos, egresos, ingresos - egresos, porCategoria, movimientos);
    }

    [HttpPost]
    [Permiso(ModuloPermiso.FinanzasGenerales, AccionPermiso.Registrar)]
    public async Task<ActionResult<MovimientoDto>> Registrar(MovimientoCrearDto dto)
    {
        if (!CategoriasMovimiento.Corresponde(dto.Tipo, dto.Categoria))
            return BadRequest(new { mensaje = $"La categoría {dto.Categoria} no corresponde a un {dto.Tipo.ToString().ToLower()}." });
        if (dto.Categoria == CategoriaMovimiento.CobroPaciente)
            return BadRequest(new { mensaje = "Los cobros a pacientes se registran desde la ficha del paciente (Pagos)." });

        var mov = new MovimientoCaja
        {
            Tipo = dto.Tipo,
            Categoria = dto.Categoria,
            Fecha = dto.Fecha,
            Descripcion = dto.Descripcion.Trim(),
            Monto = decimal.Round(dto.Monto, 2),
            MedioPago = dto.MedioPago,
            UsuarioId = User.GetUsuarioId(),
        };
        db.MovimientosCaja.Add(mov);
        await db.SaveChangesAsync();

        return Created($"api/caja/{mov.Id}", new MovimientoDto(mov.Id, mov.Tipo, mov.Categoria, mov.Fecha,
            mov.Descripcion, mov.Monto, mov.MedioPago, User.Identity?.Name ?? "", false));
    }

    /// <summary>Solo movimientos manuales. Los automáticos se corrigen desde su origen (anular pago, etc.).</summary>
    [HttpDelete("{id:int}")]
    [Permiso(ModuloPermiso.FinanzasGenerales, AccionPermiso.Anular)]
    public async Task<IActionResult> Eliminar(int id)
    {
        var mov = await db.MovimientosCaja.FindAsync(id);
        if (mov is null) return NotFound();
        if (mov.EsAutomatico)
            return Conflict(new { mensaje = "Este movimiento se generó automáticamente; corregilo desde el pago o el trabajo de laboratorio." });

        db.MovimientosCaja.Remove(mov);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Movimientos que cuentan para la caja (excluye cobros anulados).</summary>
    private IQueryable<MovimientoCaja> MovimientosVigentes() =>
        db.MovimientosCaja.AsNoTracking().Where(m => m.Pago == null || !m.Pago.Anulado);
}
