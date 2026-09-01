namespace CalendarioBackend.Core.Models;

/// <summary>
/// Raíz del árbol del calendario. Mapea la clase "Calendario" del diagrama: l_años.
/// Los años se crean de forma perezosa (solo cuando se necesitan), evitando generar
/// estructuras completas para rangos de fechas que nunca se usan.
/// </summary>
public class Calendario
{
    public Guid Id { get; }

    private readonly List<Anio> _años = new();
    public IReadOnlyList<Anio> LAños => _años.AsReadOnly();

    public Calendario()
    {
        Id = Guid.NewGuid();
    }

    /// <summary>Obtiene el año solicitado, generándolo (con sus meses, semanas y días) si aún no existe.</summary>
    public Anio ObtenerOCrearAño(int numAño)
    {
        var año = _años.FirstOrDefault(a => a.NumAño == numAño);
        if (año is null)
        {
            año = new Anio((ushort)numAño);
            _años.Add(año);
            _años.Sort((a, b) => a.NumAño.CompareTo(b.NumAño));
        }

        return año;
    }

    public Dia? BuscarDia(DateOnly fecha)
    {
        var año = _años.FirstOrDefault(a => a.NumAño == fecha.Year);
        return año?.BuscarDia(fecha);
    }
}
