namespace CalendarioBackend.Core.Models;

/// <summary>
/// Representa un año calendario completo. Mapea la clase "Año" del diagrama:
/// Num_año, Es_bisiesto, l_meses. Al construirse genera automáticamente sus 12 meses
/// (asociación Año 1 -- 1..12 Mes).
/// </summary>
public class Anio
{
    public ushort NumAño { get; }
    public bool EsBisiesto { get; }

    private readonly List<Mes> _meses = new();
    public IReadOnlyList<Mes> LMeses => _meses.AsReadOnly();

    public Anio(ushort numAño)
    {
        if (numAño < 1)
            throw new ArgumentOutOfRangeException(nameof(numAño), "El año debe ser un valor positivo.");

        NumAño = numAño;
        EsBisiesto = DateTime.IsLeapYear(numAño);

        for (byte mes = 1; mes <= 12; mes++)
            _meses.Add(new Mes(numAño, mes));
    }

    public Mes ObtenerMes(byte numMes)
    {
        if (numMes is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(numMes), "El mes debe estar entre 1 y 12.");

        return _meses[numMes - 1];
    }

    public Dia? BuscarDia(DateOnly fecha)
    {
        if (fecha.Year != NumAño) return null;
        return ObtenerMes((byte)fecha.Month).BuscarDia(fecha);
    }
}
