using CalendarioBackend.Core.Models;
using CalendarioBackend.Core.Repositories;
using CalendarioBackend.Core.Services;

namespace CalendarioBackend.Tests;

public class EquipoServiceTests
{
    private readonly InMemoryRepositorio _repo = new();
    private readonly AutenticacionService _auth;
    private readonly EquipoService _equipos;
    private readonly CalendarioService _calendario;
    private readonly Colaborador _ana;
    private readonly Colaborador _luis;
    private readonly Colaborador _eva;

    public EquipoServiceTests()
    {
        var acceso = new AccesoEquipoService(_repo);
        _auth = new AutenticacionService(_repo, _repo);
        _equipos = new EquipoService(_repo, _repo, _repo, acceso);
        _calendario = new CalendarioService(_repo, acceso);
        _ana = _auth.RegistrarUsuario("ana", "Clave123");
        _luis = _auth.RegistrarUsuario("luis", "Clave123");
        _eva = _auth.RegistrarUsuario("eva", "Clave123");
    }

    private Equipo EquipoConLuis()
    {
        var equipo = _equipos.CrearEquipo(_ana.Id, "Ventas");
        var invitacion = _equipos.Invitar(equipo.Id, _ana.Id, "luis");
        _equipos.ResponderInvitacion(invitacion.Id, _luis.Id, aceptar: true);
        return equipo;
    }

    [Fact]
    public void QuienCreaUnEquipoQuedaComoLider()
    {
        var equipo = _equipos.CrearEquipo(_ana.Id, "Ventas");

        Assert.Equal(RolEquipo.Lider, _repo.ObtenerRol(equipo.Id, _ana.Id));
        Assert.False(equipo.EsPersonal);
    }

    [Fact]
    public void UnUsuarioTieneSuCalendarioPersonalYPuedePertenecerAVariosEquipos()
    {
        _equipos.CrearEquipo(_ana.Id, "Ventas");
        var deLuis = _equipos.CrearEquipo(_luis.Id, "Soporte");
        var invitacion = _equipos.Invitar(deLuis.Id, _luis.Id, "ana");
        _equipos.ResponderInvitacion(invitacion.Id, _ana.Id, aceptar: true);

        var calendariosDeAna = _equipos.ListarEquiposDe(_ana.Id);

        Assert.Equal(3, calendariosDeAna.Count); // personal + Ventas (líder) + Soporte (miembro)
        Assert.True(calendariosDeAna[0].Equipo.EsPersonal);
        Assert.Equal(RolEquipo.Lider, calendariosDeAna.Single(c => c.Equipo.NombreEquipo == "Ventas").Rol);
        Assert.Equal(RolEquipo.Miembro, calendariosDeAna.Single(c => c.Equipo.NombreEquipo == "Soporte").Rol);
    }

    [Fact]
    public void SoloElLiderPuedeInvitar()
    {
        var equipo = EquipoConLuis();

        Assert.Throws<UnauthorizedAccessException>(() => _equipos.Invitar(equipo.Id, _luis.Id, "eva"));
    }

    [Fact]
    public void NoSePuedeInvitarDosVecesAlMismoUsuarioNiAUnMiembroNiAUnInexistente()
    {
        var equipo = _equipos.CrearEquipo(_ana.Id, "Ventas");
        _equipos.Invitar(equipo.Id, _ana.Id, "eva");

        Assert.Throws<InvalidOperationException>(() => _equipos.Invitar(equipo.Id, _ana.Id, "EVA"));
        Assert.Throws<InvalidOperationException>(() => _equipos.Invitar(equipo.Id, _ana.Id, "ana"));
        Assert.Throws<KeyNotFoundException>(() => _equipos.Invitar(equipo.Id, _ana.Id, "nadie"));
    }

    [Fact]
    public void NoSePuedeInvitarAUnCalendarioPersonal()
    {
        var personal = _auth.ObtenerOCrearCalendarioPersonal(_ana);

        Assert.Throws<InvalidOperationException>(() => _equipos.Invitar(personal.Id, _ana.Id, "luis"));
    }

    [Fact]
    public void SoloElInvitadoPuedeResponderYAlAceptarPasaASerMiembro()
    {
        var equipo = _equipos.CrearEquipo(_ana.Id, "Ventas");
        var invitacion = _equipos.Invitar(equipo.Id, _ana.Id, "luis");

        Assert.Single(_equipos.ListarInvitacionesRecibidas(_luis.Id));
        Assert.Throws<KeyNotFoundException>(() => _equipos.ResponderInvitacion(invitacion.Id, _eva.Id, aceptar: true));

        _equipos.ResponderInvitacion(invitacion.Id, _luis.Id, aceptar: true);

        Assert.Equal(RolEquipo.Miembro, _repo.ObtenerRol(equipo.Id, _luis.Id));
        Assert.Empty(_equipos.ListarInvitacionesRecibidas(_luis.Id));
        Assert.Throws<InvalidOperationException>(() => _equipos.ResponderInvitacion(invitacion.Id, _luis.Id, aceptar: true));
    }

    [Fact]
    public void AlRechazarOCancelarNoSeAgregaAlMiembro()
    {
        var equipo = _equipos.CrearEquipo(_ana.Id, "Ventas");
        var rechazada = _equipos.Invitar(equipo.Id, _ana.Id, "luis");
        _equipos.ResponderInvitacion(rechazada.Id, _luis.Id, aceptar: false);
        var cancelada = _equipos.Invitar(equipo.Id, _ana.Id, "eva");
        _equipos.CancelarInvitacion(equipo.Id, cancelada.Id, _ana.Id);

        Assert.Null(_repo.ObtenerRol(equipo.Id, _luis.Id));
        Assert.Null(_repo.ObtenerRol(equipo.Id, _eva.Id));
        Assert.Empty(_equipos.ListarInvitacionesDeEquipo(equipo.Id, _ana.Id));
    }

    [Fact]
    public void UnAjenoRecibeNoEncontradoYNoPuedeVerElEquipo()
    {
        var equipo = _equipos.CrearEquipo(_ana.Id, "Ventas");

        Assert.Throws<KeyNotFoundException>(() => _equipos.ObtenerDetalle(equipo.Id, _eva.Id));
        Assert.Throws<KeyNotFoundException>(() =>
            _calendario.ObtenerEventosDelDia(equipo.Id, _eva.Id, new DateOnly(2026, 10, 5)));
    }

    [Fact]
    public void ElLiderNoPuedeSalirSinCederElLiderazgo()
    {
        var equipo = EquipoConLuis();

        Assert.Throws<InvalidOperationException>(() => _equipos.EliminarMiembro(equipo.Id, _ana.Id, _ana.Id));

        _equipos.TransferirLiderazgo(equipo.Id, _ana.Id, _luis.Id);
        Assert.Equal(RolEquipo.Lider, _repo.ObtenerRol(equipo.Id, _luis.Id));
        Assert.Equal(RolEquipo.Miembro, _repo.ObtenerRol(equipo.Id, _ana.Id));

        _equipos.EliminarMiembro(equipo.Id, _ana.Id, _ana.Id); // ahora puede salir
        Assert.Null(_repo.ObtenerRol(equipo.Id, _ana.Id));
    }

    [Fact]
    public void UnMiembroPuedeSalirPeroNoExpulsarAOtros()
    {
        var equipo = EquipoConLuis();
        var invitacion = _equipos.Invitar(equipo.Id, _ana.Id, "eva");
        _equipos.ResponderInvitacion(invitacion.Id, _eva.Id, aceptar: true);

        Assert.Throws<UnauthorizedAccessException>(() => _equipos.EliminarMiembro(equipo.Id, _luis.Id, _eva.Id));

        _equipos.EliminarMiembro(equipo.Id, _ana.Id, _eva.Id); // el líder expulsa
        _equipos.EliminarMiembro(equipo.Id, _luis.Id, _luis.Id); // un miembro sale por su cuenta

        Assert.Single(_repo.ObtenerMiembros(equipo.Id));
    }

    [Fact]
    public void SoloElLiderRenombraYEliminaYElPersonalNoSePuedeTocar()
    {
        var equipo = EquipoConLuis();

        Assert.Throws<UnauthorizedAccessException>(() => _equipos.Renombrar(equipo.Id, _luis.Id, "Otro"));
        Assert.Throws<UnauthorizedAccessException>(() => _equipos.EliminarEquipo(equipo.Id, _luis.Id));

        Assert.Equal("Comercial", _equipos.Renombrar(equipo.Id, _ana.Id, "Comercial").NombreEquipo);

        var personal = _auth.ObtenerOCrearCalendarioPersonal(_ana);
        Assert.Throws<InvalidOperationException>(() => _equipos.Renombrar(personal.Id, _ana.Id, "Mío"));
        Assert.Throws<InvalidOperationException>(() => _equipos.EliminarEquipo(personal.Id, _ana.Id));

        _equipos.EliminarEquipo(equipo.Id, _ana.Id);
        Assert.Null(_repo.ObtenerRol(equipo.Id, _luis.Id));
    }

    [Fact]
    public void NoSePuedeCrearDosEquiposConElMismoNombreSiendoLider()
    {
        _equipos.CrearEquipo(_ana.Id, "Ventas");

        Assert.Throws<InvalidOperationException>(() => _equipos.CrearEquipo(_ana.Id, " ventas "));
        _equipos.CrearEquipo(_luis.Id, "Ventas"); // otro usuario sí puede
    }

    // ------------------------------------------------------------ eventos

    [Fact]
    public void ElOrganizadorPorDefectoEsQuienCreaElEventoYSoloElLiderOElOrganizadorLoEliminan()
    {
        var equipo = EquipoConLuis();
        var evento = _calendario.AgregarEvento(equipo.Id, _luis.Id, new DateOnly(2026, 10, 6), "Demo", new TimeOnly(10, 0), "Sala 1");

        Assert.Equal(_luis.Id, evento.ColaboradorOrganizadorId);

        var invitacion = _equipos.Invitar(equipo.Id, _ana.Id, "eva");
        _equipos.ResponderInvitacion(invitacion.Id, _eva.Id, aceptar: true);
        Assert.Throws<UnauthorizedAccessException>(() => _calendario.EliminarEvento(equipo.Id, _eva.Id, evento.Id));

        Assert.True(_calendario.EliminarEvento(equipo.Id, _ana.Id, evento.Id)); // líder
    }

    [Fact]
    public void NoSePermitenDosEventosALaMismaHoraNiOrganizadoresAjenosNiFechasFueraDeRango()
    {
        var equipo = EquipoConLuis();
        var fecha = new DateOnly(2026, 10, 6);
        _calendario.AgregarEvento(equipo.Id, _ana.Id, fecha, "Uno", new TimeOnly(9, 0), "Sala");

        Assert.Throws<InvalidOperationException>(() =>
            _calendario.AgregarEvento(equipo.Id, _luis.Id, fecha, "Dos", new TimeOnly(9, 0), "Sala"));
        Assert.Throws<InvalidOperationException>(() =>
            _calendario.AgregarEvento(equipo.Id, _ana.Id, fecha, "Tres", new TimeOnly(11, 0), "Sala", null, _eva.Id));
        Assert.Throws<ArgumentException>(() =>
            _calendario.AgregarEvento(equipo.Id, _ana.Id, new DateOnly(1500, 1, 1), "Viejo", new TimeOnly(9, 0), "Sala"));
        Assert.Throws<ArgumentException>(() =>
            _calendario.ObtenerEventosDelMes(equipo.Id, _ana.Id, 2026, 13));
        Assert.Throws<ArgumentException>(() =>
            _calendario.AgregarEvento(equipo.Id, _ana.Id, fecha, new string('x', 200), new TimeOnly(12, 0), "Sala"));
    }

    [Fact]
    public void LosEventosDelMesSoloIncluyenElMesPedido()
    {
        var equipo = _equipos.CrearEquipo(_ana.Id, "Ventas");
        _calendario.AgregarEvento(equipo.Id, _ana.Id, new DateOnly(2026, 10, 31), "Fin de mes", new TimeOnly(9, 0), "A");
        _calendario.AgregarEvento(equipo.Id, _ana.Id, new DateOnly(2026, 11, 1), "Inicio de mes", new TimeOnly(9, 0), "B");

        var octubre = _calendario.ObtenerEventosDelMes(equipo.Id, _ana.Id, 2026, 10);

        Assert.Equal("Fin de mes", Assert.Single(octubre).Evento.NombreEvento);
    }
}
