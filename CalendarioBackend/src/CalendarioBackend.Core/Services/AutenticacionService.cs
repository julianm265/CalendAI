using CalendarioBackend.Core.Models;
using CalendarioBackend.Core.Repositories;

namespace CalendarioBackend.Core.Services;

/// <summary>
/// Servicio de autenticación de colaboradores. Busca al usuario dentro de todos los
/// equipos registrados y valida su contraseña (comparando hashes, nunca texto plano).
/// </summary>
public class AutenticacionService
{
    private readonly IEquipoRepository _equipoRepository;

    public AutenticacionService(IEquipoRepository equipoRepository)
    {
        _equipoRepository = equipoRepository;
    }

    /// <summary>Devuelve el colaborador autenticado o null si usuario/contraseña no son válidos.</summary>
    public Colaborador? Autenticar(string usuario, string contraseña)
    {
        foreach (var equipo in _equipoRepository.ObtenerTodos())
        {
            var colaborador = equipo.BuscarColaborador(usuario);
            if (colaborador is not null && colaborador.ValidarContraseña(contraseña))
                return colaborador;
        }

        return null;
    }
}
