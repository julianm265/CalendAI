using CalendarioBackend.Api.Seguridad;
using CalendarioBackend.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace CalendarioBackend.Api.Controllers;

[ApiController]
[Route("api/autenticacion")]
public class AutenticacionController : ControllerBase
{
    private readonly AutenticacionService _autenticacionService;
    private readonly SesionService _sesionService;
    private readonly SesionActual _sesionActual;

    public AutenticacionController(
        AutenticacionService autenticacionService,
        SesionService sesionService,
        SesionActual sesionActual)
    {
        _autenticacionService = autenticacionService;
        _sesionService = sesionService;
        _sesionActual = sesionActual;
    }

    [HttpPost("login")]
    public ActionResult<LoginResponse> IniciarSesion(LoginRequest request)
    {
        var colaborador = _autenticacionService.Autenticar(request.Usuario, request.Contraseña);
        return colaborador is null
            ? Unauthorized()
            : Ok(new LoginResponse(
                new ColaboradorResumen(colaborador.Id, colaborador.Usuario),
                _autenticacionService.ObtenerOCrearCalendarioPersonal(colaborador) is { } equipo
                    ? new EquipoLoginResumen(equipo.Id, equipo.NombreEquipo, equipo.LColaboradores.Count, equipo.EsPersonal)
                    : null,
                _sesionService.Crear(colaborador.Id)));
    }

    [HttpPost("registro")]
    public ActionResult<ColaboradorResumen> Registrar(RegistroRequest request)
    {
        try
        {
            var colaborador = _autenticacionService.RegistrarUsuario(request.Usuario, request.Contraseña);
            return Created("api/autenticacion/registro", new ColaboradorResumen(colaborador.Id, colaborador.Usuario));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { error = exception.Message });
        }
    }

    [HttpPost("logout")]
    public ActionResult<LogoutResponse> CerrarSesion()
    {
        _sesionService.Cerrar(_sesionActual.Token);
        return Ok(new LogoutResponse(true));
    }
}

public sealed record LoginRequest(string Usuario, string Contraseña);
public sealed record LogoutResponse(bool Cerrada);
public sealed record RegistroRequest(string Usuario, string Contraseña);
public sealed record LoginResponse(ColaboradorResumen Colaborador, EquipoLoginResumen? Equipo, string Token);
public sealed record EquipoLoginResumen(Guid Id, string Nombre, int Colaboradores, bool EsPersonal);
