using CalendarioBackend.Core.Models;
using CalendarioBackend.Core.Repositories;
using CalendarioBackend.Core.Services;

namespace CalendarioBackend.Api.Seguridad;

/// <summary>
/// Resuelve el colaborador autenticado a partir del encabezado
/// <c>Authorization: Bearer &lt;token&gt;</c> del pedido en curso.
/// </summary>
public sealed class SesionActual
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly SesionService _sesionService;
    private readonly IColaboradorRepository _colaboradorRepository;

    public SesionActual(
        IHttpContextAccessor httpContextAccessor,
        SesionService sesionService,
        IColaboradorRepository colaboradorRepository)
    {
        _httpContextAccessor = httpContextAccessor;
        _sesionService = sesionService;
        _colaboradorRepository = colaboradorRepository;
    }

    public string? Token
    {
        get
        {
            var encabezado = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
            return string.IsNullOrWhiteSpace(encabezado) || !encabezado.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? null
                : encabezado["Bearer ".Length..].Trim();
        }
    }

    public Guid? ColaboradorId => _sesionService.ObtenerColaboradorId(Token);

    public Colaborador? Colaborador =>
        ColaboradorId is Guid id ? _colaboradorRepository.ObtenerPorId(id) : null;
}
