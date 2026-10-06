namespace CalendarioBackend.Core.Documents;

/// <summary>Orden en el que un documento escribe las fechas numéricas ambiguas (05/10/2026).</summary>
public enum DateOrder
{
    /// <summary>Se deduce del propio documento (por defecto, día-mes-año si no hay evidencia).</summary>
    Auto = 0,
    /// <summary>día/mes/año (formato habitual en Latinoamérica y Europa).</summary>
    DayFirst = 1,
    /// <summary>mes/día/año (formato habitual en EE. UU.).</summary>
    MonthFirst = 2
}

/// <summary>
/// Una línea de texto ya extraída de un documento. Si la línea venía de una tabla
/// (o de columnas separadas visualmente) <see cref="Cells"/> trae el contenido de cada celda.
/// </summary>
public sealed record DocumentLine(string Text, int? Page, IReadOnlyList<string>? Cells = null);

/// <summary>Evento detectado en un documento, con la fecha ya interpretada sin ambigüedad.</summary>
public sealed record DocumentEventCandidate(
    DateOnly Fecha,
    DateOnly? FechaFin,
    TimeOnly? Hora,
    string NombreEvento,
    string? Lugar,
    string TextoEncontrado,
    string FechaOriginal,
    int? Pagina,
    bool EsAmbigua,
    DateOnly? FechaAlternativa,
    bool AnioInferido,
    string Confianza);

/// <summary>Resultado completo del análisis de un documento.</summary>
public sealed record DocumentAnalysis(
    IReadOnlyList<DocumentEventCandidate> Eventos,
    DateOrder OrdenAplicado,
    bool FormatoMixto,
    bool HayFechasAmbiguas);
