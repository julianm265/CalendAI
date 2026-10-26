using CalendarioBackend.Core.Models;

namespace CalendarioBackend.Core.Repositories;

public interface IInvitacionRepository
{
    Invitacion Agregar(Invitacion invitacion);
    Invitacion? ObtenerPorId(Guid id);
    bool ExistePendiente(Guid equipoId, Guid invitadoId);
    IReadOnlyList<InvitacionDetalle> ObtenerPendientesDeEquipo(Guid equipoId);
    IReadOnlyList<InvitacionDetalle> ObtenerPendientesDeColaborador(Guid colaboradorId);
    void Actualizar(Invitacion invitacion);
}
