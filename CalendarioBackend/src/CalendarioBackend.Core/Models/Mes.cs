using System.Globalization;

namespace CalendarioBackend.Core.Models;

/// <summary>
/// Representa un mes del año. Mapea la clase "Mes" del diagrama: Num_Mes, L_Semana, Treinta_y_uno.
/// Al construirse genera automáticamente sus semanas y días reales según el calendario gregoriano
/// (asociación Mes 1 -- 1..12 dentro de Año, y Mes -- Semana).
/// </summary>
public class Mes
{
    private static readonly CultureInfo CulturaEs = new("es-ES");

    public byte NumMes { get; }
    public int NumAño { get; }
    public string NombreMes => new DateTime(NumAño, NumMes, 1).ToString("MMMM", CulturaEs);

    private readonly List<Semana> _semanas = new();
    public IReadOnlyList<Semana> LSemana => _semanas.AsReadOnly();

    internal Mes(int numAño, byte numMes)
    {
        if (numMes is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(numMes), "El mes debe estar entre 1 y 12.");

        NumAño = numAño;
        NumMes = numMes;

        int diasEnMes = DateTime.DaysInMonth(numAño, numMes);
        
        GenerarSemanas(diasEnMes);
    }

    /// <summary>
    /// Divide el mes en semanas reales (lunes a domingo, recortadas en los extremos del mes)
    /// y crea un objeto Dia por cada fecha, cumpliendo la asociación Semana 1 -- 1..n Dia.
    /// </summary>
    private void GenerarSemanas(int diasEnMes)
    {
        var primerDia = new DateOnly(NumAño, NumMes, 1);
        var ultimoDia = new DateOnly(NumAño, NumMes, diasEnMes);
        var cursor = primerDia;

        while (cursor <= ultimoDia)
        {
            // DayOfWeek: Sunday = 0 ... Saturday = 6. Se arma la semana de lunes a domingo.
            int diasHastaFinDeSemana = ((int)DayOfWeek.Sunday - (int)cursor.DayOfWeek + 7) % 7;
            var finSemanaTentativo = cursor.AddDays(diasHastaFinDeSemana);
            var finSemana = finSemanaTentativo > ultimoDia ? ultimoDia : finSemanaTentativo;

            var semana = new Semana(cursor, finSemana);
            for (var fecha = cursor; fecha <= finSemana; fecha = fecha.AddDays(1))
                semana.AgregarDia(new Dia(fecha));

            _semanas.Add(semana);
            cursor = finSemana.AddDays(1);
        }
    }

    public Dia? BuscarDia(DateOnly fecha) =>
        _semanas.SelectMany(s => s.LDias).FirstOrDefault(d => d.Fecha == fecha);
}
