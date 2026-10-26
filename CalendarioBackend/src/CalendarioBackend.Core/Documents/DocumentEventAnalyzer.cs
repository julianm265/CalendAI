using System.Text;
using System.Text.RegularExpressions;

namespace CalendarioBackend.Core.Documents;

/// <summary>
/// Convierte el texto de un documento en eventos de calendario: fecha (sin ambigüedades), nombre,
/// lugar y hora. No depende de PDF ni de Word: recibe líneas ya extraídas, por lo que se puede
/// probar de forma aislada.
///
/// Reglas principales para las fechas:
///  1. Se escanean todos los formatos (numérico, ISO, con nombre de mes, rangos).
///  2. Las fechas numéricas inequívocas (25/03/2026 o 03/25/2026) revelan el orden del documento.
///  3. Las ambiguas (03/04/2026) usan ese orden; si el documento no da evidencia, o mezcla órdenes,
///     se marcan como ambiguas y se ofrece la lectura alternativa en vez de adivinar en silencio.
///  4. Un día de la semana escrito junto a la fecha ("lunes 03/04/2026") también la desambigua.
///  5. Toda fecha se valida contra el calendario real (no existen 31/04 ni 29/02/2027).
/// </summary>
public sealed class DocumentEventAnalyzer
{
    private const char Marker = '\u0001';
    private const int MaxNameLength = 120;
    private const int MaxPlaceLength = 120;
    private const int MaxContextLength = 200;
    private const int LookAheadLines = 3;
    private const RegexOptions Opts = RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;

    private enum NameSource { None, Following, Heading, Line, Label, Table }

    private const string VenueNouns =
        "sala|sal[oó]n|auditorio|aula|oficina|edificio|bloque|laboratorio|biblioteca|teatro|estadio|coliseo|plaza|parque|" +
        "hotel|centro|campus|universidad|colegio|iglesia|cancha|coworking|restaurante|caf[eé]|casa|museo|club|torre|piso|" +
        "cl[ií]nica|hospital|sede|room|hall|office|building|center|centre|library|theat(?:er|re)|stadium|zoom|teams|google\\s+meet";

    private const string StrongVenueNouns =
        "sala|sal[oó]n|auditorio|aula|oficina|edificio|bloque|laboratorio|biblioteca|teatro|estadio|coliseo|sede|" +
        "room|hall|office|building|library|stadium|zoom|teams|google\\s+meet";

    // "en el Auditorio Central", "en la Sala 3 a las 10": venue conocida precedida de "en".
    private static readonly Regex VenueRx = new(
        @"\b(?:en|at)\s+(?:(?:el|la|los|las|the)\s+)?(?<v>(?:" + VenueNouns + @")(?![\p{L}])" +
        @"(?:(?!\s+(?:a\s+las?|para|con|donde|que|desde|hasta)(?![\p{L}]))[^,.;|()\u0001])*)",
        Opts);

    // "Reunión, Sala 2" / "Taller - Auditorio Central": venue fuerte tras un separador, sin "en".
    private static readonly Regex SeparatedVenueRx = new(
        @"(?:^|[,;|(\-–—])\s*(?<v>(?:" + StrongVenueNouns + @")(?![\p{L}])[^,;|()\u0001]*)(?:\s*\))?",
        Opts);

    // Línea completa que empieza con una venue: "Sala 2", "Auditorio Central, piso 3".
    private static readonly Regex VenueStartRx = new(
        @"^\s*(?<v>(?:" + StrongVenueNouns + @")(?![\p{L}])[^;|\u0001]*)$",
        Opts);

    // "en Medellín", "en Universidad de Antioquia": nombre propio (mayúscula) al final de la cláusula.
    // Sin IgnoreCase a propósito: la mayúscula inicial distingue un lugar de "en la mañana".
    private static readonly Regex ProperPlaceRx = new(
        @"\b[Ee]n\s+(?:(?:el|la)\s+)?(?!(?:Enero|Febrero|Marzo|Abril|Mayo|Junio|Julio|Agosto|Septiembre|Setiembre|Octubre|Noviembre|Diciembre|Lunes|Martes|Mi[eé]rcoles|Jueves|Viernes|S[aá]bado|Domingo)(?![\p{L}]))" +
        @"(?<v>[A-ZÁÉÍÓÚÑ][\p{L}\d'’\-]*(?:\s+(?:(?:de|del|la|las|los|el|y|e)\s+)?[A-ZÁÉÍÓÚÑ0-9][\p{L}\d'’\-]*){0,5}" +
        @"(?:,\s*[A-ZÁÉÍÓÚÑ][\p{L}'’\-]*(?:\s+[A-ZÁÉÍÓÚÑ][\p{L}'’\-]*){0,2})?)" +
        @"(?=\s*(?:[,.;|(]|\u0001|$)|\s+(?:a\s+las?|para|desde|hasta|con)(?![\p{L}]))",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex LabelRx = new(
        @"(?<![\p{L}])(?<label>nombre\s+del\s+evento|nombre|evento|actividad|t[ií]tulo|asunto|tema|reuni[oó]n|" +
        @"lugar|sede|ubicaci[oó]n|direcci[oó]n|sitio|sal[oó]n|event|activity|title|subject|venue|location|place|where|" +
        @"hora|horario|time|fecha|date|descripci[oó]n|description|organiza(?:dor|do\s+por)?|responsable)\s*[:：]",
        Opts);

    private static readonly HashSet<string> NameLabels = new()
    {
        "nombre del evento", "nombre", "evento", "actividad", "titulo", "asunto", "tema", "reunion",
        "event", "activity", "title", "subject"
    };

    private static readonly HashSet<string> PlaceLabels = new()
    {
        "lugar", "sede", "ubicacion", "direccion", "sitio", "salon", "venue", "location", "place", "where"
    };

    private static readonly HashSet<string> DateLabels = new() { "fecha", "date" };

    private static readonly Regex WeekdayBeforeMarkerRx = new(
        @"(?<![\p{L}])(?:lunes|martes|mi[eé]rcoles|jueves|viernes|s[aá]bado|domingo|lun|mar|mi[eé]|jue|vie|s[aá]b|dom|mon|tue|wed|thu|fri|sat|sun)(?![\p{L}])\.?,?\s*(?=\u0001)",
        Opts);

    private static readonly Regex ConnectorsBeforeMarkerRx = new(
        @"(?:(?<![\p{L}])(?:desde|hasta|para|a\s+partir|partir|el|la|las|los|d[ií]a|fecha|del|de|al|a|on|from|until|at|the|hora|hrs?)(?![\p{L}])[\s:,\-–—]*){0,4}\u0001",
        Opts);

    private static readonly Regex EmptyBracketsRx = new(@"\(\s*\)|\[\s*\]", RegexOptions.Compiled);

    private static readonly Regex LeadingBulletRx = new(
        @"^\s*(?:[•·▪●■◦○▶►✓✔*]|[-–—](?=\s)|\d{1,2}[.)](?=\s))\s*",
        RegexOptions.Compiled);

    private static readonly Regex EdgePunctuationRx = new(
        @"^[\s\-–—:;,.|/\\•·]+|[\s\-–—:;,.|/\\•·]+$",
        RegexOptions.Compiled);

    private static readonly Regex TrailingConnectorsRx = new(
        @"(?:\s+(?:y|e|o|a|de|del|al|el|la|los|las|en|para|con|desde|hasta|por|que|a\s+las?))+$",
        Opts);

    private static readonly Regex LeadingConnectorsRx = new(
        @"^(?:(?:y|e|o|de|del|al|para|con|desde|hasta|por|que|a)\s+)+",
        Opts);

    private static readonly Regex WhitespaceRx = new(@"\s+", RegexOptions.Compiled);

    private static readonly Regex RangeConnectorRx = new(
        @"^\s*(?:al?|-|–|—|hasta(?:\s+el)?|to|until)\s*$",
        Opts);

    // Fechas que no son eventos: emisión del documento, nacimiento, vigencias...
    private static readonly Regex NoiseRx = new(
        @"\b(?:emisi[oó]n|expedici[oó]n|nacimiento|elaborad[oa]|generad[oa]|impres[oa]|radicad[oa]|vigencia|actualizad[oa]|printed|issued|generated)\b",
        Opts);

    private static readonly Regex WeekdayWordRx = new(
        @"(?<![\p{L}])(?<w>[\p{L}]{3,10})\.?,?\s*(?:el\s+)?(?:d[ií]a\s+)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Dictionary<string, DayOfWeek> Weekdays = new()
    {
        ["lunes"] = DayOfWeek.Monday, ["lun"] = DayOfWeek.Monday, ["monday"] = DayOfWeek.Monday, ["mon"] = DayOfWeek.Monday,
        ["martes"] = DayOfWeek.Tuesday, ["mar"] = DayOfWeek.Tuesday, ["tuesday"] = DayOfWeek.Tuesday, ["tue"] = DayOfWeek.Tuesday,
        ["miercoles"] = DayOfWeek.Wednesday, ["mie"] = DayOfWeek.Wednesday, ["wednesday"] = DayOfWeek.Wednesday, ["wed"] = DayOfWeek.Wednesday,
        ["jueves"] = DayOfWeek.Thursday, ["jue"] = DayOfWeek.Thursday, ["thursday"] = DayOfWeek.Thursday, ["thu"] = DayOfWeek.Thursday,
        ["viernes"] = DayOfWeek.Friday, ["vie"] = DayOfWeek.Friday, ["friday"] = DayOfWeek.Friday, ["fri"] = DayOfWeek.Friday,
        ["sabado"] = DayOfWeek.Saturday, ["sab"] = DayOfWeek.Saturday, ["saturday"] = DayOfWeek.Saturday, ["sat"] = DayOfWeek.Saturday,
        ["domingo"] = DayOfWeek.Sunday, ["dom"] = DayOfWeek.Sunday, ["sunday"] = DayOfWeek.Sunday, ["sun"] = DayOfWeek.Sunday
    };

    private static readonly Dictionary<string, string> HeaderWords = new()
    {
        ["fecha"] = "date", ["fechas"] = "date", ["date"] = "date", ["dia"] = "date", ["cuando"] = "date",
        ["evento"] = "name", ["eventos"] = "name", ["actividad"] = "name", ["actividades"] = "name",
        ["nombre"] = "name", ["titulo"] = "name", ["asunto"] = "name", ["tema"] = "name", ["event"] = "name",
        ["activity"] = "name", ["title"] = "name", ["subject"] = "name", ["descripcion"] = "name",
        ["detalle"] = "name", ["concepto"] = "name", ["nombre del evento"] = "name",
        ["nombre de la actividad"] = "name", ["descripcion de la actividad"] = "name",
        ["lugar"] = "place", ["sede"] = "place", ["ubicacion"] = "place", ["salon"] = "place", ["sitio"] = "place",
        ["direccion"] = "place", ["place"] = "place", ["location"] = "place", ["venue"] = "place",
        ["donde"] = "place", ["aula"] = "place",
        ["hora"] = "time", ["horario"] = "time", ["time"] = "time", ["hora inicio"] = "time", ["hora de inicio"] = "time"
    };

    private static readonly HashSet<string> StopWords = new()
    {
        "del", "los", "las", "para", "con", "que", "por", "desde", "hasta", "este", "esta", "the", "and"
    };

    // ------------------------------------------------------------------ tipos internos

    private readonly record struct Span(int Index, int End);

    private sealed class LineInfo
    {
        public LineInfo(DocumentLine source, string text, string[]? cells)
        {
            Source = source;
            Text = text;
            Cells = cells;
        }

        public DocumentLine Source { get; }
        public string Text { get; }
        public string[]? Cells { get; }
        public int? Page => Source.Page;
        public List<DateToken> Tokens { get; set; } = new();
        public List<TimeHit> Times { get; set; } = new();
        public bool Consumed { get; set; }
    }

    private sealed class DateItem
    {
        public int Index { get; set; }
        public int End { get; set; }
        public string Original { get; set; } = string.Empty;
        public int Day { get; set; }
        public int Month { get; set; }
        public int? Year { get; set; }
        public int? EndDay { get; set; }
        public int? EndMonth { get; set; }
        public int? EndYear { get; set; }
        public bool IsList { get; set; }
        public bool Ambiguous { get; set; }
        public int AltDay { get; set; }
        public int AltMonth { get; set; }
    }

    private sealed class FinalDate
    {
        public FinalDate(DateItem item, DateOnly start, DateOnly? end, DateOnly? alt, bool yearInferred)
        {
            Item = item;
            Start = start;
            End = end;
            Alt = alt;
            YearInferred = yearInferred;
        }

        public DateItem Item { get; }
        public DateOnly Start { get; }
        public DateOnly? End { get; }
        public DateOnly? Alt { get; }
        public bool YearInferred { get; }
        public string? Name { get; set; }
        public string? Place { get; set; }
        public TimeOnly? Time { get; set; }
        public NameSource Source { get; set; } = NameSource.None;
    }

    private sealed class TextFields
    {
        public string? Name { get; init; }
        public string? Place { get; init; }
        public bool NameFromLabel { get; init; }
    }

    // ------------------------------------------------------------------ API pública

    public DocumentAnalysis Analyze(
        IReadOnlyList<DocumentLine> lines,
        DateOrder preferredOrder = DateOrder.Auto,
        DateOnly? referenceDate = null)
    {
        var today = referenceDate ?? DateOnly.FromDateTime(DateTime.Today);
        var infos = BuildLineInfos(lines);

        var (order, noEvidence, mixed) = DetectOrder(infos, preferredOrder);
        var flagAmbiguous = preferredOrder == DateOrder.Auto && (noEvidence || mixed);
        var documentYear = DetectDocumentYear(infos, today.Year);

        var candidates = new List<DocumentEventCandidate>();
        var seen = new HashSet<string>();

        Dictionary<int, string>? headerRoles = null;
        var headerWidth = 0;

        for (var i = 0; i < infos.Count; i++)
        {
            var info = infos[i];

            if (info.Cells != null && info.Tokens.Count == 0)
            {
                var roles = DetectHeader(info.Cells);
                if (roles != null)
                {
                    headerRoles = roles;
                    headerWidth = info.Cells.Length;
                    info.Consumed = true;
                    continue;
                }
            }

            if (info.Cells == null) headerRoles = null;
            if (info.Tokens.Count == 0 || info.Consumed) continue;

            var finals = BuildFinalDates(info, order, flagAmbiguous, documentYear);
            if (finals.Count == 0) continue;

            var spans = CollectSpans(info);
            FillFields(infos, i, info, finals, spans, headerRoles, headerWidth);

            var noisy = NoiseRx.IsMatch(info.Text);
            foreach (var final in finals)
            {
                var name = final.Name ?? $"Evento del {final.Start:dd/MM/yyyy}";
                var score = final.Source switch
                {
                    NameSource.Table or NameSource.Label => 3,
                    NameSource.Line => 2,
                    NameSource.Heading or NameSource.Following => 1,
                    _ => 0
                };
                if (final.Place != null) score++;
                if (final.Time != null) score++;
                if (final.Item.Ambiguous) score--;
                if (final.YearInferred) score--;
                if (noisy && final.Source is NameSource.Line or NameSource.Heading or NameSource.Following or NameSource.None)
                    score -= 2;

                var key = string.Join("|", final.Start.ToString("O"), final.End?.ToString("O") ?? "-",
                    final.Time?.ToString("HH:mm") ?? "-", TextNormalizer.Fold(name));
                if (!seen.Add(key)) continue;

                candidates.Add(new DocumentEventCandidate(
                    final.Start,
                    final.End,
                    final.Time,
                    name,
                    final.Place,
                    Truncate(info.Text, MaxContextLength),
                    final.Item.Original,
                    info.Page,
                    final.Item.Ambiguous,
                    final.Alt,
                    final.YearInferred,
                    score >= 3 ? "alta" : score >= 1 ? "media" : "baja"));
            }
        }

        var ordered = candidates
            .OrderBy(c => c.Fecha)
            .ThenBy(c => c.Hora ?? TimeOnly.MaxValue)
            .ThenBy(c => c.Pagina ?? 0)
            .ToArray();

        return new DocumentAnalysis(
            ordered,
            order,
            mixed,
            ordered.Any(c => c.EsAmbigua));
    }

    // ------------------------------------------------------------------ preparación

    private static List<LineInfo> BuildLineInfos(IReadOnlyList<DocumentLine> lines)
    {
        var infos = new List<LineInfo>();
        foreach (var line in lines)
        {
            var text = Collapse(TextNormalizer.Normalize(line.Text));
            string[]? cells = null;

            if (line.Cells is { Count: >= 2 })
            {
                cells = line.Cells.Select(c => Collapse(TextNormalizer.Normalize(c))).ToArray();
                text = string.Join(" | ", cells.Where(c => c.Length > 0));
            }

            if (text.Length == 0) continue;

            var info = new LineInfo(line, text, cells);
            info.Tokens = DateScanner.Scan(text).ToList();
            info.Times = TimeScanner.Scan(text)
                .Where(t => !info.Tokens.Any(d => t.Index < d.End && d.Index < t.Index + t.Length))
                .ToList();
            infos.Add(info);
        }

        return infos;
    }

    private static (DateOrder Order, bool NoEvidence, bool Mixed) DetectOrder(
        List<LineInfo> infos,
        DateOrder preferred)
    {
        var dayFirst = 0;
        var monthFirst = 0;
        foreach (var token in infos.SelectMany(i => i.Tokens).Where(t => t.Kind == DateTokenKind.Numeric))
        {
            if (token.First > 12 && token.Second is >= 1 and <= 12) dayFirst++;
            else if (token.Second > 12 && token.First is >= 1 and <= 12) monthFirst++;
        }

        DateOrder order;
        var noEvidence = false;
        if (preferred != DateOrder.Auto) order = preferred;
        else if (dayFirst > monthFirst) order = DateOrder.DayFirst;
        else if (monthFirst > dayFirst) order = DateOrder.MonthFirst;
        else
        {
            order = DateOrder.DayFirst;
            noEvidence = true;
        }

        var mixed = (order == DateOrder.DayFirst && monthFirst > 0) || (order == DateOrder.MonthFirst && dayFirst > 0);
        return (order, noEvidence, mixed);
    }

    private static int DetectDocumentYear(List<LineInfo> infos, int fallback)
    {
        var years = infos.SelectMany(i => i.Tokens)
            .Where(t => t.Year != null)
            .Select(t => t.Year!.Value)
            .GroupBy(y => y)
            .OrderByDescending(g => g.Count())
            .ThenByDescending(g => g.Key)
            .Select(g => g.Key)
            .ToList();

        return years.Count > 0 ? years[0] : fallback;
    }

    // ------------------------------------------------------------------ fechas

    private static List<FinalDate> BuildFinalDates(LineInfo info, DateOrder order, bool flagAmbiguous, int documentYear)
    {
        var items = new List<DateItem>();
        foreach (var token in info.Tokens)
        {
            var item = Resolve(token, info.Text, order, flagAmbiguous);
            if (item == null) continue;

            if (item.IsList && item.EndDay != null)
            {
                var second = new DateItem
                {
                    Index = item.End,
                    End = item.End,
                    Original = item.Original,
                    Day = item.EndDay.Value,
                    Month = item.Month,
                    Year = item.Year
                };
                item.EndDay = null;
                item.EndMonth = null;
                item.EndYear = null;
                items.Add(item);
                items.Add(second);
            }
            else
            {
                items.Add(item);
            }
        }

        items = MergeRanges(items, info.Text);

        var finals = new List<FinalDate>();
        foreach (var item in items)
        {
            if (TryFinalize(item, documentYear, out var start, out var end, out var alt, out var inferred))
                finals.Add(new FinalDate(item, start, end, alt, inferred));
        }

        return finals;
    }

    private static DateItem? Resolve(DateToken token, string text, DateOrder order, bool flagAmbiguous)
    {
        var item = new DateItem
        {
            Index = token.Index,
            End = token.End,
            Original = token.Text.Trim(),
            Year = token.Year
        };

        if (token.Kind != DateTokenKind.Numeric)
        {
            item.Day = token.Day;
            item.Month = token.Month;
            if (token.RangeEndDay != null)
            {
                item.EndDay = token.RangeEndDay;
                item.EndMonth = token.Month;
                item.EndYear = token.Year;
                item.IsList = token.IsList;
            }

            return item;
        }

        var a = token.First;
        var b = token.Second;
        if (a < 1 || b < 1) return null;

        int day;
        int month;
        var ambiguous = false;

        if (a > 12 && b <= 12) { day = a; month = b; }
        else if (b > 12 && a <= 12) { day = b; month = a; }
        else if (a > 12 && b > 12) return null;
        else if (a == b) { day = a; month = b; }
        else
        {
            ambiguous = true;
            if (order == DateOrder.MonthFirst) { day = b; month = a; }
            else { day = a; month = b; }
        }

        if (ambiguous && token.Year is int year)
        {
            // Un día de la semana junto a la fecha ("lunes 03/04/2026") resuelve la duda con certeza.
            var weekday = WeekdayBefore(text, token.Index);
            if (weekday != null)
            {
                var primaryMatches = TryDate(year, month, day)?.DayOfWeek == weekday;
                var swappedMatches = TryDate(year, day, month)?.DayOfWeek == weekday;
                if (primaryMatches != swappedMatches)
                {
                    if (swappedMatches) (day, month) = (month, day);
                    ambiguous = false;
                }
            }
        }

        item.Day = day;
        item.Month = month;
        item.Ambiguous = ambiguous && flagAmbiguous;
        item.AltDay = month;
        item.AltMonth = day;
        return item;
    }

    private static DayOfWeek? WeekdayBefore(string text, int index)
    {
        var start = Math.Max(0, index - 24);
        var before = text.Substring(start, index - start);
        var match = WeekdayWordRx.Match(before);
        if (!match.Success) return null;

        return Weekdays.TryGetValue(TextNormalizer.Fold(match.Groups["w"].Value), out var day) ? day : null;
    }

    private static List<DateItem> MergeRanges(List<DateItem> items, string text)
    {
        var result = new List<DateItem>();
        for (var i = 0; i < items.Count; i++)
        {
            var current = items[i];
            if (current.EndDay == null && i + 1 < items.Count)
            {
                var next = items[i + 1];
                if (next.EndDay == null
                    && next.Index >= current.End
                    && next.End > next.Index
                    && RangeConnectorRx.IsMatch(text.Substring(current.End, next.Index - current.End)))
                {
                    current.EndDay = next.Day;
                    current.EndMonth = next.Month;
                    current.EndYear = next.Year;
                    current.Ambiguous |= next.Ambiguous;
                    current.End = next.End;
                    current.Original = text.Substring(current.Index, current.End - current.Index).Trim();
                    i++;
                }
            }

            result.Add(current);
        }

        return result;
    }

    private static bool TryFinalize(
        DateItem item,
        int documentYear,
        out DateOnly start,
        out DateOnly? end,
        out DateOnly? alt,
        out bool yearInferred)
    {
        start = default;
        end = null;
        alt = null;
        yearInferred = false;

        var startYear = item.Year;
        var endYear = item.EndYear;

        if (item.EndDay != null)
        {
            var endMonth = item.EndMonth ?? item.Month;
            var endDay = item.EndDay.Value;

            if (startYear == null && endYear != null)
            {
                startYear = endYear;
                if (Compare(item.Month, item.Day, endMonth, endDay) > 0) startYear = endYear - 1;
            }
            else if (startYear != null && endYear == null)
            {
                endYear = startYear;
                if (Compare(endMonth, endDay, item.Month, item.Day) < 0) endYear = startYear + 1;
            }
            else if (startYear == null)
            {
                startYear = documentYear;
                endYear = documentYear;
                yearInferred = true;
                if (Compare(endMonth, endDay, item.Month, item.Day) < 0) endYear = documentYear + 1;
            }
        }
        else if (startYear == null)
        {
            startYear = documentYear;
            yearInferred = true;
        }

        var startDate = TryDate(startYear!.Value, item.Month, item.Day);
        if (startDate == null) return false;
        start = startDate.Value;

        if (item.EndDay != null && endYear != null)
        {
            var endDate = TryDate(endYear.Value, item.EndMonth ?? item.Month, item.EndDay.Value);
            if (endDate != null && endDate.Value > start) end = endDate;
        }

        if (item.Ambiguous && end == null)
            alt = TryDate(startYear.Value, item.AltMonth, item.AltDay);

        return true;
    }

    private static int Compare(int month1, int day1, int month2, int day2) =>
        (month1 * 100 + day1).CompareTo(month2 * 100 + day2);

    private static DateOnly? TryDate(int year, int month, int day)
    {
        if (year < 1900 || year > 2100 || month < 1 || month > 12 || day < 1) return null;
        if (day > DateTime.DaysInMonth(year, month)) return null;
        return new DateOnly(year, month, day);
    }

    // ------------------------------------------------------------------ nombre, lugar y hora

    private static List<Span> CollectSpans(LineInfo info) =>
        info.Tokens.Select(t => new Span(t.Index, t.End))
            .Concat(info.Times.Select(t => new Span(t.Index, t.Index + t.Length)))
            .OrderBy(s => s.Index)
            .ToList();

    private static void FillFields(
        List<LineInfo> infos,
        int lineIndex,
        LineInfo info,
        List<FinalDate> finals,
        List<Span> spans,
        Dictionary<int, string>? headerRoles,
        int headerWidth)
    {
        var tableFound = false;

        if (info.Cells != null)
        {
            var roles = headerRoles != null && headerWidth == info.Cells.Length ? headerRoles : null;
            var row = ExtractFromCells(info.Cells, roles);
            if (row.Name != null)
            {
                tableFound = true;
                foreach (var final in finals)
                {
                    final.Name = row.Name;
                    final.Place = row.Place;
                    final.Time = row.Time;
                    final.Source = NameSource.Table;
                }
            }
        }

        if (!tableFound)
        {
            if (finals.Count == 1)
            {
                var masked = Mask(info.Text, 0, info.Text.Length, spans);
                var parsed = ParseText(masked);
                var only = finals[0];
                only.Name = parsed.Name;
                only.Place = parsed.Place;
                only.Source = parsed.Name == null ? NameSource.None : parsed.NameFromLabel ? NameSource.Label : NameSource.Line;
            }
            else
            {
                for (var j = 0; j < finals.Count; j++)
                {
                    if (j > 0 && finals[j].Item.Index == finals[j].Item.End)
                    {
                        // "5 y 6 de octubre": la segunda fecha comparte nombre y lugar con la primera.
                        finals[j].Name = finals[j - 1].Name;
                        finals[j].Place = finals[j - 1].Place;
                        finals[j].Source = finals[j - 1].Source;
                        continue;
                    }

                    var previousEnd = j == 0 ? 0 : Math.Max(0, finals[j - 1].Item.End);
                    var nextStart = j + 1 < finals.Count ? Math.Max(finals[j].Item.End, finals[j + 1].Item.Index) : info.Text.Length;
                    var item = finals[j].Item;

                    var before = Mask(info.Text, Math.Min(previousEnd, item.Index), item.Index, spans);
                    var after = Mask(info.Text, item.End, Math.Max(item.End, nextStart), spans);
                    var cut = after.IndexOfAny(new[] { ';', '|' });
                    if (cut >= 0) after = after[..cut];

                    var trimmedBefore = before.TrimEnd();
                    var segment = trimmedBefore.EndsWith(':') && HasWords(trimmedBefore)
                        ? before
                        : HasWords(after) ? after : before;

                    var parsed = ParseText(segment);
                    finals[j].Name = parsed.Name;
                    finals[j].Place = parsed.Place;
                    finals[j].Source = parsed.Name == null ? NameSource.None : parsed.NameFromLabel ? NameSource.Label : NameSource.Line;
                }
            }

            foreach (var final in finals)
                final.Time = NearestTime(info.Times, final.Item.Index);

            if (finals.Count == 1 && info.Cells == null)
                ApplyNeighborLines(infos, lineIndex, info, finals[0]);
        }
    }

    private static TimeOnly? NearestTime(List<TimeHit> times, int index)
    {
        if (times.Count == 0) return null;
        return times.OrderBy(t => Math.Abs(t.Index - index)).First().Time;
    }

    /// <summary>Completa nombre, lugar y hora con las líneas vecinas (título arriba, "Lugar:" abajo).</summary>
    private static void ApplyNeighborLines(List<LineInfo> infos, int index, LineInfo info, FinalDate final)
    {
        if (final.Name == null && index > 0)
        {
            var previous = infos[index - 1];
            if (!previous.Consumed
                && previous.Tokens.Count == 0
                && previous.Cells == null
                && previous.Page == info.Page
                && IsHeadingCandidate(previous.Text))
            {
                var heading = CleanText(previous.Text);
                if (heading != null)
                {
                    final.Name = heading;
                    final.Source = NameSource.Heading;
                    previous.Consumed = true;
                }
            }
        }

        for (var k = index + 1; k < infos.Count && k <= index + LookAheadLines; k++)
        {
            var next = infos[k];
            if (next.Tokens.Count > 0 || next.Cells != null || next.Page != info.Page) break;

            var masked = Mask(next.Text, 0, next.Text.Length, CollectSpans(next));
            var parsed = ParseText(masked);
            var took = false;

            if (final.Place == null)
            {
                var start = VenueStartRx.Match(masked);
                if (parsed.Place != null)
                {
                    final.Place = parsed.Place;
                    took = true;
                }
                else if (start.Success)
                {
                    final.Place = CleanPlace(start.Groups["v"].Value);
                    took = final.Place != null;
                }
            }

            if (final.Time == null && next.Times.Count > 0)
            {
                final.Time = next.Times[0].Time;
                took = true;
            }

            if (!took && final.Name == null && parsed.Name != null && parsed.Name.Length <= 100)
            {
                final.Name = parsed.Name;
                final.Source = parsed.NameFromLabel ? NameSource.Label : NameSource.Following;
                took = true;
            }

            if (took) next.Consumed = true;
        }
    }

    private static bool IsHeadingCandidate(string text)
    {
        if (text.Length is < 3 or > 100) return false;
        if (text.EndsWith(':')) return false;
        if (LabelRx.IsMatch(text)) return false;
        if (VenueStartRx.IsMatch(text)) return false;
        return true;
    }

    private static TextFields ParseText(string masked)
    {
        string? labeledName = null;
        string? labeledPlace = null;
        var leftover = masked;

        var matches = LabelRx.Matches(masked);
        if (matches.Count > 0)
        {
            var sb = new StringBuilder(masked[..matches[0].Index]);
            for (var k = 0; k < matches.Count; k++)
            {
                var match = matches[k];
                var valueStart = match.Index + match.Length;
                var valueEnd = k + 1 < matches.Count ? matches[k + 1].Index : masked.Length;
                var value = masked[valueStart..valueEnd];
                var pipe = value.IndexOf('|');
                if (pipe >= 0) value = value[..pipe];

                var label = Regex.Replace(TextNormalizer.Fold(match.Groups["label"].Value), @"\s+", " ");
                if (NameLabels.Contains(label)) labeledName ??= CleanText(value);
                else if (PlaceLabels.Contains(label)) labeledPlace ??= CleanPlace(value);
                else if (DateLabels.Contains(label)) sb.Append(' ').Append(value);
            }

            leftover = sb.ToString();
        }

        var place = labeledPlace ?? ExtractPlaceFromText(ref leftover);
        var name = labeledName ?? CleanText(leftover);

        return new TextFields { Name = name, Place = place, NameFromLabel = labeledName != null };
    }

    private static string? ExtractPlaceFromText(ref string text)
    {
        foreach (var regex in new[] { VenueRx, SeparatedVenueRx, ProperPlaceRx })
        {
            var match = regex.Match(text);
            if (!match.Success) continue;

            var place = CleanPlace(match.Groups["v"].Value);
            if (place == null) continue;

            text = text.Remove(match.Index, match.Length);
            return place;
        }

        return null;
    }

    // ------------------------------------------------------------------ tablas

    private static Dictionary<int, string>? DetectHeader(string[] cells)
    {
        var roles = new Dictionary<int, string>();
        for (var i = 0; i < cells.Length; i++)
        {
            var key = TextNormalizer.Fold(cells[i]).TrimEnd(':', ' ');
            if (HeaderWords.TryGetValue(key, out var role)) roles[i] = role;
        }

        var nonEmpty = cells.Count(c => c.Length > 0);
        return roles.Count >= 2 && roles.Count >= nonEmpty - 1 ? roles : null;
    }

    private static (string? Name, string? Place, TimeOnly? Time) ExtractFromCells(
        string[] cells,
        Dictionary<int, string>? roles)
    {
        string? name = null;
        string? place = null;
        TimeOnly? time = null;
        var rest = new List<string>();

        for (var i = 0; i < cells.Length; i++)
        {
            var cell = cells[i];
            if (cell.Length == 0) continue;

            string? role = null;
            if (roles != null) roles.TryGetValue(i, out role);

            var cellDates = DateScanner.Scan(cell);
            var cellTimes = TimeScanner.Scan(cell)
                .Where(t => !cellDates.Any(d => t.Index < d.End && d.Index < t.Index + t.Length))
                .ToList();
            var cellSpans = cellDates.Select(d => new Span(d.Index, d.End))
                .Concat(cellTimes.Select(t => new Span(t.Index, t.Index + t.Length)))
                .OrderBy(s => s.Index)
                .ToList();
            var masked = Mask(cell, 0, cell.Length, cellSpans);

            switch (role)
            {
                case "date":
                    break;
                case "time":
                    if (cellTimes.Count > 0) time ??= cellTimes[0].Time;
                    break;
                case "name":
                    name ??= CleanText(masked);
                    break;
                case "place":
                    place ??= CleanPlace(masked);
                    break;
                default:
                    if (roles != null) break;
                    if (cellDates.Count > 0) break;
                    if (cellTimes.Count > 0 && CleanText(masked) == null)
                    {
                        time ??= cellTimes[0].Time;
                        break;
                    }

                    rest.Add(masked);
                    break;
            }
        }

        if (roles == null)
        {
            foreach (var cell in rest)
            {
                if (place == null && VenueStartRx.IsMatch(cell)) place = CleanPlace(cell);
                else if (name == null) name = CleanText(cell);
            }

            if (place == null && name != null)
            {
                foreach (var cell in rest)
                {
                    var candidate = CleanText(cell);
                    if (candidate != null && candidate != name)
                    {
                        place = CleanPlace(cell);
                        break;
                    }
                }
            }
        }

        return (name, place, time);
    }

    // ------------------------------------------------------------------ limpieza de texto

    private static string Mask(string text, int start, int end, IReadOnlyList<Span> spans)
    {
        var sb = new StringBuilder();
        var position = start;
        foreach (var span in spans)
        {
            if (span.End <= position || span.Index >= end) continue;

            var from = Math.Max(span.Index, position);
            if (from > position) sb.Append(text, position, from - position);
            sb.Append(Marker);
            position = Math.Min(span.End, end);
        }

        if (position < end) sb.Append(text, position, end - position);
        return sb.ToString();
    }

    private static string? CleanText(string value)
    {
        var s = WeekdayBeforeMarkerRx.Replace(value, string.Empty);
        s = ConnectorsBeforeMarkerRx.Replace(s, " ");
        s = s.Replace(Marker, ' ');
        s = EmptyBracketsRx.Replace(s, " ");
        s = LeadingBulletRx.Replace(s.TrimStart(), string.Empty);
        s = TrimEdges(Collapse(s));

        if (!IsMeaningful(s)) return null;

        s = char.ToUpperInvariant(s[0]) + s[1..];
        return Truncate(s, MaxNameLength);
    }

    private static string? CleanPlace(string value)
    {
        var cut = value.IndexOf(Marker);
        if (cut >= 0) value = value[..cut];
        var semicolon = value.IndexOf(';');
        if (semicolon >= 0) value = value[..semicolon];

        var s = TrimEdges(Collapse(value));
        s = Regex.Replace(s, @"^(?:en|at)\s+(?:(?:el|la|los|las|the)\s+)?", string.Empty, RegexOptions.IgnoreCase);
        s = TrimEdges(s);

        if (s.Length < 2 || !s.Any(char.IsLetterOrDigit)) return null;
        return Truncate(s, MaxPlaceLength);
    }

    private static string TrimEdges(string s)
    {
        for (var i = 0; i < 3; i++)
        {
            var before = s;
            s = EdgePunctuationRx.Replace(s, string.Empty).Trim();
            s = TrailingConnectorsRx.Replace(s, string.Empty).Trim();
            s = LeadingConnectorsRx.Replace(s, string.Empty).Trim();
            if (s == before) break;
        }

        return s;
    }

    private static bool IsMeaningful(string s)
    {
        if (s.Count(char.IsLetter) < 3) return false;
        return !StopWords.Contains(TextNormalizer.Fold(s));
    }

    private static bool HasWords(string s) => s.Replace(Marker, ' ').Count(char.IsLetter) >= 3;

    private static string Collapse(string s) => WhitespaceRx.Replace(s, " ").Trim();

    private static string Truncate(string s, int max)
    {
        if (s.Length <= max) return s;
        var cut = s.LastIndexOf(' ', max - 1);
        var length = cut > max / 2 ? cut : max - 1;
        return s[..length].TrimEnd(' ', ',', ';', ':', '-') + "…";
    }
}
