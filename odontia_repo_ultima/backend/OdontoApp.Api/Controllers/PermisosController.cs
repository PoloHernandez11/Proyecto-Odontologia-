using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OdontoApp.Api.Auth;
using OdontoApp.Api.Dtos;
using OdontoApp.Api.Models;

namespace OdontoApp.Api.Controllers;

/// <summary>Matriz de permisos de la secretaria. Solo el odontólogo la configura.</summary>
[ApiController]
[Route("api/permisos")]
[Authorize(Roles = Roles.Staff)]
public class PermisosController(PermisosService permisos) : ControllerBase
{
    [HttpGet("secretaria")]
    [Authorize(Roles = Roles.Odontologo)]
    public async Task<ActionResult<List<PermisoItemDto>>> Secretaria() =>
        await permisos.MatrizAsync(User.GetConsultorioId(), RolUsuario.Secretaria);

    [HttpPut("secretaria")]
    [Authorize(Roles = Roles.Odontologo)]
    public async Task<IActionResult> GuardarSecretaria(MatrizPermisosDto dto)
    {
        await permisos.GuardarAsync(User.GetConsultorioId(), RolUsuario.Secretaria, dto.Items);
        return NoContent();
    }

    /// <summary>Historial de acciones de la secretaria. Solo el odontólogo lo ve.</summary>
    [HttpGet("actividad")]
    [Authorize(Roles = Roles.Odontologo)]
    public async Task<ActionResult<List<ActividadPermisoDto>>> Actividad([FromQuery] int limite = 200) =>
        await permisos.ActividadAsync(User.GetConsultorioId(), Math.Clamp(limite, 1, 500));

    /// <summary>Permisos del usuario que hace la consulta (para mostrar u ocultar secciones).</summary>
    [HttpGet("mios")]
    public async Task<ActionResult<List<PermisoItemDto>>> Mios()
    {
        var rol = User.EsOdontologo() ? RolUsuario.Odontologo : RolUsuario.Secretaria;
        return await permisos.MatrizAsync(User.GetConsultorioId(), rol);
    }
}
