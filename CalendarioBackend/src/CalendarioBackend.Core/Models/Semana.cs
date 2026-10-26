namespace CalendarioBackend.Core.Models;

/// <summary>
/// Representa una semana calendario dentro de un mes.
/// Mapea la clase "Semana" del diagrama: Fecha_inicio, Fecha_final, L_dias.
/// Se construye automáticamente desde <see cref="Mes"/>, respetando la asociación 1 -- 1..n con Dia.
/// </summary>
public class Semana
{
    public DateOnly FechaInicio { get; }
    public DateOnly FechaFinal { get; }

    private readonly List<Dia> _dias = new();
    public IReadOnlyList<Dia> LDias => _dias.AsReadOnly();

    internal Semana(DateOnly fechaInicio, DateOnly fechaFinal)
    {
        if (fechaFinal < fechaInicio)
            throw new ArgumentException("La fecha final no puede ser anterior a la fecha de inicio.");

        FechaInicio = fechaInicio;
        FechaFinal = fechaFinal;
    }

    internal void AgregarDia(Dia dia) => _dias.Add(dia);

    public Dia? BuscarDia(DateOnly fecha) => _dias.FirstOrDefault(d => d.Fecha == fecha);
}
