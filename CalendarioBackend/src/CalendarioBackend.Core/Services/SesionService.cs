using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace CalendarioBackend.Core.Services;

/// <summary>
/// Emite y valida los tokens de sesión que autentican cada pedido a la API.
/// Los tokens viven en memoria del proceso, así que un reinicio de la API obliga
/// a volver a iniciar sesión (no se guardan credenciales ni tokens en la base).
/// </summary>
public sealed class SesionService
{
    private static readonly TimeSpan Duracion = TimeSpan.FromHours(12);

    private readonly ConcurrentDictionary<string, Sesion> _sesiones = new(StringComparer.Ordinal);

    public string Crear(Guid colaboradorId)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        _sesiones[token] = new Sesion(colaboradorId, DateTimeOffset.UtcNow.Add(Duracion));
        return token;
    }

    /// <summary>Devuelve el colaborador dueño del token, o null si no existe o venció.</summary>
    public Guid? ObtenerColaboradorId(string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || !_sesiones.TryGetValue(token, out var sesion))
            return null;

        if (sesion.Expira > DateTimeOffset.UtcNow)
            return sesion.ColaboradorId;

        _sesiones.TryRemove(token, out _);
        return null;
    }

    public void Cerrar(string? token)
    {
        if (!string.IsNullOrWhiteSpace(token))
            _sesiones.TryRemove(token, out _);
    }

    private sealed record Sesion(Guid ColaboradorId, DateTimeOffset Expira);
}
