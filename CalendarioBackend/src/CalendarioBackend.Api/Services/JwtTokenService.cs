using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CalendarioBackend.Api.Services;

public sealed class JwtTokenService(IConfiguration configuration)
{
    public string Crear(string usuario)
    {
        var header = Codificar("""{"alg":"HS256","typ":"JWT"}""");
        var payload = Codificar(JsonSerializer.Serialize(new { sub = usuario, name = usuario, exp = DateTimeOffset.UtcNow.AddHours(8).ToUnixTimeSeconds() }));
        return $"{header}.{payload}.{Firmar($"{header}.{payload}")}";
    }

    public string? Validar(string? authorization)
    {
        if (string.IsNullOrWhiteSpace(authorization) || !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;
        var partes = authorization["Bearer ".Length..].Split('.');
        if (partes.Length != 3) return null;
        try
        {
            var esperado = Decodificar(Firmar($"{partes[0]}.{partes[1]}"));
            var recibido = Decodificar(partes[2]);
            if (!CryptographicOperations.FixedTimeEquals(esperado, recibido)) return null;
            using var json = JsonDocument.Parse(Encoding.UTF8.GetString(Decodificar(partes[1])));
            var payload = json.RootElement;
            return payload.GetProperty("exp").GetInt64() > DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                ? payload.GetProperty("sub").GetString()
                : null;
        }
        catch (Exception exception) when (exception is FormatException or JsonException or KeyNotFoundException)
        {
            return null;
        }
    }

    private string Firmar(string texto)
    {
        var key = configuration["JWT_KEY"];
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("La firma JWT no está configurada.");
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(texto)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string Codificar(string texto) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(texto)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Decodificar(string texto)
    {
        var base64 = texto.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
        return Convert.FromBase64String(base64);
    }
}
