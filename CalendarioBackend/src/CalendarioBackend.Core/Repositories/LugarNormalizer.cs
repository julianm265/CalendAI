using System.Globalization;
using System.Text;

namespace CalendarioBackend.Core.Repositories;

public static class LugarNormalizer
{
    /// <summary>Clave para agrupar lugares escritos distinto: sin tildes, minúsculas y espacios colapsados.</summary>
    public static string Clave(string lugar)
    {
        var descompuesto = lugar.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var caracter in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark)
                builder.Append(caracter);
        }

        return string.Join(' ', builder.ToString().Normalize(NormalizationForm.FormC)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    public static IReadOnlyList<LugarFrecuente> Agrupar(IEnumerable<string> lugares, int limite) =>
        lugares
            .Select(lugar => new { Original = lugar.Trim(), Clave = Clave(lugar) })
            .Where(lugar => lugar.Clave.Length > 0)
            .GroupBy(lugar => lugar.Clave)
            .OrderByDescending(grupo => grupo.Count())
            .ThenBy(grupo => grupo.Min(item => item.Original), StringComparer.OrdinalIgnoreCase)
            .Take(limite)
            .Select(grupo => new LugarFrecuente(
                grupo.OrderBy(item => item.Original.Length).First().Original,
                grupo.Count()))
            .ToList();
}
