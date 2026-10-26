using System.Text.Json.Nodes;

namespace CalendarioBackend.Core.Asistente;

internal static class JsonSeguro
{
    /// <summary>Lee un texto de un nodo JSON sin lanzar excepción si el modelo devolvió otro tipo.</summary>
    public static string? Texto(JsonNode? nodo) =>
        nodo is JsonValue valor && valor.TryGetValue<string>(out var texto) ? texto : null;

    public static int? Entero(JsonNode? nodo) =>
        nodo is JsonValue valor && valor.TryGetValue<int>(out var numero) ? numero : null;
}
