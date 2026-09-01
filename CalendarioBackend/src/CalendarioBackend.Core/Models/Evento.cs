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

    public Evento(string nombreEvento, TimeOnly horaEvento, string? lugarEvento = null,
        string? descripcion = null, Guid? colaboradorOrganizadorId = null)
    {
        if (string.IsNullOrWhiteSpace(nombreEvento))
            throw new ArgumentException("El nombre del evento no puede estar vacío.", nameof(nombreEvento));
        Id = Guid.NewGuid();
        NombreEvento = nombreEvento;
        HoraEvento = horaEvento;
        LugarEvento = lugarEvento;
        Descripcion = descripcion;
        ColaboradorOrganizadorId = colaboradorOrganizadorId;
    }

    public override string ToString() => $"{HoraEvento:HH\\:mm} - {NombreEvento} ({LugarEvento})";
}
