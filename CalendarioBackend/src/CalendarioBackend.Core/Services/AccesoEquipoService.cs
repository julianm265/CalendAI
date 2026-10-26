using CalendarioBackend.Core.Models;
using CalendarioBackend.Core.Repositories;

namespace CalendarioBackend.Core.Services;

/// <summary>
/// Reglas de acceso a un equipo. Quien no pertenece al equipo recibe "no encontrado" (404) en vez de
/// "prohibido", para no revelar qué equipos existen.
/// </summary>
public sealed class AccesoEquipoService(IEquipoRepository equipos)
{
    public RolEquipo? ObtenerRol(Guid equipoId, Guid colaboradorId) =>
        equipos.ObtenerRol(equipoId, colaboradorId);

    public RolEquipo ExigirMiembro(Guid equipoId, Guid colaboradorId) =>
        equipos.ObtenerRol(equipoId, colaboradorId)
        ?? throw new KeyNotFoundException("No se encontró el equipo.");

    public void ExigirLider(Guid equipoId, Guid colaboradorId)
    {
        if (ExigirMiembro(equipoId, colaboradorId) != RolEquipo.Lider)
            throw new UnauthorizedAccessException("Solo el líder del equipo puede realizar esta acción.");
    }
}
