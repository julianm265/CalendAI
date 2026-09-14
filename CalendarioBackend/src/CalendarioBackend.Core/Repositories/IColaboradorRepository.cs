using CalendarioBackend.Core.Models;

namespace CalendarioBackend.Core.Repositories;

public interface IColaboradorRepository
{
    Colaborador? ObtenerPorUsuario(string usuario);
    bool ExistePorUsuario(string usuario);
    void Agregar(Colaborador colaborador, Guid? equipoId);
}