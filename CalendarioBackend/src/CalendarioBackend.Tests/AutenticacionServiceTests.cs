using CalendarioBackend.Core.Models;
using CalendarioBackend.Core.Repositories;
using CalendarioBackend.Core.Services;

namespace CalendarioBackend.Tests;

public class AutenticacionServiceTests
{
    private readonly InMemoryRepositorio _repositorio = new();
    private readonly AutenticacionService _autenticacion;

    public AutenticacionServiceTests()
    {
        _autenticacion = new AutenticacionService(_repositorio, _repositorio);
    }

    [Fact]
    public void DadoUsuarioRegistrado_CuandoIngresaCredencialesValidas_EntoncesAutenticaConExito()
    {
        _autenticacion.RegistrarUsuario("camilo", "Password123!");

        var resultado = _autenticacion.Autenticar("camilo", "Password123!");

        Assert.NotNull(resultado);
        Assert.Equal("camilo", resultado.Usuario, ignoreCase: true);
    }

    [Fact]
    public void Autenticar_NoDistingueMayusculasNiEspaciosEnElUsuario()
    {
        _autenticacion.RegistrarUsuario("Camilo", "Password123!");

        Assert.NotNull(_autenticacion.Autenticar("  camilo ", "Password123!"));
    }

    [Fact]
    public void Autenticar_ConContrasenaErronea_RetornaNull()
    {
        _autenticacion.RegistrarUsuario("juan", "ClaveCorrecta123");

        Assert.Null(_autenticacion.Autenticar("juan", "ClaveErronea"));
    }

    [Fact]
    public void Autenticar_ConUsuarioInexistente_RetornaNull()
    {
        Assert.Null(_autenticacion.Autenticar("nadie", "Password123!"));
    }

    [Fact]
    public void RegistrarUsuario_ConUsuarioExistente_LanzaInvalidOperationException()
    {
        _autenticacion.RegistrarUsuario("maria", "Clave123");

        Assert.Throws<InvalidOperationException>(() => _autenticacion.RegistrarUsuario("MARIA", "OtraClave456"));
    }

    [Theory]
    [InlineData("ab", "Clave123")]
    [InlineData("con espacios", "Clave123")]
    [InlineData("usuario", "123")]
    public void RegistrarUsuario_ConDatosInvalidos_LanzaArgumentException(string usuario, string contraseña)
    {
        Assert.Throws<ArgumentException>(() => _autenticacion.RegistrarUsuario(usuario, contraseña));
    }

    [Fact]
    public void RegistrarUsuario_CreaElCalendarioPersonalConElUsuarioComoLider()
    {
        var colaborador = _autenticacion.RegistrarUsuario("ana", "Clave123");

        var personal = _repositorio.ObtenerPersonal(colaborador.Id);

        Assert.NotNull(personal);
        Assert.True(personal.EsPersonal);
        Assert.Equal(RolEquipo.Lider, _repositorio.ObtenerRol(personal.Id, colaborador.Id));
    }

    [Fact]
    public void ObtenerOCrearCalendarioPersonal_NoCreaUnSegundoCalendario()
    {
        var colaborador = _autenticacion.RegistrarUsuario("ana", "Clave123");

        var primero = _autenticacion.ObtenerOCrearCalendarioPersonal(colaborador);
        var segundo = _autenticacion.ObtenerOCrearCalendarioPersonal(colaborador);

        Assert.Equal(primero.Id, segundo.Id);
    }

    [Fact]
    public void LaContrasenaSeGuardaConSalYNuncaEnTextoPlano()
    {
        var uno = new Colaborador("ana", "Clave123");
        var dos = new Colaborador("bea", "Clave123");

        Assert.NotEqual(uno.ObtenerContraseñaHash(), dos.ObtenerContraseñaHash());
        Assert.DoesNotContain("Clave123", uno.ObtenerContraseñaHash());
        Assert.False(uno.NecesitaRehash);
    }

    [Fact]
    public void UnHashAntiguoSha256SigueSiendoValidoYSeMigra()
    {
        // SHA-256 hexadecimal de "Clave123" (formato de la versión anterior).
        var hashAntiguo = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("Clave123")));
        var legado = new Colaborador(Guid.NewGuid(), "viejo", hashAntiguo);
        _repositorio.Agregar(legado);

        Assert.True(legado.NecesitaRehash);
        var autenticado = _autenticacion.Autenticar("viejo", "Clave123");

        Assert.NotNull(autenticado);
        Assert.False(autenticado.NecesitaRehash);
        Assert.NotNull(_autenticacion.Autenticar("viejo", "Clave123")); // sigue entrando tras migrar
        Assert.Null(_autenticacion.Autenticar("viejo", "otra"));
    }
}
