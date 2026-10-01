using System;
using System.Collections.Generic;
using System.Linq;
using CalendarioBackend.Core.Models;
using CalendarioBackend.Core.Repositories;
using CalendarioBackend.Core.Services;
using Xunit;

namespace CalendarioBackend.Tests
{
    // ---- Repositorios falsos en memoria (implementan las interfaces actuales de main) ----
    public class FakeEquipoRepository : IEquipoRepository
    {
        private readonly List<Equipo> _equipos = new();

        public Equipo Agregar(Equipo equipo) { _equipos.Add(equipo); return equipo; }
        public void Guardar(Equipo equipo) { }
        public void AgregarColaborador(Guid equipoId, Colaborador colaborador) { }
        public void EliminarColaborador(Guid equipoId, Guid colaboradorId) { }
        public void AgregarEvento(Guid equipoId, DateOnly fecha, Evento evento) { }
        public void EliminarEvento(Guid equipoId, Guid eventoId) { }
        public Equipo? ObtenerPorId(Guid id) => _equipos.FirstOrDefault(e => e.Id == id);
        public Equipo? ObtenerPorNombre(string nombre) =>
            _equipos.FirstOrDefault(e => e.NombreEquipo.Equals(nombre, StringComparison.OrdinalIgnoreCase));
        public IReadOnlyList<Equipo> ObtenerTodos() => _equipos.AsReadOnly();
        public bool Eliminar(Guid id) => _equipos.RemoveAll(e => e.Id == id) > 0;

        public Equipo CrearCalendarioPersonal(Colaborador colaborador)
        {
            var equipo = new Equipo($"Calendario de {colaborador.Usuario}", esPersonal: true);
            equipo.AgregarColaborador(colaborador);
            _equipos.Add(equipo);
            return equipo;
        }
    }

    public class FakeColaboradorRepository : IColaboradorRepository
    {
        private readonly List<Colaborador> _colaboradores = new();

        public Colaborador? ObtenerPorUsuario(string usuario) =>
            _colaboradores.FirstOrDefault(c => c.Usuario.Equals(usuario, StringComparison.OrdinalIgnoreCase));
        public bool ExistePorUsuario(string usuario) => ObtenerPorUsuario(usuario) is not null;
        public void Agregar(Colaborador colaborador, Guid? equipoId) => _colaboradores.Add(colaborador);
        public void AsignarAEquipo(Guid colaboradorId, Guid equipoId) { }
    }

    public class AutenticacionServiceTests
    {
        private readonly AutenticacionService _autenticacionService;

        public AutenticacionServiceTests()
        {
            var equipos = new FakeEquipoRepository();
            var colaboradores = new FakeColaboradorRepository();
            _autenticacionService = new AutenticacionService(equipos, colaboradores);
        }

        // PRUEBA 1: Criterio de aceptación Dado / Cuando / Entonces
        [Fact]
        public void DadoUsuarioRegistrado_CuandoIngresaCredencialesValidas_EntoncesAutenticaConExito()
        {
            string usuarioValido = "camilo";
            string claveValida = "Password123!";
            _autenticacionService.RegistrarUsuario(usuarioValido, claveValida);

            Colaborador? resultado = _autenticacionService.Autenticar(usuarioValido, claveValida);

            Assert.NotNull(resultado);
            Assert.Equal(usuarioValido, resultado!.Usuario, ignoreCase: true);
        }

        // PRUEBA 2: Contraseña incorrecta
        [Fact]
        public void Autenticar_ConContrasenaErronea_RetornaNull()
        {
            string usuario = "juan";
            _autenticacionService.RegistrarUsuario(usuario, "ClaveCorrecta123");

            Colaborador? resultado = _autenticacionService.Autenticar(usuario, "ClaveErronea");

            Assert.Null(resultado);
        }

        // PRUEBA 3: Usuario duplicado
        [Fact]
        public void RegistrarUsuario_ConUsuarioExistente_LanzaInvalidOperationException()
        {
            string usuarioExistente = "maria";
            _autenticacionService.RegistrarUsuario(usuarioExistente, "Clave123");

            Assert.Throws<InvalidOperationException>(() =>
                _autenticacionService.RegistrarUsuario(usuarioExistente, "OtraClave456")
            );
        }
    }
}