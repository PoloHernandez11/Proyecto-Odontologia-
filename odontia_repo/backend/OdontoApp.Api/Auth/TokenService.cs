using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OdontoApp.Api.Models;

namespace OdontoApp.Api.Auth;

public class JwtOptions
{
    public string Issuer { get; set; } = "OdontoApp";
    public string Audience { get; set; } = "OdontoApp.Web";
    /// <summary>Clave HMAC de al menos 32 caracteres. Configurarla con user-secrets o variable de entorno, nunca en el repo.</summary>
    public string Key { get; set; } = string.Empty;
    public int ExpiraEnHoras { get; set; } = 10; // una jornada de consultorio
}

public class TokenService(IOptions<JwtOptions> options)
{
    /// <summary>Claim con el id del consultorio del usuario (multi-consultorio listo).</summary>
    public const string ConsultorioClaim = "consultorio_id";

    private readonly JwtOptions _opt = options.Value;

    public (string Token, DateTime Expira) Generar(Usuario usuario)
    {
        var expira = DateTime.UtcNow.AddHours(_opt.ExpiraEnHoras);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new Claim(ClaimTypes.Name, usuario.Nombre),
            new Claim(ClaimTypes.Role, usuario.Rol.ToString()),
            new Claim(ConsultorioClaim, usuario.ConsultorioId.ToString()),
        };
        var credenciales = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.Key)), SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(_opt.Issuer, _opt.Audience, claims,
            expires: expira, signingCredentials: credenciales);

        return (new JwtSecurityTokenHandler().WriteToken(token), expira);
    }
}
