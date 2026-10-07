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

    public Equipo CrearEquipo(string nombreEquipo, Colaborador lider)
    {
        if (_equipoRepository.ObtenerPorNombre(nombreEquipo) is not null)
            throw new InvalidOperationException($"Ya existe un equipo llamado '{nombreEquipo}'.");

        var equipo = new Equipo(nombreEquipo, liderId: lider.Id);
        equipo.AgregarColaborador(lider);
        return _equipoRepository.Agregar(equipo);
    }

    public Equipo CrearEquipo(string nombreEquipo)
    {
        if (_equipoRepository.ObtenerPorNombre(nombreEquipo) is not null)
            throw new InvalidOperationException($"Ya existe un equipo llamado '{nombreEquipo}'.");
        return _equipoRepository.Agregar(new Equipo(nombreEquipo));
    }

    public Colaborador RegistrarColaborador(Guid equipoId, string usuario, string contraseña)
    {
        _ = ObtenerEquipoOFallar(equipoId);
        var colaborador = _colaboradorRepository?.ObtenerPorUsuario(usuario);
        if (colaborador is null)
        {
            colaborador = new Colaborador(usuario, contraseña);
            _colaboradorRepository?.Agregar(colaborador, null);
        }
        _equipoRepository.AgregarMiembro(equipoId, colaborador);
        return colaborador;
    }

    public void Invitar(Guid equipoId, string usuario, Guid solicitanteId)
    {
        var equipo = ObtenerEquipoOFallar(equipoId);
        if (equipo.LiderId != solicitanteId)
            throw new UnauthorizedAccessException("Solo el líder puede invitar usuarios.");
        var colaborador = _colaboradorRepository?.ObtenerPorUsuario(usuario)
            ?? throw new KeyNotFoundException($"No existe el usuario '{usuario}'.");
        if (_equipoRepository.EsMiembro(equipoId, colaborador.Id))
            throw new InvalidOperationException("El usuario ya pertenece al equipo.");
        _equipoRepository.CrearInvitacion(equipoId, colaborador);
    }

    public IReadOnlyList<Equipo> EquiposDe(Guid colaboradorId) =>
        _equipoRepository.ObtenerPorColaborador(colaboradorId);

    public IReadOnlyList<(Guid Id, Equipo Equipo)> InvitacionesDe(Guid colaboradorId) =>
        _equipoRepository.ObtenerInvitaciones(colaboradorId);

    public void AceptarInvitacion(Guid invitacionId, Guid colaboradorId) =>
        _equipoRepository.AceptarInvitacion(invitacionId, colaboradorId);

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
