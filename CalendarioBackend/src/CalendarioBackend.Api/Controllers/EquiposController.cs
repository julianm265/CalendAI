using CalendarioBackend.Core.Models;
using CalendarioBackend.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace CalendarioBackend.Api.Controllers;

[ApiController]
[Route("api/equipos")]
public class EquiposController : ControllerBase
{
    private readonly EquipoService _equipoService;
    private readonly AutenticacionService _autenticacionService;

    public EquiposController(EquipoService equipoService, AutenticacionService autenticacionService)
    {
        _equipoService = equipoService;
        _autenticacionService = autenticacionService;
    }

    [HttpGet]
    public ActionResult<IReadOnlyList<EquipoResumen>> Listar()
    {
        var usuario = Request.Headers["X-Usuario"].FirstOrDefault();
        var equipos = string.IsNullOrWhiteSpace(usuario)
            ? _equipoService.ListarEquipos()
            : _equipoService.EquiposDe(_autenticacionService.AutenticarPorUsuario(usuario)?.Id ?? Guid.Empty);

        return Ok(equipos.Select(equipo => new EquipoResumen(
            equipo.Id, equipo.NombreEquipo, equipo.LColaboradores.Count, equipo.EsPersonal, equipo.LiderId)).ToList());
    }

    [HttpGet("{id:guid}")]
    public ActionResult<EquipoDetalle> Obtener(Guid id)
    {
        try
        {
            var equipo = _equipoService.ObtenerEquipo(id);
            return Ok(new EquipoDetalle(equipo.Id, equipo.NombreEquipo,
                equipo.LColaboradores.Select(c => new ColaboradorResumen(c.Id, c.Usuario)).ToList(),
                equipo.EsPersonal, equipo.LiderId));
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost]
    public ActionResult<EquipoResumen> Crear(CrearEquipoRequest request)
    {
        try
        {
            var equipo = _equipoService.CrearEquipo(request.NombreEquipo, ObtenerUsuarioActual());
            var resumen = new EquipoResumen(equipo.Id, equipo.NombreEquipo, 1, false, equipo.LiderId);
            return CreatedAtAction(nameof(Obtener), new { id = equipo.Id }, resumen);
        }
        catch (UnauthorizedAccessException exception) { return Unauthorized(new { error = exception.Message }); }
        catch (ArgumentException exception) { return BadRequest(new { error = exception.Message }); }
        catch (InvalidOperationException exception) { return Conflict(new { error = exception.Message }); }
    }

    [HttpPost("{id:guid}/invitaciones")]
    public IActionResult Invitar(Guid id, InvitarUsuarioRequest request)
    {
        try
        {
            _equipoService.Invitar(id, request.Usuario, ObtenerUsuarioActual().Id);
            return Accepted();
        }
        catch (KeyNotFoundException exception) { return NotFound(new { error = exception.Message }); }
        catch (UnauthorizedAccessException exception) { return Unauthorized(new { error = exception.Message }); }
        catch (InvalidOperationException exception) { return Conflict(new { error = exception.Message }); }
    }

    [HttpGet("invitaciones")]
    public ActionResult<IReadOnlyList<InvitacionEquipoResponse>> Invitaciones()
    {
        try
        {
            var usuario = ObtenerUsuarioActual();
            return Ok(_equipoService.InvitacionesDe(usuario.Id)
                .Select(item => new InvitacionEquipoResponse(item.Id, item.Equipo.Id, item.Equipo.NombreEquipo)));
        }
        catch (UnauthorizedAccessException exception) { return Unauthorized(new { error = exception.Message }); }
    }

    [HttpPost("invitaciones/{invitacionId:guid}/aceptar")]
    public IActionResult AceptarInvitacion(Guid invitacionId)
    {
        try
        {
            _equipoService.AceptarInvitacion(invitacionId, ObtenerUsuarioActual().Id);
            return NoContent();
        }
        catch (KeyNotFoundException exception) { return NotFound(new { error = exception.Message }); }
        catch (UnauthorizedAccessException exception) { return Unauthorized(new { error = exception.Message }); }
    }

    [HttpPost("{id:guid}/colaboradores")]
    public ActionResult<ColaboradorResumen> RegistrarColaborador(Guid id, RegistrarColaboradorRequest request)
    {
        try
        {
            var colaborador = _equipoService.RegistrarColaborador(id, request.Usuario, request.Contraseña);
            return Ok(new ColaboradorResumen(colaborador.Id, colaborador.Usuario));
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException exception) { return BadRequest(new { error = exception.Message }); }
        catch (InvalidOperationException exception) { return Conflict(new { error = exception.Message }); }
    }

    private Colaborador ObtenerUsuarioActual()
    {
        var usuario = Request.Headers["X-Usuario"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(usuario))
            throw new UnauthorizedAccessException("Envía el encabezado X-Usuario.");
        return _autenticacionService.AutenticarPorUsuario(usuario)
            ?? throw new UnauthorizedAccessException("El usuario no existe.");
    }
}

public sealed record CrearEquipoRequest(string NombreEquipo);
public sealed record RegistrarColaboradorRequest(string Usuario, string Contraseña);
public sealed record InvitarUsuarioRequest(string Usuario);
public sealed record EquipoResumen(Guid Id, string Nombre, int Colaboradores, bool EsPersonal, Guid? LiderId = null);
public sealed record EquipoDetalle(Guid Id, string Nombre, IReadOnlyList<ColaboradorResumen> Colaboradores, bool EsPersonal, Guid? LiderId = null);
public sealed record ColaboradorResumen(Guid Id, string Usuario);
public sealed record InvitacionEquipoResponse(Guid Id, Guid EquipoId, string EquipoNombre);
