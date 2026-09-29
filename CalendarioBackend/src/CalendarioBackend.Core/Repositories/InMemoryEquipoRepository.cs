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

    public void Guardar(Equipo equipo) => _equipos[equipo.Id] = equipo;

    public void AgregarColaborador(Guid equipoId, Colaborador colaborador)
    {
        var equipo = ObtenerEquipoOFallar(equipoId);
        if (equipo.BuscarColaborador(colaborador.Id) is null)
            equipo.AgregarColaborador(colaborador);
    }

    public void EliminarColaborador(Guid equipoId, Guid colaboradorId) =>
        ObtenerEquipoOFallar(equipoId).EliminarColaborador(colaboradorId);

    public void AgregarEvento(Guid equipoId, DateOnly fecha, Evento evento)
    {
        var equipo = ObtenerEquipoOFallar(equipoId);
        var dia = equipo.Calendario.ObtenerOCrearAño(fecha.Year).BuscarDia(fecha)
            ?? throw new InvalidOperationException($"No fue posible resolver el día {fecha:dd/MM/yyyy}.");
        if (dia.LEventos.All(item => item.Id != evento.Id))
            dia.AgregarEventoPersistido(evento);
    }

    public void EliminarEvento(Guid equipoId, Guid eventoId)
    {
        var equipo = ObtenerEquipoOFallar(equipoId);
        foreach (var año in equipo.Calendario.LAños)
            foreach (var mes in año.LMeses)
                foreach (var semana in mes.LSemana)
                    foreach (var dia in semana.LDias)
                        if (dia.EliminarEvento(eventoId))
                            return;
    }

    public Equipo? ObtenerPorId(Guid id) =>
        _equipos.TryGetValue(id, out var equipo) ? equipo : null;

    public Equipo? ObtenerPorNombre(string nombre) =>
        _equipos.Values.FirstOrDefault(e => e.NombreEquipo.Equals(nombre, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<Equipo> ObtenerTodos() => _equipos.Values.ToList().AsReadOnly();

    public bool Eliminar(Guid id) => _equipos.TryRemove(id, out _);

    private Equipo ObtenerEquipoOFallar(Guid equipoId) =>
        ObtenerPorId(equipoId) ?? throw new KeyNotFoundException($"No se encontró el equipo con id '{equipoId}'.");

    public Equipo CrearCalendarioPersonal(Colaborador colaborador)
    {
        var equipo = new Equipo($"Calendario de {colaborador.Usuario}", true);
        equipo.AgregarColaborador(colaborador);
        Agregar(equipo);
        return equipo;
    }
}
