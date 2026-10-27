using System.Security.Cryptography;
using System.Text;

namespace CalendarioBackend.Core.Models;

/// <summary>
/// Representa un miembro de un equipo. Mapea la clase "Colaborador" del diagrama:
/// Usuario, Contraseña. Por buenas prácticas de un backend real, la contraseña nunca
/// se guarda en texto plano: se almacena su hash SHA-256.
/// </summary>
public class Colaborador
{
    private const int IteracionesHash = 210_000;
    private const int LongitudSalt = 16;
    private const int LongitudHash = 32;

    public Guid Id { get; }
    public string Usuario { get; private set; }
    private string ContraseñaHash { get; set; }

    public Colaborador(string usuario, string contraseña)
    {
        if (string.IsNullOrWhiteSpace(usuario))
            throw new ArgumentException("El usuario no puede estar vacío.", nameof(usuario));
        if (string.IsNullOrWhiteSpace(contraseña) || contraseña.Length < 6)
            throw new ArgumentException("La contraseña debe tener al menos 6 caracteres.", nameof(contraseña));

        Id = Guid.NewGuid();
        Usuario = usuario;
        ContraseñaHash = Hash(contraseña);
    }

    internal Colaborador(Guid id, string usuario, string contraseñaHash)
    {
        Id = id;
        Usuario = usuario;
        ContraseñaHash = contraseñaHash;
    }

    public bool ValidarContraseña(string intento)
    {
        if (ContraseñaHash.StartsWith("v2.", StringComparison.Ordinal))
            return ValidarHashDerivado(intento, ContraseñaHash);

        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(ContraseñaHash),
                SHA256.HashData(Encoding.UTF8.GetBytes(intento)));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    internal string ObtenerContraseñaHash() => ContraseñaHash;

    public void CambiarContraseña(string actual, string nueva)
    {
        if (!ValidarContraseña(actual))
            throw new UnauthorizedAccessException("La contraseña actual es incorrecta.");
        if (string.IsNullOrWhiteSpace(nueva) || nueva.Length < 6)
            throw new ArgumentException("La nueva contraseña debe tener al menos 6 caracteres.", nameof(nueva));

        ContraseñaHash = Hash(nueva);
    }

    public void CambiarUsuario(string nuevoUsuario)
    {
        if (string.IsNullOrWhiteSpace(nuevoUsuario))
            throw new ArgumentException("El usuario no puede estar vacío.", nameof(nuevoUsuario));

        Usuario = nuevoUsuario;
    }

    private static string Hash(string texto)
    {
        var salt = RandomNumberGenerator.GetBytes(LongitudSalt);
        var derivado = Rfc2898DeriveBytes.Pbkdf2(
            texto,
            salt,
            IteracionesHash,
            HashAlgorithmName.SHA256,
            LongitudHash);
        return $"v2.{IteracionesHash}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(derivado)}";
    }

    private static bool ValidarHashDerivado(string intento, string hashPersistido)
    {
        var partes = hashPersistido.Split('.');
        if (partes.Length != 4 ||
            !int.TryParse(partes[1], out var iteraciones) ||
            iteraciones <= 0)
            return false;

        try
        {
            var salt = Convert.FromBase64String(partes[2]);
            var hashEsperado = Convert.FromBase64String(partes[3]);
            var hashCalculado = Rfc2898DeriveBytes.Pbkdf2(
                intento,
                salt,
                iteraciones,
                HashAlgorithmName.SHA256,
                hashEsperado.Length);
            return CryptographicOperations.FixedTimeEquals(hashCalculado, hashEsperado);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public override string ToString() => Usuario;
}
