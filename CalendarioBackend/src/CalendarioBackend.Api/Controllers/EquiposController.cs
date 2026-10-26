using CalendarioBackend.Api.Services;
using CalendarioBackend.Core.Models;
using CalendarioBackend.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CalendarioBackend.Api.Controllers;

/// <summary>Equipos (calendarios compartidos) del usuario autenticado. Todo exige sesión iniciada.</summary>
[ApiController]
[Authorize]
[Route("api/equipos")]
public class EquiposController(EquipoService equipos) : ControllerBase
{
    /// <summary>Los calendarios del usuario: el personal y los equipos a los que pertenece.</summary>
    [HttpGet]
    public ActionResult<IReadOnlyList<EquipoResumen>> Listar() =>
        Ok(equipos.ListarEquiposDe(User.ObtenerColaboradorId())
            .Select(item => new EquipoResumen(
                item.Equipo.Id, item.Equipo.NombreEquipo, item.Miembros, item.Equipo.EsPersonal, item.Rol.ATexto()))
            .ToList());

    [HttpGet("{id:guid}")]
    public ActionResult<EquipoDetalle> Obtener(Guid id)
    {
        var (equipo, miRol, miembros) = equipos.ObtenerDetalle(id, User.ObtenerColaboradorId());
        return Ok(new EquipoDetalle(
            equipo.Id,
            equipo.NombreEquipo,
            miembros.Select(m => new MiembroResumen(m.ColaboradorId, m.Usuario, m.Rol.ATexto(), m.UnidoEn)).ToList(),
            equipo.EsPersonal,
            miRol.ATexto()));
    }

    /// <summary>Crea un equipo; quien lo crea queda como su líder.</summary>
    [HttpPost]
    public ActionResult<EquipoResumen> Crear(CrearEquipoRequest request)
    {
        var equipo = equipos.CrearEquipo(User.ObtenerColaboradorId(), request.NombreEquipo);
        return CreatedAtAction(
            nameof(Obtener),
            new { id = equipo.Id },
            new EquipoResumen(equipo.Id, equipo.NombreEquipo, 1, false, RolEquipo.Lider.ATexto()));
    }

    [HttpPut("{id:guid}")]
    public ActionResult<EquipoResumen> Renombrar(Guid id, RenombrarEquipoRequest request)
    {
        var equipo = equipos.Renombrar(id, User.ObtenerColaboradorId(), request.NombreEquipo);
        return Ok(new EquipoResumen(equipo.Id, equipo.NombreEquipo, 0, equipo.EsPersonal, RolEquipo.Lider.ATexto()));
    }

    [HttpDelete("{id:guid}")]
    public IActionResult Eliminar(Guid id)
    {
        equipos.EliminarEquipo(id, User.ObtenerColaboradorId());
        return NoContent();
    }

    // ------------------------------------------------------------ miembros

    /// <summary>Quita a un miembro (líder) o permite salir del equipo (el propio usuario).</summary>
    [HttpDelete("{id:guid}/colaboradores/{colaboradorId:guid}")]
    public IActionResult QuitarMiembro(Guid id, Guid colaboradorId)
    {
        equipos.EliminarMiembro(id, User.ObtenerColaboradorId(), colaboradorId);
        return NoContent();
    }

    [HttpPost("{id:guid}/liderazgo")]
    public IActionResult TransferirLiderazgo(Guid id, TransferirLiderazgoRequest request)
    {
        equipos.TransferirLiderazgo(id, User.ObtenerColaboradorId(), request.ColaboradorId);
        return NoContent();
    }

    // ------------------------------------------------------------ invitaciones del equipo (líder)

    [HttpGet("{id:guid}/invitaciones")]
    public ActionResult<IReadOnlyList<InvitacionResponse>> ListarInvitaciones(Guid id) =>
        Ok(equipos.ListarInvitacionesDeEquipo(id, User.ObtenerColaboradorId())
            .Select(InvitacionResponse.Desde).ToList());

    [HttpPost("{id:guid}/invitaciones")]
    public ActionResult<InvitacionResponse> Invitar(Guid id, InvitarRequest request)
    {
        var invitacion = equipos.Invitar(id, User.ObtenerColaboradorId(), request.Usuario);
        return Created($"api/equipos/{id}/invitaciones/{invitacion.Id}", InvitacionResponse.Desde(invitacion));
    }

    [HttpDelete("{id:guid}/invitaciones/{invitacionId:guid}")]
    public IActionResult CancelarInvitacion(Guid id, Guid invitacionId)
    {
        equipos.CancelarInvitacion(id, invitacionId, User.ObtenerColaboradorId());
        return NoContent();
    }
}

/// <summary>Invitaciones que ha recibido el usuario autenticado.</summary>
[ApiController]
[Authorize]
[Route("api/invitaciones")]
public class InvitacionesController(EquipoService equipos) : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyList<InvitacionResponse>> Pendientes() =>
        Ok(equipos.ListarInvitacionesRecibidas(User.ObtenerColaboradorId())
            .Select(InvitacionResponse.Desde).ToList());

    [HttpPost("{id:guid}/aceptar")]
    public IActionResult Aceptar(Guid id)
    {
        equipos.ResponderInvitacion(id, User.ObtenerColaboradorId(), aceptar: true);
        return NoContent();
    }

    [HttpPost("{id:guid}/rechazar")]
    public IActionResult Rechazar(Guid id)
    {
        equipos.ResponderInvitacion(id, User.ObtenerColaboradorId(), aceptar: false);
        return NoContent();
    }
}

public sealed record CrearEquipoRequest(string NombreEquipo);
public sealed record RenombrarEquipoRequest(string NombreEquipo);
public sealed record InvitarRequest(string Usuario);
public sealed record TransferirLiderazgoRequest(Guid ColaboradorId);

/// <summary>Rol: "lider" o "miembro" (el del usuario autenticado dentro de ese equipo).</summary>
public sealed record EquipoResumen(Guid Id, string Nombre, int Colaboradores, bool EsPersonal, string Rol);
public sealed record EquipoDetalle(
    Guid Id, string Nombre, IReadOnlyList<MiembroResumen> Colaboradores, bool EsPersonal, string MiRol);
public sealed record ColaboradorResumen(Guid Id, string Usuario);
public sealed record MiembroResumen(Guid Id, string Usuario, string Rol, DateTime UnidoEn);

public sealed record InvitacionResponse(
    Guid Id, Guid EquipoId, string Equipo, Guid InvitadoId, string Invitado,
    Guid InvitadoPorId, string InvitadoPor, DateTime CreadaEn)
{
    public static InvitacionResponse Desde(InvitacionDetalle d) =>
        new(d.Id, d.EquipoId, d.Equipo, d.InvitadoId, d.Invitado, d.InvitadoPorId, d.InvitadoPor, d.CreadaEn);
}
