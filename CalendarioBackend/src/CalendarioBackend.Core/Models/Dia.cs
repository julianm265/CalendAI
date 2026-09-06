namespace CalendarioBackend.Core.Models;

/// <summary>
/// Representa un día del calendario. Mapea la clase "Dia" del diagrama: Fecha, L_eventos.
/// La asociación "Almacena" (Dia 1 -- 1..n Evento) se implementa como una lista interna
/// con métodos de alto nivel para agregar/eliminar/consultar eventos.
/// </summary>
public class Dia
{
    public DateOnly Fecha { get; }
    public DayOfWeek DiaSemana => Fecha.DayOfWeek;

    private readonly List<Evento> _eventos = new();
    public IReadOnlyList<Evento> LEventos => _eventos.AsReadOnly();

    public Dia(DateOnly fecha)
    {
        Fecha = fecha;
    }

    public Evento AgregarEvento(string nombreEvento, TimeOnly hora, string? lugar,
        string? descripcion = null, Guid? colaboradorOrganizadorId = null)
    {
        if (_eventos.Any(e => e.HoraEvento == hora))
            throw new InvalidOperationException(
                $"Ya existe un evento programado a las {hora:HH\\:mm} el {Fecha:dd/MM/yyyy}.");

        var evento = new Evento(nombreEvento, hora, lugar, descripcion, colaboradorOrganizadorId);
        _eventos.Add(evento);
        return evento;
    }

    public bool EliminarEvento(Guid eventoId) => _eventos.RemoveAll(e => e.Id == eventoId) > 0;

    internal void CargarEvento(Evento evento) => _eventos.Add(evento);

    public IEnumerable<Evento> ObtenerEventosOrdenados() => _eventos.OrderBy(e => e.HoraEvento);
}
