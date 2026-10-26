using System.Globalization;
using CalendarioBackend.Api.Services;
using CalendarioBackend.Core.Asistente;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CalendarioBackend.Api.Controllers;

/// <summary>
/// Agente conversacional (Gemini) que propone fechas y lugares para reunirse. Solo propone: crear el evento
/// es una acción explícita del usuario desde el frontend.
/// </summary>
[ApiController]
[Authorize]
[Route("api/equipos/{equipoId:guid}/asistente")]
public sealed class AsistenteController(
    AsistenteReunionesService asistente,
    IModeloGenerativo modelo,
    TimeProvider reloj) : ControllerBase
{
    /// <summary>Indica si el servidor tiene la IA configurada (para que el frontend muestre u oculte el asistente).</summary>
    [HttpGet("estado")]
    public ActionResult<EstadoAsistenteResponse> Estado(Guid equipoId) =>
        Ok(new EstadoAsistenteResponse(modelo.EstaConfigurado));

    [HttpPost("chat")]
    [EnableRateLimiting("asistente")]
    public async Task<ActionResult<ChatResponse>> Conversar(
        Guid equipoId, ChatRequest request, CancellationToken cancellationToken)
    {
        var mensajes = (request.Mensajes ?? [])
            .Select(m => new MensajeChat((m.Rol ?? string.Empty).Trim().ToLowerInvariant(), m.Texto ?? string.Empty))
            .ToList();

        var respuesta = await asistente.ConversarAsync(
            equipoId, User.ObtenerColaboradorId(), mensajes, ResolverHoy(request.Hoy), cancellationToken);

        return Ok(new ChatResponse(
            respuesta.Respuesta,
            respuesta.Propuestas.Select(p => new PropuestaResponse(
                p.Fecha, p.Hora.ToString("HH:mm", CultureInfo.InvariantCulture), p.Lugar, p.Motivo,
                p.Libres, p.Total, p.Ocupados)).ToList(),
            respuesta.Integrantes,
            respuesta.LugaresConsiderados.Select(l => new LugarConsideradoResponse(l.Lugar, l.Usos, l.Integrantes)).ToList()));
    }

    /// <summary>
    /// "Hoy" lo define el navegador (zona horaria del usuario). Se acepta solo si está a ±1 día de la fecha del
    /// servidor; si no, se usa la del servidor.
    /// </summary>
    private DateOnly ResolverHoy(string? hoyCliente)
    {
        var servidor = DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime);
        return DateOnly.TryParseExact(hoyCliente, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var hoy)
            && Math.Abs(hoy.DayNumber - servidor.DayNumber) <= 1
            ? hoy
            : servidor;
    }
}

public sealed record ChatMensajeRequest(string? Rol, string? Texto);
public sealed record ChatRequest(IReadOnlyList<ChatMensajeRequest>? Mensajes, string? Hoy);
public sealed record EstadoAsistenteResponse(bool Disponible);
public sealed record PropuestaResponse(
    DateOnly Fecha, string Hora, string? Lugar, string Motivo, int Libres, int Total, IReadOnlyList<string> Ocupados);
public sealed record LugarConsideradoResponse(string Lugar, int Usos, IReadOnlyList<string> Integrantes);
public sealed record ChatResponse(
    string Respuesta,
    IReadOnlyList<PropuestaResponse> Propuestas,
    IReadOnlyList<string> Integrantes,
    IReadOnlyList<LugarConsideradoResponse> LugaresConsiderados);
