using System.Net.Mail;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OdontoApp.Api.Auth;
using OdontoApp.Api.Data;
using OdontoApp.Api.Dtos;
using OdontoApp.Api.Models;

namespace OdontoApp.Api.Controllers;

/// <summary>
/// Invitaciones para la secretaria. Solo el odontólogo las administra.
/// El token se devuelve una única vez, al crear la invitación; después solo queda su hash.
/// </summary>
[ApiController]
[Route("api/invitaciones")]
[Authorize(Roles = Roles.Odontologo)]
public class InvitacionesController(OdontoDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<InvitacionDto>>> Listar()
    {
        var consultorioId = User.GetConsultorioId();
        var invitaciones = await db.Invitaciones.AsNoTracking()
            .Include(i => i.CreadaPor)
            .Where(i => i.ConsultorioId == consultorioId)
            .OrderByDescending(i => i.CreadaEn)
            .ToListAsync();

        var ahora = DateTime.UtcNow;
        return invitaciones.Select(i => Mapear(i, ahora)).ToList();
    }

    [HttpPost]
    public async Task<ActionResult<InvitacionCreadaDto>> Crear(CrearInvitacionDto dto)
    {
        var consultorioId = User.GetConsultorioId();
        var email = dto.Email.Trim().ToLowerInvariant();
        if (!EsEmailValido(email))
            return BadRequest(new { mensaje = "El correo no es válido." });

        if (await db.Usuarios.AnyAsync(u => u.ConsultorioId == consultorioId && u.Email.ToLower() == email))
            return Conflict(new { mensaje = "Ya existe un usuario del consultorio con ese correo." });

        var ahora = DateTime.UtcNow;
        var pendiente = await db.Invitaciones.AnyAsync(i =>
            i.ConsultorioId == consultorioId && i.Email == email &&
            i.UsadaEn == null && i.RevocadaEn == null && i.ExpiraEn > ahora);
        if (pendiente)
            return Conflict(new { mensaje = "Ya hay una invitación pendiente para ese correo. Podés revocarla y crear otra." });

        var token = InvitacionToken.Generar();
        var invitacion = new Invitacion
        {
            ConsultorioId = consultorioId,
            Email = email,
            Rol = RolUsuario.Secretaria, // el único rol que se invita; el odontólogo es el dueño
            TokenHash = InvitacionToken.Hash(token),
            CreadaPorId = User.GetUsuarioId(),
            CreadaEn = ahora,
            ExpiraEn = ahora.Add(InvitacionToken.Vigencia),
        };
        db.Invitaciones.Add(invitacion);
        await db.SaveChangesAsync();

        var creadora = await db.Usuarios.AsNoTracking().FirstAsync(u => u.Id == invitacion.CreadaPorId);
        return CreatedAtAction(nameof(Listar), new InvitacionCreadaDto(Mapear(invitacion, ahora, creadora.Nombre), token));
    }

    /// <summary>Revoca una invitación pendiente. El enlace deja de funcionar al instante.</summary>
    [HttpPost("{id:int}/revocar")]
    public async Task<IActionResult> Revocar(int id)
    {
        var consultorioId = User.GetConsultorioId();
        var invitacion = await db.Invitaciones.FirstOrDefaultAsync(i => i.Id == id && i.ConsultorioId == consultorioId);
        if (invitacion is null) return NotFound();
        if (invitacion.UsadaEn is not null)
            return BadRequest(new { mensaje = "La invitación ya fue aceptada." });

        invitacion.RevocadaEn ??= DateTime.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ---------- helpers ----------

    private static InvitacionDto Mapear(Invitacion i, DateTime ahora, string? creadoraNombre = null) => new(
        i.Id, i.Email, i.Rol, Estado(i, ahora), i.CreadaEn, i.ExpiraEn, i.UsadaEn,
        creadoraNombre ?? i.CreadaPor?.Nombre ?? string.Empty);

    private static string Estado(Invitacion i, DateTime ahora)
    {
        if (i.UsadaEn is not null) return "Aceptada";
        if (i.RevocadaEn is not null) return "Revocada";
        if (i.ExpiraEn <= ahora) return "Vencida";
        return "Pendiente";
    }

    private static bool EsEmailValido(string email)
    {
        try { return new MailAddress(email).Address == email; }
        catch { return false; }
    }
}
