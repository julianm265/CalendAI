using CalendarioBackend.Core.Models;

namespace CalendarioBackend.Core.Repositories;

/// <summary>
/// Persistencia de equipos, membresías y eventos. Todas las consultas son puntuales (por equipo,
/// usuario o rango de fechas): nunca se carga el calendario completo en memoria.
/// </summary>
public interface IEquipoRepository
{
    // ---- Equipos
    /// <summary>Crea el equipo con su calendario y registra a <paramref name="liderId"/> como líder.</summary>
    Equipo Agregar(Equipo equipo, Guid liderId);
    Equipo? ObtenerPorId(Guid id);
    Equipo? ObtenerPersonal(Guid colaboradorId);
    IReadOnlyList<EquipoDelUsuario> ObtenerDelColaborador(Guid colaboradorId);
    bool ExisteNombreLiderado(Guid liderId, string nombre);
    void Renombrar(Guid equipoId, string nombre);
    bool Eliminar(Guid id);

    // ---- Membresías
    RolEquipo? ObtenerRol(Guid equipoId, Guid colaboradorId);
    IReadOnlyList<MiembroEquipo> ObtenerMiembros(Guid equipoId);
    void AgregarMiembro(Guid equipoId, Guid colaboradorId, RolEquipo rol);
    bool EliminarMiembro(Guid equipoId, Guid colaboradorId);
    void TransferirLiderazgo(Guid equipoId, Guid actualLiderId, Guid nuevoLiderId);

    // ---- Eventos
    IReadOnlyList<Evento> ObtenerEventos(Guid equipoId, DateOnly fecha);
    IReadOnlyList<(DateOnly Fecha, Evento Evento)> ObtenerEventos(Guid equipoId, DateOnly desde, DateOnly hasta);
    (DateOnly Fecha, Evento Evento)? ObtenerEvento(Guid equipoId, Guid eventoId);

    /// <exception cref="InvalidOperationException">Ya hay un evento a esa hora ese día.</exception>
    void AgregarEvento(Guid equipoId, DateOnly fecha, Evento evento);
    bool EliminarEvento(Guid equipoId, Guid eventoId);

    /// <summary>Lugares más usados en los eventos de un calendario.</summary>
    IReadOnlyList<LugarFrecuente> ObtenerLugaresFrecuentes(Guid equipoId, int limite);

    /// <summary>
    /// Lugares más usados en los eventos que un usuario organizó en un equipo concreto y en su calendario
    /// personal (nunca en otros equipos), para sugerir sitios de reunión que le resulten cómodos.
    /// </summary>
    IReadOnlyList<LugarFrecuente> ObtenerLugaresDelColaborador(Guid colaboradorId, Guid equipoId, int limite);
}

public sealed record LugarFrecuente(string Lugar, int VecesUsado);
