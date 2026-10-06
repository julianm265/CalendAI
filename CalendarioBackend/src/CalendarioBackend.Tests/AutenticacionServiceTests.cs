using System;
using System.Collections.Generic;
using CalendarioBackend.Core.Models;
using CalendarioBackend.Core.Repositories;
using CalendarioBackend.Core.Services;
using Xunit;

namespace CalendarioBackend.Tests
{
    public class FakeEquipoRepository : IEquipoRepository, IColaboradorRepository
    {
        private readonly List<Equipo> _equipos = new();
        private readonly List<Colaborador> _colaboradores = new();

        public IReadOnlyList<Equipo> ObtenerTodos() => _equipos.AsReadOnly();

        public Equipo? ObtenerPorId(Guid id) => _equipos.FirstOrDefault(equipo => equipo.Id == id);

        public Equipo? ObtenerPorNombre(string nombre) =>
            _equipos.FirstOrDefault(equipo => equipo.NombreEquipo.Equals(nombre, StringComparison.OrdinalIgnoreCase));

        public Equipo Agregar(Equipo equipo)
        {
            _equipos.Add(equipo);
            return equipo;
        }

        public Equipo AgregarEquipoConColaborador(Equipo equipo, Colaborador colaborador, string rol)
        {
            equipo.AgregarColaborador(colaborador, rol);
            return Agregar(equipo);
        }

        public void Guardar(Equipo equipo)
        {
            var indice = _equipos.FindIndex(actual => actual.Id == equipo.Id);
            if (indice < 0)
                _equipos.Add(equipo);
            else
                _equipos[indice] = equipo;
        }

        public void AgregarColaborador(Guid equipoId, Colaborador colaborador, string rol = "Miembro")
        {
            var equipo = ObtenerPorId(equipoId) ?? throw new KeyNotFoundException();
            if (equipo.BuscarColaborador(colaborador.Id) is null)
                equipo.AgregarColaborador(colaborador, rol);
            if (!_colaboradores.Any(actual => actual.Id == colaborador.Id))
                _colaboradores.Add(colaborador);
        }

        public void EliminarColaborador(Guid equipoId, Guid colaboradorId)
        {
            var equipo = ObtenerPorId(equipoId) ?? throw new KeyNotFoundException();
            equipo.EliminarColaborador(colaboradorId);
        }

        public Colaborador? ObtenerPorUsuario(string usuario) =>
            _colaboradores.FirstOrDefault(colaborador =>
                colaborador.Usuario.Equals(usuario, StringComparison.OrdinalIgnoreCase))
            ?? _equipos.Select(equipo => equipo.BuscarColaborador(usuario))
                .FirstOrDefault(colaborador => colaborador is not null);

        public bool ExistePorUsuario(string usuario) => ObtenerPorUsuario(usuario) is not null;

        public void Agregar(Colaborador colaborador, Guid? equipoId)
        {
            if (ExistePorUsuario(colaborador.Usuario))
                throw new InvalidOperationException($"El usuario '{colaborador.Usuario}' ya existe.");

            if (equipoId is not null)
            {
                var equipo = ObtenerPorId(equipoId.Value) ?? throw new KeyNotFoundException();
                equipo.AgregarColaborador(colaborador);
            }

            _colaboradores.Add(colaborador);
        }

        public void AsignarAEquipo(Guid colaboradorId, Guid equipoId)
        {
            var colaborador = _colaboradores.FirstOrDefault(item => item.Id == colaboradorId)
                ?? throw new KeyNotFoundException();
            var equipo = ObtenerPorId(equipoId) ?? throw new KeyNotFoundException();
            if (equipo.BuscarColaborador(colaboradorId) is null)
                equipo.AgregarColaborador(colaborador);
        }

        public void AgregarEvento(Guid equipoId, DateOnly fecha, Evento evento) => throw new NotImplementedException();

        public void EliminarEvento(Guid equipoId, Guid eventoId) => throw new NotImplementedException();

        public IReadOnlyList<LugarFrecuente> ObtenerLugaresFrecuentes(Guid equipoId, int limite) =>
            throw new NotImplementedException();

        public Equipo CrearCalendarioPersonal(Colaborador colaborador)
        {
            var equipo = new Equipo($"Calendario de {colaborador.Usuario}", true);
            equipo.AgregarColaborador(colaborador, "Miembro");
            return Agregar(equipo);
        }

        public void Actualizar(Equipo equipo) { }

        public bool Eliminar(Guid id) => _equipos.RemoveAll(equipo => equipo.Id == id) > 0;
    }

    public class AutenticacionServiceTests
    {
        private readonly AutenticacionService _autenticacionService;

        public AutenticacionServiceTests()
        {
            var fakeRepo = new FakeEquipoRepository();
            _autenticacionService = new AutenticacionService(fakeRepo, fakeRepo);
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

        [Fact]
        public void RegistrarUsuario_SinRepositorioDeColaboradores_LanzaInvalidOperationException()
        {
            var servicio = new AutenticacionService(new FakeEquipoRepository());

            Assert.Throws<InvalidOperationException>(() => servicio.RegistrarUsuario("camilo", "Password123!"));
        }

        [Fact]
        public void AgregarYQuitarMiembro_ConservaLaCuentaRegistrada()
        {
            var repository = new FakeEquipoRepository();
            var account = new Colaborador("julianm265", "ClaveSegura123");
            repository.Agregar(account, null);
            var team = repository.Agregar(new Equipo("Proyecto CalendAI"));
            var service = new EquipoService(repository, repository);

            service.AgregarMiembro(team.Id, account.Usuario);
            Assert.NotNull(team.BuscarColaborador(account.Id));

            service.EliminarColaborador(team.Id, account.Id);

            Assert.Null(team.BuscarColaborador(account.Id));
            Assert.NotNull(repository.ObtenerPorUsuario(account.Usuario));
        }

        [Fact]
        public void CrearEquipo_AsignaLiderAlCreadorYMiembroAlInvitado()
        {
            var repository = new FakeEquipoRepository();
            var leader = new Colaborador("diego", "ClaveSegura123");
            var member = new Colaborador("julianm265", "ClaveSegura456");
            repository.Agregar(leader, null);
            repository.Agregar(member, null);
            var service = new EquipoService(repository, repository);

            var team = service.CrearEquipo("Proyecto CalendAI", leader.Usuario);
            Assert.Single(team.LColaboradores);
            Assert.Same(leader, team.BuscarColaborador(leader.Id));
            Assert.Equal("Líder", team.ObtenerRolColaborador(leader.Id));

            service.AgregarMiembro(team.Id, member.Usuario);

            Assert.Equal("Líder", team.ObtenerRolColaborador(leader.Id));
            Assert.Equal("Miembro", team.ObtenerRolColaborador(member.Id));
        }

        [Fact]
        public void ListarEquipos_ExcluyeCalendariosPersonalesYFiltraPorUsuario()
        {
            var repository = new FakeEquipoRepository();
            var user = new Colaborador("diego", "ClaveSegura123");
            repository.Agregar(user, null);
            var personal = repository.CrearCalendarioPersonal(user);
            var team = repository.AgregarEquipoConColaborador(
                new Equipo("Proyecto CalendAI"),
                user,
                "Líder");
            var service = new EquipoService(repository, repository);

            var teams = service.ListarEquipos(user.Usuario);

            Assert.DoesNotContain(personal, teams);
            Assert.Contains(team, teams);
        }
    }
}