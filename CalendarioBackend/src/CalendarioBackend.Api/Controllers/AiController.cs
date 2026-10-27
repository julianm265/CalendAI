using CalendarioBackend.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace CalendarioBackend.Api.Controllers;

[ApiController]
[Route("api/ia")]
public sealed class AiController : ControllerBase
{
    private readonly GeminiAssistantService _assistant;

    public AiController(GeminiAssistantService assistant)
    {
        _assistant = assistant;
    }

    [HttpPost("chat")]
    public async Task<ActionResult<AiChatResponse>> Chat(
        [FromBody] AiChatRequest request,
        [FromHeader(Name = "X-Usuario")] string? usuario,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _assistant.ConversarAsync(
                request.EquipoId,
                usuario ?? "",
                request.Mensaje,
                request.Historial,
                cancellationToken);
            return Ok(response);
        }
        catch (UnauthorizedAccessException exception)
        {
            return Unauthorized(new { error = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { error = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Problem(
                detail: exception.Message,
                statusCode: StatusCodes.Status502BadGateway,
                title: "El asistente no pudo completar la solicitud.");
        }
    }
}
