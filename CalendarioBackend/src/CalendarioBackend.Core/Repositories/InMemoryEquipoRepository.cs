using System.Collections.Concurrent;
using CalendarioBackend.Core.Models;

namespace CalendarioBackend.Core.Repositories;

/// <summary>
/// Implementación en memoria de IEquipoRepository, útil para desarrollo, pruebas y demos.
/// Thread-safe gracias a ConcurrentDictionary.
/// </summary>
public class InMemoryEquipoRepository : IEquipoRepository
{
    private readonly ConcurrentDictionary<Guid, Equipo> _equipos = new();

    public Equipo Agregar(Equipo equipo)
    {
        if (!_equipos.TryAdd(equipo.Id, equipo))
            throw new InvalidOperationException($"Ya existe un equipo con id '{equipo.Id}'.");

        return equipo;
    }

    public void Guardar(Equipo equipo)
    {
        _equipos[equipo.Id] = equipo;
    }

    public Equipo? ObtenerPorId(Guid id) =>
        _equipos.TryGetValue(id, out var equipo) ? equipo : null;

    public Equipo? ObtenerPorNombre(string nombre) =>
        _equipos.Values.FirstOrDefault(e => e.NombreEquipo.Equals(nombre, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<Equipo> ObtenerTodos() => _equipos.Values.ToList().AsReadOnly();

    public bool Eliminar(Guid id) => _equipos.TryRemove(id, out _);
}
