using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CalendarioBackend.Core.Documents;

internal enum DateTokenKind
{
    /// <summary>05/10/2026, 5-10-26, 05.10.2026 (el orden día/mes se resuelve después).</summary>
    Numeric,
    /// <summary>2026-10-05, 2026/10/05 (año primero: no es ambigua).</summary>
    Iso,
    /// <summary>5 de octubre de 2026, Oct 5th 2026, del 5 al 7 de octubre...</summary>
    Textual
}

/// <summary>Fecha localizada dentro de un texto, todavía sin validar contra el calendario real.</summary>
internal sealed class DateToken
{
    public int Index { get; init; }
    public int Length { get; init; }
    public string Text { get; init; } = string.Empty;
    public DateTokenKind Kind { get; init; }

    /// <summary>Solo Numeric: primer y segundo número tal cual aparecen.</summary>
    public int First { get; init; }
    public int Second { get; init; }

    /// <summary>Iso/Textual: día y mes ya conocidos.</summary>
    public int Day { get; init; }
    public int Month { get; init; }

    /// <summary>Año escrito en el texto; null si el texto no lo trae ("5 de octubre").</summary>
    public int? Year { get; init; }

    /// <summary>"del 5 al 7 de octubre": día final del rango.</summary>
    public int? RangeEndDay { get; init; }

    /// <summary>"5 y 6 de octubre": son dos fechas sueltas, no un rango.</summary>
    public bool IsList { get; init; }

    public int End => Index + Length;
}

internal static class TextNormalizer
{
    /// <summary>Unifica guiones, espacios especiales y caracteres invisibles que rompen las fechas.</summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            switch (c)
            {
                case '\u00AD':
                case '\u200B':
                case '\u200C':
                case '\u200D':
                case '\uFEFF':
                    break;
                case '\u2010':
                case '\u2011':
                case '\u2012':
                case '\u2212':
                    sb.Append('-');
                    break;
                case '\u00A0':
                case '\u2002':
                case '\u2003':
                case '\u2007':
                case '\u2009':
                case '\u202F':
                    sb.Append(' ');
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }

        return sb.ToString();
    }

    /// <summary>Minúsculas y sin tildes: "Miércoles" -> "miercoles".</summary>
    public static string Fold(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        return sb.ToString().ToLowerInvariant();
    }
}

/// <summary>
/// Localiza fechas escritas en cualquier formato razonable (numérico, ISO, con nombre de mes en
/// español o inglés, abreviado o completo, con rangos). No decide el orden día/mes: eso lo hace
/// el analizador mirando el documento completo.
/// </summary>
internal static class DateScanner
{
    internal const string MonthNames =
        "enero|febrero|marzo|abril|mayo|junio|julio|agosto|septiembre|setiembre|octubre|noviembre|diciembre|" +
        "january|february|march|april|june|july|august|september|october|november|december|" +
        "sept|ene|feb|mar|abr|may|jun|jul|ago|sep|oct|nov|dic|jan|apr|aug|dec";

    private const string DayBody = @"0?[1-9]|[12]\d|3[01]";
    private const string Year4Body = @"(?:19|20)\d{2}";
    private const RegexOptions Opts = RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;

    private static readonly Dictionary<string, int> Months = new(StringComparer.OrdinalIgnoreCase)
    {
        ["enero"] = 1, ["ene"] = 1, ["january"] = 1, ["jan"] = 1,
        ["febrero"] = 2, ["feb"] = 2, ["february"] = 2,
        ["marzo"] = 3, ["mar"] = 3, ["march"] = 3,
        ["abril"] = 4, ["abr"] = 4, ["april"] = 4, ["apr"] = 4,
        ["mayo"] = 5, ["may"] = 5,
        ["junio"] = 6, ["jun"] = 6, ["june"] = 6,
        ["julio"] = 7, ["jul"] = 7, ["july"] = 7,
        ["agosto"] = 8, ["ago"] = 8, ["august"] = 8, ["aug"] = 8,
        ["septiembre"] = 9, ["setiembre"] = 9, ["sept"] = 9, ["sep"] = 9, ["september"] = 9,
        ["octubre"] = 10, ["oct"] = 10, ["october"] = 10,
        ["noviembre"] = 11, ["nov"] = 11, ["november"] = 11,
        ["diciembre"] = 12, ["dic"] = 12, ["december"] = 12, ["dec"] = 12
    };

    // 2026-10-05 · 2026/10/05 · 2026.10.05
    private static readonly Regex IsoRx = new(
        @"(?<![\d/.\-])(?<y>" + Year4Body + @")(?<s>[-/.])(?<m>0?[1-9]|1[0-2])\k<s>(?<d>" + DayBody + @")(?!\d)",
        Opts);

    // 05/10/2026 · 5-10-26 · 05.10.2026
    private static readonly Regex NumericRx = new(
        @"(?<![\d/.\-])(?<a>\d{1,2})(?<s>[/\-.])(?<b>\d{1,2})\k<s>(?<y>\d{4}|\d{2})(?!\d)(?![/\-]\d)",
        Opts);

    // 5 de octubre de 2026 · 5 oct 2026 · 5-oct-26 · 1ro de mayo · 5th October 2026
    private static readonly Regex DayMonthRx = new(
        @"(?<![\d/])(?<d>" + DayBody + @")(?:\s?(?:st|nd|rd|th|º|°|ro|er|do|to|vo)(?![\p{L}]))?" +
        @"(?:\s+de\s+|\s*[-/.]\s*|\s+)(?<m>" + MonthNames + @")(?![\p{L}])\.?" +
        @"(?:(?:\s+del?\s+|\s*,\s*|\s+|\s*[-/]\s*)(?<y>" + Year4Body + @")(?!\d)|[-/](?<yy>\d{2})(?!\d))?",
        Opts);

    // octubre 5, 2026 · Oct 5th 2026
    private static readonly Regex MonthDayRx = new(
        @"(?<![\p{L}\d])(?<m>" + MonthNames + @")(?![\p{L}])\.?\s+(?<d>" + DayBody + @")(?:st|nd|rd|th)?(?!\d)" +
        @"(?:\s*,?\s*(?<y>" + Year4Body + @")(?!\d))?",
        Opts);

    // del 5 al 7 de octubre de 2026 · 5-7 de octubre · 5 y 6 de octubre
    private static readonly Regex SharedMonthRangeRx = new(
        @"(?<![\d/])(?:(?:del?|from)\s+)?(?<d1>" + DayBody + @")\s*(?:(?<sep>al?|-|–|—|hasta(?:\s+el)?|to)|(?<list>y|&))\s*" +
        @"(?<d2>" + DayBody + @")\s+(?:de\s+)?(?<m>" + MonthNames + @")(?![\p{L}])\.?" +
        @"(?:(?:\s+del?\s+|\s*,\s*|\s+)(?<y>" + Year4Body + @")(?!\d))?",
        Opts);

    public static IReadOnlyList<DateToken> Scan(string text)
    {
        var found = new List<DateToken>();

        foreach (Match m in IsoRx.Matches(text))
        {
            found.Add(new DateToken
            {
                Index = m.Index,
                Length = m.Length,
                Text = m.Value,
                Kind = DateTokenKind.Iso,
                Day = int.Parse(m.Groups["d"].Value, CultureInfo.InvariantCulture),
                Month = int.Parse(m.Groups["m"].Value, CultureInfo.InvariantCulture),
                Year = int.Parse(m.Groups["y"].Value, CultureInfo.InvariantCulture)
            });
        }

        foreach (Match m in NumericRx.Matches(text))
        {
            var yearText = m.Groups["y"].Value;
            var aText = m.Groups["a"].Value;
            var bText = m.Groups["b"].Value;

            // "1.2.26" suele ser una versión o un número de sección, no una fecha.
            if (m.Groups["s"].Value == "." && yearText.Length == 2 && (aText.Length < 2 || bText.Length < 2))
                continue;

            var year = ExpandYear(int.Parse(yearText, CultureInfo.InvariantCulture), yearText.Length);
            if (year is < 1900 or > 2100)
                continue;

            found.Add(new DateToken
            {
                Index = m.Index,
                Length = m.Length,
                Text = m.Value,
                Kind = DateTokenKind.Numeric,
                First = int.Parse(aText, CultureInfo.InvariantCulture),
                Second = int.Parse(bText, CultureInfo.InvariantCulture),
                Year = year
            });
        }

        foreach (Match m in DayMonthRx.Matches(text))
        {
            found.Add(new DateToken
            {
                Index = m.Index,
                Length = m.Length,
                Text = m.Value,
                Kind = DateTokenKind.Textual,
                Day = int.Parse(m.Groups["d"].Value, CultureInfo.InvariantCulture),
                Month = Months[m.Groups["m"].Value],
                Year = ParseOptionalYear(m)
            });
        }

        foreach (Match m in MonthDayRx.Matches(text))
        {
            found.Add(new DateToken
            {
                Index = m.Index,
                Length = m.Length,
                Text = m.Value,
                Kind = DateTokenKind.Textual,
                Day = int.Parse(m.Groups["d"].Value, CultureInfo.InvariantCulture),
                Month = Months[m.Groups["m"].Value],
                Year = ParseOptionalYear(m)
            });
        }

        foreach (Match m in SharedMonthRangeRx.Matches(text))
        {
            found.Add(new DateToken
            {
                Index = m.Index,
                Length = m.Length,
                Text = m.Value,
                Kind = DateTokenKind.Textual,
                Day = int.Parse(m.Groups["d1"].Value, CultureInfo.InvariantCulture),
                RangeEndDay = int.Parse(m.Groups["d2"].Value, CultureInfo.InvariantCulture),
                IsList = m.Groups["list"].Success,
                Month = Months[m.Groups["m"].Value],
                Year = ParseOptionalYear(m)
            });
        }

        // Si dos patrones se pisan gana el que empieza antes y, a igual inicio, el más largo.
        found.Sort((x, y) => x.Index != y.Index ? x.Index.CompareTo(y.Index) : y.Length.CompareTo(x.Length));

        var result = new List<DateToken>();
        var lastEnd = -1;
        foreach (var token in found)
        {
            if (token.Index < lastEnd) continue;
            result.Add(token);
            lastEnd = token.End;
        }

        return result;
    }

    private static int? ParseOptionalYear(Match m)
    {
        if (m.Groups["y"].Success)
            return int.Parse(m.Groups["y"].Value, CultureInfo.InvariantCulture);

        if (m.Groups["yy"].Success)
            return ExpandYear(int.Parse(m.Groups["yy"].Value, CultureInfo.InvariantCulture), 2);

        return null;
    }

    /// <summary>Años de dos cifras: 00-69 → 2000-2069, 70-99 → 1970-1999.</summary>
    private static int ExpandYear(int year, int digits) =>
        digits == 2 ? (year < 70 ? 2000 + year : 1900 + year) : year;
}

internal sealed record TimeHit(int Index, int Length, TimeOnly Time);

/// <summary>Localiza horas: 10:30, 10:30 am, 3 p.m., 15h, 8 de la noche.</summary>
internal static class TimeScanner
{
    private const RegexOptions Opts = RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;

    private static readonly Regex TimeRx = new(
        @"(?:(?<=\d)T|(?<![\d:]))(?:" +
        @"(?<h1>[01]?\d|2[0-3])[:h](?<m1>[0-5]\d)(?!\d)(?::[0-5]\d)?(?:\s*(?<ap1>[ap]\.?\s?m(?![\p{L}])\.?))?" +
        @"|(?<h2>1[0-2]|0?[1-9])\s*(?<ap2>[ap]\.?\s?m(?![\p{L}])\.?)" +
        @"|(?<h3>[01]?\d|2[0-3])\s*(?:hrs?\.?|h)(?![\p{L}])" +
        @"|(?<h4>1[0-2]|0?[1-9])(?::(?<m4>[0-5]\d))?\s+de\s+la\s+(?<dp>mañana|tarde|noche|madrugada)(?![\p{L}])" +
        @")",
        Opts);

    public static IReadOnlyList<TimeHit> Scan(string text)
    {
        var hits = new List<TimeHit>();
        foreach (Match m in TimeRx.Matches(text))
        {
            if (TryConvert(m, out var time))
                hits.Add(new TimeHit(m.Index, m.Length, time));
        }

        return hits;
    }

    private static bool TryConvert(Match m, out TimeOnly time)
    {
        time = default;
        int hour;
        var minute = 0;

        if (m.Groups["h1"].Success)
        {
            hour = int.Parse(m.Groups["h1"].Value, CultureInfo.InvariantCulture);
            minute = int.Parse(m.Groups["m1"].Value, CultureInfo.InvariantCulture);
            if (m.Groups["ap1"].Success && hour <= 12)
                hour = To24(hour, char.ToLowerInvariant(m.Groups["ap1"].Value[0]) == 'p');
        }
        else if (m.Groups["h2"].Success)
        {
            hour = To24(
                int.Parse(m.Groups["h2"].Value, CultureInfo.InvariantCulture),
                char.ToLowerInvariant(m.Groups["ap2"].Value[0]) == 'p');
        }
        else if (m.Groups["h3"].Success)
        {
            hour = int.Parse(m.Groups["h3"].Value, CultureInfo.InvariantCulture);
        }
        else
        {
            hour = int.Parse(m.Groups["h4"].Value, CultureInfo.InvariantCulture);
            if (m.Groups["m4"].Success)
                minute = int.Parse(m.Groups["m4"].Value, CultureInfo.InvariantCulture);

            var dayPart = m.Groups["dp"].Value.ToLowerInvariant();
            if (dayPart is "tarde" or "noche")
                hour = hour == 12 && dayPart == "noche" ? 0 : (hour < 12 ? hour + 12 : hour);
            else if (dayPart == "madrugada" && hour == 12)
                hour = 0;
        }

        if (hour is < 0 or > 23) return false;
        time = new TimeOnly(hour, minute);
        return true;
    }

    private static int To24(int hour, bool isPm)
    {
        if (isPm) return hour == 12 ? 12 : hour + 12;
        return hour == 12 ? 0 : hour;
    }
}
