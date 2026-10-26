using CalendarioBackend.Core.Models;
using CalendarioBackend.Core.Repositories;

namespace CalendarioBackend.Core.Services;

/// <summary>
/// Equipos (calendarios compartidos), sus miembros e invitaciones.
/// Reglas: quien crea un equipo es su líder; solo el líder invita, expulsa, renombra, elimina o cede el
/// liderazgo; cualquier miembro puede salir (el líder, solo tras ceder el liderazgo); el calendario
/// personal no se invita, no se renombra y no se elimina.
/// </summary>
public class EquipoService(
    IEquipoRepository equipos,
    IColaboradorRepository colaboradores,
    IInvitacionRepository invitaciones,
    AccesoEquipoService acceso)
{
    public Equipo CrearEquipo(Guid liderId, string nombreEquipo)
    {
        var nombre = Equipo.NormalizarNombre(nombreEquipo);
        if (equipos.ExisteNombreLiderado(liderId, nombre))
            throw new InvalidOperationException($"Ya diriges un equipo llamado '{nombre}'.");

        return equipos.Agregar(new Equipo(nombre, esPersonal: false, creadoPorId: liderId), liderId);
    }

    public IReadOnlyList<EquipoDelUsuario> ListarEquiposDe(Guid colaboradorId) =>
        equipos.ObtenerDelColaborador(colaboradorId);

    public (Equipo Equipo, RolEquipo MiRol, IReadOnlyList<MiembroEquipo> Miembros) ObtenerDetalle(
        Guid equipoId, Guid solicitanteId)
    {
        var rol = acceso.ExigirMiembro(equipoId, solicitanteId);
        var equipo = equipos.ObtenerPorId(equipoId) ?? throw new KeyNotFoundException("No se encontró el equipo.");
        return (equipo, rol, equipos.ObtenerMiembros(equipoId));
    }

    public Equipo Renombrar(Guid equipoId, Guid solicitanteId, string nuevoNombre)
    {
        acceso.ExigirLider(equipoId, solicitanteId);
        var equipo = ObtenerEquipoNoPersonal(equipoId, "El calendario personal no se puede renombrar.");

        var nombre = Equipo.NormalizarNombre(nuevoNombre);
        if (!nombre.Equals(equipo.NombreEquipo, StringComparison.OrdinalIgnoreCase)
            && equipos.ExisteNombreLiderado(solicitanteId, nombre))
            throw new InvalidOperationException($"Ya diriges un equipo llamado '{nombre}'.");

        equipos.Renombrar(equipoId, nombre);
        equipo.Renombrar(nombre);
        return equipo;
    }

    public void EliminarEquipo(Guid equipoId, Guid solicitanteId)
    {
        acceso.ExigirLider(equipoId, solicitanteId);
        ObtenerEquipoNoPersonal(equipoId, "El calendario personal no se puede eliminar.");
        equipos.Eliminar(equipoId);
    }

    // ------------------------------------------------------------ invitaciones (lado del líder)

    public InvitacionDetalle Invitar(Guid equipoId, Guid solicitanteId, string usuarioInvitado)
    {
        acceso.ExigirLider(equipoId, solicitanteId);
        var equipo = ObtenerEquipoNoPersonal(equipoId, "No se puede invitar a nadie a un calendario personal.");

        var buscado = (usuarioInvitado ?? string.Empty).Trim();
        var invitado = colaboradores.ObtenerPorUsuario(buscado)
            ?? throw new KeyNotFoundException($"No existe ningún usuario llamado '{buscado}'.");

        if (invitado.Id == solicitanteId)
            throw new InvalidOperationException("Ya perteneces a este equipo.");
        if (acceso.ObtenerRol(equipoId, invitado.Id) is not null)
            throw new InvalidOperationException($"'{invitado.Usuario}' ya pertenece al equipo.");
        if (invitaciones.ExistePendiente(equipoId, invitado.Id))
            throw new InvalidOperationException($"'{invitado.Usuario}' ya tiene una invitación pendiente.");

        var invitacion = invitaciones.Agregar(new Invitacion(equipoId, invitado.Id, solicitanteId));
        var lider = colaboradores.ObtenerPorId(solicitanteId);
        return new InvitacionDetalle(
            invitacion.Id, equipo.Id, equipo.NombreEquipo, invitado.Id, invitado.Usuario,
            solicitanteId, lider?.Usuario ?? string.Empty, invitacion.Estado, invitacion.CreadaEn);
    }

    public IReadOnlyList<InvitacionDetalle> ListarInvitacionesDeEquipo(Guid equipoId, Guid solicitanteId)
    {
        acceso.ExigirLider(equipoId, solicitanteId);
        return invitaciones.ObtenerPendientesDeEquipo(equipoId);
    }

    public void CancelarInvitacion(Guid equipoId, Guid invitacionId, Guid solicitanteId)
    {
        acceso.ExigirLider(equipoId, solicitanteId);
        var invitacion = invitaciones.ObtenerPorId(invitacionId);
        if (invitacion is null || invitacion.EquipoId != equipoId)
            throw new KeyNotFoundException("No se encontró la invitación.");

        invitacion.Cancelar();
        invitaciones.Actualizar(invitacion);
    }

    // ------------------------------------------------------------ invitaciones (lado del invitado)

    public IReadOnlyList<InvitacionDetalle> ListarInvitacionesRecibidas(Guid colaboradorId) =>
        invitaciones.ObtenerPendientesDeColaborador(colaboradorId);

    /// <summary>Acepta o rechaza una invitación. Solo puede responderla la persona invitada.</summary>
    public void ResponderInvitacion(Guid invitacionId, Guid colaboradorId, bool aceptar)
    {
        var invitacion = invitaciones.ObtenerPorId(invitacionId);
        if (invitacion is null || invitacion.InvitadoId != colaboradorId)
            throw new KeyNotFoundException("No se encontró la invitación.");

        if (aceptar)
        {
            if (equipos.ObtenerPorId(invitacion.EquipoId) is null)
                throw new KeyNotFoundException("El equipo ya no existe.");

            invitacion.Aceptar();
            if (acceso.ObtenerRol(invitacion.EquipoId, colaboradorId) is null)
                equipos.AgregarMiembro(invitacion.EquipoId, colaboradorId, RolEquipo.Miembro);
        }
        else
        {
            invitacion.Rechazar();
        }

        invitaciones.Actualizar(invitacion);
    }

    // ------------------------------------------------------------ miembros

    /// <summary>
    /// Quita a un miembro. Un miembro puede quitarse a sí mismo (salir); expulsar a otro exige ser líder.
    /// El líder no puede salir mientras haya más miembros: primero debe ceder el liderazgo.
    /// </summary>
    public void EliminarMiembro(Guid equipoId, Guid solicitanteId, Guid colaboradorId)
    {
        var rolSolicitante = acceso.ExigirMiembro(equipoId, solicitanteId);
        var equipo = ObtenerEquipoNoPersonal(equipoId, "No puedes salir de tu calendario personal.");

        if (colaboradorId != solicitanteId && rolSolicitante != RolEquipo.Lider)
            throw new UnauthorizedAccessException("Solo el líder del equipo puede quitar a otros miembros.");

        var rolObjetivo = acceso.ObtenerRol(equipoId, colaboradorId)
            ?? throw new KeyNotFoundException("El usuario no pertenece a este equipo.");

        if (rolObjetivo == RolEquipo.Lider)
        {
            var otros = equipos.ObtenerMiembros(equipo.Id).Count - 1;
            throw new InvalidOperationException(otros > 0
                ? "El líder no puede salir del equipo: cede primero el liderazgo a otro miembro."
                : "Eres el único miembro: si ya no necesitas el equipo, elimínalo.");
        }

        equipos.EliminarMiembro(equipoId, colaboradorId);
    }

    public void TransferirLiderazgo(Guid equipoId, Guid solicitanteId, Guid nuevoLiderId)
    {
        acceso.ExigirLider(equipoId, solicitanteId);
        ObtenerEquipoNoPersonal(equipoId, "El calendario personal no tiene otros miembros.");

        if (nuevoLiderId == solicitanteId)
            throw new InvalidOperationException("Ya eres el líder del equipo.");
        if (acceso.ObtenerRol(equipoId, nuevoLiderId) is null)
            throw new KeyNotFoundException("El usuario no pertenece a este equipo.");

        equipos.TransferirLiderazgo(equipoId, solicitanteId, nuevoLiderId);
    }

    private Equipo ObtenerEquipoNoPersonal(Guid equipoId, string mensajeSiEsPersonal)
    {
        var equipo = equipos.ObtenerPorId(equipoId) ?? throw new KeyNotFoundException("No se encontró el equipo.");
        if (equipo.EsPersonal) throw new InvalidOperationException(mensajeSiEsPersonal);
        return equipo;
    }
}
