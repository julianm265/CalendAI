using CalendarioBackend.Api.Services;
using CalendarioBackend.Core.Models;
using CalendarioBackend.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CalendarioBackend.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/equipos/{equipoId:guid}/eventos")]
public class EventosController(CalendarioService calendario) : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyList<EventoResponse>> ObtenerDelDia(Guid equipoId, [FromQuery] DateOnly fecha) =>
        Ok(calendario.ObtenerEventosDelDia(equipoId, User.ObtenerColaboradorId(), fecha)
            .Select(EventoResponse.Desde).ToList());

    [HttpGet("mes")]
    public ActionResult<IReadOnlyList<EventoMesResponse>> ObtenerDelMes(
        Guid equipoId, [FromQuery] int año, [FromQuery] int mes) =>
        Ok(calendario.ObtenerEventosDelMes(equipoId, User.ObtenerColaboradorId(), año, mes)
            .Select(r => new EventoMesResponse(r.Fecha, EventoResponse.Desde(r.Evento)))
            .ToList());

    /// <summary>Crea un evento. Si no se indica organizador, lo es quien lo crea.</summary>
    [HttpPost]
    public ActionResult<EventoResponse> Agregar(Guid equipoId, AgregarEventoRequest request)
    {
        var evento = calendario.AgregarEvento(
            equipoId,
            User.ObtenerColaboradorId(),
            request.Fecha,
            request.NombreEvento,
            request.Hora,
            request.Lugar,
            request.Descripcion,
            request.ColaboradorOrganizadorId);

        return Ok(EventoResponse.Desde(evento));
    }

    /// <summary>Elimina un evento. Pueden hacerlo el líder del equipo o el organizador del evento.</summary>
    [HttpDelete("{eventoId:guid}")]
    public IActionResult Eliminar(Guid equipoId, Guid eventoId) =>
        calendario.EliminarEvento(equipoId, User.ObtenerColaboradorId(), eventoId)
            ? NoContent()
            : NotFound();
}

public sealed record AgregarEventoRequest(
    DateOnly Fecha,
    string NombreEvento,
    TimeOnly Hora,
    string Lugar,
    string? Descripcion,
    Guid? ColaboradorOrganizadorId);

/// <summary>Evento tal como lo ve el frontend (no se expone el modelo de dominio).</summary>
public sealed record EventoResponse(
    Guid Id,
    string NombreEvento,
    TimeOnly HoraEvento,
    string? LugarEvento,
    string? Descripcion,
    Guid? ColaboradorOrganizadorId)
{
    public static EventoResponse Desde(Evento e) =>
        new(e.Id, e.NombreEvento, e.HoraEvento, e.LugarEvento, e.Descripcion, e.ColaboradorOrganizadorId);
}

public sealed record EventoMesResponse(DateOnly Fecha, EventoResponse Evento);
