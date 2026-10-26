using CalendarioBackend.Core.Models;
using CalendarioBackend.Core.Repositories;

namespace CalendarioBackend.Core.Services;

/// <summary>
/// Eventos de un calendario (personal o de equipo). Todas las operaciones comprueban que quien llama
/// pertenece al equipo; los eventos se consultan por día o por mes directamente en el repositorio.
/// </summary>
public class CalendarioService(IEquipoRepository equipos, AccesoEquipoService acceso)
{
    public const int AñoMinimo = 1900;
    public const int AñoMaximo = 2100;

    /// <summary>
    /// Crea un evento. Si no se indica organizador, lo es quien lo crea. Dos eventos del mismo calendario
    /// no pueden coincidir en fecha y hora.
    /// </summary>
    public Evento AgregarEvento(Guid equipoId, Guid solicitanteId, DateOnly fecha, string nombreEvento,
        TimeOnly hora, string? lugar, string? descripcion = null, Guid? colaboradorOrganizadorId = null)
    {
        acceso.ExigirMiembro(equipoId, solicitanteId);
        ValidarFecha(fecha);

        var organizadorId = colaboradorOrganizadorId ?? solicitanteId;
        if (acceso.ObtenerRol(equipoId, organizadorId) is null)
            throw new InvalidOperationException("El organizador indicado no pertenece a este equipo.");

        var evento = new Evento(nombreEvento, hora, lugar, descripcion, organizadorId);
        equipos.AgregarEvento(equipoId, fecha, evento);
        return evento;
    }

    /// <summary>Elimina un evento. Pueden hacerlo el líder del equipo o el organizador del evento.</summary>
    public bool EliminarEvento(Guid equipoId, Guid solicitanteId, Guid eventoId)
    {
        var rol = acceso.ExigirMiembro(equipoId, solicitanteId);
        var existente = equipos.ObtenerEvento(equipoId, eventoId);
        if (existente is null) return false;

        var esOrganizador = existente.Value.Evento.ColaboradorOrganizadorId == solicitanteId;
        if (rol != RolEquipo.Lider && !esOrganizador)
            throw new UnauthorizedAccessException("Solo el líder o el organizador del evento pueden eliminarlo.");

        return equipos.EliminarEvento(equipoId, eventoId);
    }

    public IReadOnlyList<Evento> ObtenerEventosDelDia(Guid equipoId, Guid solicitanteId, DateOnly fecha)
    {
        acceso.ExigirMiembro(equipoId, solicitanteId);
        ValidarFecha(fecha);
        return equipos.ObtenerEventos(equipoId, fecha);
    }

    public IReadOnlyList<(DateOnly Fecha, Evento Evento)> ObtenerEventosDelMes(
        Guid equipoId, Guid solicitanteId, int año, int mes)
    {
        acceso.ExigirMiembro(equipoId, solicitanteId);
        if (año < AñoMinimo || año > AñoMaximo)
            throw new ArgumentException($"El año debe estar entre {AñoMinimo} y {AñoMaximo}.", nameof(año));
        if (mes is < 1 or > 12)
            throw new ArgumentException("El mes debe estar entre 1 y 12.", nameof(mes));

        var desde = new DateOnly(año, mes, 1);
        var hasta = new DateOnly(año, mes, DateTime.DaysInMonth(año, mes));
        return equipos.ObtenerEventos(equipoId, desde, hasta);
    }

    private static void ValidarFecha(DateOnly fecha)
    {
        if (fecha.Year < AñoMinimo || fecha.Year > AñoMaximo)
            throw new ArgumentException($"La fecha debe estar entre los años {AñoMinimo} y {AñoMaximo}.", nameof(fecha));
    }
}
