using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using CalendarioBackend.Core.Asistente;

namespace CalendarioBackend.Api.Services;

public sealed class GeminiOptions
{
    /// <summary>Clave de la API de Gemini (Google AI Studio). Solo vive en el servidor.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Modelo a usar. Configurable porque Google retira y agrega modelos con frecuencia.</summary>
    public string Model { get; set; } = "gemini-3.5-flash-lite";
}

/// <summary>
/// Cliente mínimo de la API REST de Gemini (generateContent) con salida estructurada en JSON.
/// La clave viaja en la cabecera x-goog-api-key, nunca en la URL, y no se registra en los logs.
/// </summary>
public sealed class GeminiClient(
    IHttpClientFactory httpClientFactory,
    GeminiOptions opciones,
    ILogger<GeminiClient> logger) : IModeloGenerativo
{
    public const string NombreCliente = "Gemini";
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models/";

    public bool EstaConfigurado => !string.IsNullOrWhiteSpace(opciones.ApiKey);

    public async Task<string> GenerarJsonAsync(SolicitudModelo solicitud, CancellationToken cancellationToken)
    {
        if (!EstaConfigurado)
            throw new AsistenteException("El asistente de IA no está configurado en el servidor.", noConfigurado: true);

        var modelo = NormalizarModelo(opciones.Model);
        var cuerpo = new JsonObject
        {
            ["systemInstruction"] = new JsonObject
            {
                ["parts"] = new JsonArray(new JsonObject { ["text"] = solicitud.Instrucciones })
            },
            ["contents"] = new JsonArray(solicitud.Mensajes
                .Select(m => (JsonNode)new JsonObject
                {
                    ["role"] = m.Rol,
                    ["parts"] = new JsonArray(new JsonObject { ["text"] = m.Texto })
                }).ToArray()),
            ["generationConfig"] = new JsonObject
            {
                ["responseMimeType"] = "application/json",
                ["responseSchema"] = JsonNode.Parse(solicitud.EsquemaJson),
                ["temperature"] = solicitud.Temperatura
            }
        };

        using var peticion = new HttpRequestMessage(
            HttpMethod.Post, $"{BaseUrl}{Uri.EscapeDataString(modelo)}:generateContent")
        {
            Content = JsonContent.Create(cuerpo)
        };
        peticion.Headers.Add("x-goog-api-key", opciones.ApiKey.Trim());

        HttpResponseMessage respuesta;
        try
        {
            respuesta = await httpClientFactory.CreateClient(NombreCliente).SendAsync(peticion, cancellationToken);
        }
        catch (HttpRequestException excepcion)
        {
            throw new AsistenteException("No se pudo conectar con el servicio de IA. Inténtalo de nuevo.", interna: excepcion);
        }
        catch (TaskCanceledException excepcion) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AsistenteException("El servicio de IA tardó demasiado en responder. Inténtalo de nuevo.", interna: excepcion);
        }

        using (respuesta)
        {
            var texto = await respuesta.Content.ReadAsStringAsync(cancellationToken);
            if (!respuesta.IsSuccessStatusCode)
            {
                // El cuerpo del error de Google no incluye datos del usuario, pero se registra solo el código.
                logger.LogWarning("Gemini respondió {Codigo} para el modelo {Modelo}", (int)respuesta.StatusCode, modelo);
                throw new AsistenteException(MensajeDeError(respuesta.StatusCode));
            }

            return ExtraerTexto(texto);
        }
    }

    private static string MensajeDeError(HttpStatusCode codigo) => codigo switch
    {
        HttpStatusCode.TooManyRequests => "Se alcanzó el límite de uso del servicio de IA. Espera un momento e inténtalo de nuevo.",
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "El servicio de IA rechazó la clave configurada en el servidor.",
        HttpStatusCode.BadRequest => "El servicio de IA no aceptó la solicitud (revisa la clave y el modelo configurados).",
        HttpStatusCode.NotFound => "No se encontró el modelo de IA. Verifica GEMINI_MODEL y que GEMINI_API_KEY sea una clave válida de Google AI Studio.",
        _ => "El servicio de IA no está disponible en este momento."
    };

    private static string NormalizarModelo(string modelo)
    {
        var resultado = modelo.Trim();
        return resultado.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
            ? resultado["models/".Length..]
            : resultado;
    }

    private static string ExtraerTexto(string cuerpo)
    {
        JsonNode? raiz;
        try
        {
            raiz = JsonNode.Parse(cuerpo);
        }
        catch (JsonException excepcion)
        {
            throw new AsistenteException("El servicio de IA devolvió una respuesta no válida.", interna: excepcion);
        }

        string? texto;
        try
        {
            if (raiz?["promptFeedback"]?["blockReason"] is not null)
                throw new AsistenteException("El servicio de IA bloqueó la solicitud. Reformula tu mensaje.");

            var partes = raiz?["candidates"]?[0]?["content"]?["parts"] as JsonArray;
            texto = partes is null
                ? null
                : string.Concat(partes.Select(parte => parte?["text"] is JsonValue v && v.TryGetValue<string>(out var t) ? t : string.Empty));
        }
        catch (InvalidOperationException excepcion)
        {
            throw new AsistenteException("El servicio de IA devolvió una respuesta con un formato inesperado.", interna: excepcion);
        }

        return string.IsNullOrWhiteSpace(texto)
            ? throw new AsistenteException("El servicio de IA no devolvió contenido. Inténtalo de nuevo.")
            : texto;
    }
}
