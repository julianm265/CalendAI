using CalendarioBackend.Core.Models;
using CalendarioBackend.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace CalendarioBackend.Api.Controllers;

[ApiController]
[Route("api/equipos/{equipoId:guid}/eventos")]
public class EventosController : ControllerBase
{
    private readonly CalendarioService _calendarioService;

    public EventosController(CalendarioService calendarioService)
    {
        _calendarioService = calendarioService;
    }

    [HttpGet]
    public ActionResult<IEnumerable<Evento>> ObtenerDelDia(Guid equipoId, [FromQuery] DateOnly fecha)
    {
        try
        {
            return Ok(_calendarioService.ObtenerEventosDelDia(equipoId, fecha));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("mes")]
    public ActionResult<IEnumerable<EventoMesResponse>> ObtenerDelMes(
        Guid equipoId,
        [FromQuery] int año,
        [FromQuery] byte mes)
    {
        try
        {
            var eventos = _calendarioService.ObtenerEventosDelMes(equipoId, año, mes)
                .Select(resultado => new EventoMesResponse(resultado.Fecha, resultado.Evento));
            return Ok(eventos);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    public ActionResult<Evento> Agregar(Guid equipoId, AgregarEventoRequest request)
    {
        try
        {
            var evento = _calendarioService.AgregarEvento(
                equipoId,
                request.Fecha,
                request.NombreEvento,
                request.Hora,
                request.Lugar,
                request.Descripcion,
                request.ColaboradorOrganizadorId);

            return Ok(evento);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
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

    [HttpDelete("{eventoId:guid}")]
    public IActionResult Eliminar(Guid equipoId, Guid eventoId, [FromQuery] DateOnly fecha)
    {
        try
        {
            return _calendarioService.EliminarEvento(equipoId, fecha, eventoId)
                ? NoContent()
                : NotFound();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}

public sealed record AgregarEventoRequest(
    DateOnly Fecha,
    string NombreEvento,
    TimeOnly Hora,
    string Lugar,
    string? Descripcion,
    Guid? ColaboradorOrganizadorId);

public sealed record EventoMesResponse(DateOnly Fecha, Evento Evento);
