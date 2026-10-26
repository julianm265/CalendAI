namespace CalendarioBackend.Core.Models;

/// <summary>
/// Representa un evento puntual dentro de un día del calendario.
/// Mapea la clase "Evento" del diagrama: Nombre_evento, Hora_Evento, Lugar_Evento.
/// </summary>
public class Evento
{
    public Guid Id { get; }
    public string NombreEvento { get; set; }
    public TimeOnly HoraEvento { get; set; }
    public string? LugarEvento { get; set; }

    /// <summary>Campos adicionales útiles para un backend real, no rompen el modelo del diagrama.</summary>
    public string? Descripcion { get; set; }
    public Guid? ColaboradorOrganizadorId { get; set; }

    public const int MaxNombre = 160;
    public const int MaxLugar = 200;
    public const int MaxDescripcion = 500;

    public Evento(string nombreEvento, TimeOnly horaEvento, string? lugarEvento = null,
        string? descripcion = null, Guid? colaboradorOrganizadorId = null)
    {
        var nombre = nombreEvento?.Trim() ?? string.Empty;
        if (nombre.Length == 0)
            throw new ArgumentException("El nombre del evento no puede estar vacío.", nameof(nombreEvento));
        if (nombre.Length > MaxNombre)
            throw new ArgumentException($"El nombre del evento no puede superar {MaxNombre} caracteres.", nameof(nombreEvento));

        Id = Guid.NewGuid();
        NombreEvento = nombre;
        HoraEvento = horaEvento;
        LugarEvento = Limitar(lugarEvento, MaxLugar, "El lugar");
        Descripcion = Limitar(descripcion, MaxDescripcion, "La descripción");
        ColaboradorOrganizadorId = colaboradorOrganizadorId;
    }

    private static string? Limitar(string? valor, int maximo, string campo)
    {
        var limpio = valor?.Trim();
        if (string.IsNullOrEmpty(limpio)) return null;
        if (limpio.Length > maximo)
            throw new ArgumentException($"{campo} no puede superar {maximo} caracteres.");
        return limpio;
    }

    internal Evento(Guid id, string nombreEvento, TimeOnly horaEvento, string? lugarEvento,
        string? descripcion, Guid? colaboradorOrganizadorId)
    {
        Id = id;
        NombreEvento = nombreEvento;
        HoraEvento = horaEvento;
        LugarEvento = lugarEvento;
        Descripcion = descripcion;
        ColaboradorOrganizadorId = colaboradorOrganizadorId;
    }

    public override string ToString() => $"{HoraEvento:HH\\:mm} - {NombreEvento} ({LugarEvento})";
}
