using System.Security.Claims;
using System.Text;
using CalendarioBackend.Core.Models;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CalendarioBackend.Api.Services;

public sealed class JwtOptions
{
    public const string Issuer = "calendai";
    public const string Audience = "calendai-app";

    public string Key { get; set; } = string.Empty;
    public int HorasDeVida { get; set; } = 8;

    public SymmetricSecurityKey ClaveDeFirma() => new(Encoding.UTF8.GetBytes(Key));
}

public interface ITokenService
{
    TokenEmitido Crear(Colaborador colaborador);
}

public sealed record TokenEmitido(string Token, DateTime ExpiraEn);

/// <summary>Emite los tokens JWT (HS256) con los que el frontend se identifica ante la API.</summary>
public sealed class TokenService(JwtOptions opciones) : ITokenService
{
    public TokenEmitido Crear(Colaborador colaborador)
    {
        var expira = DateTime.UtcNow.AddHours(opciones.HorasDeVida);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = JwtOptions.Issuer,
            Audience = JwtOptions.Audience,
            Expires = expira,
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, colaborador.Id.ToString()),
                new Claim("usuario", colaborador.Usuario)
            }),
            SigningCredentials = new SigningCredentials(opciones.ClaveDeFirma(), SecurityAlgorithms.HmacSha256)
        };

        return new TokenEmitido(new JsonWebTokenHandler().CreateToken(descriptor), expira);
    }
}
