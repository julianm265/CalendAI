namespace CalendarioBackend.Core.Models;

/// <summary>
/// Un calendario compartido. Puede ser el calendario personal de un usuario (<see cref="EsPersonal"/>)
/// o un equipo/empresa con varios miembros. Quién pertenece al equipo y con qué rol se maneja con
/// <see cref="MiembroEquipo"/>; el creador del equipo es su líder.
/// </summary>
public class Equipo
{
    public const int MaxNombre = 120;

    public Guid Id { get; }
    public string NombreEquipo { get; private set; }
    public Calendario Calendario { get; }
    public bool EsPersonal { get; }
    public Guid? CreadoPorId { get; }

    public Equipo(string nombreEquipo, bool esPersonal = false, Guid? creadoPorId = null)
    {
        NombreEquipo = NormalizarNombre(nombreEquipo);
        Id = Guid.NewGuid();
        Calendario = new Calendario();
        EsPersonal = esPersonal;
        CreadoPorId = creadoPorId;
    }

    internal Equipo(Guid id, string nombreEquipo, Calendario calendario, bool esPersonal, Guid? creadoPorId)
    {
        Id = id;
        NombreEquipo = nombreEquipo;
        Calendario = calendario;
        EsPersonal = esPersonal;
        CreadoPorId = creadoPorId;
    }

    public void Renombrar(string nuevoNombre)
    {
        if (EsPersonal)
            throw new InvalidOperationException("El calendario personal no se puede renombrar.");

        NombreEquipo = NormalizarNombre(nuevoNombre);
    }

    public static string NormalizarNombre(string? nombre)
    {
        var limpio = nombre?.Trim() ?? string.Empty;
        if (limpio.Length == 0)
            throw new ArgumentException("El nombre del equipo no puede estar vacío.", nameof(nombre));
        if (limpio.Length < 2)
            throw new ArgumentException("El nombre del equipo debe tener al menos 2 caracteres.", nameof(nombre));
        if (limpio.Length > MaxNombre)
            throw new ArgumentException($"El nombre del equipo no puede superar {MaxNombre} caracteres.", nameof(nombre));

        return limpio;
    }
}
