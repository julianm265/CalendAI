using CalendarioBackend.Core.Models;

namespace CalendarioBackend.Core.Repositories;

public interface IColaboradorRepository
{
    Colaborador? ObtenerPorUsuario(string usuario);
    Colaborador? ObtenerPorId(Guid id);
    bool ExistePorUsuario(string usuario);
    void Agregar(Colaborador colaborador, Guid? equipoId);
    void AsignarAEquipo(Guid colaboradorId, Guid equipoId);
}