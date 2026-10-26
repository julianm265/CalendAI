using System.Text;
using CalendarioBackend.Core.Documents;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace CalendarioBackend.Api.Services;

public interface IDocumentDateExtractionService
{
    Task<DocumentAnalysis> ExtractEventsAsync(
        IFormFile file,
        DateOrder order,
        CancellationToken cancellationToken);
}

/// <summary>
/// Lee un PDF o un .docx conservando la estructura (líneas y tablas) y delega la interpretación
/// de fechas, nombres y lugares en <see cref="DocumentEventAnalyzer"/>.
/// </summary>
public sealed class DocumentDateExtractionService : IDocumentDateExtractionService
{
    private const long MaxFileSize = 10 * 1024 * 1024;
    private static readonly string[] SupportedExtensions = [".pdf", ".docx"];

    private readonly DocumentEventAnalyzer _analyzer = new();

    public Task<DocumentAnalysis> ExtractEventsAsync(
        IFormFile file,
        DateOrder order,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
            throw new ArgumentException("El archivo está vacío.");

        if (file.Length > MaxFileSize)
            throw new ArgumentException("El archivo supera el límite permitido de 10 MB.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!SupportedExtensions.Contains(extension))
            throw new ArgumentException("Solo se admiten archivos PDF o Word en formato .docx.");

        IReadOnlyList<DocumentLine> lines;
        try
        {
            using var stream = file.OpenReadStream();
            lines = extension == ".pdf"
                ? ReadPdf(stream, cancellationToken)
                : ReadDocx(stream, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not ArgumentException)
        {
            throw new ArgumentException(
                "No se pudo leer el archivo. Verifica que no esté dañado ni protegido con contraseña.",
                exception);
        }

        if (lines.Count == 0)
        {
            throw new ArgumentException(
                "El documento no contiene texto seleccionable. Si es un PDF escaneado, conviértelo antes con OCR.");
        }

        return Task.FromResult(_analyzer.Analyze(lines, order));
    }

    // ------------------------------------------------------------------ PDF

    private static IReadOnlyList<DocumentLine> ReadPdf(Stream stream, CancellationToken cancellationToken)
    {
        using var document = PdfDocument.Open(stream);
        var lines = new List<DocumentLine>();

        foreach (var page in document.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var words = page.GetWords()
                .Where(word => !string.IsNullOrWhiteSpace(word.Text))
                .OrderByDescending(word => word.BoundingBox.Bottom)
                .ThenBy(word => word.BoundingBox.Left)
                .ToList();

            var current = new List<Word>();
            var currentBottom = 0.0;

            foreach (var word in words)
            {
                var tolerance = Math.Max(word.BoundingBox.Height, 4) * 0.5;
                if (current.Count > 0 && Math.Abs(word.BoundingBox.Bottom - currentBottom) > tolerance)
                {
                    lines.Add(BuildPdfLine(current, page.Number));
                    current = new List<Word>();
                }

                if (current.Count == 0) currentBottom = word.BoundingBox.Bottom;
                current.Add(word);
            }

            if (current.Count > 0)
                lines.Add(BuildPdfLine(current, page.Number));
        }

        return lines;
    }

    /// <summary>
    /// Une las palabras de una línea de izquierda a derecha. Un hueco grande entre palabras indica
    /// otra columna/celda, así se reconstruyen las tablas "Fecha | Evento | Lugar".
    /// </summary>
    private static DocumentLine BuildPdfLine(List<Word> words, int page)
    {
        var ordered = words.OrderBy(word => word.BoundingBox.Left).ToList();
        var averageHeight = ordered.Average(word => Math.Max(word.BoundingBox.Height, 4));
        var gapThreshold = Math.Max(averageHeight * 2.0, 14);

        var cells = new List<string>();
        var cell = new StringBuilder();
        Word? previous = null;

        foreach (var word in ordered)
        {
            if (previous != null && word.BoundingBox.Left - previous.BoundingBox.Right > gapThreshold)
            {
                cells.Add(cell.ToString().Trim());
                cell.Clear();
            }

            if (cell.Length > 0) cell.Append(' ');
            cell.Append(word.Text);
            previous = word;
        }

        cells.Add(cell.ToString().Trim());

        // Un símbolo suelto (viñeta) no es una columna.
        var meaningful = cells.Where(c => c.Any(char.IsLetterOrDigit)).ToList();
        var text = string.Join(" ", cells);
        return meaningful.Count >= 2
            ? new DocumentLine(text, page, meaningful)
            : new DocumentLine(text, page);
    }

    // ------------------------------------------------------------------ Word

    private static IReadOnlyList<DocumentLine> ReadDocx(Stream stream, CancellationToken cancellationToken)
    {
        using var document = WordprocessingDocument.Open(stream, false);
        var body = document.MainDocumentPart?.Document?.Body
            ?? throw new ArgumentException("El documento Word no contiene texto legible.");

        var lines = new List<DocumentLine>();
        foreach (var element in body.ChildElements)
        {
            cancellationToken.ThrowIfCancellationRequested();

            switch (element)
            {
                case W.Paragraph paragraph:
                    AddParagraph(paragraph, lines);
                    break;
                case W.Table table:
                    AddTable(table, lines);
                    break;
                default:
                    // Controles de contenido (SdtBlock) y otros contenedores.
                    foreach (var nested in element.Descendants<W.Paragraph>())
                        AddParagraph(nested, lines);
                    break;
            }
        }

        return lines;
    }

    private static void AddParagraph(W.Paragraph paragraph, List<DocumentLine> lines)
    {
        foreach (var row in GetParagraphText(paragraph).Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(row)) continue;

            var parts = row.Split('\t')
                .Select(part => part.Trim())
                .Where(part => part.Length > 0)
                .ToList();

            lines.Add(parts.Count >= 2
                ? new DocumentLine(string.Join(" ", parts), null, parts)
                : new DocumentLine(row.Trim(), null));
        }
    }

    private static void AddTable(W.Table table, List<DocumentLine> lines)
    {
        foreach (var row in table.Elements<W.TableRow>())
        {
            var cells = row.Elements<W.TableCell>()
                .Select(cell => string.Join(" ", cell.Descendants<W.Paragraph>()
                    .Select(GetParagraphText)
                    .Select(text => text.Replace('\n', ' ').Replace('\t', ' ').Trim())
                    .Where(text => text.Length > 0)))
                .ToList();

            if (cells.All(string.IsNullOrWhiteSpace)) continue;

            var text = string.Join(" ", cells.Where(cell => cell.Length > 0));
            lines.Add(cells.Count >= 2
                ? new DocumentLine(text, null, cells)
                : new DocumentLine(text, null));
        }
    }

    /// <summary>
    /// Concatena los runs del párrafo sin separadores: Word parte "15 de mar" + "zo" en runs distintos
    /// y unirlos con saltos de línea (como se hacía antes) rompía esas fechas.
    /// </summary>
    private static string GetParagraphText(W.Paragraph paragraph)
    {
        var sb = new StringBuilder();
        foreach (var node in paragraph.Descendants())
        {
            switch (node)
            {
                case W.Text text:
                    sb.Append(text.Text);
                    break;
                case W.TabChar:
                    sb.Append('\t');
                    break;
                case W.Break:
                case W.CarriageReturn:
                    sb.Append('\n');
                    break;
                case W.NoBreakHyphen:
                    sb.Append('-');
                    break;
            }
        }

        return sb.ToString();
    }
}
