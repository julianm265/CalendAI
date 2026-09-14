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

    public Equipo CrearEquipo(string nombreEquipo)
    {
        if (_equipoRepository.ObtenerPorNombre(nombreEquipo) is not null)
            throw new InvalidOperationException($"Ya existe un equipo llamado '{nombreEquipo}'.");

        var equipo = new Equipo(nombreEquipo);
        return _equipoRepository.Agregar(equipo);
    }

    public Colaborador RegistrarColaborador(Guid equipoId, string usuario, string contraseña)
    {
        var equipo = ObtenerEquipoOFallar(equipoId);
        if (_colaboradorRepository?.ExistePorUsuario(usuario) == true ||
            _equipoRepository.ObtenerTodos().Any(item => item.BuscarColaborador(usuario) is not null))
            throw new InvalidOperationException($"El usuario '{usuario}' ya existe.");

        var colaborador = new Colaborador(usuario, contraseña);
        equipo.AgregarColaborador(colaborador);
        _equipoRepository.Guardar(equipo);
        return colaborador;
    }

    public void EliminarColaborador(Guid equipoId, Guid colaboradorId)
    {
        var equipo = ObtenerEquipoOFallar(equipoId);
        if (!equipo.EliminarColaborador(colaboradorId))
            throw new KeyNotFoundException("El colaborador no pertenece a este equipo.");
    }

    public IReadOnlyList<Equipo> ListarEquipos() => _equipoRepository.ObtenerTodos();

    public Equipo ObtenerEquipo(Guid equipoId) => ObtenerEquipoOFallar(equipoId);

    private Equipo ObtenerEquipoOFallar(Guid equipoId) =>
        _equipoRepository.ObtenerPorId(equipoId)
        ?? throw new KeyNotFoundException($"No se encontró el equipo con id '{equipoId}'.");
}
