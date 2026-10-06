using System.Net.Http.Json;
using System.Text.Json;

namespace CalendarioBackend.Api.Services;

public interface IGooglePlacesService
{
    Task<IReadOnlyList<LugarGoogle>> BuscarAsync(string consulta, CancellationToken cancellationToken);
}

public sealed record LugarGoogle(
    string PlaceId,
    string Nombre,
    string Direccion,
    double? Latitud,
    double? Longitud,
    IReadOnlyList<string> Tipos);

public sealed class GooglePlacesService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration) : IGooglePlacesService
{
    public async Task<IReadOnlyList<LugarGoogle>> BuscarAsync(
        string consulta,
        CancellationToken cancellationToken)
    {
        var apiKey = configuration["GooglePlaces:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("La búsqueda de lugares no está configurada.");

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://places.googleapis.com/v1/places:searchText")
        {
            Content = JsonContent.Create(new { textQuery = consulta, languageCode = "es" }),
        };
        request.Headers.Add("X-Goog-Api-Key", apiKey);
        request.Headers.Add("X-Goog-FieldMask", "places.id,places.displayName,places.formattedAddress,places.location,places.types");

        using var response = await httpClientFactory.CreateClient("GooglePlaces").SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var detalle = await ExtraerDetalleError(response, cancellationToken);
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(detalle)
                    ? $"Google Places respondió {(int)response.StatusCode}."
                    : $"Google Places no pudo completar la búsqueda: {detalle}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;
        if (!root.TryGetProperty("places", out var results) || results.ValueKind != JsonValueKind.Array)
            return Array.Empty<LugarGoogle>();

        return results.EnumerateArray().Select(MapearLugar).ToList();
    }

    private static LugarGoogle MapearLugar(JsonElement resultado)
    {
        var location = resultado.TryGetProperty("location", out var locationValue)
            ? locationValue
            : default;
        var displayName = resultado.TryGetProperty("displayName", out var displayNameValue)
            && displayNameValue.TryGetProperty("text", out var displayNameText)
            ? displayNameText.GetString() ?? ""
            : "";

        return new LugarGoogle(
            ObtenerTexto(resultado, "id"),
            displayName,
            ObtenerTexto(resultado, "formattedAddress"),
            ObtenerNumero(location, "latitude"),
            ObtenerNumero(location, "longitude"),
            resultado.TryGetProperty("types", out var types) && types.ValueKind == JsonValueKind.Array
                ? types.EnumerateArray().Select(item => item.GetString() ?? "").Where(item => item.Length > 0).ToList()
                : Array.Empty<string>());
    }

    private static async Task<string?> ExtraerDetalleError(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var documento = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            return documento.RootElement.TryGetProperty("error", out var error)
                && error.TryGetProperty("message", out var message)
                ? message.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string ObtenerTexto(JsonElement elemento, string propiedad) =>
        elemento.TryGetProperty(propiedad, out var valor) ? valor.GetString() ?? "" : "";

    private static double? ObtenerNumero(JsonElement elemento, string propiedad) =>
        elemento.ValueKind != JsonValueKind.Undefined
            && elemento.TryGetProperty(propiedad, out var valor)
            && valor.TryGetDouble(out var numero)
                ? numero
                : null;
}
