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

    public bool ValidarContraseña(string intento) => ContraseñaHash == Hash(intento);

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
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(texto));
        return Convert.ToHexString(bytes);
    }

    public override string ToString() => Usuario;
}
