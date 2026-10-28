using CalendarioBackend.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace CalendarioBackend.Api.Controllers;

[ApiController]
[Route("api/equipos/{equipoId:guid}/asistente")]
public sealed class AiController : ControllerBase
{
    private readonly GeminiAssistantService _assistant;
    private readonly JwtTokenService _tokens;

    public AiController(GeminiAssistantService assistant, JwtTokenService tokens)
    {
        _assistant = assistant;
        _tokens = tokens;
    }

    [HttpGet("estado")]
    public ActionResult<AsistenteEstado> Estado() => Ok(_assistant.ObtenerEstado());

    [HttpPost("chat")]
    public async Task<ActionResult<AsistenteChatResponse>> Chat(
        Guid equipoId,
        [FromBody] AsistenteChatRequest request,
        [FromHeader(Name = "X-Usuario")] string? usuario,
        CancellationToken cancellationToken)
    {
        try
        {
            var identidad = _tokens.Validar(Request.Headers.Authorization.FirstOrDefault());
            if (identidad is null) return Unauthorized(new { error = "Se requiere un JWT Bearer válido." });
            return Ok(await _assistant.ConversarAsync(
                equipoId, identidad, request.Mensajes, request.Hoy, cancellationToken));
        }
        catch (UnauthorizedAccessException exception) { return Unauthorized(new { error = exception.Message }); }
        catch (ArgumentException exception) { return BadRequest(new { error = exception.Message }); }
        catch (KeyNotFoundException exception) { return NotFound(new { error = exception.Message }); }
        catch (InvalidOperationException exception)
        {
            return Problem(detail: exception.Message, statusCode: StatusCodes.Status502BadGateway,
                title: "El asistente no pudo completar la solicitud.");
        }
    }
}
