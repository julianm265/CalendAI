using CalendarioBackend.Core.Models;
using CalendarioBackend.Core.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CalendarioBackend.Core.Repositories;

public sealed class PostgresEquipoRepository : IEquipoRepository
{
    private readonly CalendaiDbContext _db;

    public PostgresEquipoRepository(CalendaiDbContext db)
    {
        _db = db;
    }

    public Equipo Agregar(Equipo equipo)
    {
        _db.Equipos.Add(new EquipoRow { Id = equipo.Id, Nombre = equipo.NombreEquipo });
        _db.Calendarios.Add(new CalendarioRow { Id = equipo.Calendario.Id, EquipoId = equipo.Id });
        _db.SaveChanges();
        return equipo;
    }

    public void Guardar(Equipo equipo)
    {
        var equipoRow = _db.Equipos.Single(row => row.Id == equipo.Id);
        equipoRow.Nombre = equipo.NombreEquipo;

        var colaboradores = _db.Colaboradores.Where(row => row.EquipoId == equipo.Id).ToList();
        var eventos = _db.Eventos.Where(row => row.EquipoId == equipo.Id).ToList();
        _db.Colaboradores.RemoveRange(colaboradores);
        _db.Eventos.RemoveRange(eventos);
        _db.SaveChanges();

        _db.Colaboradores.AddRange(equipo.LColaboradores.Select(colaborador => new ColaboradorRow
        {
            Id = colaborador.Id,
            EquipoId = equipo.Id,
            Usuario = colaborador.Usuario,
            ContrasenaHash = colaborador.ObtenerHashParaPersistencia()
        }));

        _db.Eventos.AddRange(EnumerarEventos(equipo).Select(item => new EventoRow
        {
            Id = item.Evento.Id,
            EquipoId = equipo.Id,
            ColaboradorOrganizadorId = item.Evento.ColaboradorOrganizadorId,
            Fecha = item.Fecha,
            Nombre = item.Evento.NombreEvento,
            Hora = item.Evento.HoraEvento,
            Lugar = item.Evento.LugarEvento,
            Descripcion = item.Evento.Descripcion
        }));
        _db.SaveChanges();
    }

    public Equipo? ObtenerPorId(Guid id) => Cargar(_db.Equipos.AsNoTracking().SingleOrDefault(row => row.Id == id));

    public Equipo? ObtenerPorNombre(string nombre)
    {
        var nombreNormalizado = nombre.ToLower();
        var row = _db.Equipos.AsNoTracking()
            .FirstOrDefault(item => item.Nombre.ToLower() == nombreNormalizado);
        return Cargar(row);
    }

    public IReadOnlyList<Equipo> ObtenerTodos() =>
        _db.Equipos.AsNoTracking().OrderBy(row => row.Nombre).ToList()
            .Select(Cargar)
            .OfType<Equipo>()
            .ToList()
            .AsReadOnly();

    public bool Eliminar(Guid id)
    {
        var row = _db.Equipos.SingleOrDefault(item => item.Id == id);
        if (row is null)
            return false;

        _db.Equipos.Remove(row);
        _db.SaveChanges();
        return true;
    }

    private Equipo? Cargar(EquipoRow? row)
    {
        if (row is null)
            return null;

        var equipo = new Equipo(row.Id, row.Nombre, new Calendario());
        var colaboradores = _db.Colaboradores.AsNoTracking().Where(item => item.EquipoId == row.Id).ToList();
        foreach (var colaborador in colaboradores)
            equipo.CargarColaborador(new Colaborador(colaborador.Id, colaborador.Usuario, colaborador.ContrasenaHash));

        var eventos = _db.Eventos.AsNoTracking().Where(item => item.EquipoId == row.Id).ToList();
        foreach (var evento in eventos)
        {
            var dia = equipo.Calendario.ObtenerOCrearAño(evento.Fecha.Year).BuscarDia(evento.Fecha)
                ?? throw new InvalidOperationException($"No fue posible resolver el día {evento.Fecha}.");
            dia.CargarEvento(new Evento(evento.Id, evento.Nombre, evento.Hora, evento.Lugar,
                evento.Descripcion, evento.ColaboradorOrganizadorId));
        }

        return equipo;
    }

    private static IEnumerable<(DateOnly Fecha, Evento Evento)> EnumerarEventos(Equipo equipo)
    {
        return equipo.Calendario.LAños
            .SelectMany(anio => anio.LMeses)
            .SelectMany(mes => mes.LSemana)
            .SelectMany(semana => semana.LDias)
            .SelectMany(dia => dia.LEventos.Select(evento => (dia.Fecha, evento)));
    }
}
