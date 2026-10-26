using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace CalendarioBackend.Core.Models;

/// <summary>
/// Usuario de la aplicación. Mapea la clase "Colaborador" del diagrama: Usuario, Contraseña.
/// La contraseña nunca se guarda en texto plano: se almacena un hash PBKDF2-SHA256 con sal aleatoria
/// ("v2.iteraciones.sal.hash"). Los hashes SHA-256 sin sal de versiones anteriores siguen siendo válidos
/// y se migran al formato nuevo en el siguiente inicio de sesión.
/// </summary>
public class Colaborador
{
    public const int MinUsuario = 3;
    public const int MaxUsuario = 50;
    public const int MinContraseña = 6;
    public const int MaxContraseña = 128;

    private const string Prefijo = "v2";
    private const int Iteraciones = 210_000;
    private static readonly Regex UsuarioValido = new(@"^[\p{L}\p{N}._\-]+$", RegexOptions.Compiled);

    public Guid Id { get; }
    public string Usuario { get; private set; }
    private string ContraseñaHash { get; set; }

    public Colaborador(string usuario, string contraseña)
    {
        Usuario = NormalizarUsuario(usuario);
        ValidarContraseñaNueva(contraseña, nameof(contraseña));

        Id = Guid.NewGuid();
        ContraseñaHash = Hash(contraseña);
    }

    internal Colaborador(Guid id, string usuario, string contraseñaHash)
    {
        Id = id;
        Usuario = usuario;
        ContraseñaHash = contraseñaHash;
    }

    /// <summary>Recorta espacios y valida longitud y caracteres permitidos del nombre de usuario.</summary>
    public static string NormalizarUsuario(string? usuario)
    {
        var limpio = usuario?.Trim() ?? string.Empty;
        if (limpio.Length == 0)
            throw new ArgumentException("El usuario no puede estar vacío.", nameof(usuario));
        if (limpio.Length < MinUsuario || limpio.Length > MaxUsuario)
            throw new ArgumentException($"El usuario debe tener entre {MinUsuario} y {MaxUsuario} caracteres.", nameof(usuario));
        if (!UsuarioValido.IsMatch(limpio))
            throw new ArgumentException("El usuario solo puede contener letras, números, puntos, guiones y guiones bajos.", nameof(usuario));

        return limpio;
    }

    public bool ValidarContraseña(string intento)
    {
        if (string.IsNullOrEmpty(intento) || intento.Length > MaxContraseña) return false;

        var partes = ContraseñaHash.Split('.');
        if (partes.Length == 4 && partes[0] == Prefijo)
        {
            try
            {
                var iteraciones = int.Parse(partes[1]);
                var sal = Convert.FromBase64String(partes[2]);
                var esperado = Convert.FromBase64String(partes[3]);
                var calculado = Rfc2898DeriveBytes.Pbkdf2(intento, sal, iteraciones, HashAlgorithmName.SHA256, esperado.Length);
                return CryptographicOperations.FixedTimeEquals(calculado, esperado);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        // Formato anterior: SHA-256 hexadecimal sin sal.
        var legado = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(intento)));
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(legado),
            Encoding.UTF8.GetBytes(ContraseñaHash.ToUpperInvariant()));
    }

    /// <summary>True si el hash guardado es del formato antiguo y conviene regenerarlo.</summary>
    public bool NecesitaRehash => !ContraseñaHash.StartsWith(Prefijo + ".", StringComparison.Ordinal);

    /// <summary>Regenera el hash con el formato actual. Solo se llama tras validar la contraseña.</summary>
    public void Rehash(string contraseña) => ContraseñaHash = Hash(contraseña);

    internal string ObtenerContraseñaHash() => ContraseñaHash;

    public void CambiarContraseña(string actual, string nueva)
    {
        if (!ValidarContraseña(actual))
            throw new UnauthorizedAccessException("La contraseña actual es incorrecta.");
        ValidarContraseñaNueva(nueva, nameof(nueva));

        ContraseñaHash = Hash(nueva);
    }

    public void CambiarUsuario(string nuevoUsuario) => Usuario = NormalizarUsuario(nuevoUsuario);

    private static void ValidarContraseñaNueva(string? contraseña, string parametro)
    {
        if (string.IsNullOrWhiteSpace(contraseña) || contraseña.Length < MinContraseña)
            throw new ArgumentException($"La contraseña debe tener al menos {MinContraseña} caracteres.", parametro);
        if (contraseña.Length > MaxContraseña)
            throw new ArgumentException($"La contraseña no puede superar {MaxContraseña} caracteres.", parametro);
    }

    private static string Hash(string texto)
    {
        var sal = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(texto, sal, Iteraciones, HashAlgorithmName.SHA256, 32);
        return $"{Prefijo}.{Iteraciones}.{Convert.ToBase64String(sal)}.{Convert.ToBase64String(hash)}";
    }

    public override string ToString() => Usuario;
}
