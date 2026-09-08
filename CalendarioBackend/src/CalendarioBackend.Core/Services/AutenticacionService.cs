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
    private readonly List<Colaborador> _usuariosIndependientes = new();

    public AutenticacionService(IEquipoRepository equipoRepository)
    {
        _equipoRepository = equipoRepository;
    }

    /// <summary>Devuelve el colaborador autenticado o null si usuario/contraseña no son válidos.</summary>
    public Colaborador? Autenticar(string usuario, string contraseña)
    {
        var usuarioIndependiente = _usuariosIndependientes
            .FirstOrDefault(colaborador => colaborador.Usuario.Equals(usuario, StringComparison.OrdinalIgnoreCase));

        if (usuarioIndependiente is not null && usuarioIndependiente.ValidarContraseña(contraseña))
            return usuarioIndependiente;

        foreach (var equipo in _equipoRepository.ObtenerTodos())
        {
            var colaborador = equipo.BuscarColaborador(usuario);
            if (colaborador is not null && colaborador.ValidarContraseña(contraseña))
                return colaborador;
        }

        return null;
    }

    /// <summary>Registra una cuenta que todavía no pertenece a ningún equipo.</summary>
    public Colaborador RegistrarUsuario(string usuario, string contraseña)
    {
        if (_usuariosIndependientes.Any(colaborador =>
                colaborador.Usuario.Equals(usuario, StringComparison.OrdinalIgnoreCase)) ||
            _equipoRepository.ObtenerTodos().Any(equipo => equipo.BuscarColaborador(usuario) is not null))
        {
            throw new InvalidOperationException($"El usuario '{usuario}' ya existe.");
        }

        var colaborador = new Colaborador(usuario, contraseña);
        _usuariosIndependientes.Add(colaborador);
        return colaborador;
    }
}
