namespace CalendarioBackend.Core.Models;

/// <summary>
/// Representa un equipo de trabajo. Mapea la clase "Equipo" del diagrama:
/// l_colaboradores, Nombre_equipo. La asociación con Calendario (sin multiplicidad
/// explícita en el diagrama) se interpreta como 1 a 1: cada equipo tiene su propio calendario.
/// </summary>
public class Equipo
{
    public Guid Id { get; }
    public string NombreEquipo { get; set; }
    public Calendario Calendario { get; }
    public bool EsPersonal { get; }
    public Guid? LiderId { get; private set; }

    private readonly List<Colaborador> _colaboradores = new();
    public IReadOnlyList<Colaborador> LColaboradores => _colaboradores.AsReadOnly();

    public Equipo(string nombreEquipo, bool esPersonal = false, Guid? liderId = null)
    {
        if (string.IsNullOrWhiteSpace(nombreEquipo))
            throw new ArgumentException("El nombre del equipo no puede estar vacío.", nameof(nombreEquipo));

        Id = Guid.NewGuid();
        NombreEquipo = nombreEquipo;
        Calendario = new Calendario();
        EsPersonal = esPersonal;
        LiderId = liderId;
    }

    internal Equipo(Guid id, string nombreEquipo, Calendario calendario, bool esPersonal = false, Guid? liderId = null)
    {
        Id = id;
        NombreEquipo = nombreEquipo;
        Calendario = calendario;
        EsPersonal = esPersonal;
        LiderId = liderId;
    }

    public void DesignarLider(Guid colaboradorId)
    {
        if (BuscarColaborador(colaboradorId) is null)
            throw new InvalidOperationException("El líder debe pertenecer al equipo.");
        LiderId = colaboradorId;
    }

    public void AgregarColaborador(Colaborador colaborador)
    {
        if (_colaboradores.Any(c => c.Usuario.Equals(colaborador.Usuario, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"El colaborador '{colaborador.Usuario}' ya pertenece al equipo.");

        _colaboradores.Add(colaborador);
    }

    public bool EliminarColaborador(Guid colaboradorId) =>
        _colaboradores.RemoveAll(c => c.Id == colaboradorId) > 0;

    public Colaborador? BuscarColaborador(string usuario) =>
        _colaboradores.FirstOrDefault(c => c.Usuario.Equals(usuario, StringComparison.OrdinalIgnoreCase));

    public Colaborador? BuscarColaborador(Guid colaboradorId) =>
        _colaboradores.FirstOrDefault(c => c.Id == colaboradorId);
}
