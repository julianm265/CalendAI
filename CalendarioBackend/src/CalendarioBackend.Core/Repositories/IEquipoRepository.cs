using CalendarioBackend.Core.Models;

namespace CalendarioBackend.Core.Repositories;

/// <summary>
/// Abstracción de persistencia para Equipo (y transitivamente su Calendario y Colaboradores).
/// Permite sustituir la implementación en memoria por EF Core, Dapper, Mongo, etc. sin tocar
/// la capa de servicios.
/// </summary>
public interface IEquipoRepository
{
    Equipo Agregar(Equipo equipo);
    void Guardar(Equipo equipo);
    Equipo? ObtenerPorId(Guid id);
    Equipo? ObtenerPorNombre(string nombre);
    IReadOnlyList<Equipo> ObtenerTodos();
    bool Eliminar(Guid id);
    Equipo CrearCalendarioPersonal(Colaborador colaborador);
}
