using CalendarioBackend.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace CalendarioBackend.Api.Controllers;

[ApiController]
[Route("api/autenticacion")]
public class AutenticacionController : ControllerBase
{
    private readonly AutenticacionService _autenticacionService;

    public AutenticacionController(AutenticacionService autenticacionService)
    {
        _autenticacionService = autenticacionService;
    }

    [HttpPost("login")]
    public ActionResult<ColaboradorResumen> IniciarSesion(LoginRequest request)
    {
        var colaborador = _autenticacionService.Autenticar(request.Usuario, request.Contraseña);
        return colaborador is null
            ? Unauthorized()
            : Ok(new ColaboradorResumen(colaborador.Id, colaborador.Usuario));
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
}

public sealed record LoginRequest(string Usuario, string Contraseña);
public sealed record RegistroRequest(string Usuario, string Contraseña);
