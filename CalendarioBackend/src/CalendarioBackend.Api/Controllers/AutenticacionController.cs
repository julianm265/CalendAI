using CalendarioBackend.Api.Services;
using CalendarioBackend.Core.Models;
using CalendarioBackend.Core.Repositories;
using CalendarioBackend.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CalendarioBackend.Api.Controllers;

[ApiController]
[Route("api/autenticacion")]
public class AutenticacionController(
    AutenticacionService autenticacion,
    IEquipoRepository equipos,
    ITokenService tokens) : ControllerBase
{
    [HttpPost("login")]
    [EnableRateLimiting("autenticacion")]
    public ActionResult<LoginResponse> IniciarSesion(LoginRequest request)
    {
        var colaborador = autenticacion.Autenticar(request.Usuario, request.Contraseña);
        if (colaborador is null)
            return Unauthorized(new { error = "Usuario o contraseña incorrectos." });

        var personal = autenticacion.ObtenerOCrearCalendarioPersonal(colaborador);
        var emitido = tokens.Crear(colaborador);
        var miembros = equipos.ObtenerMiembros(personal.Id).Count;

        return Ok(new LoginResponse(
            emitido.Token,
            emitido.ExpiraEn,
            new ColaboradorResumen(colaborador.Id, colaborador.Usuario),
            new EquipoLoginResumen(personal.Id, personal.NombreEquipo, miembros, personal.EsPersonal, RolEquipo.Lider.ATexto())));
    }

    [HttpPost("registro")]
    [EnableRateLimiting("autenticacion")]
    public ActionResult<ColaboradorResumen> Registrar(RegistroRequest request)
    {
        var colaborador = autenticacion.RegistrarUsuario(request.Usuario, request.Contraseña);
        return Created("api/autenticacion/registro", new ColaboradorResumen(colaborador.Id, colaborador.Usuario));
    }

    [HttpGet("yo")]
    [Authorize]
    public ActionResult<ColaboradorResumen> Yo()
    {
        var colaborador = autenticacion.ObtenerPorId(User.ObtenerColaboradorId());
        return colaborador is null
            ? Unauthorized()
            : Ok(new ColaboradorResumen(colaborador.Id, colaborador.Usuario));
    }
}

public sealed record LoginRequest(string Usuario, string Contraseña);
public sealed record RegistroRequest(string Usuario, string Contraseña);
public sealed record LoginResponse(string Token, DateTime ExpiraEn, ColaboradorResumen Colaborador, EquipoLoginResumen Equipo);
public sealed record EquipoLoginResumen(Guid Id, string Nombre, int Colaboradores, bool EsPersonal, string Rol);
