using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OdontoApp.Api.Auth;
using OdontoApp.Api.Data;
using OdontoApp.Api.Dtos;
using OdontoApp.Api.Models;

namespace OdontoApp.Api.Controllers;

/// <summary>Tareas del consultorio, compartidas entre la secretaria y el odontólogo.</summary>
[ApiController]
[Route("api/tareas")]
[Authorize(Roles = Roles.Staff)]
public class TareasController(OdontoDbContext db) : ControllerBase
{
    /// <summary>Abiertas (ordenadas por vencimiento y prioridad) y, opcionalmente, las completadas de los últimos 7 días.</summary>
    [HttpGet]
    public async Task<ActionResult<List<TareaDto>>> Listar([FromQuery] bool completadas = false)
    {
        var desde = DateTime.UtcNow.AddDays(-7);
        var tareas = await db.Tareas.AsNoTracking().Include(t => t.Usuario)
            .Where(t => !t.Completada || (completadas && t.CompletadaEn >= desde))
            .ToListAsync();

        return tareas
            .OrderBy(t => t.Completada)
            .ThenBy(t => t.Vence ?? DateOnly.MaxValue)
            .ThenBy(t => t.Prioridad)
            .ThenBy(t => t.CreadoEn)
            .Select(ADto)
            .ToList();
    }

    [HttpPost]
    public async Task<ActionResult<TareaDto>> Crear(TareaGuardarDto dto)
    {
        var tarea = new Tarea
        {
            Texto = dto.Texto.Trim(),
            Prioridad = dto.Prioridad,
            Categoria = string.IsNullOrWhiteSpace(dto.Categoria) ? null : dto.Categoria.Trim(),
            Vence = dto.Vence,
            UsuarioId = User.GetUsuarioId(),
            CreadoEn = DateTime.UtcNow,
        };
        db.Tareas.Add(tarea);
        await db.SaveChangesAsync();
        await db.Entry(tarea).Reference(t => t.Usuario).LoadAsync();
        return Created($"api/tareas/{tarea.Id}", ADto(tarea));
    }

    /// <summary>Marca o desmarca una tarea como completada.</summary>
    [HttpPatch("{id:int}/completar")]
    public async Task<IActionResult> Completar(int id, [FromQuery] bool valor = true)
    {
        var tarea = await db.Tareas.FindAsync(id);
        if (tarea is null) return NotFound();
        tarea.Completada = valor;
        tarea.CompletadaEn = valor ? DateTime.UtcNow : null;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Eliminar(int id)
    {
        var tarea = await db.Tareas.FindAsync(id);
        if (tarea is null) return NotFound();
        db.Tareas.Remove(tarea);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static TareaDto ADto(Tarea t) =>
        new(t.Id, t.Texto, t.Prioridad, t.Categoria, t.Vence, t.Completada, t.CompletadaEn, t.Usuario.Nombre, t.CreadoEn);
}
