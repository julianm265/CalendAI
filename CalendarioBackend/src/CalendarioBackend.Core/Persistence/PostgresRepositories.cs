using CalendarioBackend.Core.Models;
using CalendarioBackend.Core.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CalendarioBackend.Core.Persistence;

internal static class Guardado
{
    /// <summary>
    /// Guarda los cambios. Una violación de unicidad de PostgreSQL (23505) se traduce a
    /// InvalidOperationException con <paramref name="mensajeDuplicado"/>; cualquier otro error de base de datos
    /// se deja propagar (es un fallo del servidor, no un conflicto del usuario).
    /// </summary>
    public static void Guardar(CalendarioDbContext db, string mensajeDuplicado)
    {
        try
        {
            db.SaveChanges();
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new InvalidOperationException(mensajeDuplicado, exception);
        }
    }
}

public sealed class PostgresColaboradorRepository(CalendarioDbContext db) : IColaboradorRepository
{
    public Colaborador? ObtenerPorId(Guid id)
    {
        var row = db.Colaboradores.AsNoTracking().FirstOrDefault(item => item.Id == id);
        return row is null ? null : Mapear(row);
    }

    public Colaborador? ObtenerPorUsuario(string usuario)
    {
        var clave = usuario.Trim().ToLower();
        var row = db.Colaboradores.AsNoTracking().FirstOrDefault(item => item.Usuario.ToLower() == clave);
        return row is null ? null : Mapear(row);
    }

    public bool ExistePorUsuario(string usuario)
    {
        var clave = usuario.Trim().ToLower();
        return db.Colaboradores.Any(item => item.Usuario.ToLower() == clave);
    }

    public void Agregar(Colaborador colaborador)
    {
        db.Colaboradores.Add(new ColaboradorRow
        {
            Id = colaborador.Id,
            Usuario = colaborador.Usuario,
            ContraseñaHash = colaborador.ObtenerContraseñaHash()
        });
        Guardado.Guardar(db, $"El usuario '{colaborador.Usuario}' ya existe.");
    }

    public void ActualizarCredenciales(Colaborador colaborador)
    {
        var hash = colaborador.ObtenerContraseñaHash();
        db.Colaboradores.Where(item => item.Id == colaborador.Id)
            .ExecuteUpdate(set => set.SetProperty(item => item.ContraseñaHash, hash));
    }

    private static Colaborador Mapear(ColaboradorRow row) => new(row.Id, row.Usuario, row.ContraseñaHash);
}

public sealed class PostgresEquipoRepository(CalendarioDbContext db) : IEquipoRepository
{
    // ------------------------------------------------------------ equipos

    public Equipo Agregar(Equipo equipo, Guid liderId)
    {
        db.Equipos.Add(new EquipoRow
        {
            Id = equipo.Id,
            Nombre = equipo.NombreEquipo,
            EsPersonal = equipo.EsPersonal,
            CreadoPorId = liderId,
            CreadoEn = DateTime.UtcNow
        });
        db.Calendarios.Add(new CalendarioRow { Id = equipo.Calendario.Id, EquipoId = equipo.Id });
        db.Membresias.Add(new MembresiaRow
        {
            EquipoId = equipo.Id,
            ColaboradorId = liderId,
            Rol = RolEquipo.Lider.ATexto(),
            UnidoEn = DateTime.UtcNow
        });

        // Una sola SaveChanges = una sola transacción: el equipo nunca queda sin líder.
        Guardado.Guardar(db, equipo.EsPersonal
            ? "El usuario ya tiene un calendario personal."
            : "No fue posible crear el equipo.");
        return equipo;
    }

    public Equipo? ObtenerPorId(Guid id)
    {
        var row = db.Equipos.AsNoTracking().FirstOrDefault(item => item.Id == id);
        if (row is null) return null;

        var calendarioId = db.Calendarios.AsNoTracking()
            .Where(item => item.EquipoId == id).Select(item => (Guid?)item.Id).FirstOrDefault();
        return Mapear(row, calendarioId);
    }

    public Equipo? ObtenerPersonal(Guid colaboradorId)
    {
        var id = PersonalId(colaboradorId);
        return id is null ? null : ObtenerPorId(id.Value);
    }

    private Guid? PersonalId(Guid colaboradorId) =>
        db.Equipos.AsNoTracking()
            .Where(equipo => equipo.EsPersonal
                && db.Membresias.Any(m => m.EquipoId == equipo.Id && m.ColaboradorId == colaboradorId))
            .OrderBy(equipo => equipo.CreadoEn)
            .Select(equipo => (Guid?)equipo.Id)
            .FirstOrDefault();

    public IReadOnlyList<EquipoDelUsuario> ObtenerDelColaborador(Guid colaboradorId)
    {
        var filas = (from m in db.Membresias.AsNoTracking()
                     where m.ColaboradorId == colaboradorId
                     join e in db.Equipos.AsNoTracking() on m.EquipoId equals e.Id
                     select new
                     {
                         Equipo = e,
                         m.Rol,
                         Miembros = db.Membresias.Count(x => x.EquipoId == e.Id)
                     }).ToList();

        var ids = filas.Select(f => f.Equipo.Id).ToList();
        var calendarios = db.Calendarios.AsNoTracking()
            .Where(c => ids.Contains(c.EquipoId))
            .ToDictionary(c => c.EquipoId, c => c.Id);

        return filas
            .OrderByDescending(f => f.Equipo.EsPersonal)
            .ThenBy(f => f.Equipo.Nombre, StringComparer.OrdinalIgnoreCase)
            .Select(f => new EquipoDelUsuario(
                Mapear(f.Equipo, calendarios.TryGetValue(f.Equipo.Id, out var cid) ? cid : null),
                RolEquipoExtensiones.DesdeTexto(f.Rol),
                f.Miembros))
            .ToList();
    }

    public bool ExisteNombreLiderado(Guid liderId, string nombre)
    {
        var clave = nombre.Trim().ToLower();
        return (from m in db.Membresias
                where m.ColaboradorId == liderId && m.Rol == "lider"
                join e in db.Equipos on m.EquipoId equals e.Id
                where !e.EsPersonal && e.Nombre.ToLower() == clave
                select e.Id).Any();
    }

    public void Renombrar(Guid equipoId, string nombre) =>
        db.Equipos.Where(e => e.Id == equipoId)
            .ExecuteUpdate(set => set.SetProperty(e => e.Nombre, nombre));

    // Membresías, calendario, eventos e invitaciones se eliminan por cascada (ON DELETE CASCADE).
    public bool Eliminar(Guid id) => db.Equipos.Where(e => e.Id == id).ExecuteDelete() > 0;

    // ------------------------------------------------------------ membresías

    public RolEquipo? ObtenerRol(Guid equipoId, Guid colaboradorId)
    {
        var rol = db.Membresias.AsNoTracking()
            .Where(m => m.EquipoId == equipoId && m.ColaboradorId == colaboradorId)
            .Select(m => m.Rol)
            .FirstOrDefault();
        return rol is null ? null : RolEquipoExtensiones.DesdeTexto(rol);
    }

    public IReadOnlyList<MiembroEquipo> ObtenerMiembros(Guid equipoId)
    {
        var filas = (from m in db.Membresias.AsNoTracking()
                     where m.EquipoId == equipoId
                     join c in db.Colaboradores.AsNoTracking() on m.ColaboradorId equals c.Id
                     select new { c.Id, c.Usuario, m.Rol, m.UnidoEn }).ToList();

        return filas
            .Select(f => new MiembroEquipo(f.Id, f.Usuario, RolEquipoExtensiones.DesdeTexto(f.Rol), f.UnidoEn))
            .OrderByDescending(m => m.Rol)
            .ThenBy(m => m.Usuario, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public void AgregarMiembro(Guid equipoId, Guid colaboradorId, RolEquipo rol)
    {
        db.Membresias.Add(new MembresiaRow
        {
            EquipoId = equipoId,
            ColaboradorId = colaboradorId,
            Rol = rol.ATexto(),
            UnidoEn = DateTime.UtcNow
        });
        Guardado.Guardar(db, "El usuario ya pertenece al equipo.");
    }

    public bool EliminarMiembro(Guid equipoId, Guid colaboradorId) =>
        db.Membresias.Where(m => m.EquipoId == equipoId && m.ColaboradorId == colaboradorId).ExecuteDelete() > 0;

    public void TransferirLiderazgo(Guid equipoId, Guid actualLiderId, Guid nuevoLiderId)
    {
        // Hay un índice único parcial "un líder por equipo": primero se degrada al anterior, luego se asciende.
        using var transaccion = db.Database.BeginTransaction();

        db.Membresias.Where(m => m.EquipoId == equipoId && m.ColaboradorId == actualLiderId && m.Rol == "lider")
            .ExecuteUpdate(set => set.SetProperty(m => m.Rol, "miembro"));
        var ascendidos = db.Membresias.Where(m => m.EquipoId == equipoId && m.ColaboradorId == nuevoLiderId)
            .ExecuteUpdate(set => set.SetProperty(m => m.Rol, "lider"));

        if (ascendidos == 0)
            throw new KeyNotFoundException("El usuario no pertenece al equipo.");

        transaccion.Commit();
    }

    // ------------------------------------------------------------ eventos

    public IReadOnlyList<Evento> ObtenerEventos(Guid equipoId, DateOnly fecha) =>
        db.Eventos.AsNoTracking()
            .Where(e => e.EquipoId == equipoId && e.Fecha == fecha)
            .OrderBy(e => e.Hora)
            .ToList()
            .Select(MapearEvento)
            .ToList();

    public IReadOnlyList<(DateOnly Fecha, Evento Evento)> ObtenerEventos(Guid equipoId, DateOnly desde, DateOnly hasta) =>
        db.Eventos.AsNoTracking()
            .Where(e => e.EquipoId == equipoId && e.Fecha >= desde && e.Fecha <= hasta)
            .OrderBy(e => e.Fecha).ThenBy(e => e.Hora)
            .ToList()
            .Select(e => (e.Fecha, MapearEvento(e)))
            .ToList();

    public (DateOnly Fecha, Evento Evento)? ObtenerEvento(Guid equipoId, Guid eventoId)
    {
        var row = db.Eventos.AsNoTracking().FirstOrDefault(e => e.EquipoId == equipoId && e.Id == eventoId);
        return row is null ? null : (row.Fecha, MapearEvento(row));
    }

    public void AgregarEvento(Guid equipoId, DateOnly fecha, Evento evento)
    {
        var mensaje = $"Ya existe un evento programado a las {evento.HoraEvento:HH\\:mm} el {fecha:dd/MM/yyyy}.";
        if (db.Eventos.Any(e => e.EquipoId == equipoId && e.Fecha == fecha && e.Hora == evento.HoraEvento))
            throw new InvalidOperationException(mensaje);

        db.Eventos.Add(new EventoRow
        {
            Id = evento.Id,
            EquipoId = equipoId,
            Fecha = fecha,
            Hora = evento.HoraEvento,
            Nombre = evento.NombreEvento,
            Lugar = evento.LugarEvento,
            Descripcion = evento.Descripcion,
            ColaboradorOrganizadorId = evento.ColaboradorOrganizadorId
        });
        Guardado.Guardar(db, mensaje); // por si dos peticiones simultáneas pasaron la comprobación anterior
    }

    public bool EliminarEvento(Guid equipoId, Guid eventoId) =>
        db.Eventos.Where(e => e.EquipoId == equipoId && e.Id == eventoId).ExecuteDelete() > 0;

    public IReadOnlyList<LugarFrecuente> ObtenerLugaresFrecuentes(Guid equipoId, int limite)
    {
        var lugares = db.Eventos.AsNoTracking()
            .Where(e => e.EquipoId == equipoId && e.Lugar != null && e.Lugar != "")
            .Select(e => e.Lugar!)
            .ToList();
        return LugarNormalizer.Agrupar(lugares, limite);
    }

    public IReadOnlyList<LugarFrecuente> ObtenerLugaresDelColaborador(Guid colaboradorId, Guid equipoId, int limite)
    {
        var personalId = PersonalId(colaboradorId);
        var lugares = db.Eventos.AsNoTracking()
            .Where(e => e.ColaboradorOrganizadorId == colaboradorId
                && (e.EquipoId == equipoId || e.EquipoId == personalId)
                && e.Lugar != null && e.Lugar != "")
            .Select(e => e.Lugar!)
            .ToList();
        return LugarNormalizer.Agrupar(lugares, limite);
    }

    // ------------------------------------------------------------ mapeo

    private static Equipo Mapear(EquipoRow row, Guid? calendarioId) =>
        new(row.Id, row.Nombre, calendarioId is Guid id ? new Calendario(id) : new Calendario(), row.EsPersonal, row.CreadoPorId);

    private static Evento MapearEvento(EventoRow row) =>
        new(row.Id, row.Nombre, row.Hora, row.Lugar, row.Descripcion, row.ColaboradorOrganizadorId);
}

public sealed class PostgresInvitacionRepository(CalendarioDbContext db) : IInvitacionRepository
{
    public Invitacion Agregar(Invitacion invitacion)
    {
        db.Invitaciones.Add(new InvitacionRow
        {
            Id = invitacion.Id,
            EquipoId = invitacion.EquipoId,
            InvitadoId = invitacion.InvitadoId,
            InvitadoPorId = invitacion.InvitadoPorId,
            Estado = TextoEstado(invitacion.Estado),
            CreadaEn = invitacion.CreadaEn
        });
        Guardado.Guardar(db, "Ya hay una invitación pendiente para ese usuario.");
        return invitacion;
    }

    public Invitacion? ObtenerPorId(Guid id)
    {
        var row = db.Invitaciones.AsNoTracking().FirstOrDefault(item => item.Id == id);
        return row is null
            ? null
            : new Invitacion(row.Id, row.EquipoId, row.InvitadoId, row.InvitadoPorId,
                ParsearEstado(row.Estado), row.CreadaEn, row.RespondidaEn);
    }

    public bool ExistePendiente(Guid equipoId, Guid invitadoId) =>
        db.Invitaciones.Any(i => i.EquipoId == equipoId && i.InvitadoId == invitadoId && i.Estado == "pendiente");

    public IReadOnlyList<InvitacionDetalle> ObtenerPendientesDeEquipo(Guid equipoId) =>
        Detallar(db.Invitaciones.AsNoTracking().Where(i => i.EquipoId == equipoId && i.Estado == "pendiente"));

    public IReadOnlyList<InvitacionDetalle> ObtenerPendientesDeColaborador(Guid colaboradorId) =>
        Detallar(db.Invitaciones.AsNoTracking().Where(i => i.InvitadoId == colaboradorId && i.Estado == "pendiente"));

    public void Actualizar(Invitacion invitacion)
    {
        var estado = TextoEstado(invitacion.Estado);
        var respondida = invitacion.RespondidaEn;
        db.Invitaciones.Where(i => i.Id == invitacion.Id)
            .ExecuteUpdate(set => set
                .SetProperty(i => i.Estado, estado)
                .SetProperty(i => i.RespondidaEn, respondida));
    }

    private IReadOnlyList<InvitacionDetalle> Detallar(IQueryable<InvitacionRow> consulta)
    {
        var filas = (from i in consulta
                     join e in db.Equipos.AsNoTracking() on i.EquipoId equals e.Id
                     join invitado in db.Colaboradores.AsNoTracking() on i.InvitadoId equals invitado.Id
                     join por in db.Colaboradores.AsNoTracking() on i.InvitadoPorId equals por.Id
                     select new
                     {
                         i.Id, i.EquipoId, Equipo = e.Nombre,
                         i.InvitadoId, Invitado = invitado.Usuario,
                         i.InvitadoPorId, InvitadoPor = por.Usuario,
                         i.Estado, i.CreadaEn
                     }).ToList();

        return filas
            .OrderByDescending(f => f.CreadaEn)
            .Select(f => new InvitacionDetalle(
                f.Id, f.EquipoId, f.Equipo, f.InvitadoId, f.Invitado,
                f.InvitadoPorId, f.InvitadoPor, ParsearEstado(f.Estado), f.CreadaEn))
            .ToList();
    }

    private static string TextoEstado(EstadoInvitacion estado) => estado switch
    {
        EstadoInvitacion.Aceptada => "aceptada",
        EstadoInvitacion.Rechazada => "rechazada",
        EstadoInvitacion.Cancelada => "cancelada",
        _ => "pendiente"
    };

    private static EstadoInvitacion ParsearEstado(string texto) => texto switch
    {
        "aceptada" => EstadoInvitacion.Aceptada,
        "rechazada" => EstadoInvitacion.Rechazada,
        "cancelada" => EstadoInvitacion.Cancelada,
        _ => EstadoInvitacion.Pendiente
    };
}
