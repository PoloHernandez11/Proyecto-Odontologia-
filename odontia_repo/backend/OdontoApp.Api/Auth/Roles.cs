using System.Security.Claims;
using OdontoApp.Api.Models;

namespace OdontoApp.Api.Auth;

/// <summary>Nombres de rol para usar en [Authorize(Roles = ...)]. Coinciden con RolUsuario.ToString().</summary>
public static class Roles
{
    public const string Secretaria = nameof(RolUsuario.Secretaria);
    public const string Odontologo = nameof(RolUsuario.Odontologo);

    /// <summary>Cualquier usuario del consultorio.</summary>
    public const string Staff = Secretaria + "," + Odontologo;
}

public static class ClaimsPrincipalExtensions
{
    public static int GetUsuarioId(this ClaimsPrincipal user) =>
        int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? throw new InvalidOperationException("El token no contiene el id de usuario."));

    public static bool EsOdontologo(this ClaimsPrincipal user) => user.IsInRole(Roles.Odontologo);

    /// <summary>Consultorio del usuario autenticado. Todas las consultas de datos deben filtrar por él.</summary>
    public static int GetConsultorioId(this ClaimsPrincipal user) =>
        int.Parse(user.FindFirstValue(TokenService.ConsultorioClaim)
                  ?? throw new InvalidOperationException("El token no contiene el consultorio."));
}
