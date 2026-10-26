using CalendarioBackend.Api.Services;
using CalendarioBackend.Core.Repositories;
using CalendarioBackend.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CalendarioBackend.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/lugares")]
public sealed class LugaresController(IGooglePlacesService placesService) : ControllerBase
{
    [HttpGet("buscar")]
    public async Task<ActionResult<IReadOnlyList<LugarGoogle>>> Buscar(
        [FromQuery] string? query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
            return BadRequest(new { error = "Escribe al menos dos caracteres para buscar un lugar." });

        try
        {
            return Ok(await placesService.BuscarAsync(query.Trim(), cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = exception.Message });
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = "No se pudo consultar Google Places." });
        }
    }
}

[ApiController]
[Authorize]
[Route("api/equipos/{equipoId:guid}/lugares")]
public sealed class LugaresEquipoController(IEquipoRepository equipos, AccesoEquipoService acceso) : ControllerBase
{
    [HttpGet("preferidos")]
    public ActionResult<IReadOnlyList<LugarFrecuente>> Preferidos(Guid equipoId, [FromQuery] int limite = 8)
    {
        acceso.ExigirMiembro(equipoId, User.ObtenerColaboradorId());
        return Ok(equipos.ObtenerLugaresFrecuentes(equipoId, Math.Clamp(limite, 1, 20)));
    }
}
