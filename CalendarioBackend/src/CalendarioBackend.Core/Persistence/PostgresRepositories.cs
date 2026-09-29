using CalendarioBackend.Core.Models;
using CalendarioBackend.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace CalendarioBackend.Core.Persistence;

public sealed class PostgresColaboradorRepository(CalendarioDbContext db) : IColaboradorRepository
{
    public Colaborador? ObtenerPorUsuario(string usuario)
    {
        var row = db.Colaboradores.AsNoTracking()
            .FirstOrDefault(row => row.Usuario.ToLower() == usuario.ToLower());
        return row is null ? null : new Colaborador(row.Id, row.Usuario, row.ContraseñaHash);
    }

    public bool ExistePorUsuario(string usuario) => db.Colaboradores.Any(row => row.Usuario.ToLower() == usuario.ToLower());

    public void Agregar(Colaborador colaborador, Guid? equipoId)
    {
        db.Colaboradores.Add(new ColaboradorRow
        {
            Id = colaborador.Id, Usuario = colaborador.Usuario,
            ContraseñaHash = colaborador.ObtenerContraseñaHash(), EquipoId = equipoId
        });
        db.SaveChanges();
    }

    public void AsignarAEquipo(Guid colaboradorId, Guid equipoId)
    {
        var row = db.Colaboradores.FirstOrDefault(item => item.Id == colaboradorId)
            ?? throw new KeyNotFoundException("No se encontró el colaborador.");
        row.EquipoId = equipoId;
        db.SaveChanges();
    }

}

public sealed class PostgresEquipoRepository(CalendarioDbContext db) : IEquipoRepository
{
    public Equipo Agregar(Equipo equipo)
    {
        db.Equipos.Add(new EquipoRow { Id = equipo.Id, Nombre = equipo.NombreEquipo, EsPersonal = equipo.EsPersonal });
        db.Calendarios.Add(new CalendarioRow { Id = equipo.Calendario.Id, EquipoId = equipo.Id });
        db.SaveChanges();
        return equipo;
    }

    public void Guardar(Equipo equipo)
    {
        using var transaccion = db.Database.BeginTransaction();

        var equipoRow = db.Equipos.FirstOrDefault(row => row.Id == equipo.Id)
            ?? throw new KeyNotFoundException($"No se encontró el equipo con id '{equipo.Id}'.");
        equipoRow.Nombre = equipo.NombreEquipo;

        SincronizarColaboradores(equipo);
        SincronizarEventos(equipo);

        GuardarCambios();
        transaccion.Commit();
    }

    public void AgregarColaborador(Guid equipoId, Colaborador colaborador)
    {
        var row = db.Colaboradores.FirstOrDefault(item => item.Id == colaborador.Id);
        if (row is null)
        {
            db.Colaboradores.Add(CrearFila(colaborador, equipoId));
        }
        else
        {
            row.EquipoId = equipoId;
            row.Usuario = colaborador.Usuario;
            row.ContraseñaHash = colaborador.ObtenerContraseñaHash();
        }

        GuardarCambios();
    }

    public void EliminarColaborador(Guid equipoId, Guid colaboradorId)
    {
        var row = db.Colaboradores.FirstOrDefault(item => item.Id == colaboradorId && item.EquipoId == equipoId);
        if (row is null)
            return;

        db.Colaboradores.Remove(row);
        GuardarCambios();
    }

    public void AgregarEvento(Guid equipoId, DateOnly fecha, Evento evento)
    {
        db.Eventos.Add(CrearFila(evento, equipoId, fecha));
        GuardarCambios();
    }

    public void EliminarEvento(Guid equipoId, Guid eventoId)
    {
        var row = db.Eventos.FirstOrDefault(item => item.Id == eventoId && item.EquipoId == equipoId);
        if (row is null)
            return;

        db.Eventos.Remove(row);
        GuardarCambios();
    }

    public Equipo? ObtenerPorId(Guid id) => Cargar(db.Equipos.AsNoTracking().FirstOrDefault(row => row.Id == id));
    public Equipo? ObtenerPorNombre(string nombre) => Cargar(db.Equipos.AsNoTracking().FirstOrDefault(row => row.Nombre.ToLower() == nombre.ToLower()));
    public IReadOnlyList<Equipo> ObtenerTodos() => db.Equipos.AsNoTracking().ToList().Select(Cargar).Where(equipo => equipo is not null).Cast<Equipo>().ToList();
    public bool Eliminar(Guid id) { var equipo = db.Equipos.Find(id); if (equipo is null) return false; db.Equipos.Remove(equipo); db.SaveChanges(); return true; }

    public Equipo CrearCalendarioPersonal(Colaborador colaborador)
    {
        var equipo = new Equipo($"Calendario de {colaborador.Usuario}", true);
        db.Equipos.Add(new EquipoRow { Id = equipo.Id, Nombre = equipo.NombreEquipo, EsPersonal = true });
        db.Calendarios.Add(new CalendarioRow { Id = equipo.Calendario.Id, EquipoId = equipo.Id });
        equipo.AgregarColaborador(colaborador);
        db.SaveChanges();
        return equipo;
    }

    private void SincronizarColaboradores(Equipo equipo)
    {
        var filas = db.Colaboradores.Where(row => row.EquipoId == equipo.Id).ToDictionary(row => row.Id);

        foreach (var colaborador in equipo.LColaboradores)
        {
            if (filas.Remove(colaborador.Id, out var fila))
            {
                fila.Usuario = colaborador.Usuario;
                fila.ContraseñaHash = colaborador.ObtenerContraseñaHash();
                continue;
            }

            var existente = db.Colaboradores.FirstOrDefault(row => row.Id == colaborador.Id);
            if (existente is null)
                db.Colaboradores.Add(CrearFila(colaborador, equipo.Id));
            else
                existente.EquipoId = equipo.Id;
        }

        db.Colaboradores.RemoveRange(filas.Values);
    }

    private void SincronizarEventos(Equipo equipo)
    {
        var filas = db.Eventos.Where(row => row.EquipoId == equipo.Id).ToDictionary(row => row.Id);

        foreach (var (fecha, evento) in EnumerarEventos(equipo))
        {
            if (filas.Remove(evento.Id, out var fila))
            {
                fila.Fecha = fecha;
                fila.Hora = evento.HoraEvento;
                fila.Nombre = evento.NombreEvento;
                fila.Lugar = evento.LugarEvento;
                fila.Descripcion = evento.Descripcion;
                fila.ColaboradorOrganizadorId = evento.ColaboradorOrganizadorId;
                continue;
            }

            db.Eventos.Add(CrearFila(evento, equipo.Id, fecha));
        }

        db.Eventos.RemoveRange(filas.Values);
    }

    private static IEnumerable<(DateOnly Fecha, Evento Evento)> EnumerarEventos(Equipo equipo) =>
        from año in equipo.Calendario.LAños
        from mes in año.LMeses
        from semana in mes.LSemana
        from dia in semana.LDias
        from evento in dia.LEventos
        select (dia.Fecha, evento);

    private static ColaboradorRow CrearFila(Colaborador colaborador, Guid equipoId) => new()
    {
        Id = colaborador.Id,
        Usuario = colaborador.Usuario,
        ContraseñaHash = colaborador.ObtenerContraseñaHash(),
        EquipoId = equipoId
    };

    private static EventoRow CrearFila(Evento evento, Guid equipoId, DateOnly fecha) => new()
    {
        Id = evento.Id,
        EquipoId = equipoId,
        Fecha = fecha,
        Hora = evento.HoraEvento,
        Nombre = evento.NombreEvento,
        Lugar = evento.LugarEvento,
        Descripcion = evento.Descripcion,
        ColaboradorOrganizadorId = evento.ColaboradorOrganizadorId
    };

    private void GuardarCambios()
    {
        try
        {
            db.SaveChanges();
        }
        catch (DbUpdateException exception)
        {
            throw new InvalidOperationException("No fue posible guardar los cambios en la base de datos.", exception);
        }
    }

    private Equipo? Cargar(EquipoRow? row)
    {
        if (row is null) return null;
        var calendarioId = db.Calendarios.AsNoTracking().FirstOrDefault(item => item.EquipoId == row.Id)?.Id;
        var calendario = calendarioId is Guid id ? new Calendario(id) : new Calendario();
        var equipo = new Equipo(row.Id, row.Nombre, calendario, row.EsPersonal);
        foreach (var colaborador in db.Colaboradores.AsNoTracking().Where(item => item.EquipoId == row.Id))
            equipo.AgregarColaborador(new Colaborador(colaborador.Id, colaborador.Usuario, colaborador.ContraseñaHash));
        foreach (var evento in db.Eventos.AsNoTracking().Where(item => item.EquipoId == row.Id))
        {
            var dia = equipo.Calendario.ObtenerOCrearAño(evento.Fecha.Year).BuscarDia(evento.Fecha)!;
            dia.AgregarEventoPersistido(new Evento(evento.Id, evento.Nombre, evento.Hora, evento.Lugar, evento.Descripcion, evento.ColaboradorOrganizadorId));
        }
        return equipo;
    }

}