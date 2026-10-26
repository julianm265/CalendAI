using CalendarioBackend.Api.Services;
using CalendarioBackend.Core.Asistente;
using CalendarioBackend.Core.Documents;
using CalendarioBackend.Core.Repositories;
using CalendarioBackend.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CalendarioBackend.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/equipos/{equipoId:guid}/documentos")]
public sealed class DocumentosController(
    IDocumentDateExtractionService extractionService,
    RefinadorDeEventosService refinador,
    IEquipoRepository equipos,
    AccesoEquipoService acceso) : ControllerBase
{
    /// <summary>
    /// Extrae eventos (fecha, nombre, lugar y hora) de un archivo PDF o Word (.docx).
    /// <paramref name="formatoFecha"/>: "auto" (por defecto, se deduce del documento), "dmy" (día/mes/año)
    /// o "mdy" (mes/día/año). <paramref name="mejorarConIA"/>: si es true (por defecto) y el servidor tiene IA
    /// configurada, Gemini limpia los nombres y lugares; las fechas nunca las modifica la IA.
    /// </summary>
    [HttpPost("fechas")]
    [EnableRateLimiting("asistente")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<DocumentAnalysisResponse>> ExtractDates(
        Guid equipoId,
        IFormFile file,
        [FromQuery] string? formatoFecha,
        CancellationToken cancellationToken,
        [FromQuery] bool mejorarConIA = true)
    {
        acceso.ExigirMiembro(equipoId, User.ObtenerColaboradorId());

        if (file is null)
            return BadRequest(new { error = "Selecciona un archivo PDF o Word." });

        if (!TryParseOrder(formatoFecha, out var order))
            return BadRequest(new { error = "El formato de fecha debe ser 'auto', 'dmy' o 'mdy'." });

        var analysis = await extractionService.ExtractEventsAsync(file, order, cancellationToken);

        IReadOnlyList<DocumentEventCandidate> eventos = analysis.Eventos;
        var iaAplicada = false;
        var mejorados = 0;
        string? avisoIA = null;

        if (mejorarConIA && eventos.Count > 0)
        {
            var conocidos = equipos.ObtenerLugaresFrecuentes(equipoId, 15).Select(l => l.Lugar).ToList();
            var refinado = await refinador.RefinarAsync(eventos, conocidos, cancellationToken);
            eventos = refinado.Eventos;
            mejorados = refinado.Mejorados;
            avisoIA = refinado.Aviso;
            iaAplicada = refinado.Aviso is null || refinado.Mejorados > 0;
        }

        return Ok(new DocumentAnalysisResponse(
            eventos.Select(item => new DocumentEventResponse(
                item.Fecha,
                item.FechaFin,
                item.Hora?.ToString("HH:mm"),
                item.NombreEvento,
                item.Lugar,
                item.TextoEncontrado,
                item.FechaOriginal,
                item.Pagina,
                item.EsAmbigua,
                item.FechaAlternativa,
                item.AnioInferido,
                item.Confianza,
                item.MejoradoConIA,
                item.NombreOriginal,
                item.LugarOriginal)).ToArray(),
            analysis.OrdenAplicado == DateOrder.MonthFirst ? "mdy" : "dmy",
            analysis.FormatoMixto,
            analysis.HayFechasAmbiguas,
            iaAplicada,
            mejorados,
            avisoIA));
    }

    private static bool TryParseOrder(string? value, out DateOrder order)
    {
        order = DateOrder.Auto;
        switch (value?.Trim().ToLowerInvariant())
        {
            case null:
            case "":
            case "auto":
                return true;
            case "dmy":
                order = DateOrder.DayFirst;
                return true;
            case "mdy":
                order = DateOrder.MonthFirst;
                return true;
            default:
                return false;
        }
    }
}

/// <summary>Evento detectado en un documento. Hora en formato HH:mm (null si el documento no la indica).</summary>
public sealed record DocumentEventResponse(
    DateOnly Fecha,
    DateOnly? FechaFin,
    string? Hora,
    string NombreEvento,
    string? Lugar,
    string TextoEncontrado,
    string FechaOriginal,
    int? Pagina,
    bool EsAmbigua,
    DateOnly? FechaAlternativa,
    bool AnioInferido,
    string Confianza,
    bool MejoradoConIA,
    string? NombreOriginal,
    string? LugarOriginal);

public sealed record DocumentAnalysisResponse(
    IReadOnlyList<DocumentEventResponse> Eventos,
    string FormatoFecha,
    bool FormatoMixto,
    bool HayFechasAmbiguas,
    bool IaAplicada,
    int EventosMejoradosConIA,
    string? AvisoIA);
