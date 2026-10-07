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
        db.Equipos.Add(new EquipoRow { Id = equipo.Id, Nombre = equipo.NombreEquipo, EsPersonal = equipo.EsPersonal, LiderId = equipo.LiderId });
        db.Calendarios.Add(new CalendarioRow { Id = equipo.Calendario.Id, EquipoId = equipo.Id });
        foreach (var colaborador in equipo.LColaboradores)
            db.EquipoMiembros.Add(new EquipoMiembroRow { EquipoId = equipo.Id, ColaboradorId = colaborador.Id });
        db.SaveChanges();
        return equipo;
    }

    public void Guardar(Equipo equipo)
    {
        using var transaccion = db.Database.BeginTransaction();

        var equipoRow = db.Equipos.FirstOrDefault(row => row.Id == equipo.Id)
            ?? throw new KeyNotFoundException($"No se encontró el equipo con id '{equipo.Id}'.");
        equipoRow.Nombre = equipo.NombreEquipo;
        equipoRow.LiderId = equipo.LiderId;

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
        db.Equipos.Add(new EquipoRow { Id = equipo.Id, Nombre = equipo.NombreEquipo, EsPersonal = true, LiderId = colaborador.Id });
        db.Calendarios.Add(new CalendarioRow { Id = equipo.Calendario.Id, EquipoId = equipo.Id });
        db.EquipoMiembros.Add(new EquipoMiembroRow { EquipoId = equipo.Id, ColaboradorId = colaborador.Id });
        equipo.AgregarColaborador(colaborador);
        db.SaveChanges();
        return equipo;
    }

    public IReadOnlyList<Equipo> ObtenerPorColaborador(Guid colaboradorId) =>
        db.EquipoMiembros.AsNoTracking()
            .Where(item => item.ColaboradorId == colaboradorId)
            .Join(db.Equipos.AsNoTracking(), item => item.EquipoId, equipo => equipo.Id, (_, equipo) => equipo)
            .ToList()
            .Select(Cargar)
            .Where(equipo => equipo is not null)
            .Cast<Equipo>()
            .ToList();

    public void AgregarMiembro(Guid equipoId, Colaborador colaborador)
    {
        if (!db.EquipoMiembros.Any(item => item.EquipoId == equipoId && item.ColaboradorId == colaborador.Id))
            db.EquipoMiembros.Add(new EquipoMiembroRow { EquipoId = equipoId, ColaboradorId = colaborador.Id });
        db.SaveChanges();
    }

    public bool EsMiembro(Guid equipoId, Guid colaboradorId) =>
        db.EquipoMiembros.Any(item => item.EquipoId == equipoId && item.ColaboradorId == colaboradorId);

    public void CrearInvitacion(Guid equipoId, Colaborador colaborador)
    {
        if (!db.InvitacionesEquipo.Any(item => item.EquipoId == equipoId && item.ColaboradorId == colaborador.Id))
        {
            db.InvitacionesEquipo.Add(new InvitacionEquipoRow
            {
                Id = Guid.NewGuid(), EquipoId = equipoId, ColaboradorId = colaborador.Id, CreadaEn = DateTime.UtcNow
            });
            db.SaveChanges();
        }
    }

    public IReadOnlyList<(Guid Id, Equipo Equipo)> ObtenerInvitaciones(Guid colaboradorId) =>
        db.InvitacionesEquipo.AsNoTracking()
            .Where(item => item.ColaboradorId == colaboradorId)
            .Join(db.Equipos.AsNoTracking(), item => item.EquipoId, equipo => equipo.Id,
                (invitacion, equipo) => new { invitacion.Id, EquipoRow = equipo })
            .ToList()
            .Select(item => new { item.Id, Equipo = Cargar(item.EquipoRow) })
            .Where(item => item.Equipo is not null)
            .Select(item => (item.Id, item.Equipo!))
            .ToList();

    public void AceptarInvitacion(Guid invitacionId, Guid colaboradorId)
    {
        var invitacion = db.InvitacionesEquipo.FirstOrDefault(item => item.Id == invitacionId && item.ColaboradorId == colaboradorId)
            ?? throw new KeyNotFoundException("No se encontró la invitación.");
        AgregarMiembro(invitacion.EquipoId, db.Colaboradores.AsNoTracking()
            .Where(item => item.Id == colaboradorId)
            .Select(item => new Colaborador(item.Id, item.Usuario, item.ContraseñaHash))
            .First());
        db.InvitacionesEquipo.Remove(invitacion);
        db.SaveChanges();
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
        var equipo = new Equipo(row.Id, row.Nombre, calendario, row.EsPersonal, row.LiderId);
        var idsMiembros = db.EquipoMiembros.AsNoTracking().Where(item => item.EquipoId == row.Id).Select(item => item.ColaboradorId).ToList();
        var colaboradores = db.Colaboradores.AsNoTracking()
            .Where(item => idsMiembros.Contains(item.Id) || item.EquipoId == row.Id);
        foreach (var colaborador in colaboradores)
            equipo.AgregarColaborador(new Colaborador(colaborador.Id, colaborador.Usuario, colaborador.ContraseñaHash));
        foreach (var evento in db.Eventos.AsNoTracking().Where(item => item.EquipoId == row.Id))
        {
            var dia = equipo.Calendario.ObtenerOCrearAño(evento.Fecha.Year).BuscarDia(evento.Fecha)!;
            dia.AgregarEventoPersistido(new Evento(evento.Id, evento.Nombre, evento.Hora, evento.Lugar, evento.Descripcion, evento.ColaboradorOrganizadorId));
        }
        return equipo;
    }

}