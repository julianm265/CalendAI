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
    void AgregarColaborador(Guid equipoId, Colaborador colaborador);
    void EliminarColaborador(Guid equipoId, Guid colaboradorId);
    void AgregarEvento(Guid equipoId, DateOnly fecha, Evento evento);
    void EliminarEvento(Guid equipoId, Guid eventoId);
    IReadOnlyList<LugarFrecuente> ObtenerLugaresFrecuentes(Guid equipoId, int limite);
    Equipo? ObtenerPorId(Guid id);
    Equipo? ObtenerPorNombre(string nombre);
    IReadOnlyList<Equipo> ObtenerTodos();
    bool Eliminar(Guid id);
    Equipo CrearCalendarioPersonal(Colaborador colaborador);
    IReadOnlyList<Equipo> ObtenerPorColaborador(Guid colaboradorId);
    void AgregarMiembro(Guid equipoId, Colaborador colaborador);
    bool EsMiembro(Guid equipoId, Guid colaboradorId);
    void CrearInvitacion(Guid equipoId, Colaborador colaborador);
    IReadOnlyList<(Guid Id, Equipo Equipo)> ObtenerInvitaciones(Guid colaboradorId);
    void AceptarInvitacion(Guid invitacionId, Guid colaboradorId);
}

public sealed record LugarFrecuente(string Lugar, int VecesUsado);
