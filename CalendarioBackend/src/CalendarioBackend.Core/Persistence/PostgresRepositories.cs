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

}

public sealed class PostgresEquipoRepository(CalendarioDbContext db) : IEquipoRepository
{
    public Equipo Agregar(Equipo equipo)
    {
        db.Equipos.Add(new EquipoRow { Id = equipo.Id, Nombre = equipo.NombreEquipo });
        db.SaveChanges();
        return equipo;
    }

    public void Guardar(Equipo equipo)
    {
        db.Equipos.Update(new EquipoRow { Id = equipo.Id, Nombre = equipo.NombreEquipo });
        db.Colaboradores.RemoveRange(db.Colaboradores.Where(row => row.EquipoId == equipo.Id));
        db.Eventos.RemoveRange(db.Eventos.Where(row => row.EquipoId == equipo.Id));
        foreach (var colaborador in equipo.LColaboradores)
            db.Colaboradores.Add(new ColaboradorRow { Id = colaborador.Id, Usuario = colaborador.Usuario, ContraseñaHash = colaborador.ObtenerContraseñaHash(), EquipoId = equipo.Id });
        foreach (var año in equipo.Calendario.LAños)
            foreach (var mes in año.LMeses)
                foreach (var semana in mes.LSemana)
                    foreach (var dia in semana.LDias)
                        foreach (var evento in dia.LEventos)
                            db.Eventos.Add(new EventoRow { Id = evento.Id, EquipoId = equipo.Id, Fecha = dia.Fecha, Hora = evento.HoraEvento, Nombre = evento.NombreEvento, Lugar = evento.LugarEvento, Descripcion = evento.Descripcion, ColaboradorOrganizadorId = evento.ColaboradorOrganizadorId });
        db.SaveChanges();
    }

    public Equipo? ObtenerPorId(Guid id) => Cargar(db.Equipos.AsNoTracking().FirstOrDefault(row => row.Id == id));
    public Equipo? ObtenerPorNombre(string nombre) => Cargar(db.Equipos.AsNoTracking().FirstOrDefault(row => row.Nombre.ToLower() == nombre.ToLower()));
    public IReadOnlyList<Equipo> ObtenerTodos() => db.Equipos.AsNoTracking().ToList().Select(Cargar).Where(equipo => equipo is not null).Cast<Equipo>().ToList();
    public bool Eliminar(Guid id) { var equipo = db.Equipos.Find(id); if (equipo is null) return false; db.Equipos.Remove(equipo); db.SaveChanges(); return true; }

    private Equipo? Cargar(EquipoRow? row)
    {
        if (row is null) return null;
        var equipo = new Equipo(row.Id, row.Nombre, new Calendario());
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