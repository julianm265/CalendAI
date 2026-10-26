using CalendarioBackend.Core.Models;

namespace CalendarioBackend.Core.Repositories;

public interface IColaboradorRepository
{
    Colaborador? ObtenerPorId(Guid id);
    Colaborador? ObtenerPorUsuario(string usuario);
    bool ExistePorUsuario(string usuario);
    void Agregar(Colaborador colaborador);

    /// <summary>Guarda el hash de contraseña actual del colaborador (por ejemplo tras migrarlo de formato).</summary>
    void ActualizarCredenciales(Colaborador colaborador);
}
