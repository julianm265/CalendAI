namespace CalendarioBackend.Core.Models;

public enum EstadoInvitacion
{
    Pendiente = 0,
    Aceptada = 1,
    Rechazada = 2,
    Cancelada = 3
}

/// <summary>Invitación de un líder a otro usuario para unirse a un equipo.</summary>
public class Invitacion
{
    public Guid Id { get; }
    public Guid EquipoId { get; }
    public Guid InvitadoId { get; }
    public Guid InvitadoPorId { get; }
    public EstadoInvitacion Estado { get; private set; }
    public DateTime CreadaEn { get; }
    public DateTime? RespondidaEn { get; private set; }

    public Invitacion(Guid equipoId, Guid invitadoId, Guid invitadoPorId)
    {
        if (invitadoId == invitadoPorId)
            throw new InvalidOperationException("No puedes invitarte a ti mismo.");

        Id = Guid.NewGuid();
        EquipoId = equipoId;
        InvitadoId = invitadoId;
        InvitadoPorId = invitadoPorId;
        Estado = EstadoInvitacion.Pendiente;
        CreadaEn = DateTime.UtcNow;
    }

    internal Invitacion(Guid id, Guid equipoId, Guid invitadoId, Guid invitadoPorId,
        EstadoInvitacion estado, DateTime creadaEn, DateTime? respondidaEn)
    {
        Id = id;
        EquipoId = equipoId;
        InvitadoId = invitadoId;
        InvitadoPorId = invitadoPorId;
        Estado = estado;
        CreadaEn = creadaEn;
        RespondidaEn = respondidaEn;
    }

    public void Aceptar() => Cerrar(EstadoInvitacion.Aceptada);
    public void Rechazar() => Cerrar(EstadoInvitacion.Rechazada);
    public void Cancelar() => Cerrar(EstadoInvitacion.Cancelada);

    private void Cerrar(EstadoInvitacion nuevoEstado)
    {
        if (Estado != EstadoInvitacion.Pendiente)
            throw new InvalidOperationException("La invitación ya fue respondida o cancelada.");

        Estado = nuevoEstado;
        RespondidaEn = DateTime.UtcNow;
    }
}

/// <summary>Invitación con los nombres ya resueltos, lista para mostrarse.</summary>
public sealed record InvitacionDetalle(
    Guid Id,
    Guid EquipoId,
    string Equipo,
    Guid InvitadoId,
    string Invitado,
    Guid InvitadoPorId,
    string InvitadoPor,
    EstadoInvitacion Estado,
    DateTime CreadaEn);
