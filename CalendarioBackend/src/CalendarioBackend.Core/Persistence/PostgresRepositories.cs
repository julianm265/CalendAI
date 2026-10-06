using CalendarioBackend.Core.Models;
using CalendarioBackend.Core.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

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
            ContraseñaHash = colaborador.ObtenerContraseñaHash(), EquipoId = null
        });
        if (equipoId is Guid id)
            db.EquipoColaboradores.Add(new EquipoColaboradorRow { EquipoId = id, ColaboradorId = colaborador.Id });
        db.SaveChanges();
    }

    public void AsignarAEquipo(Guid colaboradorId, Guid equipoId)
    {
        var row = db.Colaboradores.FirstOrDefault(item => item.Id == colaboradorId)
            ?? throw new KeyNotFoundException("No se encontró el colaborador.");
        row.EquipoId = equipoId;
        if (!db.EquipoColaboradores.Any(item => item.EquipoId == equipoId && item.ColaboradorId == colaboradorId))
            db.EquipoColaboradores.Add(new EquipoColaboradorRow { EquipoId = equipoId, ColaboradorId = colaboradorId });
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

    public Equipo AgregarEquipoConColaborador(Equipo equipo, Colaborador colaborador, string rol)
    {
        equipo.AgregarColaborador(colaborador, rol);
        using var transaccion = db.Database.BeginTransaction();
        db.Equipos.Add(new EquipoRow { Id = equipo.Id, Nombre = equipo.NombreEquipo, EsPersonal = false });
        db.Calendarios.Add(new CalendarioRow { Id = equipo.Calendario.Id, EquipoId = equipo.Id });
        db.EquipoColaboradores.Add(new EquipoColaboradorRow
        {
            EquipoId = equipo.Id,
            ColaboradorId = colaborador.Id,
            Rol = rol
        });
        db.SaveChanges();
        transaccion.Commit();

        var equipoPersistido = ObtenerPorId(equipo.Id)
            ?? throw new InvalidOperationException("El equipo se guardó, pero no se pudo volver a cargar.");
        var miembroPersistido = equipoPersistido.BuscarColaborador(colaborador.Id);
        if (miembroPersistido is null || equipoPersistido.ObtenerRolColaborador(colaborador.Id) != rol)
            throw new InvalidOperationException("El equipo se guardó sin persistir correctamente su miembro y rol.");

        return equipoPersistido;
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

    public void AgregarColaborador(Guid equipoId, Colaborador colaborador, string rol = "Miembro")
    {
        if (!db.Colaboradores.Any(item => item.Id == colaborador.Id))
            throw new KeyNotFoundException("No se encontró el usuario registrado.");

        if (db.EquipoColaboradores.Any(item => item.EquipoId == equipoId && item.ColaboradorId == colaborador.Id))
            throw new InvalidOperationException($"El usuario '{colaborador.Usuario}' ya pertenece al equipo.");

        db.EquipoColaboradores.Add(new EquipoColaboradorRow
        {
            EquipoId = equipoId,
            ColaboradorId = colaborador.Id,
            Rol = rol
        });

        GuardarCambios();
    }

    public void EliminarColaborador(Guid equipoId, Guid colaboradorId)
    {
        var row = db.EquipoColaboradores.FirstOrDefault(item => item.ColaboradorId == colaboradorId && item.EquipoId == equipoId);
        if (row is null)
            return;

        db.EquipoColaboradores.Remove(row);
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

    public IReadOnlyList<LugarFrecuente> ObtenerLugaresFrecuentes(Guid equipoId, int limite)
    {
        var lugares = db.Eventos.AsNoTracking()
            .Where(evento => evento.EquipoId == equipoId && evento.Lugar != null && evento.Lugar != "")
            .Select(evento => evento.Lugar!)
            .ToList();

        return lugares
            .Select(lugar => new { Original = lugar.Trim(), Clave = NormalizarLugar(lugar) })
            .Where(lugar => lugar.Clave.Length > 0)
            .GroupBy(lugar => lugar.Clave)
            .OrderByDescending(grupo => grupo.Count())
            .ThenBy(grupo => grupo.Min(item => item.Original), StringComparer.OrdinalIgnoreCase)
            .Take(limite)
            .Select(grupo => new LugarFrecuente(
                grupo.OrderBy(item => item.Original.Length).First().Original,
                grupo.Count()))
            .ToList();
    }

    private static string NormalizarLugar(string lugar)
    {
        var descompuesto = lugar.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var caracter in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark)
                builder.Append(caracter);
        }

        return string.Join(' ', builder.ToString().Normalize(NormalizationForm.FormC)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
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
        db.EquipoColaboradores.Add(new EquipoColaboradorRow
        {
            EquipoId = equipo.Id,
            ColaboradorId = colaborador.Id,
            Rol = "Miembro"
        });
        db.SaveChanges();
        return equipo;
    }

    private void SincronizarColaboradores(Equipo equipo)
    {
        var filas = db.EquipoColaboradores.Where(row => row.EquipoId == equipo.Id)
            .ToDictionary(row => row.ColaboradorId);

        foreach (var colaborador in equipo.LColaboradores)
        {
            if (filas.Remove(colaborador.Id)) continue;
            db.EquipoColaboradores.Add(new EquipoColaboradorRow { EquipoId = equipo.Id, ColaboradorId = colaborador.Id });
        }

        db.EquipoColaboradores.RemoveRange(filas.Values);
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
        var colaboradores = from relacion in db.EquipoColaboradores.AsNoTracking()
                            join colaborador in db.Colaboradores.AsNoTracking() on relacion.ColaboradorId equals colaborador.Id
                            where relacion.EquipoId == row.Id
                            select new { Colaborador = colaborador, relacion.Rol };
        foreach (var item in colaboradores)
            equipo.AgregarColaborador(
                new Colaborador(item.Colaborador.Id, item.Colaborador.Usuario, item.Colaborador.ContraseñaHash),
                item.Rol);
        foreach (var evento in db.Eventos.AsNoTracking().Where(item => item.EquipoId == row.Id))
        {
            var dia = equipo.Calendario.ObtenerOCrearAño(evento.Fecha.Year).BuscarDia(evento.Fecha)!;
            dia.AgregarEventoPersistido(new Evento(evento.Id, evento.Nombre, evento.Hora, evento.Lugar, evento.Descripcion, evento.ColaboradorOrganizadorId));
        }
        return equipo;
    }

}