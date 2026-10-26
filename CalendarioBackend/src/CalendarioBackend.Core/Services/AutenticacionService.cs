using CalendarioBackend.Core.Models;
using CalendarioBackend.Core.Repositories;

namespace CalendarioBackend.Core.Services;

/// <summary>
/// Registro e inicio de sesión de usuarios. Cada usuario nuevo recibe de inmediato su calendario
/// personal; los calendarios de equipo se crean o se aceptan después (ver <see cref="EquipoService"/>).
/// </summary>
public class AutenticacionService(IColaboradorRepository colaboradores, IEquipoRepository equipos)
{
    /// <summary>Devuelve el colaborador autenticado o null si usuario/contraseña no son válidos.</summary>
    public Colaborador? Autenticar(string usuario, string contraseña)
    {
        if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrEmpty(contraseña))
            return null;

        var colaborador = colaboradores.ObtenerPorUsuario(usuario.Trim());
        if (colaborador is null || !colaborador.ValidarContraseña(contraseña))
            return null;

        if (colaborador.NecesitaRehash)
        {
            colaborador.Rehash(contraseña);
            colaboradores.ActualizarCredenciales(colaborador);
        }

        return colaborador;
    }

    /// <summary>Crea la cuenta y su calendario personal. El usuario queda como líder de ese calendario.</summary>
    public Colaborador RegistrarUsuario(string usuario, string contraseña)
    {
        var colaborador = new Colaborador(usuario, contraseña);
        if (colaboradores.ExistePorUsuario(colaborador.Usuario))
            throw new InvalidOperationException($"El usuario '{colaborador.Usuario}' ya existe.");

        colaboradores.Agregar(colaborador);
        ObtenerOCrearCalendarioPersonal(colaborador);
        return colaborador;
    }

    /// <summary>Calendario personal del usuario; lo crea si todavía no existe (usuarios anteriores a los equipos).</summary>
    public Equipo ObtenerOCrearCalendarioPersonal(Colaborador colaborador)
    {
        var personal = equipos.ObtenerPersonal(colaborador.Id);
        if (personal is not null) return personal;

        return equipos.Agregar(
            new Equipo($"Calendario de {colaborador.Usuario}", esPersonal: true, creadoPorId: colaborador.Id),
            colaborador.Id);
    }

    public Colaborador? ObtenerPorId(Guid id) => colaboradores.ObtenerPorId(id);
}
