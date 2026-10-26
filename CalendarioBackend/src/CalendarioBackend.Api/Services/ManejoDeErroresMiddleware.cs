using System.Net.Mime;
using CalendarioBackend.Core.Asistente;

namespace CalendarioBackend.Api.Services;

/// <summary>
/// Traduce las excepciones de negocio a respuestas HTTP coherentes, en un solo lugar:
/// KeyNotFound → 404, Argument → 400, UnauthorizedAccess → 403, InvalidOperation → 409,
/// asistente de IA no configurado → 503 y fallo del servicio de IA → 502.
/// Cualquier otra excepción es un error del servidor: se registra y se responde 500 sin detalles internos.
/// </summary>
public sealed class ManejoDeErroresMiddleware(RequestDelegate siguiente, ILogger<ManejoDeErroresMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext contexto)
    {
        try
        {
            await siguiente(contexto);
        }
        catch (OperationCanceledException) when (contexto.RequestAborted.IsCancellationRequested)
        {
            // El cliente cerró la conexión: no es un error.
        }
        catch (Exception excepcion)
        {
            if (contexto.Response.HasStarted) throw;

            var (estado, mensaje) = excepcion switch
            {
                KeyNotFoundException => (StatusCodes.Status404NotFound, excepcion.Message),
                ArgumentException => (StatusCodes.Status400BadRequest, excepcion.Message),
                UnauthorizedAccessException => (StatusCodes.Status403Forbidden, excepcion.Message),
                InvalidOperationException => (StatusCodes.Status409Conflict, excepcion.Message),
                AsistenteException { NoConfigurado: true } => (StatusCodes.Status503ServiceUnavailable, excepcion.Message),
                AsistenteException => (StatusCodes.Status502BadGateway, excepcion.Message),
                _ => (StatusCodes.Status500InternalServerError, "Ocurrió un error inesperado en el servidor.")
            };

            if (estado >= StatusCodes.Status500InternalServerError)
                logger.LogError(excepcion, "Error en {Metodo} {Ruta}", contexto.Request.Method, contexto.Request.Path);

            contexto.Response.Clear();
            contexto.Response.StatusCode = estado;
            contexto.Response.ContentType = MediaTypeNames.Application.Json;
            await contexto.Response.WriteAsJsonAsync(new { error = mensaje });
        }
    }
}
