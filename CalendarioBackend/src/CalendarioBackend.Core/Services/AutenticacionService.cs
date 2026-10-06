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
    private readonly IColaboradorRepository? _colaboradorRepository;

    public AutenticacionService(IEquipoRepository equipoRepository, IColaboradorRepository colaboradorRepository)
    {
        _equipoRepository = equipoRepository;
        _colaboradorRepository = colaboradorRepository;
    }

    public AutenticacionService(IEquipoRepository equipoRepository)
    {
        _equipoRepository = equipoRepository;
    }

    /// <summary>Devuelve el colaborador autenticado o null si usuario/contraseña no son válidos.</summary>
    public Colaborador? Autenticar(string usuario, string contraseña)
    {
        if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrEmpty(contraseña))
            return null;

        var colaborador = _colaboradorRepository?.ObtenerPorUsuario(usuario)
            ?? _equipoRepository.ObtenerTodos()
                .Select(equipo => equipo.BuscarColaborador(usuario))
                .FirstOrDefault(encontrado => encontrado is not null);
        return colaborador is not null && colaborador.ValidarContraseña(contraseña)
            ? colaborador
            : null;
    }

    public Equipo? ObtenerEquipoDelColaborador(Colaborador colaborador)
    {
        var equipos = _equipoRepository.ObtenerTodos();
        return equipos.FirstOrDefault(equipo =>
                   equipo.EsPersonal && equipo.BuscarColaborador(colaborador.Id) is not null)
               ?? equipos.FirstOrDefault(equipo => equipo.BuscarColaborador(colaborador.Id) is not null);
    }

    public Equipo ObtenerOCrearCalendarioPersonal(Colaborador colaborador)
    {
        var equipo = _equipoRepository.ObtenerTodos()
            .FirstOrDefault(actual =>
                actual.EsPersonal && actual.BuscarColaborador(colaborador.Id) is not null);
        if (equipo is not null) return equipo;

        var calendarioPersonal = _equipoRepository.CrearCalendarioPersonal(colaborador);
        _colaboradorRepository?.AsignarAEquipo(colaborador.Id, calendarioPersonal.Id);
        return calendarioPersonal;
    }

    /// <summary>Registra una cuenta que todavía no pertenece a ningún equipo.</summary>
    public Colaborador RegistrarUsuario(string usuario, string contraseña)
    {
        if (_colaboradorRepository is null)
            throw new InvalidOperationException("Se requiere un repositorio de colaboradores para registrar usuarios.");

        if (_colaboradorRepository.ExistePorUsuario(usuario) ||
            _equipoRepository.ObtenerTodos().Any(equipo => equipo.BuscarColaborador(usuario) is not null))
        {
            throw new InvalidOperationException($"El usuario '{usuario}' ya existe.");
        }

        var colaborador = new Colaborador(usuario, contraseña);
        _colaboradorRepository.Agregar(colaborador, null);
        return colaborador;
    }
}
