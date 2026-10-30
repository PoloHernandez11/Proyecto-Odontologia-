using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OdontoApp.Api.Auth;
using OdontoApp.Api.Data;
using OdontoApp.Api.Dtos;

namespace OdontoApp.Api.Controllers;

/// <summary>Gestión de miembros del consultorio. Solo el odontólogo.</summary>
[ApiController]
[Route("api/usuarios")]
[Authorize(Roles = Roles.Odontologo)]
public class UsuariosController(OdontoDbContext db) : ControllerBase
{
    /// <summary>Activa o desactiva a un miembro. Nadie puede desactivarse a sí mismo, así que el dueño siempre puede entrar.</summary>
    [HttpPut("{id:int}/activo")]
    public async Task<IActionResult> Activar(int id, ActivarUsuarioDto dto)
    {
        var consultorioId = User.GetConsultorioId();
        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == id && u.ConsultorioId == consultorioId);
        if (usuario is null) return NotFound();

        if (!dto.Activo)
        {
            if (usuario.Id == User.GetUsuarioId())
                return BadRequest(new { mensaje = "No podés desactivar tu propio usuario." });
        }

        usuario.Activo = dto.Activo;
        await db.SaveChangesAsync();
        return NoContent();
    }
}
