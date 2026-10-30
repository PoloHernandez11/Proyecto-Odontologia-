using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OdontoApp.Api.Auth;
using OdontoApp.Api.Data;
using OdontoApp.Api.Dtos;
using OdontoApp.Api.Models;

namespace OdontoApp.Api.Controllers;

/// <summary>
/// Autenticación con Firebase (proveedor Google). La API no guarda contraseñas: verifica el ID token
/// de Firebase y emite su propio token de sesión de vida corta.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController(OdontoDbContext db, TokenService tokens, FirebaseIdentidad google) : ControllerBase
{
    /// <summary>
    /// Inicio de sesión. Solo entra quien ya está cargado en el consultorio (por el primer ingreso o por invitación)
    /// y tiene el usuario activo. Cualquier otra cuenta de Google recibe 403.
    /// </summary>
    [HttpPost("google")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> LoginGoogle(GoogleLoginRequest req)
    {
        var identidad = await google.ValidarAsync(req.Credential);
        if (identidad is null)
            return Unauthorized(new { mensaje = "No se pudo verificar la cuenta de Google." });

        var usuario = await db.Usuarios.Include(u => u.Consultorio)
            .FirstOrDefaultAsync(u => u.GoogleSub == identidad.Sub)
            ?? await db.Usuarios.Include(u => u.Consultorio)
                .FirstOrDefaultAsync(u => u.GoogleSub == null && u.Email == identidad.Email);

        if (usuario is null)
            return StatusCode(StatusCodes.Status403Forbidden,
                new { mensaje = "Tu cuenta no está autorizada en ningún consultorio. Pedile una invitación al odontólogo." });

        if (!usuario.Activo)
            return StatusCode(StatusCodes.Status403Forbidden,
                new { mensaje = "Tu acceso al consultorio está desactivado. Consultá con el odontólogo." });

        // Primer ingreso de una cuenta cargada por correo: queda vinculada a su identificador de Google.
        if (usuario.GoogleSub is null)
        {
            usuario.GoogleSub = identidad.Sub;
            await db.SaveChangesAsync();
        }

        return Respuesta(usuario);
    }

    /// <summary>Si el consultorio ya existe y si Google está configurado en el servidor.</summary>
    [HttpGet("estado")]
    [AllowAnonymous]
    public async Task<ActionResult<EstadoConsultorioDto>> Estado() =>
        new EstadoConsultorioDto(await db.Usuarios.AnyAsync(), google.Configurado);

    /// <summary>
    /// Primer ingreso. Solo funciona mientras no exista ningún usuario: crea el consultorio y al odontólogo dueño,
    /// usando la cuenta de Google con la que inició sesión.
    /// </summary>
    [HttpPost("registro-dueno")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> RegistrarDueno(RegistroDuenoDto dto)
    {
        if (await db.Usuarios.AnyAsync())
            return Conflict(new { mensaje = "El consultorio ya está registrado. Pedile al odontólogo una invitación." });

        var identidad = await google.ValidarAsync(dto.Credential);
        if (identidad is null)
            return Unauthorized(new { mensaje = "No se pudo verificar la cuenta de Google." });

        var consultorio = new Consultorio { Nombre = dto.NombreConsultorio.Trim() };
        var dueno = new Usuario
        {
            Consultorio = consultorio,
            Nombre = string.IsNullOrWhiteSpace(identidad.Nombre) ? identidad.Email : identidad.Nombre.Trim(),
            Email = identidad.Email,
            GoogleSub = identidad.Sub,
            Rol = RolUsuario.Odontologo,
        };
        db.Usuarios.Add(dueno);
        await db.SaveChangesAsync();

        return Respuesta(dueno);
    }

    /// <summary>Datos de una invitación vigente, para mostrar la pantalla de aceptación.</summary>
    [HttpGet("invitaciones/{token}")]
    [AllowAnonymous]
    public async Task<ActionResult<InvitacionPublicaDto>> VerInvitacion(string token)
    {
        var invitacion = await BuscarInvitacion(token);
        if (invitacion is null) return NotFound(new { mensaje = "La invitación no existe." });
        if (!EstaVigente(invitacion))
            return BadRequest(new { mensaje = "La invitación ya no es válida (vencida, usada o revocada)." });

        return new InvitacionPublicaDto(invitacion.Email, invitacion.Rol,
            invitacion.Consultorio.Nombre, invitacion.ExpiraEn);
    }

    /// <summary>
    /// La secretaria acepta la invitación iniciando sesión con Google. El correo de la cuenta de Google
    /// tiene que ser el mismo que figura en la invitación. El claim se hace con un UPDATE condicional
    /// dentro de una transacción: si dos personas usan el mismo enlace a la vez, solo una gana.
    /// </summary>
    [HttpPost("invitaciones/aceptar")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> AceptarInvitacion(AceptarInvitacionDto dto)
    {
        var invitacion = await BuscarInvitacion(dto.Token);
        if (invitacion is null || !EstaVigente(invitacion))
            return BadRequest(new { mensaje = "La invitación ya no es válida (vencida, usada o revocada)." });

        var identidad = await google.ValidarAsync(dto.Credential);
        if (identidad is null)
            return Unauthorized(new { mensaje = "No se pudo verificar la cuenta de Google." });

        if (!string.Equals(identidad.Email, invitacion.Email, StringComparison.OrdinalIgnoreCase))
            return StatusCode(StatusCodes.Status403Forbidden,
                new { mensaje = $"Esta invitación es para {invitacion.Email}. Iniciá sesión con esa cuenta de Google." });

        if (await db.Usuarios.AnyAsync(u => u.ConsultorioId == invitacion.ConsultorioId && u.Email == identidad.Email))
            return Conflict(new { mensaje = "Ya tenés acceso a este consultorio." });

        await using var tx = await db.Database.BeginTransactionAsync();

        var ahora = DateTime.UtcNow;
        var reclamadas = await db.Invitaciones
            .Where(i => i.Id == invitacion.Id && i.UsadaEn == null && i.RevocadaEn == null)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.UsadaEn, ahora));
        if (reclamadas == 0)
            return BadRequest(new { mensaje = "La invitación ya fue usada." });

        var usuario = new Usuario
        {
            ConsultorioId = invitacion.ConsultorioId,
            Nombre = string.IsNullOrWhiteSpace(identidad.Nombre) ? identidad.Email : identidad.Nombre.Trim(),
            Email = identidad.Email,
            GoogleSub = identidad.Sub,
            Rol = invitacion.Rol,
        };
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();

        await db.Invitaciones.Where(i => i.Id == invitacion.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.UsuarioCreadoId, usuario.Id));
        await tx.CommitAsync();

        usuario.Consultorio = invitacion.Consultorio;
        return Respuesta(usuario);
    }

    /// <summary>Miembros del consultorio del odontólogo (activos e inactivos).</summary>
    [HttpGet("usuarios")]
    [Authorize(Roles = Roles.Odontologo)]
    public async Task<ActionResult<List<UsuarioDto>>> Usuarios()
    {
        var consultorioId = User.GetConsultorioId();
        return await db.Usuarios.AsNoTracking()
            .Where(u => u.ConsultorioId == consultorioId)
            .OrderBy(u => u.Id)
            .Select(u => new UsuarioDto(u.Id, u.Nombre, u.Email, u.Rol, u.Activo))
            .ToListAsync();
    }

    // ---------- helpers ----------

    private async Task<Invitacion?> BuscarInvitacion(string token)
    {
        var hash = InvitacionToken.Hash(token);
        return await db.Invitaciones.Include(i => i.Consultorio)
            .SingleOrDefaultAsync(i => i.TokenHash == hash);
    }

    private static bool EstaVigente(Invitacion i) =>
        i.UsadaEn is null && i.RevocadaEn is null && i.ExpiraEn > DateTime.UtcNow;

    private LoginResponse Respuesta(Usuario u)
    {
        var (token, expira) = tokens.Generar(u);
        return new LoginResponse(token, expira, u.Id, u.Nombre, u.Email, u.Rol, u.ConsultorioId, u.Consultorio.Nombre);
    }
}
