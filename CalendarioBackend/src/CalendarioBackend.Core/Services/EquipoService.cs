using CalendarioBackend.Core.Models;
using CalendarioBackend.Core.Repositories;

namespace CalendarioBackend.Core.Services;

/// <summary>
/// Orquesta la creación y administración de equipos y sus colaboradores.
/// Cada equipo creado obtiene automáticamente su propio Calendario (ver Equipo).
/// </summary>
public class EquipoService
{
    private readonly IEquipoRepository _equipoRepository;
    private readonly IColaboradorRepository? _colaboradorRepository;

    public EquipoService(IEquipoRepository equipoRepository, IColaboradorRepository? colaboradorRepository = null)
    {
        _equipoRepository = equipoRepository;
        _colaboradorRepository = colaboradorRepository;
    }

    /// <summary>
    /// Crea un equipo. Si se indica un creador, queda como su primer miembro: solo los
    /// miembros de un equipo pueden ver sus eventos o sumar colaboradores.
    /// </summary>
    public Equipo CrearEquipo(string nombreEquipo, Colaborador? creador = null)
    {
        if (_equipoRepository.ObtenerPorNombre(nombreEquipo) is not null)
            throw new InvalidOperationException($"Ya existe un equipo llamado '{nombreEquipo}'.");

        var equipo = _equipoRepository.Agregar(new Equipo(nombreEquipo));
        if (creador is null)
            return equipo;

        equipo.AgregarColaborador(creador);
        _equipoRepository.AgregarColaborador(equipo.Id, creador);
        return equipo;
    }

    /// <summary>Indica si el colaborador pertenece al equipo (autorización de la API).</summary>
    public bool EsMiembro(Guid equipoId, Guid colaboradorId) =>
        _equipoRepository.ObtenerPorId(equipoId)?.BuscarColaborador(colaboradorId) is not null;

    public IReadOnlyList<Equipo> ListarEquiposDe(Guid colaboradorId) =>
        _equipoRepository.ObtenerTodos()
            .Where(equipo => equipo.BuscarColaborador(colaboradorId) is not null)
            .ToList();

    public Colaborador RegistrarColaborador(Guid equipoId, string usuario, string contraseña)
    {
        var equipo = ObtenerEquipoOFallar(equipoId);
        if (equipo.EsPersonal)
            throw new InvalidOperationException("Un calendario personal no admite otros colaboradores.");
        if (_colaboradorRepository?.ExistePorUsuario(usuario) == true ||
            _equipoRepository.ObtenerTodos().Any(item => item.BuscarColaborador(usuario) is not null))
            throw new InvalidOperationException($"El usuario '{usuario}' ya existe.");

        var colaborador = new Colaborador(usuario, contraseña);
        equipo.AgregarColaborador(colaborador);
        _equipoRepository.AgregarColaborador(equipo.Id, colaborador);
        return colaborador;
    }

    public void EliminarColaborador(Guid equipoId, Guid colaboradorId)
    {
        var equipo = ObtenerEquipoOFallar(equipoId);
        if (!equipo.EliminarColaborador(colaboradorId))
            throw new KeyNotFoundException("El colaborador no pertenece a este equipo.");
        _equipoRepository.EliminarColaborador(equipo.Id, colaboradorId);
    }

    public IReadOnlyList<Equipo> ListarEquipos() => _equipoRepository.ObtenerTodos();

    public Equipo ObtenerEquipo(Guid equipoId) => ObtenerEquipoOFallar(equipoId);

    private Equipo ObtenerEquipoOFallar(Guid equipoId) =>
        _equipoRepository.ObtenerPorId(equipoId)
        ?? throw new KeyNotFoundException($"No se encontró el equipo con id '{equipoId}'.");
}
