using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using CalendarioBackend.Core.Models;

namespace CalendarioBackend.Core.Repositories;

/// <summary>
/// Implementación en memoria de IEquipoRepository, útil para desarrollo, pruebas y demos.
/// Thread-safe gracias a ConcurrentDictionary.
/// </summary>
public class InMemoryEquipoRepository : IEquipoRepository, IColaboradorRepository
{
    private readonly ConcurrentDictionary<Guid, Equipo> _equipos = new();
    private readonly ConcurrentDictionary<Guid, Colaborador> _colaboradores = new();

    public Equipo Agregar(Equipo equipo)
    {
        if (!_equipos.TryAdd(equipo.Id, equipo))
            throw new InvalidOperationException($"Ya existe un equipo con id '{equipo.Id}'.");

        return equipo;
    }

    public Equipo AgregarEquipoConColaborador(Equipo equipo, Colaborador colaborador, string rol)
    {
        if (!_colaboradores.ContainsKey(colaborador.Id))
            throw new KeyNotFoundException("No se encontró el usuario registrado.");
        equipo.AgregarColaborador(colaborador, rol);
        return Agregar(equipo);
    }

    public void Guardar(Equipo equipo) => _equipos[equipo.Id] = equipo;

    public void AgregarColaborador(Guid equipoId, Colaborador colaborador, string rol = "Miembro")
    {
        var equipo = ObtenerEquipoOFallar(equipoId);
        _colaboradores.TryAdd(colaborador.Id, colaborador);
        if (equipo.BuscarColaborador(colaborador.Id) is null)
            equipo.AgregarColaborador(colaborador, rol);
    }

    public Colaborador? ObtenerPorUsuario(string usuario) =>
        _colaboradores.Values.FirstOrDefault(colaborador =>
            colaborador.Usuario.Equals(usuario, StringComparison.OrdinalIgnoreCase));

    public bool ExistePorUsuario(string usuario) => ObtenerPorUsuario(usuario) is not null;

    public void Agregar(Colaborador colaborador, Guid? equipoId)
    {
        if (ExistePorUsuario(colaborador.Usuario))
            throw new InvalidOperationException($"El usuario '{colaborador.Usuario}' ya existe.");
        var equipo = equipoId is Guid id ? ObtenerEquipoOFallar(id) : null;
        if (!_colaboradores.TryAdd(colaborador.Id, colaborador))
            throw new InvalidOperationException($"Ya existe el colaborador con id '{colaborador.Id}'.");
        equipo?.AgregarColaborador(colaborador, "Miembro");
    }

    public void AsignarAEquipo(Guid colaboradorId, Guid equipoId)
    {
        if (!_colaboradores.TryGetValue(colaboradorId, out var colaborador))
            throw new KeyNotFoundException("No se encontró el colaborador.");
        AgregarColaborador(equipoId, colaborador, "Miembro");
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

    public IReadOnlyList<LugarFrecuente> ObtenerLugaresFrecuentes(Guid equipoId, int limite)
    {
        var lugares = EnumerarEventos(ObtenerEquipoOFallar(equipoId))
            .Select(item => item.LugarEvento)
            .Where(lugar => !string.IsNullOrWhiteSpace(lugar))
            .Select(lugar => new { Original = lugar!.Trim(), Clave = NormalizarLugar(lugar) })
            .Where(lugar => lugar.Clave.Length > 0)
            .GroupBy(lugar => lugar.Clave)
            .OrderByDescending(grupo => grupo.Count())
            .ThenBy(grupo => grupo.Min(item => item.Original), StringComparer.OrdinalIgnoreCase)
            .Take(limite)
            .Select(grupo => new LugarFrecuente(
                grupo.OrderBy(item => item.Original.Length).First().Original,
                grupo.Count()))
            .ToList();

        return lugares;
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
        equipo.AgregarColaborador(colaborador, "Miembro");
        _colaboradores.TryAdd(colaborador.Id, colaborador);
        Agregar(equipo);
        return equipo;
    }

    private static IEnumerable<Evento> EnumerarEventos(Equipo equipo) =>
        from año in equipo.Calendario.LAños
        from mes in año.LMeses
        from semana in mes.LSemana
        from dia in semana.LDias
        from evento in dia.LEventos
        select evento;

    private static string NormalizarLugar(string lugar)
    {
        var descompuesto = lugar.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var caracter in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark)
                builder.Append(caracter);
        }

        return string.Join(' ', builder.ToString().Normalize(NormalizationForm.FormC)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
