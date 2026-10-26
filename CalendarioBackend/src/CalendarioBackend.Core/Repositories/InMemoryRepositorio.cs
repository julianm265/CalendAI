using CalendarioBackend.Core.Models;

namespace CalendarioBackend.Core.Repositories;

/// <summary>
/// Implementación en memoria de los tres repositorios, para la consola de demostración y las pruebas.
/// Todas las operaciones están protegidas por un único candado: es simple y suficiente para ese uso.
/// </summary>
public sealed class InMemoryRepositorio : IEquipoRepository, IColaboradorRepository, IInvitacionRepository
{
    private readonly object _candado = new();
    private readonly Dictionary<Guid, Equipo> _equipos = new();
    private readonly Dictionary<Guid, Colaborador> _colaboradores = new();
    private readonly List<(Guid EquipoId, Guid ColaboradorId, RolEquipo Rol, DateTime UnidoEn)> _membresias = new();
    private readonly List<(Guid EquipoId, DateOnly Fecha, Evento Evento)> _eventos = new();
    private readonly Dictionary<Guid, Invitacion> _invitaciones = new();

    // ------------------------------------------------------------ colaboradores

    // Las tres interfaces tienen un ObtenerPorId que solo difiere en el tipo devuelto: se implementan de forma explícita.
    Colaborador? IColaboradorRepository.ObtenerPorId(Guid id)
    {
        lock (_candado) return _colaboradores.GetValueOrDefault(id);
    }

    public Colaborador? ObtenerPorUsuario(string usuario)
    {
        lock (_candado)
            return _colaboradores.Values.FirstOrDefault(c =>
                c.Usuario.Equals(usuario.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public bool ExistePorUsuario(string usuario) => ObtenerPorUsuario(usuario) is not null;

    public void Agregar(Colaborador colaborador)
    {
        lock (_candado)
        {
            if (_colaboradores.Values.Any(c => c.Usuario.Equals(colaborador.Usuario, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"El usuario '{colaborador.Usuario}' ya existe.");

            _colaboradores[colaborador.Id] = colaborador;
        }
    }

    public void ActualizarCredenciales(Colaborador colaborador)
    {
        lock (_candado) _colaboradores[colaborador.Id] = colaborador;
    }

    // ------------------------------------------------------------ equipos

    public Equipo Agregar(Equipo equipo, Guid liderId)
    {
        lock (_candado)
        {
            if (!_equipos.TryAdd(equipo.Id, equipo))
                throw new InvalidOperationException($"Ya existe un equipo con id '{equipo.Id}'.");

            _membresias.Add((equipo.Id, liderId, RolEquipo.Lider, DateTime.UtcNow));
            return equipo;
        }
    }

    Equipo? IEquipoRepository.ObtenerPorId(Guid id)
    {
        lock (_candado) return _equipos.GetValueOrDefault(id);
    }

    public Equipo? ObtenerPersonal(Guid colaboradorId)
    {
        lock (_candado) return ObtenerPersonalInterno(colaboradorId);
    }

    private Equipo? ObtenerPersonalInterno(Guid colaboradorId) =>
        _membresias
            .Where(m => m.ColaboradorId == colaboradorId && _equipos[m.EquipoId].EsPersonal)
            .Select(m => _equipos[m.EquipoId])
            .FirstOrDefault();

    public IReadOnlyList<EquipoDelUsuario> ObtenerDelColaborador(Guid colaboradorId)
    {
        lock (_candado)
            return _membresias
                .Where(m => m.ColaboradorId == colaboradorId)
                .Select(m => new EquipoDelUsuario(
                    _equipos[m.EquipoId], m.Rol, _membresias.Count(x => x.EquipoId == m.EquipoId)))
                .OrderByDescending(item => item.Equipo.EsPersonal)
                .ThenBy(item => item.Equipo.NombreEquipo, StringComparer.OrdinalIgnoreCase)
                .ToList();
    }

    public bool ExisteNombreLiderado(Guid liderId, string nombre)
    {
        lock (_candado)
            return _membresias.Any(m =>
                m.ColaboradorId == liderId
                && m.Rol == RolEquipo.Lider
                && !_equipos[m.EquipoId].EsPersonal
                && _equipos[m.EquipoId].NombreEquipo.Equals(nombre, StringComparison.OrdinalIgnoreCase));
    }

    public void Renombrar(Guid equipoId, string nombre)
    {
        lock (_candado)
        {
            if (!_equipos.TryGetValue(equipoId, out var equipo))
                throw new KeyNotFoundException("No se encontró el equipo.");
            equipo.Renombrar(nombre);
        }
    }

    public bool Eliminar(Guid id)
    {
        lock (_candado)
        {
            _membresias.RemoveAll(m => m.EquipoId == id);
            _eventos.RemoveAll(e => e.EquipoId == id);
            foreach (var invitacion in _invitaciones.Values.Where(i => i.EquipoId == id).ToList())
                _invitaciones.Remove(invitacion.Id);
            return _equipos.Remove(id);
        }
    }

    // ------------------------------------------------------------ membresías

    public RolEquipo? ObtenerRol(Guid equipoId, Guid colaboradorId)
    {
        lock (_candado)
        {
            foreach (var m in _membresias)
                if (m.EquipoId == equipoId && m.ColaboradorId == colaboradorId) return m.Rol;
            return null;
        }
    }

    public IReadOnlyList<MiembroEquipo> ObtenerMiembros(Guid equipoId)
    {
        lock (_candado)
            return _membresias
                .Where(m => m.EquipoId == equipoId)
                .Select(m => new MiembroEquipo(m.ColaboradorId, _colaboradores[m.ColaboradorId].Usuario, m.Rol, m.UnidoEn))
                .OrderByDescending(m => m.Rol)
                .ThenBy(m => m.Usuario, StringComparer.OrdinalIgnoreCase)
                .ToList();
    }

    public void AgregarMiembro(Guid equipoId, Guid colaboradorId, RolEquipo rol)
    {
        lock (_candado)
        {
            if (!_equipos.ContainsKey(equipoId)) throw new KeyNotFoundException("No se encontró el equipo.");
            if (_membresias.Any(m => m.EquipoId == equipoId && m.ColaboradorId == colaboradorId))
                throw new InvalidOperationException("El usuario ya pertenece al equipo.");

            _membresias.Add((equipoId, colaboradorId, rol, DateTime.UtcNow));
        }
    }

    public bool EliminarMiembro(Guid equipoId, Guid colaboradorId)
    {
        lock (_candado)
            return _membresias.RemoveAll(m => m.EquipoId == equipoId && m.ColaboradorId == colaboradorId) > 0;
    }

    public void TransferirLiderazgo(Guid equipoId, Guid actualLiderId, Guid nuevoLiderId)
    {
        lock (_candado)
        {
            var actual = _membresias.FindIndex(m => m.EquipoId == equipoId && m.ColaboradorId == actualLiderId);
            var nuevo = _membresias.FindIndex(m => m.EquipoId == equipoId && m.ColaboradorId == nuevoLiderId);
            if (actual < 0 || nuevo < 0) throw new KeyNotFoundException("El usuario no pertenece al equipo.");

            var anterior = _membresias[actual];
            var siguiente = _membresias[nuevo];
            _membresias[actual] = (anterior.EquipoId, anterior.ColaboradorId, RolEquipo.Miembro, anterior.UnidoEn);
            _membresias[nuevo] = (siguiente.EquipoId, siguiente.ColaboradorId, RolEquipo.Lider, siguiente.UnidoEn);
        }
    }

    // ------------------------------------------------------------ eventos

    public IReadOnlyList<Evento> ObtenerEventos(Guid equipoId, DateOnly fecha)
    {
        lock (_candado)
            return _eventos.Where(e => e.EquipoId == equipoId && e.Fecha == fecha)
                .Select(e => e.Evento).OrderBy(e => e.HoraEvento).ToList();
    }

    public IReadOnlyList<(DateOnly Fecha, Evento Evento)> ObtenerEventos(Guid equipoId, DateOnly desde, DateOnly hasta)
    {
        lock (_candado)
            return _eventos.Where(e => e.EquipoId == equipoId && e.Fecha >= desde && e.Fecha <= hasta)
                .OrderBy(e => e.Fecha).ThenBy(e => e.Evento.HoraEvento)
                .Select(e => (e.Fecha, e.Evento)).ToList();
    }

    public (DateOnly Fecha, Evento Evento)? ObtenerEvento(Guid equipoId, Guid eventoId)
    {
        lock (_candado)
        {
            foreach (var e in _eventos)
                if (e.EquipoId == equipoId && e.Evento.Id == eventoId) return (e.Fecha, e.Evento);
            return null;
        }
    }

    public void AgregarEvento(Guid equipoId, DateOnly fecha, Evento evento)
    {
        lock (_candado)
        {
            if (!_equipos.ContainsKey(equipoId)) throw new KeyNotFoundException("No se encontró el equipo.");
            if (_eventos.Any(e => e.EquipoId == equipoId && e.Fecha == fecha && e.Evento.HoraEvento == evento.HoraEvento))
                throw new InvalidOperationException(
                    $"Ya existe un evento programado a las {evento.HoraEvento:HH\\:mm} el {fecha:dd/MM/yyyy}.");

            _eventos.Add((equipoId, fecha, evento));
        }
    }

    public bool EliminarEvento(Guid equipoId, Guid eventoId)
    {
        lock (_candado)
            return _eventos.RemoveAll(e => e.EquipoId == equipoId && e.Evento.Id == eventoId) > 0;
    }

    public IReadOnlyList<LugarFrecuente> ObtenerLugaresFrecuentes(Guid equipoId, int limite)
    {
        lock (_candado)
            return LugarNormalizer.Agrupar(
                _eventos.Where(e => e.EquipoId == equipoId && !string.IsNullOrWhiteSpace(e.Evento.LugarEvento))
                    .Select(e => e.Evento.LugarEvento!),
                limite);
    }

    public IReadOnlyList<LugarFrecuente> ObtenerLugaresDelColaborador(Guid colaboradorId, Guid equipoId, int limite)
    {
        lock (_candado)
        {
            var personalId = ObtenerPersonalInterno(colaboradorId)?.Id;
            return LugarNormalizer.Agrupar(
                _eventos.Where(e => e.Evento.ColaboradorOrganizadorId == colaboradorId
                        && (e.EquipoId == equipoId || e.EquipoId == personalId)
                        && !string.IsNullOrWhiteSpace(e.Evento.LugarEvento))
                    .Select(e => e.Evento.LugarEvento!),
                limite);
        }
    }

    // ------------------------------------------------------------ invitaciones

    public Invitacion Agregar(Invitacion invitacion)
    {
        lock (_candado)
        {
            if (ExistePendienteInterno(invitacion.EquipoId, invitacion.InvitadoId))
                throw new InvalidOperationException("Ya hay una invitación pendiente para ese usuario.");

            _invitaciones[invitacion.Id] = invitacion;
            return invitacion;
        }
    }

    Invitacion? IInvitacionRepository.ObtenerPorId(Guid id)
    {
        lock (_candado) return _invitaciones.GetValueOrDefault(id);
    }

    public bool ExistePendiente(Guid equipoId, Guid invitadoId)
    {
        lock (_candado) return ExistePendienteInterno(equipoId, invitadoId);
    }

    private bool ExistePendienteInterno(Guid equipoId, Guid invitadoId) =>
        _invitaciones.Values.Any(i =>
            i.EquipoId == equipoId && i.InvitadoId == invitadoId && i.Estado == EstadoInvitacion.Pendiente);

    public IReadOnlyList<InvitacionDetalle> ObtenerPendientesDeEquipo(Guid equipoId)
    {
        lock (_candado)
            return _invitaciones.Values
                .Where(i => i.EquipoId == equipoId && i.Estado == EstadoInvitacion.Pendiente)
                .OrderByDescending(i => i.CreadaEn).Select(Detallar).ToList();
    }

    public IReadOnlyList<InvitacionDetalle> ObtenerPendientesDeColaborador(Guid colaboradorId)
    {
        lock (_candado)
            return _invitaciones.Values
                .Where(i => i.InvitadoId == colaboradorId && i.Estado == EstadoInvitacion.Pendiente)
                .OrderByDescending(i => i.CreadaEn).Select(Detallar).ToList();
    }

    public void Actualizar(Invitacion invitacion)
    {
        lock (_candado) _invitaciones[invitacion.Id] = invitacion;
    }

    private InvitacionDetalle Detallar(Invitacion i) => new(
        i.Id, i.EquipoId, _equipos[i.EquipoId].NombreEquipo,
        i.InvitadoId, _colaboradores[i.InvitadoId].Usuario,
        i.InvitadoPorId, _colaboradores[i.InvitadoPorId].Usuario,
        i.Estado, i.CreadaEn);
}
