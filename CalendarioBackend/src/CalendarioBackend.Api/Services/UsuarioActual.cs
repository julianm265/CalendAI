using System.Security.Claims;

namespace CalendarioBackend.Api.Services;

public static class UsuarioActual
{
    /// <summary>Id del colaborador autenticado (claim "sub" del token).</summary>
    public static Guid ObtenerColaboradorId(this ClaimsPrincipal usuario)
    {
        var valor = usuario.FindFirst("sub")?.Value ?? usuario.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(valor, out var id)
            ? id
            : throw new UnauthorizedAccessException("La sesión no es válida. Inicia sesión de nuevo.");
    }
}
