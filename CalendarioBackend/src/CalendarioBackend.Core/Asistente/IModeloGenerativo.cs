namespace CalendarioBackend.Core.Asistente;

/// <summary>Un turno de la conversación enviada al modelo. Rol: "user" o "model".</summary>
public sealed record MensajeModelo(string Rol, string Texto);

/// <summary>
/// Petición al modelo generativo: instrucciones de sistema, conversación y el esquema JSON que debe
/// cumplir la respuesta (formato JSON Schema de la API de Gemini, serializado como texto).
/// </summary>
public sealed record SolicitudModelo(
    string Instrucciones,
    IReadOnlyList<MensajeModelo> Mensajes,
    string EsquemaJson,
    double Temperatura = 0.2);

/// <summary>Abstracción del modelo de IA (hoy Gemini). Permite probar el agente sin red.</summary>
public interface IModeloGenerativo
{
    /// <summary>False si falta la clave de API: el resto de la aplicación funciona igual, sin IA.</summary>
    bool EstaConfigurado { get; }

    /// <summary>Devuelve el texto JSON generado por el modelo.</summary>
    /// <exception cref="AsistenteException">El modelo no está configurado o falló la llamada.</exception>
    Task<string> GenerarJsonAsync(SolicitudModelo solicitud, CancellationToken cancellationToken);
}

/// <summary>Fallo del asistente de IA (no configurado, límite de uso, servicio caído, respuesta inválida).</summary>
public sealed class AsistenteException(string mensaje, bool noConfigurado = false, Exception? interna = null)
    : Exception(mensaje, interna)
{
    /// <summary>True si el servidor no tiene clave de API: se responde 503 en vez de 502.</summary>
    public bool NoConfigurado { get; } = noConfigurado;
}
