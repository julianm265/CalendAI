using CalendarioBackend.Core.Models;
using CalendarioBackend.Core.Repositories;

namespace CalendarioBackend.Core.Services;

/// <summary>
/// Punto de entrada principal para operar sobre el calendario de un equipo:
/// agregar/eliminar/consultar eventos por día o por mes. Encapsula la navegación
/// por el árbol Calendario -> Año -> Mes -> Semana -> Dia -> Evento.
/// </summary>
public class CalendarioService
{
    private readonly IEquipoRepository _equipoRepository;

    public CalendarioService(IEquipoRepository equipoRepository)
    {
        _equipoRepository = equipoRepository;
    }

    public Evento AgregarEvento(Guid equipoId, DateOnly fecha, string nombreEvento, TimeOnly hora,
        string lugar, string? descripcion = null, Guid? colaboradorOrganizadorId = null)
    {
        var equipo = ObtenerEquipoOFallar(equipoId);

        if (colaboradorOrganizadorId is Guid organizadorId &&
            equipo.BuscarColaborador(organizadorId) is null)
        {
            throw new InvalidOperationException(
                "El organizador indicado no pertenece a este equipo.");
        }

        var año = equipo.Calendario.ObtenerOCrearAño(fecha.Year);
        var dia = año.BuscarDia(fecha)
            ?? throw new InvalidOperationException($"No fue posible resolver el día {fecha:dd/MM/yyyy}.");

        var evento = dia.AgregarEvento(nombreEvento, hora, lugar, descripcion, colaboradorOrganizadorId);
        _equipoRepository.AgregarEvento(equipo.Id, fecha, evento);
        return evento;
    }

    public bool EliminarEvento(Guid equipoId, DateOnly fecha, Guid eventoId)
    {
        var equipo = ObtenerEquipoOFallar(equipoId);
        var dia = equipo.Calendario.BuscarDia(fecha);
        var eliminado = dia?.EliminarEvento(eventoId) ?? false;
        if (eliminado)
            _equipoRepository.EliminarEvento(equipo.Id, eventoId);
        return eliminado;
    }

    public IEnumerable<Evento> ObtenerEventosDelDia(Guid equipoId, DateOnly fecha)
    {
        var equipo = ObtenerEquipoOFallar(equipoId);
        var dia = equipo.Calendario.BuscarDia(fecha);
        return dia?.ObtenerEventosOrdenados() ?? Enumerable.Empty<Evento>();
    }

    public IEnumerable<(DateOnly Fecha, Evento Evento)> ObtenerEventosDelMes(Guid equipoId, int año, byte mes)
    {
        var equipo = ObtenerEquipoOFallar(equipoId);
        var añoObj = equipo.Calendario.ObtenerOCrearAño(año);
        var mesObj = añoObj.ObtenerMes(mes);

        foreach (var semana in mesObj.LSemana)
            foreach (var dia in semana.LDias)
                foreach (var evento in dia.ObtenerEventosOrdenados())
                    yield return (dia.Fecha, evento);
    }

    public Anio ObtenerAño(Guid equipoId, int numAño)
    {
        var equipo = ObtenerEquipoOFallar(equipoId);
        return equipo.Calendario.ObtenerOCrearAño(numAño);
    }

    private Equipo ObtenerEquipoOFallar(Guid equipoId) =>
        _equipoRepository.ObtenerPorId(equipoId)
        ?? throw new KeyNotFoundException($"No se encontró el equipo con id '{equipoId}'.");
}
