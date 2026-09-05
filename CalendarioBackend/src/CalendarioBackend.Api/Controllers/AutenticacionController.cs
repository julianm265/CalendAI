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
}

public sealed record LoginRequest(string Usuario, string Contraseña);
