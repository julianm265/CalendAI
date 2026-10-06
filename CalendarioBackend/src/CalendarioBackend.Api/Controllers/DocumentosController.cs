using CalendarioBackend.Api.Services;
using CalendarioBackend.Core.Documents;
using Microsoft.AspNetCore.Mvc;

namespace CalendarioBackend.Api.Controllers;

[ApiController]
[Route("api/equipos/{equipoId:guid}/documentos")]
public sealed class DocumentosController : ControllerBase
{
    private readonly IDocumentDateExtractionService _extractionService;

    public DocumentosController(IDocumentDateExtractionService extractionService)
    {
        _extractionService = extractionService;
    }

    /// <summary>
    /// Extrae eventos (fecha, nombre, lugar y hora) de un archivo PDF o Word (.docx).
    /// <paramref name="formatoFecha"/>: "auto" (por defecto, se deduce del documento), "dmy" (día/mes/año)
    /// o "mdy" (mes/día/año).
    /// </summary>
    [HttpPost("fechas")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<DocumentAnalysisResponse>> ExtractDates(
        Guid equipoId,
        IFormFile file,
        [FromQuery] string? formatoFecha,
        CancellationToken cancellationToken)
    {
        if (file is null)
            return BadRequest(new { error = "Selecciona un archivo PDF o Word." });

        if (!TryParseOrder(formatoFecha, out var order))
            return BadRequest(new { error = "El formato de fecha debe ser 'auto', 'dmy' o 'mdy'." });

        try
        {
            var analysis = await _extractionService.ExtractEventsAsync(file, order, cancellationToken);
            return Ok(new DocumentAnalysisResponse(
                analysis.Eventos.Select(item => new DocumentEventResponse(
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
                    item.Confianza)).ToArray(),
                analysis.OrdenAplicado == DateOrder.MonthFirst ? "mdy" : "dmy",
                analysis.FormatoMixto,
                analysis.HayFechasAmbiguas));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
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
    string Confianza);

public sealed record DocumentAnalysisResponse(
    IReadOnlyList<DocumentEventResponse> Eventos,
    string FormatoFecha,
    bool FormatoMixto,
    bool HayFechasAmbiguas);
