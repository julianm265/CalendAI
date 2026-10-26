using CalendarioBackend.Core.Models;
using CalendarioBackend.Core.Repositories;
using CalendarioBackend.Core.Services;

namespace CalendarioBackend.Core.Asistente;

/// <summary>Un horario candidato para reunirse y cuántos integrantes están libres.</summary>
public sealed record HorarioPropuesto(
    DateOnly Fecha, TimeOnly Hora, int Libres, int Total, IReadOnlyList<string> Ocupados);

/// <summary>Un lugar candidato y qué integrantes lo suelen usar.</summary>
public sealed record LugarPuntuado(string Lugar, int Usos, IReadOnlyList<string> Integrantes);

public sealed record EventoProximo(DateOnly Fecha, TimeOnly Hora, string Nombre, string? Lugar);

/// <summary>Datos objetivos (calculados, no generados por IA) sobre los que razona el asistente.</summary>
public sealed record ContextoReunion(
    Guid EquipoId,
    string Equipo,
    bool EsPersonal,
    IReadOnlyList<string> Integrantes,
    DateOnly Desde,
    DateOnly Hasta,
    IReadOnlyList<HorarioPropuesto> Horarios,
    IReadOnlyList<LugarPuntuado> Lugares,
    IReadOnlyList<EventoProximo> ProximosEventos);

/// <summary>
/// Calcula, sin IA, los mejores horarios y lugares para reunir a un equipo:
///  - Horarios: huecos de una hora en días laborables donde el calendario del equipo está libre, ordenados por
///    cuántos integrantes están libres según su calendario personal (solo se usan las horas, nunca los títulos).
///  - Lugares: los que cada integrante usa con más frecuencia (en este equipo y en su calendario personal),
///    priorizando los compartidos por más integrantes.
/// La IA solo redacta y elige entre estas opciones: no puede inventar fechas ni lugares.
/// </summary>
public sealed class PlanificadorReuniones(IEquipoRepository equipos, AccesoEquipoService acceso)
{
    public const int DiasMaximos = 30;
    private const int HoraInicio = 8;
    private const int HoraFin = 17; // última hora de inicio: 17:00 (termina a las 18:00)
    private const int MaxHorarios = 10;
    private const int MaxPorDia = 3;
    private const int MaxLugares = 8;
    private const int MaxProximos = 15;

    public ContextoReunion Construir(Guid equipoId, Guid solicitanteId, DateOnly hoy, int dias = 14)
    {
        acceso.ExigirMiembro(equipoId, solicitanteId);
        var equipo = equipos.ObtenerPorId(equipoId) ?? throw new KeyNotFoundException("No se encontró el equipo.");
        var miembros = equipos.ObtenerMiembros(equipoId);

        dias = Math.Clamp(dias, 1, DiasMaximos);
        var desde = hoy.AddDays(1);
        var hasta = hoy.AddDays(dias);

        var eventosEquipo = equipos.ObtenerEventos(equipoId, desde, hasta);
        var horasEquipo = eventosEquipo.Select(e => (e.Fecha, Hora: e.Evento.HoraEvento)).ToList();

        // Calendario personal de cada integrante: solo para saber cuándo está ocupado.
        var ocupacion = new Dictionary<string, IReadOnlyList<(DateOnly Fecha, TimeOnly Hora)>>();
        foreach (var miembro in miembros)
        {
            if (equipo.EsPersonal)
            {
                ocupacion[miembro.Usuario] = horasEquipo;
                continue;
            }

            var personal = equipos.ObtenerPersonal(miembro.ColaboradorId);
            var horas = new List<(DateOnly Fecha, TimeOnly Hora)>();
            if (personal is not null)
                foreach (var (fecha, evento) in equipos.ObtenerEventos(personal.Id, desde, hasta))
                    horas.Add((fecha, evento.HoraEvento));
            ocupacion[miembro.Usuario] = horas;
        }

        return new ContextoReunion(
            equipo.Id,
            equipo.NombreEquipo,
            equipo.EsPersonal,
            miembros.Select(m => m.Usuario).ToList(),
            desde,
            hasta,
            CalcularHorarios(desde, hasta, horasEquipo, ocupacion),
            CalcularLugares(equipoId, miembros),
            eventosEquipo.Take(MaxProximos)
                .Select(e => new EventoProximo(e.Fecha, e.Evento.HoraEvento, e.Evento.NombreEvento, e.Evento.LugarEvento))
                .ToList());
    }

    /// <summary>Visible para pruebas: puntúa los huecos libres del equipo según la disponibilidad de cada integrante.</summary>
    internal static IReadOnlyList<HorarioPropuesto> CalcularHorarios(
        DateOnly desde,
        DateOnly hasta,
        IReadOnlyList<(DateOnly Fecha, TimeOnly Hora)> eventosEquipo,
        IReadOnlyDictionary<string, IReadOnlyList<(DateOnly Fecha, TimeOnly Hora)>> ocupacion)
    {
        var candidatos = new List<HorarioPropuesto>();
        for (var fecha = desde; fecha <= hasta; fecha = fecha.AddDays(1))
        {
            if (fecha.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;

            for (var hora = HoraInicio; hora <= HoraFin; hora++)
            {
                var inicio = new TimeOnly(hora, 0);
                if (eventosEquipo.Any(e => e.Fecha == fecha && Solapa(e.Hora, inicio))) continue;

                var ocupados = ocupacion
                    .Where(par => par.Value.Any(e => e.Fecha == fecha && Solapa(e.Hora, inicio)))
                    .Select(par => par.Key)
                    .OrderBy(nombre => nombre, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                candidatos.Add(new HorarioPropuesto(fecha, inicio, ocupacion.Count - ocupados.Count, ocupacion.Count, ocupados));
            }
        }

        var elegidos = new List<HorarioPropuesto>();
        foreach (var candidato in candidatos
                     .OrderByDescending(c => c.Libres)
                     .ThenBy(c => c.Fecha)
                     .ThenBy(c => Math.Abs(c.Hora.Hour - 10))) // a igualdad, mejor media mañana
        {
            if (elegidos.Count(e => e.Fecha == candidato.Fecha) >= MaxPorDia) continue;
            elegidos.Add(candidato);
            if (elegidos.Count == MaxHorarios) break;
        }

        return elegidos.OrderBy(c => c.Fecha).ThenBy(c => c.Hora).ToList();
    }

    /// <summary>Se asume que un evento dura una hora: dos horas de inicio a menos de 60 minutos se pisan.</summary>
    private static bool Solapa(TimeOnly evento, TimeOnly inicio) =>
        Math.Abs((evento.ToTimeSpan() - inicio.ToTimeSpan()).TotalMinutes) < 60;

    private IReadOnlyList<LugarPuntuado> CalcularLugares(Guid equipoId, IReadOnlyList<MiembroEquipo> miembros)
    {
        var acumulado = new Dictionary<string, (string Lugar, int Usos, HashSet<string> Integrantes)>();

        void Sumar(string lugar, int usos, string? integrante)
        {
            var clave = LugarNormalizer.Clave(lugar);
            if (clave.Length == 0) return;

            acumulado.TryGetValue(clave, out var actual);
            var nombre = actual.Lugar is null || lugar.Length < actual.Lugar.Length ? lugar : actual.Lugar;
            var integrantes = actual.Integrantes ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (integrante is not null) integrantes.Add(integrante);
            acumulado[clave] = (nombre, actual.Usos + usos, integrantes);
        }

        foreach (var miembro in miembros)
            foreach (var lugar in equipos.ObtenerLugaresDelColaborador(miembro.ColaboradorId, equipoId, 5))
                Sumar(lugar.Lugar, lugar.VecesUsado, miembro.Usuario);

        foreach (var lugar in equipos.ObtenerLugaresFrecuentes(equipoId, 8))
            Sumar(lugar.Lugar, lugar.VecesUsado, null);

        return acumulado.Values
            .OrderByDescending(item => item.Integrantes.Count)
            .ThenByDescending(item => item.Usos)
            .ThenBy(item => item.Lugar, StringComparer.OrdinalIgnoreCase)
            .Take(MaxLugares)
            .Select(item => new LugarPuntuado(item.Lugar, item.Usos, item.Integrantes.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList()))
            .ToList();
    }
}
