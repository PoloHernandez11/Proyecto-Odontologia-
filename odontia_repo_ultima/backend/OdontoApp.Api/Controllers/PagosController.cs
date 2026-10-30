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
/// Pagos de pacientes y cuenta corriente. La secretaria cobra; solo el odontólogo puede anular.
/// Cada pago genera su ingreso en la caja en la misma transacción.
/// </summary>
[ApiController]
[Authorize(Roles = Roles.Staff)]
public class PagosController(OdontoDbContext db, IOptions<AgendaOptions> agendaOptions) : ControllerBase
{
    private readonly AgendaOptions _agenda = agendaOptions.Value;

    /// <summary>Cargos (atenciones con importe), pagos y saldo del paciente.</summary>
    [HttpGet("api/pacientes/{pacienteId:int}/cuenta")]
    [Permiso(ModuloPermiso.PagosPacientes, AccionPermiso.VerResumenes)]
    public async Task<ActionResult<CuentaCorrienteDto>> CuentaCorriente(int pacienteId)
    {
        if (!await db.Pacientes.AnyAsync(p => p.Id == pacienteId)) return NotFound();

        var cargos = await db.HistorialesClinicos.AsNoTracking()
            .Where(h => h.PacienteId == pacienteId && h.Importe != null)
            .OrderByDescending(h => h.Fecha)
            .Select(h => new CargoDto(h.Id, h.Fecha, h.Importe!.Value))
            .ToListAsync();

        var pagos = await ConsultaPagos().Where(p => p.PacienteId == pacienteId)
            .OrderByDescending(p => p.Fecha).Select(ProyectarPago).ToListAsync();

        var totalCargos = cargos.Sum(c => c.Importe);
        var totalPagos = pagos.Where(p => !p.Anulado).Sum(p => p.Monto);
        return new CuentaCorrienteDto(cargos, pagos, totalCargos, totalPagos, totalCargos - totalPagos);
    }

    [HttpPost("api/pacientes/{pacienteId:int}/pagos")]
    [Permiso(ModuloPermiso.PagosPacientes, AccionPermiso.Registrar)]
    public async Task<ActionResult<PagoDto>> Registrar(int pacienteId, PagoCrearDto dto)
    {
        var paciente = await db.Pacientes.FindAsync(pacienteId);
        if (paciente is null) return NotFound();

        var usuarioId = User.GetUsuarioId();
        var pago = new Pago
        {
            PacienteId = pacienteId,
            UsuarioId = usuarioId,
            Fecha = DateTime.UtcNow,
            Monto = decimal.Round(dto.Monto, 2),
            MedioPago = dto.MedioPago,
            Concepto = string.IsNullOrWhiteSpace(dto.Concepto) ? null : dto.Concepto.Trim(),
        };
        pago.Movimiento = new MovimientoCaja
        {
            Tipo = TipoMovimiento.Ingreso,
            Categoria = dto.MedioPago == MedioPago.ObraSocial ? CategoriaMovimiento.ObraSocial : CategoriaMovimiento.CobroPaciente,
            Fecha = DateOnly.FromDateTime(_agenda.AhoraLocal()),
            Descripcion = $"Pago de {paciente.Apellido}, {paciente.Nombre}" + (pago.Concepto is null ? "" : $" — {pago.Concepto}"),
            Monto = pago.Monto,
            MedioPago = pago.MedioPago,
            UsuarioId = usuarioId,
        };
        db.Pagos.Add(pago);
        await db.SaveChangesAsync(); // pago + movimiento en una sola transacción

        return Created($"api/pagos/{pago.Id}",
            await ConsultaPagos().Where(p => p.Id == pago.Id).Select(ProyectarPago).FirstAsync());
    }

    /// <summary>Cobros de un rango de fechas (por defecto hoy). Útil para el cierre del día de la secretaria.</summary>
    [HttpGet("api/pagos")]
    [Permiso(ModuloPermiso.PagosPacientes, AccionPermiso.VerResumenes)]
    public async Task<ActionResult<List<PagoDto>>> Listar([FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta)
    {
        var hoy = DateOnly.FromDateTime(_agenda.AhoraLocal());
        var (d, h) = (desde ?? hoy, hasta ?? desde ?? hoy);
        // Los pagos se guardan en UTC: se filtra por la fecha de su movimiento de caja, que es la fecha local.
        return await ConsultaPagos()
            .Where(p => p.Movimiento != null && p.Movimiento.Fecha >= d && p.Movimiento.Fecha <= h)
            .OrderByDescending(p => p.Fecha)
            .Select(ProyectarPago)
            .ToListAsync();
    }

    [HttpPatch("api/pagos/{id:int}/anular")]
    [Permiso(ModuloPermiso.PagosPacientes, AccionPermiso.Anular)]
    public async Task<IActionResult> Anular(int id)
    {
        var pago = await db.Pagos.FindAsync(id);
        if (pago is null) return NotFound();
        if (pago.Anulado) return Conflict(new { mensaje = "El pago ya estaba anulado." });

        // El movimiento de caja asociado deja de contar (CajaController filtra los pagos anulados).
        pago.Anulado = true;
        await db.SaveChangesAsync();
        return NoContent();
    }

    private IQueryable<Pago> ConsultaPagos() => db.Pagos.AsNoTracking();

    private static readonly System.Linq.Expressions.Expression<Func<Pago, PagoDto>> ProyectarPago = p =>
        new PagoDto(p.Id, p.PacienteId, p.Paciente.Apellido + ", " + p.Paciente.Nombre, p.Fecha, p.Monto,
            p.MedioPago, p.Concepto, p.Anulado, p.Usuario.Nombre);
}
