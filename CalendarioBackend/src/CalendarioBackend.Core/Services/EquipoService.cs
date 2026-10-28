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

    public Equipo CrearEquipo(string nombreEquipo, string usuarioCreador)
    {
        if (string.IsNullOrWhiteSpace(nombreEquipo))
            throw new ArgumentException("El nombre del equipo no puede estar vacío.", nameof(nombreEquipo));
        if (string.IsNullOrWhiteSpace(usuarioCreador))
            throw new ArgumentException("El nombre de usuario del creador es obligatorio.", nameof(usuarioCreador));
        var nombreNormalizado = nombreEquipo.Trim();
        if (_equipoRepository.ObtenerPorNombre(nombreNormalizado) is not null)
            throw new InvalidOperationException($"Ya existe un equipo llamado '{nombreNormalizado}'.");

        var creador = _colaboradorRepository?.ObtenerPorUsuario(usuarioCreador.Trim())
            ?? throw new KeyNotFoundException($"No se encontró un usuario registrado con el nombre '{usuarioCreador}'.");
        var equipo = new Equipo(nombreNormalizado);
        return _equipoRepository.AgregarEquipoConColaborador(equipo, creador, "Líder");
    }

    public Colaborador AgregarMiembro(Guid equipoId, string usuario, string? usuarioInvitador = null)
    {
        var equipo = ObtenerEquipoOFallar(equipoId);
        if (equipo.EsPersonal)
            throw new InvalidOperationException("No se pueden agregar miembros a un calendario personal.");

        if (!string.IsNullOrWhiteSpace(usuarioInvitador))
        {
            var invitador = equipo.BuscarColaborador(usuarioInvitador.Trim())
                ?? throw new UnauthorizedAccessException("El usuario actual no pertenece a este equipo.");
            if (!string.Equals(equipo.ObtenerRolColaborador(invitador.Id), "Líder", StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("Solo el líder del equipo puede invitar usuarios.");
        }

        var usuarioNormalizado = usuario?.Trim();
        var colaborador = _colaboradorRepository?.ObtenerPorUsuario(usuarioNormalizado ?? "")
            ?? throw new KeyNotFoundException($"No se encontró un usuario registrado con el nombre '{usuarioNormalizado}'.");

        equipo.AgregarColaborador(colaborador, "Miembro");
        _equipoRepository.AgregarColaborador(equipo.Id, colaborador, "Miembro");
        return colaborador;
    }

    public Colaborador RegistrarColaborador(Guid equipoId, string usuario, string contraseña)
    {
        var equipo = ObtenerEquipoOFallar(equipoId);
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
        if (equipo.EsPersonal)
            throw new InvalidOperationException("No se pueden quitar miembros de un calendario personal.");
        if (equipo.ObtenerRolColaborador(colaboradorId) == "Líder")
            throw new InvalidOperationException("No se puede eliminar al líder del equipo.");
        if (!equipo.EliminarColaborador(colaboradorId))
            throw new KeyNotFoundException("El colaborador no pertenece a este equipo.");
        _equipoRepository.EliminarColaborador(equipo.Id, colaboradorId);
    }

    public IReadOnlyList<Equipo> ListarEquipos(string? usuario = null)
    {
        var equipos = _equipoRepository.ObtenerTodos()
            .Where(equipo => !equipo.EsPersonal);
        if (string.IsNullOrWhiteSpace(usuario))
            return equipos.ToList();

        return equipos
            .Where(equipo => equipo.BuscarColaborador(usuario.Trim()) is not null)
            .ToList();
    }

    public Equipo? ObtenerCalendarioPersonal(string usuario)
    {
        if (string.IsNullOrWhiteSpace(usuario)) return null;
        var colaborador = _colaboradorRepository?.ObtenerPorUsuario(usuario.Trim());
        return colaborador is null
            ? null
            : _equipoRepository.ObtenerTodos()
                .FirstOrDefault(equipo => equipo.EsPersonal && equipo.BuscarColaborador(colaborador.Id) is not null);
    }

    public Equipo ObtenerEquipo(Guid equipoId) => ObtenerEquipoOFallar(equipoId);

    private Equipo ObtenerEquipoOFallar(Guid equipoId) =>
        _equipoRepository.ObtenerPorId(equipoId)
        ?? throw new KeyNotFoundException($"No se encontró el equipo con id '{equipoId}'.");
}
