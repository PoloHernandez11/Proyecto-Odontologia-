using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using OdontoApp.Api.Models;

namespace OdontoApp.Api.Auth;

/// <summary>
/// Aplica la matriz de permisos a una acción. El odontólogo siempre pasa.
/// Para la secretaria: SinAcceso devuelve 403. Con acceso y Con registro ejecutan la acción
/// y la guardan en el historial (con el nivel con que se hizo).
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public class PermisoAttribute(ModuloPermiso modulo, AccionPermiso accion) : Attribute, IAsyncAuthorizationFilter
{
    public ModuloPermiso Modulo { get; } = modulo;
    public AccionPermiso Accion { get; } = accion;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var usuario = context.HttpContext.User;
        if (usuario.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }
        if (usuario.EsOdontologo()) return;

        var permisos = context.HttpContext.RequestServices.GetRequiredService<PermisosService>();
        var consultorioId = usuario.GetConsultorioId();
        var nivel = await permisos.NivelAsync(consultorioId, RolUsuario.Secretaria, Modulo, Accion);

        if (nivel == NivelPermiso.SinAcceso)
        {
            context.Result = new ObjectResult(new { mensaje = "Tu rol no tiene permiso para esta acción." })
            {
                StatusCode = StatusCodes.Status403Forbidden,
            };
            return;
        }

        await permisos.RegistrarActividadAsync(consultorioId, usuario.GetUsuarioId(), Modulo, Accion, nivel);
    }
}
