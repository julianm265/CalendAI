namespace CalendarioBackend.Core.Models;

/// <summary>Rol de un usuario dentro de un equipo. Cada equipo tiene exactamente un líder.</summary>
public enum RolEquipo
{
    Miembro = 0,
    Lider = 1
}

public static class RolEquipoExtensiones
{
    public static string ATexto(this RolEquipo rol) => rol == RolEquipo.Lider ? "lider" : "miembro";

    public static RolEquipo DesdeTexto(string? texto) =>
        string.Equals(texto, "lider", StringComparison.OrdinalIgnoreCase) ? RolEquipo.Lider : RolEquipo.Miembro;
}

/// <summary>Un usuario dentro de un equipo, con su rol.</summary>
public sealed record MiembroEquipo(Guid ColaboradorId, string Usuario, RolEquipo Rol, DateTime UnidoEn);

/// <summary>Un equipo visto desde un usuario: su rol allí y cuántos miembros tiene.</summary>
public sealed record EquipoDelUsuario(Equipo Equipo, RolEquipo Rol, int Miembros);
