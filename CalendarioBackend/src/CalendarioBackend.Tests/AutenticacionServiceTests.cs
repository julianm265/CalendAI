using System;
using System.Collections.Generic;
using CalendarioBackend.Core.Models;
using CalendarioBackend.Core.Repositories;
using CalendarioBackend.Core.Services;
using Xunit;

namespace CalendarioBackend.Tests
{
    // Implementation de FakeEquipoRepository con Eliminar retornando bool
    public class FakeEquipoRepository : IEquipoRepository
    {
        private readonly List<Equipo> _equipos = new();

        public IReadOnlyList<Equipo> ObtenerTodos() => _equipos.AsReadOnly();

        public Equipo? ObtenerPorId(Guid id) => null;

        public Equipo? ObtenerPorNombre(string nombre) => null;

        public Equipo Agregar(Equipo equipo)
        {
            _equipos.Add(equipo);
            return equipo;
        }

        public void Guardar(Equipo equipo) => throw new NotImplementedException();

        public void AgregarColaborador(Guid equipoId, Colaborador colaborador) => throw new NotImplementedException();

        public void EliminarColaborador(Guid equipoId, Guid colaboradorId) => throw new NotImplementedException();

        public void AgregarEvento(Guid equipoId, DateOnly fecha, Evento evento) => throw new NotImplementedException();

        public void EliminarEvento(Guid equipoId, Guid eventoId) => throw new NotImplementedException();

        public Equipo CrearCalendarioPersonal(Colaborador colaborador) => throw new NotImplementedException();

        public void Actualizar(Equipo equipo) { }

        public bool Eliminar(Guid id) => true;
    }

    public class AutenticacionServiceTests
    {
        private readonly AutenticacionService _autenticacionService;

        public AutenticacionServiceTests()
        {
            var fakeRepo = new FakeEquipoRepository();
            _autenticacionService = new AutenticacionService(fakeRepo);
        }

        // PRUEBA 1: Criterio de aceptación Dado / Cuando / Entonces
        [Fact]
        public void DadoUsuarioRegistrado_CuandoIngresaCredencialesValidas_EntoncesAutenticaConExito()
        {
            // Dado
            string usuarioValido = "camilo";
            string claveValida = "Password123!";
            _autenticacionService.RegistrarUsuario(usuarioValido, claveValida);

            // Cuando
            Colaborador? resultado = _autenticacionService.Autenticar(usuarioValido, claveValida);

            // Entonces
            Assert.NotNull(resultado);
            Assert.Equal(usuarioValido, resultado.Usuario, ignoreCase: true);
        }

        // PRUEBA 2: Contraseña incorrecta
        [Fact]
        public void Autenticar_ConContrasenaErronea_RetornaNull()
        {
            // Dado
            string usuario = "juan";
            _autenticacionService.RegistrarUsuario(usuario, "ClaveCorrecta123");

            // Cuando
            Colaborador? resultado = _autenticacionService.Autenticar(usuario, "ClaveErronea");

            // Entonces
            Assert.Null(resultado);
        }

        // PRUEBA 3: Usuario duplicado
        [Fact]
        public void RegistrarUsuario_ConUsuarioExistente_LanzaInvalidOperationException()
        {
            // Dado
            string usuarioExistente = "maria";
            _autenticacionService.RegistrarUsuario(usuarioExistente, "Clave123");

            // Cuando / Entonces
            Assert.Throws<InvalidOperationException>(() => 
                _autenticacionService.RegistrarUsuario(usuarioExistente, "OtraClave456")
            );
        }
    }
}